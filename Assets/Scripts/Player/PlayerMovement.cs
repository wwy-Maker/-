using UnityEngine;
using HundredSchools.Core;

namespace HundredSchools.Player
{
    /// <summary>
    /// PlayerMovement —— 玩家移动与核心属性组件。
    ///
    /// 职责：
    ///   1. WASD / 方向键物理移动（Rigidbody2D.MovePosition）
    ///   2. 场地边界约束（20×20 正方形）
    ///   3. 根据学派动态设置 Sprite 颜色
    ///   4. HP / 耐力属性管理
    ///
    /// 挂载到：Player GameObject（需要 Rigidbody2D + SpriteRenderer）
    ///
    /// 为什么使用 Rigidbody2D.MovePosition 而不是直接修改 transform.position？
    ///
    ///   1. 物理系统同步：Rigidbody2D.MovePosition 会将移动意图告知 Unity 物理引擎，
    ///      物理引擎在下一个 FixedUpdate 中统一结算所有碰撞体的位置。如果你直接修改
    ///      transform.position，相当于"瞬移"到目标位置，绕过了物理引擎，可能导致：
    ///        - 穿墙：碰撞检测在你瞬移的那一帧被跳过
    ///        - 抖动：其他物体通过物理引擎移动你时，你的 transform 会在下一帧被拉回
    ///        - OnTriggerEnter2D 失效：Trigger 检测依赖物理引擎的位置插值
    ///
    ///   2. 插值平滑：当 Rigidbody2D.interpolation 开启时，MovePosition 会在帧之间
    ///      自动插值，即使物理更新频率低于渲染帧率，画面依然丝滑。直接改 transform
    ///      无法享受这个特性。
    ///
    ///   3. 确定性：所有物理对象在同一套规则下移动，碰撞结果可预测、可复现，
    ///      调试 bug 时不会出现"有时穿墙有时不穿"的玄学问题。
    /// </summary>
    public class PlayerMovement : MonoBehaviour
    {
        // ==================== 基础属性（Inspector 可见） ====================

        [Header("移动")]
        /// <summary>基础移动速度（单位/秒）</summary>
        [SerializeField, Range(1f, 20f)]
        private float moveSpeed = 5f;

        [Header("战斗属性")]
        /// <summary>最大生命值</summary>
        [SerializeField, Range(1f, 500f)]
        private float maxHp = 100f;

        /// <summary>最大耐力值（用于冲刺、技能消耗）</summary>
        [SerializeField, Range(1f, 200f)]
        private float maxStamina = 100f;

        // === GDD 学派被动（运行时从 schools.json 读取） ===

        /// <summary>闪避冷却时间（GDD：儒/法=8s，道=0s 无冷却）</summary>
        [HideInInspector] public float dodgeCooldown = 8f;

        /// <summary>攻击系数（GDD：儒=1.0，法=1.1，道=0.9）</summary>
        [HideInInspector] public float attackCoeff = 1f;

        /// <summary>体力恢复速率（GDD：道=1.5，其他=1.0）</summary>
        private float _staminaRecoveryRate = 1f;

        /// <summary>击杀回血（GDD：儒=+5HP，其他=0）</summary>
        private int _killHeal;

        [Header("学派与武器")]
        /// <summary>当前所属学派</summary>
        [SerializeField]
        private ESchool currentSchool = ESchool.Confucian;

        /// <summary>当前使用的武器 / 技艺流派</summary>
        [SerializeField]
        private EWeapon currentWeapon = EWeapon.Archery;

        [Header("场地边界")]
        /// <summary>场地半边长（20×20 正方形，取半边 = 10）</summary>
        [SerializeField]
        private float boundaryHalfSize = 10f;

        // ==================== 运行时状态 ====================

        /// <summary>当前生命值</summary>
        private float currentHp;

        /// <summary>当前耐力值</summary>
        private float currentStamina;

        /// <summary>公共只读属性：当前 HP</summary>
        public float CurrentHp => currentHp;

        /// <summary>公共只读属性：当前耐力</summary>
        public float CurrentStamina => currentStamina;

        /// <summary>公共只读属性：最大 HP</summary>
        public float MaxHp => maxHp;

        /// <summary>公共只读属性：最大耐力</summary>
        public float MaxStamina => maxStamina;

        /// <summary>公共只读属性：当前学派</summary>
        public ESchool CurrentSchool => currentSchool;

        /// <summary>公共只读属性：当前武器</summary>
        public EWeapon CurrentWeapon => currentWeapon;

        /// <summary>玩家是否已死亡</summary>
        public bool IsDead => currentHp <= 0f;

        // ==================== 组件引用 ====================

        private Rigidbody2D _rb;
        private SpriteRenderer _spriteRenderer;

        // ==================== Unity 生命周期 ====================

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody2D>();
            }

            // Dynamic 刚体：与 Kinematic 敌人产生物理碰撞，互相阻挡
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 0f;
            _rb.drag = 5f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // 确保存在非 Trigger 碰撞体用于物理阻挡
            // 如果已有 Trigger 碰撞体，额外加一个非 Trigger 的
            Collider2D existingCol = GetComponent<Collider2D>();
            if (existingCol == null)
            {
                CircleCollider2D physicsCol = gameObject.AddComponent<CircleCollider2D>();
                physicsCol.isTrigger = false; // 非 Trigger：与敌人物理碰撞
                physicsCol.radius = 0.4f;
            }
            else if (existingCol.isTrigger)
            {
                // 已有 Trigger 碰撞体（可能是手动添加的），
                // 把它的 isTrigger 关掉，让它参与物理碰撞
                existingCol.isTrigger = false;
            }

            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }

        private void Start()
        {
            // 初始化属性
            currentHp = maxHp;
            currentStamina = maxStamina;

            // GDD：从 schools.json 读取学派被动
            var schoolCfg = ConfigLoader.GetSchoolConfig(currentSchool);
            if (schoolCfg != null)
            {
                dodgeCooldown = schoolCfg.dodgeNoCooldown ? 0f : 8f;
                attackCoeff = schoolCfg.attackCoeff;
                _staminaRecoveryRate = schoolCfg.staminaRecoveryRate;
                _killHeal = schoolCfg.killHeal;
            }

            // 根据学派设置 Sprite 颜色
            ApplySchoolColor();
        }

        private void OnEnable()
        {
            EventBus.OnEnemyKilled += HandleEnemyKilledForHeal;
        }

        private void OnDisable()
        {
            EventBus.OnEnemyKilled -= HandleEnemyKilledForHeal;
        }

        private void Update()
        {
            // 暂停 / 游戏结束时不处理输入
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.IsPaused || GameManager.Instance.IsGameOver)
                {
                    _rb.velocity = Vector2.zero;
                    return;
                }
            }

            // 体力自然恢复
            if (currentStamina < maxStamina)
            {
                float recoveryRate = 5f * _staminaRecoveryRate; // 基础5/s × 学派系数
                currentStamina = Mathf.Min(maxStamina, currentStamina + recoveryRate * Time.deltaTime);
                EventBus.TriggerStaminaChanged(currentStamina, maxStamina);
            }
        }

        private void FixedUpdate()
        {
            // 暂停 / 游戏结束时不移动
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.IsPaused || GameManager.Instance.IsGameOver)
                    return;
            }

            HandleMovement();
        }

        // ==================== 移动逻辑 ====================

        /// <summary>
        /// 使用 Rigidbody2D.velocity 驱动移动。
        /// Unity 2022 使用 velocity（2023+ 才改名 linearVelocity）。
        /// Dynamic 刚体用 velocity 可让物理引擎正确结算碰撞。
        /// </summary>
        private void HandleMovement()
        {
            float inputX = Input.GetAxisRaw("Horizontal");
            float inputY = Input.GetAxisRaw("Vertical");

            Vector2 direction = new Vector2(inputX, inputY).normalized;

            _rb.velocity = direction * moveSpeed;

            // 边界约束：velocity 移动后 clamp 位置防止出界
            _rb.position = ClampToBoundary(_rb.position);
        }

        // ==================== 边界约束 ====================

        /// <summary>
        /// 将给定坐标钳制在 20×20 的场地正方形内。
        /// 假设场地中心在原点 (0, 0)。
        /// </summary>
        /// <param name="position">待约束的坐标</param>
        /// <returns>约束后的坐标</returns>
        private Vector2 ClampToBoundary(Vector2 position)
        {
            position.x = Mathf.Clamp(position.x, -boundaryHalfSize, boundaryHalfSize);
            position.y = Mathf.Clamp(position.y, -boundaryHalfSize, boundaryHalfSize);
            return position;
        }

        // ==================== 视觉反馈 ====================

        /// <summary>
        /// 根据 currentSchool 的值，动态设置 SpriteRenderer 的颜色。
        ///
        /// 颜色映射：
        ///   Confucian（儒家）→ Color.yellow  金色，象征"中正平和"
        ///   Legalist （法家）→ Color.black   黑色，象征"严刑峻法"
        ///   Taoist   （道家）→ Color.cyan    青色，象征"道法自然"
        /// </summary>
        private void ApplySchoolColor()
        {
            if (_spriteRenderer == null) return;

            switch (currentSchool)
            {
                case ESchool.Confucian:
                    _spriteRenderer.color = Color.yellow;
                    break;
                case ESchool.Legalist:
                    _spriteRenderer.color = Color.black;
                    break;
                case ESchool.Taoist:
                    _spriteRenderer.color = Color.cyan;
                    break;
                case ESchool.Mohist:
                    _spriteRenderer.color = Color.gray;
                    break;
                default:
                    _spriteRenderer.color = Color.white;
                    break;
            }
        }

        // ==================== 公开接口 ====================

        /// <summary>
        /// 玩家受到伤害。
        /// </summary>
        /// <param name="damage">伤害值</param>
        public void TakeDamage(float damage)
        {
            if (IsDead) return;
            if (damage <= 0f) return;

            currentHp = Mathf.Max(0f, currentHp - damage);
            EventBus.TriggerPlayerDamaged(currentHp, maxHp);

            if (currentHp <= 0f)
            {
                GameManager.Instance?.OnPlayerDied();
            }
        }

        /// <summary>
        /// 恢复生命值。
        /// </summary>
        /// <param name="amount">回复量</param>
        public void Heal(float amount)
        {
            if (IsDead) return;
            if (amount <= 0f) return;

            currentHp = Mathf.Min(maxHp, currentHp + amount);
            EventBus.TriggerPlayerHealed(amount);
            EventBus.TriggerPlayerDamaged(currentHp, maxHp);
        }

        /// <summary>
        /// GDD 儒家被动：击杀敌人时回复 HP。
        /// </summary>
        private void HandleEnemyKilledForHeal(Vector3 position, int knowledgeValue)
        {
            if (IsDead) return;
            if (_killHeal > 0)
                Heal(_killHeal);
        }

        /// <summary>
        /// 消耗耐力。若耐力不足则返回 false。
        /// </summary>
        /// <param name="cost">消耗量</param>
        /// <returns>是否成功消耗</returns>
        public bool ConsumeStamina(float cost)
        {
            if (cost <= 0f) return true;
            if (currentStamina < cost) return false;

            currentStamina -= cost;
            return true;
        }

        /// <summary>
        /// 恢复耐力。
        /// </summary>
        /// <param name="amount">回复量</param>
        public void RecoverStamina(float amount)
        {
            if (amount <= 0f) return;
            currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
        }

        /// <summary>
        /// 运行时切换学派（例如流派选择 / 升级时调用）。
        /// 会自动刷新 Sprite 颜色。
        /// </summary>
        public void SwitchSchool(ESchool newSchool)
        {
            currentSchool = newSchool;
            ApplySchoolColor();
        }

        /// <summary>
        /// 运行时切换武器流派。
        /// </summary>
        public void SwitchWeapon(EWeapon newWeapon)
        {
            currentWeapon = newWeapon;
        }
    }
}
