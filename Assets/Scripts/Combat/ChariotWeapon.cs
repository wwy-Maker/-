using UnityEngine;

namespace HundredSchools.Combat
{
    /// <summary>
    /// ChariotWeapon —— 御艺冲刺攻击组件。
    ///
    /// 职责：
    ///   1. 监听 Left Shift，向移动方向（或鼠标方向）瞬间位移 3 单位
    ///   2. 在冲刺轨迹上生成半透明长条矩形（0.5 秒自毁），附带伤害判定碰撞体
    ///
    /// 原理：使用 Rigidbody2D.MovePosition 进行位移，保证与物理系统同步。
    ///       轨迹矩形通过拉伸 Square Sprite + 旋转实现，零外部资源。
    ///
    /// 挂载到：Player GameObject（须有 Rigidbody2D）
    /// 调用方式：由 PlayerCombat 在 Update 中调用 HandleInput()
    /// </summary>
    public class ChariotWeapon : MonoBehaviour
    {
        // ==================== 序列化配置 ====================

        [Header("冲刺")]
        /// <summary>冲刺距离（单位）</summary>
        [SerializeField, Range(1f, 10f)]
        private float dashDistance = 3f;

        /// <summary>冲刺冷却时间（秒）</summary>
        [SerializeField, Range(0.1f, 5f)]
        private float cooldown = 0.8f;

        /// <summary>冲刺伤害</summary>
        [SerializeField, Range(1, 50)]
        private int damage = 15;

        [Header("轨迹视觉效果")]
        /// <summary>轨迹矩形的宽度（单位）</summary>
        [SerializeField, Range(0.1f, 2f)]
        private float trailWidth = 0.4f;

        /// <summary>轨迹透明度（0=完全透明，1=完全不透明）</summary>
        [SerializeField, Range(0.1f, 1f)]
        private float trailAlpha = 0.4f;

        /// <summary>轨迹持续时间（秒）</summary>
        [SerializeField, Range(0.1f, 2f)]
        private float trailDuration = 0.5f;

        // ==================== 运行时状态 ====================

        private float _cooldownTimer;
        private Rigidbody2D _rb;

        // ==================== Unity 生命周期 ====================

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            // 注意：Rigidbody2D 可能由 PlayerMovement.Awake 动态添加，
            // 如果当前为空，在 Start 中再次尝试获取
        }

        private void Start()
        {
            if (_rb == null)
                _rb = GetComponent<Rigidbody2D>();
        }

        // ==================== 公开接口 ====================

        /// <summary>
        /// 每帧由 PlayerCombat 调用。检测 Shift 键并触发冲刺攻击。
        /// </summary>
        public void HandleInput()
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;

            if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                if (_cooldownTimer <= 0f)
                {
                    PerformDash();
                    _cooldownTimer = cooldown;
                    Debug.Log("[ChariotWeapon] 冲刺！冷却: " + cooldown + "s");
                }
                else
                {
                    Debug.Log("[ChariotWeapon] 冲刺冷却中，还需 " + _cooldownTimer.ToString("F2") + "s");
                }
            }
        }

        /// <summary>切换武器时重置冷却</summary>
        public void ResetState()
        {
            _cooldownTimer = 0f;
        }

        // ==================== 冲刺实现 ====================

        private void PerformDash()
        {
            // 确定冲刺方向：优先移动输入，否则用鼠标方向
            Vector2 dashDir = GetDashDirection();
            if (dashDir.sqrMagnitude < 0.001f) return;

            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + (Vector3)dashDir * dashDistance;

            // 边界约束（从 GameManager 读取房间尺寸）
            endPos = ClampToRoom(endPos);

            // 计算实际位移后的终点（可能被边界截断）
            Vector3 actualEnd = endPos;
            float actualDistance = Vector3.Distance(startPos, actualEnd);

            // 直接设置位置（瞬移类技能，不需要 MovePosition 的插值）
            if (_rb != null)
                _rb.position = actualEnd;
            else
                transform.position = actualEnd;

            Debug.Log($"[ChariotWeapon] 冲刺！{startPos} → {actualEnd} 距离={actualDistance:F1}");

            // 生成轨迹矩形
            if (actualDistance > 0.05f)
            {
                CreateTrailRect(startPos, actualEnd, dashDir);
            }
        }

        /// <summary>
        /// 获取冲刺方向：WASD 输入方向优先，无输入时用鼠标方向。
        /// </summary>
        private Vector2 GetDashDirection()
        {
            float inputX = Input.GetAxisRaw("Horizontal");
            float inputY = Input.GetAxisRaw("Vertical");
            Vector2 inputDir = new Vector2(inputX, inputY);

            if (inputDir.sqrMagnitude > 0.01f)
                return inputDir.normalized;

            // 无按键输入 → 朝向鼠标
            Vector3 mouseWorld = WeaponUtils.GetMouseWorldPosition();
            return (mouseWorld - transform.position).normalized;
        }

        /// <summary>
        /// 将目标位置约束在战斗房间边界内。
        /// </summary>
        private Vector3 ClampToRoom(Vector3 position)
        {
            if (Core.GameManager.Instance == null) return position;

            Vector2 roomSize = Core.GameManager.Instance.roomSize;
            Vector2 halfRoom = roomSize * 0.5f;

            position.x = Mathf.Clamp(position.x, -halfRoom.x, halfRoom.x);
            position.y = Mathf.Clamp(position.y, -halfRoom.y, halfRoom.y);
            return position;
        }

        // ==================== 轨迹矩形创建 ====================

        /// <summary>
        /// 在冲刺起点和终点之间创建一个半透明矩形，表示攻击轨迹。
        ///
        /// 创建方式：
        ///   - 位置 = 起点和终点的中点
        ///   - X 轴拉伸 = 实际冲刺距离，Y 轴 = trailWidth
        ///   - 旋转 = 对齐冲刺方向
        ///   - 颜色 = 学派颜色 + alpha 透明度
        ///   - 自动销毁：0.5 秒后 Destroy
        /// </summary>
        private void CreateTrailRect(Vector3 start, Vector3 end, Vector2 direction)
        {
            float dist = Vector3.Distance(start, end);
            Vector3 midPoint = (start + end) * 0.5f;

            GameObject trailObj = new GameObject("Trail_Chariot");
            trailObj.transform.position = midPoint;

            // 拉伸矩形：X 轴 = 冲刺距离，Y 轴 = 宽度
            trailObj.transform.localScale = new Vector3(dist, trailWidth, 1f);

            // 旋转对齐冲刺方向
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            trailObj.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // SpriteRenderer：学派颜色 + 半透明
            SpriteRenderer sr = trailObj.AddComponent<SpriteRenderer>();
            sr.sprite = WeaponUtils.GetOrCreateSquareSprite();
            sr.sortingOrder = 0;

            ESchool school = WeaponUtils.GetCurrentSchool(this);
            Color baseColor = WeaponUtils.GetSchoolColor(school);
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, trailAlpha);

            // BoxCollider2D：伤害判定
            BoxCollider2D col = trailObj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one;

            // 附加伤害组件（GDD v1.9：注入学派弹幕行为）
            ESchool currentSchool = WeaponUtils.GetCurrentSchool(this);
            TrailDamage trailDmg = trailObj.AddComponent<TrailDamage>();
            trailDmg.damage = damage;
            trailDmg.school = currentSchool;
            trailDmg.behavior = GetBehaviorForSchool(currentSchool);

            // 自动销毁
            Destroy(trailObj, trailDuration);
        }

        /// <summary>
        /// GDD v1.9：学派 → 弹幕行为映射（与 ArcheryWeapon 保持一致）。
        /// </summary>
        private EBulletBehavior GetBehaviorForSchool(ESchool school)
        {
            switch (school)
            {
                case ESchool.Confucian: return EBulletBehavior.Splash;
                case ESchool.Legalist:  return EBulletBehavior.Pierce;
                case ESchool.Taoist:    return EBulletBehavior.Return;
                default:                return EBulletBehavior.Normal;
            }
        }
    }
}
