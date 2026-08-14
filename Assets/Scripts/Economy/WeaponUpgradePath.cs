using UnityEngine;
using HundredSchools.Core;

namespace HundredSchools.Economy
{
    /// <summary>
    /// WeaponUpgradePath —— 单个武器的 Lv1→Lv5 升级路径状态机。
    ///
    /// 从 weapons.json 读取升级配置，管理当前等级和分支选择。
    /// GetCumulativeEffect() 用 last-write-wins 策略合并 Lv1→currentLevel 全部效果。
    /// </summary>
    public class WeaponUpgradePath
    {
        public string WeaponId { get; private set; }
        public string WeaponName { get; private set; }
        public int CurrentLevel { get; private set; } = 1;
        public bool CanUpgrade => CurrentLevel < 5;

        private WeaponConfig _config;
        private string _chosenBranchLv3;
        private string _chosenBranchLv4;

        public WeaponUpgradePath(WeaponConfig config)
        {
            _config = config;
            WeaponId = config.id;
            WeaponName = config.name;
        }

        /// <summary>获取下一级升级费用。已满级返回 int.MaxValue。</summary>
        public int GetUpgradeCost()
        {
            if (!CanUpgrade) return int.MaxValue;
            return _config.levels[CurrentLevel].upgradeCost;
        }

        /// <summary>
        /// 查询下一级的升级选项。
        /// 返回 (isBranch, branches, cost)。
        /// 线性升级时 branches 为 null。
        /// </summary>
        public (bool isBranch, WeaponUpgradeBranch[] branches, int cost) GetNextOptions()
        {
            if (!CanUpgrade) return (false, null, int.MaxValue);

            var cfg = _config.levels[CurrentLevel];
            if (cfg.IsBranch)
                return (true, cfg.branches, cfg.upgradeCost);
            else
                return (false, null, cfg.upgradeCost);
        }

        /// <summary>执行升级。分支等级需传入 branchId，线性升级忽略。</summary>
        public void ApplyUpgrade(string branchId = null)
        {
            if (!CanUpgrade) return;

            var cfg = _config.levels[CurrentLevel];
            int targetLevel = CurrentLevel + 1;

            if (cfg.IsBranch && !string.IsNullOrEmpty(branchId))
            {
                if (targetLevel == 3) _chosenBranchLv3 = branchId;
                else if (targetLevel == 4) _chosenBranchLv4 = branchId;
            }

            CurrentLevel++;
        }

        /// <summary>
        /// last-write-wins 合并：从 Lv1 遍历到 CurrentLevel，
        /// 每级 effects 全覆盖累计结果。
        /// </summary>
        public WeaponUpgradeEffect GetCumulativeEffect()
        {
            WeaponUpgradeEffect result = new WeaponUpgradeEffect();

            for (int i = 1; i <= CurrentLevel; i++)
            {
                WeaponUpgradeEffect source = GetEffectsForLevel(i);
                if (source != null)
                    OverwriteEffect(result, source);
            }

            return result;
        }

        /// <summary>重置到 Lv1（新游戏用）。</summary>
        public void Reset()
        {
            CurrentLevel = 1;
            _chosenBranchLv3 = null;
            _chosenBranchLv4 = null;
        }

        // ==================== 内部 ====================

        private WeaponUpgradeEffect GetEffectsForLevel(int level)
        {
            var cfg = _config.levels[level - 1]; // 0-indexed

            if (cfg.IsBranch)
            {
                string chosenId = level == 3 ? _chosenBranchLv3
                               : level == 4 ? _chosenBranchLv4
                               : null;
                if (string.IsNullOrEmpty(chosenId)) return null;

                foreach (var b in cfg.branches)
                    if (b.id == chosenId) return b.effects;
                return null;
            }

            return cfg.effects;
        }

        /// <summary>last-write-wins：source 全部字段覆盖 target。</summary>
        private void OverwriteEffect(WeaponUpgradeEffect target, WeaponUpgradeEffect source)
        {
            target.damage = source.damage;
            target.fireRate = source.fireRate;
            target.chargeUnlocked = source.chargeUnlocked;
            target.pierceCount = source.pierceCount;
            target.extraProjectiles = source.extraProjectiles;
            target.dashCooldown = source.dashCooldown;
            target.trailWidth = source.trailWidth;
            target.dashDistance = source.dashDistance;
            target.trailDamagePerSec = source.trailDamagePerSec;
            target.trailDamageZone = source.trailDamageZone;
            target.fanRange = source.fanRange;
            target.barrierDuration = source.barrierDuration;
            target.reflectDamage = source.reflectDamage;
            target.thornDamage = source.thornDamage;
            target.barrierMovable = source.barrierMovable;
            target.reflectIsCircle = source.reflectIsCircle;
        }
    }
}