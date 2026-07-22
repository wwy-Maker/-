using UnityEngine;

namespace HundredSchools.Core
{
    /// <summary>
    /// CameraController —— 相机平滑跟随玩家
    /// 使用 Vector3.SmoothDamp 实现带阻尼的缓动跟踪。
    /// 挂载到场景中的 Main Camera GameObject 上。
    /// 注意：必须使用 LateUpdate 而非 Update，确保相机在所有对象移动完毕后再更新位置。
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        // ==================== 序列化配置字段 ====================

        [Header("跟随目标")]
        /// <summary>要跟随的玩家 Transform（若留空，运行时会自动查找 Tag="Player" 的对象）</summary>
        public Transform target;

        [Header("偏移量")]
        /// <summary>相机相对于目标的偏移（2D 游戏中 Z 通常为 -10，确保能看到场景）</summary>
        public Vector3 offset = new Vector3(0f, 0f, -10f);

        [Header("平滑参数")]
        /// <summary>平滑过渡时间（秒）。值越小越灵敏，越大越迟缓</summary>
        [Range(0f, 2f)]
        public float smoothTime = 0.15f;

        /// <summary>相机移动的最大速度限制（防止瞬移时镜头飞得过快）</summary>
        public float maxSpeed = 50f;

        // ==================== 运行时状态 ====================

        /// <summary>SmoothDamp 内部维护的速度引用（必须跨帧保持）</summary>
        private Vector3 _currentVelocity = Vector3.zero;

        /// <summary>相机 Z 轴的目标固定值</summary>
        private float _fixedZ;

        // ==================== Unity 生命周期 ====================

        private void Start()
        {
            _fixedZ = transform.position.z;

            // 如果 Inspector 中没有手动拖入目标，则自动查找带 "Player" 标签的对象
            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    target = player.transform;
                    Debug.Log("[CameraController] 自动找到玩家目标（Tag='Player'）");
                }
                else
                {
                    Debug.LogWarning("[CameraController] 未找到 Tag='Player' 的对象，请在 Inspector 中手动拖入 Target");
                }
            }
        }

        /// <summary>
        /// LateUpdate 在所有 Update 执行完毕后才调用。
        /// 这样可以保证相机在玩家、敌人等对象移动完成后再更新位置，消除画面抖动。
        /// </summary>
        private void LateUpdate()
        {
            if (target == null) return;

            // 计算目标位置 = 玩家当前位置 + 预设偏移量
            Vector3 targetPosition = target.position + offset;

            // 保持 Z 轴不变（2D 游戏中相机深度固定）
            targetPosition.z = _fixedZ;

            // 使用 SmoothDamp 平滑移动到目标位置
            // 参数说明：
            //   current       —— 当前相机位置
            //   target        —— 期望到达的终点位置
            //   ref velocity  —— 当前速度（SmoothDamp 内部维护，模拟弹簧阻尼系统）
            //   smoothTime    —— 近似到达目标所需的时间（秒）
            //   maxSpeed      —— 最大移动速度上限
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref _currentVelocity,
                smoothTime,
                maxSpeed
            );
        }

        // ==================== 公开接口 ====================

        /// <summary>
        /// 立即将相机瞬移到目标位置（无平滑过渡）。
        /// 用于场景切换、玩家重生等场景。
        /// </summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            Vector3 snapPos = target.position + offset;
            snapPos.z = _fixedZ;
            transform.position = snapPos;

            // 重置速度引用，防止瞬移后产生"回弹"效果
            _currentVelocity = Vector3.zero;
        }
    }
}
