using UnityEngine;
using HundredSchools.Combat;

namespace HundredSchools.Player
{
    /// <summary>
    /// PlayerCombat —— 武器系统编排器（轻量级）。
    ///
    /// 职责：
    ///   1. 持有当前武器枚举，管理武器切换
    ///   2. 每帧根据 currentWeapon 将输入分发给对应的武器组件
    ///   3. 武器切换时重置旧武器状态，防止跨武器状态污染
    ///
    /// 架构：PlayerCombat 本身不包含任何攻击逻辑。
    ///       所有攻击代码分别在 ArcheryWeapon / ChariotWeapon / RitualWeapon 中。
    ///       这符合 Unity 组件化设计 —— 每个武器是独立的 MonoBehaviour，可单独测试。
    ///
    /// 按键映射：
    ///   数字键 1 → 射艺 (Archery)
    ///   数字键 2 → 御艺 (Chariot)
    ///   数字键 3 → 礼艺 (Ritual)
    ///
    /// 挂载到：Player GameObject（须同时挂载三种 Weapon 组件）
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        // ==================== 序列化配置 ====================

        [Header("当前武器")]
        /// <summary>当前装备的武器流派</summary>
        [SerializeField]
        private EWeapon currentWeapon = EWeapon.Archery;

        /// <summary>公共只读属性</summary>
        public EWeapon CurrentWeapon => currentWeapon;

        // ==================== 武器组件引用 ====================

        private ArcheryWeapon _archery;
        private ChariotWeapon _chariot;
        private RitualWeapon _ritual;

        /// <summary>上一次活跃的武器（用于切换时重置）</summary>
        private EWeapon _previousWeapon;

        // ==================== Unity 生命周期 ====================

        private void Awake()
        {
            _archery = GetComponent<ArcheryWeapon>();
            _chariot = GetComponent<ChariotWeapon>();
            _ritual = GetComponent<RitualWeapon>();

            if (_archery == null)
                Debug.LogWarning("[PlayerCombat] 未找到 ArcheryWeapon 组件，请挂载到 Player 上");
            if (_chariot == null)
                Debug.LogWarning("[PlayerCombat] 未找到 ChariotWeapon 组件，请挂载到 Player 上");
            if (_ritual == null)
                Debug.LogWarning("[PlayerCombat] 未找到 RitualWeapon 组件，请挂载到 Player 上");
        }

        private void Update()
        {
            // 暂停 / 游戏结束时不处理输入
            if (Core.GameManager.Instance != null)
            {
                if (Core.GameManager.Instance.IsPaused || Core.GameManager.Instance.IsGameOver
                    || Core.GameManager.Instance.IsSelectingCharacter)
                    return;
            }

            // 武器快捷键切换（1/2/3）
            HandleWeaponSwitchInput();

            // 根据当前武器分发给对应组件
            switch (currentWeapon)
            {
                case EWeapon.Archery:
                    _archery?.HandleInput();
                    break;
                case EWeapon.Chariot:
                    _chariot?.HandleInput();
                    break;
                case EWeapon.Ritual:
                    _ritual?.HandleInput();
                    break;
            }
        }

        // ==================== 武器切换 ====================

        /// <summary>
        /// 监听数字键 1/2/3 进行快速武器切换。
        /// </summary>
        private void HandleWeaponSwitchInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
                SwitchWeapon(EWeapon.Archery);
            else if (Input.GetKeyDown(KeyCode.Alpha2))
                SwitchWeapon(EWeapon.Chariot);
            else if (Input.GetKeyDown(KeyCode.Alpha3))
                SwitchWeapon(EWeapon.Ritual);
        }

        /// <summary>
        /// 切换到指定武器流派。
        /// 先重置"正在离开"的武器状态，防止蓄力/冷却等状态污染到下次切回。
        /// </summary>
        public void SwitchWeapon(EWeapon newWeapon)
        {
            if (currentWeapon == newWeapon) return;

            // 重置正在离开的武器状态（关键：是 currentWeapon，不是 _previousWeapon）
            EWeapon leavingWeapon = currentWeapon;
            ResetWeaponState(leavingWeapon);

            currentWeapon = newWeapon;

            Debug.Log($"[PlayerCombat] 武器切换: {leavingWeapon} → {currentWeapon}");
        }

        /// <summary>
        /// 重置指定武器的运行时状态。
        /// </summary>
        private void ResetWeaponState(EWeapon weapon)
        {
            switch (weapon)
            {
                case EWeapon.Archery:
                    _archery?.ResetState();
                    break;
                case EWeapon.Chariot:
                    _chariot?.ResetState();
                    break;
                case EWeapon.Ritual:
                    _ritual?.ResetState();
                    break;
            }
        }
    }
}
