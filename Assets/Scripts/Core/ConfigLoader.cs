using System;
using System.Collections.Generic;
using UnityEngine;

namespace HundredSchools.Core
{
    // ==================== P0-2: 武器升级路径数据类 ====================

    /// <summary>单条升级效果（完整快照，含所有武器的全部字段）</summary>
    [Serializable]
    public class WeaponUpgradeEffect
    {
        // 通用
        public int damage;
        public float fireRate;
        // 射艺
        public bool chargeUnlocked;
        public int pierceCount;
        public int extraProjectiles;
        // 御艺
        public float dashCooldown;
        public float trailWidth;
        public float dashDistance;
        public int trailDamagePerSec;
        public bool trailDamageZone;
        // 礼艺
        public float fanRange;
        public float barrierDuration;
        public int reflectDamage;
        public int thornDamage;
        public bool barrierMovable;
        public bool reflectIsCircle;
    }

    /// <summary>一个分支选项</summary>
    [Serializable]
    public class WeaponUpgradeBranch
    {
        public string id;
        public string name;
        public string description;
        public WeaponUpgradeEffect effects;
    }

    /// <summary>一个等级配置 —— 可能是线性（effects）或分支（branches[]）</summary>
    [Serializable]
    public class WeaponLevelConfig
    {
        public int level;
        public int upgradeCost;
        public string description;
        public WeaponUpgradeEffect effects;
        public WeaponUpgradeBranch[] branches;

        public bool IsBranch => branches != null && branches.Length >= 2;
    }

    [Serializable]
    public class WeaponConfig
    {
        public string id;
        public string name;
        public WeaponLevelConfig[] levels;
    }

    [Serializable]
    public class WeaponConfigList { public WeaponConfig[] weapons; }

    [Serializable]
    public class EnemyConfig
    {
        public string id;
        public string school;
        public int hp;
        public int damage;
        public float moveSpeed;
        public float shootInterval;
        public float projectileSpeed;
        public int knowledgeDrop;
        public string behavior;  // "approach", "lockdown", "wander"
        public bool isElite;
    }

    [Serializable]
    public class EnemyConfigList { public EnemyConfig[] enemies; }

    [Serializable]
    public class WaveEntry
    {
        public int waveNumber;
        public int enemyCount;
        public float spawnInterval;
        public float wuPercent;
        public float ruPercent;
        public float faPercent;
        public float daoPercent;
        public bool isBossWave;
        public int bossHp;
        public int discipleCount;   // Boss波随从弟子数量
        public string bossSchool;   // "player" | "random" | "remaining"
    }

    [Serializable]
    public class WaveConfigList { public WaveEntry[] waves; }

    [Serializable]
    public class SchoolConfig
    {
        public string id;
        public string name;
        public float attackCoeff;       // 攻击系数（法1.1/儒1.0/道0.9）
        public float knowledgeCoeff;    // 学识掉落系数（法1.15/儒1.0/道0.95）
        public int killHeal;            // 击杀回血（儒5/其他0）
        public bool dodgeNoCooldown;    // 闪避无冷却（道true/其他false）
        public float staminaRecoveryRate; // 体力恢复速率（道1.5/其他1.0）
        public string passiveDescription;
    }

    [Serializable]
    public class SchoolConfigList { public SchoolConfig[] schools; }

    [Serializable]
    public class UpgradeConfig
    {
        public string id;
        public string name;
        public string desc;
        public int cost;
        public string type;   // "damage"|"attackSpeed"|"extraProjectile"|"maxHp"|"moveSpeed"|"staminaRecovery"
        public float value;
    }

    [Serializable]
    public class UpgradeConfigList { public UpgradeConfig[] upgrades; }

    // ==================== ConfigLoader（S05） ====================

    /// <summary>
    /// 配置加载器。从 Resources/Configs/ 加载 JSON 配置文件，缓存解析结果。
    /// ADR-001 数据驱动配置的核心实现。
    ///
    /// 用法：
    ///   ConfigLoader.Init();  // 在 GameManager.Awake 中调用一次
    ///   WeaponConfig w = ConfigLoader.GetWeapon("archery");
    /// </summary>
    public static class ConfigLoader
    {
        private static Dictionary<string, WeaponConfig> _weapons;
        private static Dictionary<string, EnemyConfig> _enemies;
        private static WaveEntry[] _waves;
        private static Dictionary<string, SchoolConfig> _schools;
        private static UpgradeConfig[] _upgrades;
        private static bool _initialized;

        public static bool IsInitialized => _initialized;

        /// <summary>加载全部配置文件。在游戏启动时调用一次。</summary>
        public static void Init()
        {
            if (_initialized) return;

            // 武器配置
            _weapons = new Dictionary<string, WeaponConfig>();
            var weaponList = LoadConfig<WeaponConfigList>("Configs/weapons");
            if (weaponList?.weapons != null)
                foreach (var w in weaponList.weapons) _weapons[w.id] = w;

            // 敌人配置
            _enemies = new Dictionary<string, EnemyConfig>();
            var enemyList = LoadConfig<EnemyConfigList>("Configs/enemies");
            if (enemyList?.enemies != null)
                foreach (var e in enemyList.enemies) _enemies[e.id] = e;

            // 波次配置（直接用数组，无需字典）
            _waves = LoadConfig<WaveConfigList>("Configs/waves")?.waves
                ?? Array.Empty<WaveEntry>();

            // 学派配置
            _schools = new Dictionary<string, SchoolConfig>();
            var schoolList = LoadConfig<SchoolConfigList>("Configs/schools");
            if (schoolList?.schools != null)
                foreach (var s in schoolList.schools) _schools[s.id] = s;

            // 升级词条配置
            _upgrades = LoadConfig<UpgradeConfigList>("Configs/upgrades")?.upgrades
                ?? System.Array.Empty<UpgradeConfig>();

            _initialized = true;
            Debug.Log($"[ConfigLoader] 初始化完成：{_weapons.Count}武器, {_enemies.Count}敌人, {_waves.Length}波次, {_schools.Count}学派, {_upgrades.Length}升级词条");
        }

        private static T LoadConfig<T>(string path) where T : class
        {
            TextAsset asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[ConfigLoader] 找不到配置文件：Resources/{path}.json");
                return null;
            }

            try
            {
                T config = JsonUtility.FromJson<T>(asset.text);
                if (config == null)
                    Debug.LogError($"[ConfigLoader] 解析失败：{path}.json");
                return config;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigLoader] 解析异常 {path}.json: {e.Message}");
                return null;
            }
        }

        // === 查询接口 ===

        public static WeaponConfig GetWeapon(string id)
        {
            _weapons.TryGetValue(id, out var cfg);
            return cfg;
        }

        public static EnemyConfig GetEnemy(string id)
        {
            _enemies.TryGetValue(id, out var cfg);
            return cfg;
        }

        public static WaveEntry[] GetAllWaves() => _waves;

        public static WaveEntry GetWave(int index)
        {
            if (index >= 0 && index < _waves.Length) return _waves[index];
            return null;
        }

        public static SchoolConfig GetSchool(string id)
        {
            _schools.TryGetValue(id, out var cfg);
            return cfg;
        }

        /// <summary>根据 ESchool 枚举获取学派配置。</summary>
        public static SchoolConfig GetSchoolConfig(ESchool school) => school switch
        {
            ESchool.Confucian => GetSchool("confucian"),
            ESchool.Legalist => GetSchool("legalist"),
            ESchool.Taoist => GetSchool("taoist"),
            _ => null
        };

        /// <summary>获取全部升级词条。</summary>
        public static UpgradeConfig[] GetAllUpgrades() => _upgrades;

        // === 配置校验（S05 ConfigValidator） ===

        /// <summary>校验已加载配置的完整性。</summary>
        public static List<string> Validate()
        {
            var errors = new List<string>();

            if (_weapons.Count == 0) errors.Add("weapons.json 为空或加载失败");
            if (_enemies.Count == 0) errors.Add("enemies.json 为空或加载失败");
            if (_waves.Length == 0) errors.Add("waves.json 为空或加载失败");
            if (_schools.Count == 0) errors.Add("schools.json 为空或加载失败");
            if (_upgrades.Length == 0) errors.Add("upgrades.json 为空或加载失败");

            foreach (var w in _weapons.Values)
            {
                if (w.levels == null || w.levels.Length == 0)
                    errors.Add($"武器 {w.id} 无等级配置");
            }

            foreach (var e in _enemies.Values)
            {
                if (e.hp <= 0) errors.Add($"敌人 {e.id} HP无效");
                if (e.knowledgeDrop <= 0) errors.Add($"敌人 {e.id} 学识掉落无效");
            }

            // 校验波次中学派百分比之和
            foreach (var wave in _waves)
            {
                if (wave.isBossWave) continue;
                float total = wave.ruPercent + wave.faPercent + wave.daoPercent + wave.wuPercent;
                if (Mathf.Abs(total - 100f) > 0.1f)
                    errors.Add($"波次{wave.waveNumber} 学派百分比之和={total}%，应为100%");
            }

            return errors;
        }
    }
}
