using UnityEngine;

namespace HundredSchools.Combat
{
    /// <summary>
    /// RitualWeapon —— 礼艺扇形击退组件。
    ///
    /// 职责：
    ///   1. 监听鼠标右键，在玩家前方生成扇形检测区域
    ///   2. 使用 Physics2D.OverlapCircleAll + 角度过滤找出扇形内的敌人
    ///   3. 对命中目标施加反向力（Rigidbody2D.AddForce），实现击退
    ///   4. 生成视觉反馈（半透明圆形，0.3 秒自毁）
    ///
    /// 挂载到：Player GameObject
    /// 调用方式：由 PlayerCombat 在 Update 中调用 HandleInput()
    /// </summary>
    public class RitualWeapon : MonoBehaviour
    {
        // ==================== 序列化配置 ====================

        [Header("扇形参数")]
        /// <summary>扇形半径（检测范围）</summary>
        [SerializeField, Range(1f, 10f)]
        private float fanRadius = 4f;

        /// <summary>扇形角度（半角，度）。总扇形 = halfAngle × 2</summary>
        [SerializeField, Range(15f, 90f)]
        private float fanHalfAngle = 45f;

        [Header("击退")]
        /// <summary>击退力的大小</summary>
        [SerializeField, Range(1f, 50f)]
        private float knockbackForce = 15f;

        /// <summary>击退伤害</summary>
        [SerializeField, Range(1, 30)]
        private int damage = 8;

        [Header("冷却")]
        [SerializeField, Range(0.1f, 5f)]
        private float cooldown = 1f;

        [Header("礼屏障（反弹波纹）")]
        [SerializeField, Range(2f, 10f)]
        private float barrierRadius = 5f;

        [SerializeField, Range(0.3f, 2f)]
        private float barrierDuration = 0.8f;

        [SerializeField, Range(10, 50)]
        private int barrierReflectDamage = 30;

        [Header("视觉反馈")]
        /// <summary>视觉指示器的持续时间（秒）</summary>
        [SerializeField, Range(0.1f, 1f)]
        private float visualDuration = 0.3f;

        /// <summary>视觉指示器的透明度</summary>
        [SerializeField, Range(0.1f, 0.8f)]
        private float visualAlpha = 0.3f;

        [Header("目标过滤")]
        /// <summary>目标所在的 Layer（后续配置敌人 Layer 后在此选择）</summary>
        [SerializeField]
        private LayerMask targetLayerMask = -1; // 默认 Everything

        // ==================== 运行时状态 ====================

        private float _cooldownTimer;

        // ==================== Unity 生命周期 ====================

        // ==================== 公开接口 ====================

        /// <summary>
        /// 每帧由 PlayerCombat 调用。检测右键并触发扇形击退。
        /// </summary>
        public void HandleInput()
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;

            if (Input.GetMouseButtonDown(1) && _cooldownTimer <= 0f)
            {
                PerformFanAttack();
                _cooldownTimer = cooldown;
            }
        }

        /// <summary>切换武器时重置冷却</summary>
        public void ResetState()
        {
            _cooldownTimer = 0f;
        }

        // ==================== 扇形攻击实现 ====================

        private void PerformFanAttack()
        {
            Vector2 playerPos = transform.position;
            Vector2 forward = GetPlayerForward();

            // 第一步：OverlapCircleAll 获取半径内所有碰撞体
            Collider2D[] hits = Physics2D.OverlapCircleAll(playerPos, fanRadius, targetLayerMask);

            int hitCount = 0;

            foreach (Collider2D col in hits)
            {
                // 跳过自己的碰撞体
                if (col.gameObject == gameObject) continue;

                Vector2 toTarget = col.transform.position - transform.position;
                float distance = toTarget.magnitude;

                if (distance < 0.01f) continue;

                Vector2 toTargetDir = toTarget.normalized;

                // 第二步：角度过滤 —— 只保留在扇形范围内的目标
                float angle = Vector2.Angle(forward, toTargetDir);
                if (angle > fanHalfAngle) continue;

                // 第三步：对目标施加击退力 + 伤害
                Rigidbody2D targetRb = col.attachedRigidbody;
                if (targetRb != null)
                {
                    Vector2 knockbackDir = toTargetDir;
                    targetRb.AddForce(knockbackDir * knockbackForce, ForceMode2D.Impulse);
                }

                // 对敌人造成伤害
                Enemy.EnemyBase enemy = col.GetComponent<Enemy.EnemyBase>();
                if (enemy != null)
                {
                    enemy.TakeDamage(damage);
                }

                hitCount++;

                // 后续：在这里调用目标上的 Damage 接口
                // IDamageable dmg = col.GetComponent<IDamageable>();
                // dmg?.TakeDamage(damage);

                Debug.Log($"[RitualWeapon] 击退目标: {col.name}, 角度: {angle:F1}°, 距离: {distance:F2}");
            }

            if (hitCount > 0)
            {
                Debug.Log($"[RitualWeapon] 扇形攻击命中 {hitCount} 个目标!");
            }
            else
            {
                Debug.Log($"[RitualWeapon] 扇形攻击未命中任何目标（范围内 Collider 总数: {hits.Length}）");
            }

            // 视觉反馈
            SpawnVisualIndicator(playerPos, forward);

            // 礼屏障：反弹 Boss 波纹
            SpawnBarrier(playerPos);
        }

        /// <summary>
        /// 在玩家位置生成礼屏障，用于反弹 BossWave。
        /// </summary>
        private void SpawnBarrier(Vector2 position)
        {
            GameObject barrierObj = new GameObject("RitualBarrier");
            barrierObj.transform.position = position;

            RitualBarrier barrier = barrierObj.AddComponent<RitualBarrier>();
            barrier.Init(barrierRadius, barrierDuration, barrierReflectDamage);
        }

        // ==================== 视觉反馈 ====================

        /// <summary>
        /// 在玩家位置生成一个半透明圆形，表示扇形检测的覆盖范围。
        /// 圆形直径 = fanRadius × 2，短暂显示后自毁。
        /// </summary>
        private void SpawnVisualIndicator(Vector2 position, Vector2 forward)
        {
            GameObject indicator = new GameObject("VFX_Ritual");
            indicator.transform.position = position;

            float diameter = fanRadius * 2f;

            // 使用圆形 Sprite（程序化生成，零外部依赖）
            SpriteRenderer sr = indicator.AddComponent<SpriteRenderer>();
            sr.sprite = WeaponUtils.GetOrCreateCircleSprite();
            sr.sortingOrder = 0;

            ESchool school = WeaponUtils.GetCurrentSchool(this);
            Color baseColor = WeaponUtils.GetSchoolColor(school);
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, visualAlpha);

            // Sprite 的 pixelsPerUnit = 64，即 1 unit = 直径 1
            // 所以放大 fanRadius*2 倍即可得到正确直径
            indicator.transform.localScale = Vector3.one * diameter;

            Destroy(indicator, visualDuration);
        }

        // ==================== 辅助方法 ====================

        /// <summary>
        /// 获取玩家当前朝向（单位向量）。
        /// 读取 transform.right，即玩家朝向鼠标的方向。
        /// </summary>
        private Vector2 GetPlayerForward()
        {
            // 玩家的旋转由 PlayerController 控制，始终朝向鼠标
            return transform.right;
        }

        // ==================== 调试可视化 ====================

#if UNITY_EDITOR
        /// <summary>
        /// 在 Scene 视图中绘制扇形检测范围，方便调试参数。
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);

            Vector3 origin = transform.position;
            Vector2 forward = transform.right;

            float halfAngleRad = fanHalfAngle * Mathf.Deg2Rad;
            float baseAngle = Mathf.Atan2(forward.y, forward.x);

            // 绘制扇形弧线
            int segments = 20;
            Vector3 prevPoint = origin + (Vector3)(Quaternion.Euler(0, 0, -fanHalfAngle) * forward * fanRadius);

            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = baseAngle - halfAngleRad + (halfAngleRad * 2f * t);
                Vector3 point = origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * fanRadius;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }

            // 两条边界线
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + (Vector3)(Quaternion.Euler(0, 0, -fanHalfAngle) * forward * fanRadius));
            Gizmos.DrawLine(origin, origin + (Vector3)(Quaternion.Euler(0, 0, fanHalfAngle) * forward * fanRadius));
        }
#endif
    }
}
