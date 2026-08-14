using UnityEngine;

namespace HundredSchools.Player
{
    /// <summary>
    /// PlayerController —— 玩家移动与基础操作（灰模阶段）
    ///
    /// 功能：
    ///   - WASD / 方向键 移动（带惯性阻尼）
    ///   - 鼠标朝向（玩家始终面向鼠标光标方向）
    ///   - 战斗房间边界限制
    ///   - 空格键冲刺（Roguelike 核心操作之一）
    ///
    /// 挂载到：Player GameObject 上
    /// 依赖组件：Rigidbody2D
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        // ==================== 序列化配置字段 ====================

        [Header("移动")]
        /// <summary>基础移动速度</summary>
        [Range(1f, 20f)]
        public float moveSpeed = 6f;

        /// <summary>移动惯性阻尼系数。值越小，起跑 / 刹车越灵敏；值越大越"滑"</summary>
        [Range(0.05f, 1f)]
        public float moveDamping = 0.2f;

        [Header("冲刺（Dash）")]
        /// <summary>冲刺速度倍率（相对 moveSpeed）</summary>
        [Range(1.5f, 5f)]
        public float dashSpeedMultiplier = 2.5f;

        /// <summary>冲刺持续时间（秒）</summary>
        [Range(0.05f, 0.5f)]
        public float dashDuration = 0.15f;

        /// <summary>冲刺冷却时间（秒）</summary>
        [Range(0.5f, 5f)]
        public float dashCooldown = 1.5f;

        [Header("战斗房间限制")]
        /// <summary>是否限制玩家在战斗房间内移动</summary>
        public bool clampToRoom = true;

        // ==================== 组件引用 ====================

        private Rigidbody2D _rb;
        private SpriteRenderer _spriteRenderer;

        // ==================== 运行时状态 ====================

        /// <summary>SmoothDamp 内部速度引用（必须跨帧保持）</summary>
        private Vector2 _currentVelocity;

        /// <summary>冲刺状态</summary>
        private bool _isDashing;
        private float _dashTimer;
        private float _dashCooldownTimer;
        private Vector2 _dashDirection;

        /// <summary>冲刺冷却进度（0~1，供 UI 显示）</summary>
        public float DashCooldownProgress => _dashCooldownTimer / dashCooldown;

        /// <summary>当前是否正在冲刺</summary>
        public bool IsDashing => _isDashing;

        // ==================== Unity 生命周期 ====================

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();

            // 如果没有 Rigidbody2D，自动添加并配置
            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody2D>();
                _rb.gravityScale = 0f;
                _rb.drag = 5f;
                _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }

        private void Update()
        {
            // 游戏暂停或结束时，不处理输入
            if (Core.GameManager.Instance != null)
            {
                if (Core.GameManager.Instance.IsPaused || Core.GameManager.Instance.IsGameOver
                    || Core.GameManager.Instance.IsSelectingCharacter)
                    return;
            }

            HandleDashInput();
            FaceMouseDirection();
        }

        private void FixedUpdate()
        {
            if (Core.GameManager.Instance != null)
            {
                if (Core.GameManager.Instance.IsPaused || Core.GameManager.Instance.IsGameOver
                    || Core.GameManager.Instance.IsSelectingCharacter)
                    return;
            }

            // ★ 常规移动由 PlayerMovement 统一处理，这里只在冲刺时覆盖速度
            HandleDashMovement();
            ClampToRoom();
        }

        // ==================== 移动逻辑 ====================

        /// <summary>
        /// 仅在冲刺期间覆盖 Rigidbody2D.velocity。
        /// 常规移动完全由 PlayerMovement 组件负责，避免双组件冲突。
        /// </summary>
        private void HandleDashMovement()
        {
            if (!_isDashing) return;

            float currentMaxSpeed = moveSpeed * dashSpeedMultiplier;
            _rb.velocity = _dashDirection * currentMaxSpeed;
        }

        // ==================== 冲刺逻辑 ====================

        /// <summary>
        /// 检测 Shift 键按下，若不在冷却中则触发闪避。
        /// 闪避方向 = 当前移动方向（若无输入则使用鼠标方向）
        /// GDD：儒/法 冷却 8s，道 冷却 0s（无冷却）
        /// </summary>
        private void HandleDashInput()
        {
            // 冷却计时器递减
            if (_dashCooldownTimer > 0f)
                _dashCooldownTimer -= Time.deltaTime;

            // 冲刺进行中：计时器递减，到时结束
            if (_isDashing)
            {
                _dashTimer -= Time.deltaTime;
                if (_dashTimer <= 0f)
                {
                    _isDashing = false;
                }
                return;
            }

            // GDD：Shift 闪避（从 PlayerMovement 读取学派冷却时间）
            // 但当御艺激活时，Shift 由 ChariotWeapon 接管（冲刺攻击），此处跳过
            if (Input.GetKeyDown(KeyCode.LeftShift) && _dashCooldownTimer <= 0f)
            {
                // 检查是否御艺激活（御艺的 Shift = 冲刺攻击，不是闪避）
                var combat = GetComponent<PlayerCombat>();
                if (combat != null && combat.CurrentWeapon == EWeapon.Chariot)
                    return;

                var pm = GetComponent<PlayerMovement>();
                float schoolCooldown = pm != null ? pm.dodgeCooldown : dashCooldown;
                if (schoolCooldown > 0f || _dashCooldownTimer <= 0f)
                {
                    StartDash();
                    _dashCooldownTimer = schoolCooldown;
                }
            }
        }

        /// <summary>
        /// 开始冲刺
        /// </summary>
        private void StartDash()
        {
            // 确定冲刺方向
            float inputX = Input.GetAxisRaw("Horizontal");
            float inputY = Input.GetAxisRaw("Vertical");
            Vector2 inputDir = new Vector2(inputX, inputY);

            if (inputDir.sqrMagnitude > 0.01f)
            {
                // 有按键输入 → 朝按键方向冲刺
                _dashDirection = inputDir.normalized;
            }
            else
            {
                // 无按键输入 → 朝鼠标方向冲刺
                Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                mouseWorld.z = 0f;
                _dashDirection = (mouseWorld - transform.position).normalized;
            }

            _isDashing = true;
            _dashTimer = dashDuration;
            _dashCooldownTimer = dashCooldown;
        }

        // ==================== 朝向逻辑 ====================

        /// <summary>
        /// 让玩家始终面向鼠标光标的方向。
        /// 灰模阶段：圆形玩家看不出旋转效果，后续给玩家加"眼睛/指针"子物体即可看到朝向。
        /// </summary>
        private void FaceMouseDirection()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            // 将鼠标屏幕坐标转为世界坐标
            Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;

            // 计算从玩家指向鼠标的方向向量
            Vector3 direction = mouseWorld - transform.position;

            if (direction.sqrMagnitude > 0.001f)
            {
                // Atan2 计算方向角（弧度），转成度数后绕 Z 轴旋转
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        // ==================== 房间边界限制 ====================

        /// <summary>
        /// 将玩家位置限制在战斗房间范围内。
        /// 使用 _rb.position 而非 transform.position，与 Rigidbody2D 物理系统同步。
        /// </summary>
        private void ClampToRoom()
        {
            if (!clampToRoom) return;
            if (Core.GameManager.Instance == null) return;

            Vector2 roomSize = Core.GameManager.Instance.roomSize;
            Vector2 halfRoom = roomSize * 0.5f;

            Vector2 pos = _rb.position;
            pos.x = Mathf.Clamp(pos.x, -halfRoom.x, halfRoom.x);
            pos.y = Mathf.Clamp(pos.y, -halfRoom.y, halfRoom.y);
            _rb.position = pos;
        }
    }
}
