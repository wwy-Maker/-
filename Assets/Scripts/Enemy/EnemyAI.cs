using UnityEngine;

namespace HundredSchools.Enemy
{
    /// <summary>
    /// EnemyAI —— 敌人基础 AI 组件。
    ///
    /// 职责：
    ///   1. 锁定玩家目标（FindObjectOfType）
    ///   2. 每帧向玩家移动（使用基类 EnemyBase.moveSpeed）
    ///   3. 始终面向玩家（transform.right = 朝向方向）
    ///   4. 距离 < 0.5 时停止移动，防止模型重叠导致物理抖动
    ///
    /// 挂载到：带有 EnemyBase 组件的敌人 GameObject 上。
    ///
    /// 为什么面向玩家很重要：
    ///   - 后续扇形攻击需要基于敌人朝向判定前方扇形范围
    ///   - 正/背面受击判定（背刺暴击等玩法）依赖朝向
    ///   - Sprite 视觉上也能看出敌人正在关注谁
    /// </summary>
    [RequireComponent(typeof(EnemyBase))]
    public class EnemyAI : MonoBehaviour
    {
        // ==================== 序列化配置 ====================

        /// <summary>AI 更新频率（秒），0 = 每帧更新。值>0可降低性能开销。</summary>
        [Header("追踪参数")]
        [SerializeField, Range(0f, 1f)]
        private float updateInterval = 0f;

        // ==================== 运行时引用 ====================

        private Transform _playerTransform;
        private EnemyBase _enemy;
        private float _updateTimer;

        // ==================== Unity 生命周期 ====================

        private void Awake()
        {
            _enemy = GetComponent<EnemyBase>();
        }

        private void Start()
        {
            // 方案一：通过 Tag="Player" 查找（最快最可靠，O(1)查找）
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
                return;
            }

            // 方案二：Tag 未设置，尝试通过组件类型查找
            Player.PlayerMovement pm = FindObjectOfType<Player.PlayerMovement>();
            if (pm != null)
            {
                _playerTransform = pm.transform;
                return;
            }

            // 方案三：PlayerMovement 也没有，尝试 PlayerController
            Player.PlayerController pc = FindObjectOfType<Player.PlayerController>();
            if (pc != null)
            {
                _playerTransform = pc.transform;
                return;
            }

            Debug.LogWarning(
                $"[EnemyAI] {name}: 无法找到玩家！请检查：\n" +
                "  1. Player 的 Tag 是否设为 'Player'\n" +
                "  2. Player 是否挂载了 PlayerMovement 或 PlayerController\n" +
                "  3. 场景中是否有 Player GameObject"
            );
        }

        private void Update()
        {
            if (_playerTransform == null) return;
            if (_enemy.IsDead || _enemy.IsFrozen) return;

            // 可配置的更新间隔（降低 AI 计算频率）
            if (updateInterval > 0f)
            {
                _updateTimer += Time.deltaTime;
                if (_updateTimer < updateInterval) return;
                _updateTimer = 0f;
            }

            TrackPlayer();

            // 触发学派弹幕攻击
            _enemy.TryShoot(_playerTransform);
        }

        // ==================== AI 逻辑 ====================

        /// <summary>
        /// 计算到玩家的方向、更新朝向、执行移动。
        /// 不再使用 stopDistance 手动停止 —— 物理碰撞由 Rigidbody2D 自动处理。
        /// 玩家为 Dynamic，敌人为 Kinematic，两者碰撞体均为非 Trigger，
        /// Unity 物理引擎会自动阻止它们互相穿透。
        /// </summary>
        private void TrackPlayer()
        {
            Vector3 toPlayer = _playerTransform.position - transform.position;

            // 朝向控制：让敌人正面始终面向玩家
            FaceDirection(toPlayer);

            // 向玩家移动（物理碰撞会在靠近时自动阻挡）
            _enemy.MoveTowards(_playerTransform.position);
        }

        /// <summary>
        /// 让敌人的正面（transform.right）指向给定方向。
        ///
        /// 为什么用 transform.right 而不是 transform.up？
        ///   Unity 2D Sprite 默认绘制时"右"是前方（Canvas/UI 风格）。
        ///   如果用 transform.up，Sprite 会侧着指向目标。
        ///   可以通过在 Inspector 中调整 Sprite 的 pivot 或初始旋转来适配。
        ///   如果不符预期，改这里即可。
        /// </summary>
        private void FaceDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        // ==================== 公开接口 ====================

        /// <summary>
        /// 手动设置追踪目标（用于波次生成时由 Spawner 传入，性能优于 FindObjectOfType）。
        /// </summary>
        public void SetTarget(Transform target)
        {
            _playerTransform = target;
        }

        /// <summary>当前与玩家的距离（只读，供调试/UI）</summary>
        public float DistanceToPlayer
        {
            get
            {
                if (_playerTransform == null) return float.MaxValue;
                return Vector3.Distance(transform.position, _playerTransform.position);
            }
        }
    }
}
