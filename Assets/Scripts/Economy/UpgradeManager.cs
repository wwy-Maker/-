using UnityEngine;
using HundredSchools.Core;

namespace HundredSchools.Economy
{
    /// <summary>
    /// UpgradeManager —— 波间升级系统管理器（P0-2 重写）。
    ///
    /// 持有主武器和副技能的升级路径状态。
    /// 对外暴露查询和消费接口，由 UpgradePanel 调用。
    /// </summary>
    public class UpgradeManager : MonoBehaviour
    {
        public static UpgradeManager Instance { get; private set; }

        private WeaponUpgradePath _mainWeaponPath;
        private WeaponUpgradePath _subSkillPath;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>游戏开始时调用，根据玩家选择初始化升级路径。</summary>
        public void Init(EWeapon mainWeapon, EWeapon subSkill)
        {
            string mainId = WeaponToId(mainWeapon);
            string subId = WeaponToId(subSkill);

            var mainCfg = ConfigLoader.GetWeapon(mainId);
            var subCfg = ConfigLoader.GetWeapon(subId);

            _mainWeaponPath = mainCfg != null ? new WeaponUpgradePath(mainCfg) : null;
            _subSkillPath = subCfg != null ? new WeaponUpgradePath(subCfg) : null;

            Debug.Log($"[UpgradeManager] 初始化: 主武器={mainWeapon}(Lv{MainLevel}), 副技能={subSkill}(Lv{SubLevel})");
        }

        // ==================== 查询接口 ====================

        public int MainLevel => _mainWeaponPath?.CurrentLevel ?? 1;
        public int SubLevel => _subSkillPath?.CurrentLevel ?? 1;
        public string MainWeaponName => _mainWeaponPath?.WeaponName ?? "";
        public string SubSkillName => _subSkillPath?.WeaponName ?? "";

        public bool CanUpgradeMain => _mainWeaponPath != null && _mainWeaponPath.CanUpgrade;
        public bool CanUpgradeSub => _subSkillPath != null && _subSkillPath.CanUpgrade;

        public int MainUpgradeCost => _mainWeaponPath?.GetUpgradeCost() ?? int.MaxValue;
        public int SubUpgradeCost => _subSkillPath?.GetUpgradeCost() ?? int.MaxValue;

        public (bool isBranch, WeaponUpgradeBranch[] branches, int cost) GetMainNextOptions() =>
            _mainWeaponPath?.GetNextOptions() ?? (false, null, int.MaxValue);

        public (bool isBranch, WeaponUpgradeBranch[] branches, int cost) GetSubNextOptions() =>
            _subSkillPath?.GetNextOptions() ?? (false, null, int.MaxValue);

        // ==================== 消费接口 ====================

        /// <summary>升级主武器。扣学识 → 应用升级 → 更新武器组件。</summary>
        public bool UpgradeMain(string branchId = null)
        {
            if (_mainWeaponPath == null || !_mainWeaponPath.CanUpgrade) return false;

            int cost = _mainWeaponPath.GetUpgradeCost();
            if (KnowledgeManager.Instance == null || !KnowledgeManager.Instance.SpendKnowledge(cost))
                return false;

            _mainWeaponPath.ApplyUpgrade(branchId);
            ApplyToComponent(_mainWeaponPath, GameManager.Instance?.SelectedMainWeapon ?? EWeapon.Archery);
            Debug.Log($"[UpgradeManager] 主武器升到 Lv{_mainWeaponPath.CurrentLevel}, 消耗{cost}学识");
            return true;
        }

        /// <summary>升级副技能。</summary>
        public bool UpgradeSub(string branchId = null)
        {
            if (_subSkillPath == null || !_subSkillPath.CanUpgrade) return false;

            int cost = _subSkillPath.GetUpgradeCost();
            if (KnowledgeManager.Instance == null || !KnowledgeManager.Instance.SpendKnowledge(cost))
                return false;

            _subSkillPath.ApplyUpgrade(branchId);
            ApplyToComponent(_subSkillPath, GameManager.Instance?.SelectedSubSkill ?? EWeapon.Archery);
            Debug.Log($"[UpgradeManager] 副技能升到 Lv{_subSkillPath.CurrentLevel}, 消耗{cost}学识");
            return true;
        }

        /// <summary>切换副技能到指定艺。当前等级继承到新副技能（P0-3 细化）。</summary>
        public void SwitchSubSkill(EWeapon newSub)
        {
            // P0-3: 副技能切换逻辑（保留等级，重新 Init）
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.SelectedSubSkill = newSub;
                var cfg = ConfigLoader.GetWeapon(WeaponToId(newSub));
                if (cfg != null)
                {
                    _subSkillPath = new WeaponUpgradePath(cfg);
                    // P0-3: 如果副技能等级应继承，在这里追加升级
                }
            }
        }

        // ==================== 内部 ====================

        /// <summary>将 WeaponUpgradePath 的累计效果应用到对应武器组件。</summary>
        private void ApplyToComponent(WeaponUpgradePath path, EWeapon weaponType)
        {
            var effect = path.GetCumulativeEffect();
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj == null) return;

            switch (weaponType)
            {
                case EWeapon.Archery:
                    playerObj.GetComponent<Combat.ArcheryWeapon>()?.ApplyUpgradeEffect(effect);
                    break;
                case EWeapon.Chariot:
                    playerObj.GetComponent<Combat.ChariotWeapon>()?.ApplyUpgradeEffect(effect);
                    break;
                case EWeapon.Ritual:
                    playerObj.GetComponent<Combat.RitualWeapon>()?.ApplyUpgradeEffect(effect);
                    break;
            }
        }

        private static string WeaponToId(EWeapon weapon) => weapon switch
        {
            EWeapon.Archery => "archery",
            EWeapon.Chariot => "chariot",
            EWeapon.Ritual  => "ritual",
            _ => "archery"
        };
    }
}
