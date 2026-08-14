using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HundredSchools.Core;

namespace HundredSchools.Flow
{
    /// <summary>
    /// WaveSpawner —— 数据驱动的波次生成器。
    ///
    /// 从 ConfigLoader 读取 waves.json 配置（GDD 丐版：5普通波+3Boss波）。
    /// 通过 EventBus 与 GameManager 解耦通信。
    ///
    /// 生成规则：
    ///   - 普通波：按 waves.json 中学派百分比动态随机抽取学派（仅儒/法/道，无"无学派"）
    ///   - Boss波：生成对应学派 Boss + discipleCount 个随从弟子
    ///   - 所有敌人阵亡 → EventBus.TriggerWaveCleared()
    /// </summary>
    public class WaveSpawner : MonoBehaviour
    {
        [Header("生成位置")]
        [SerializeField, Range(3f, 20f)] private float minSpawnRadius = 6f;
        [SerializeField, Range(3f, 20f)] private float maxSpawnRadius = 10f;

        [Header("普通敌人属性")]
        [SerializeField, Range(1f, 10f)] private float enemyMoveSpeed = 3f;
        [SerializeField, Range(10, 200)] private int enemyBaseHp = 25;
        [SerializeField, Range(5, 50)]  private int enemyScoreValue = 10;
        [SerializeField, Range(0, 20)]  private int enemyHpGrowth = 5;

        [Header("精英参数")]
        [SerializeField, Range(0.1f, 1f)] private float eliteChance = 0.15f;

        private WaveEntry[] _waves;
        private Transform _playerTransform;
        private int _currentWaveIndex = -1;
        private int _spawnedInWave;
        private int _killedInWave;
        private int _totalInWave;
        private bool _allSpawned;
        private readonly HashSet<ESchool> _encounteredBossSchools = new HashSet<ESchool>();

        public int WaveCount => _waves?.Length ?? 0;
        public int CurrentWaveIndex => _currentWaveIndex;

        private void Awake()
        {
            _waves = ConfigLoader.GetAllWaves();
            if (_waves == null || _waves.Length == 0)
                Debug.LogError("[WaveSpawner] waves.json 为空或加载失败！请检查 ConfigLoader.Init() 是否已调用。");
        }

        private void OnEnable()
        {
            EventBus.OnEnemyKilled += OnEnemyKilledHandler;
        }

        private void OnDisable()
        {
            EventBus.OnEnemyKilled -= OnEnemyKilledHandler;
        }

        private void Start()
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _playerTransform = playerObj.transform;
            if (_playerTransform == null)
                Debug.LogError("[WaveSpawner] 找不到 Player！请确保 Player GameObject 的 Tag 设为 'Player'");
        }

        // ==================== 公开 API ====================

        /// <summary>由 GameManager 调用，开始指定索引的波次。</summary>
        public void StartWave(int index)
        {
            if (_waves == null || index < 0 || index >= _waves.Length)
            {
                Debug.LogError($"[WaveSpawner] 无效波次索引: {index}");
                return;
            }

            _currentWaveIndex = index;
            var wave = _waves[index];
            _spawnedInWave = 0;
            _killedInWave = 0;
            _allSpawned = false;

            // 新一局游戏，重置 Boss 出场追踪
            if (index == 0)
                _encounteredBossSchools.Clear();

            Core.GameManager.Instance.CurrentWave = wave.waveNumber;
            EventBus.TriggerWaveChanged(wave.waveNumber);

            // 波间自然回血 15HP（GDD §5）
            var player = FindObjectOfType<Player.PlayerMovement>();
            if (player != null) player.Heal(15);

            if (wave.isBossWave)
            {
                _totalInWave = 1 + wave.discipleCount;
                StartCoroutine(SpawnBossWave(wave));
            }
            else
            {
                _totalInWave = wave.enemyCount;
                StartCoroutine(SpawnNormalWave(wave));
            }

            Debug.Log($"[WaveSpawner] === 波次{wave.waveNumber}开始！敌人总数={_totalInWave} Boss波={wave.isBossWave} ===");
        }

        // ==================== 事件处理 ====================

        private void OnEnemyKilledHandler(Vector3 position, int knowledgeValue)
        {
            // 只统计当前波次内的击杀
            if (_allSpawned || _spawnedInWave > 0)
            {
                _killedInWave++;
                Debug.Log($"[WaveSpawner] 击杀进度: {_killedInWave}/{_totalInWave}");

                if (_killedInWave >= _totalInWave && _allSpawned)
                {
                    Debug.Log("[WaveSpawner] 当前波敌人全灭！发布 OnWaveCleared");
                    EventBus.TriggerWaveCleared();
                }
            }
        }

        // ==================== 普通波次生成 ====================

        private IEnumerator SpawnNormalWave(WaveEntry wave)
        {
            while (_spawnedInWave < wave.enemyCount)
            {
                SpawnNormalEnemy(wave);
                _spawnedInWave++;
                yield return new WaitForSeconds(wave.spawnInterval);
            }
            _allSpawned = true;
            Debug.Log($"[WaveSpawner] 波次{wave.waveNumber}：全部 {wave.enemyCount} 敌人已生成");
        }

        private void SpawnNormalEnemy(WaveEntry wave)
        {
            ESchool school = PickRandomSchool(wave);
            Vector2 pos = GetRandomSpawnPosition();
            bool isElite = ShouldSpawnElite(wave);

            int hp = enemyBaseHp + _currentWaveIndex * enemyHpGrowth;

            GameObject obj = new GameObject($"Enemy_{school}_W{wave.waveNumber}");
            obj.transform.position = pos;

            Enemy.EnemyBase enemy = obj.AddComponent<Enemy.EnemyBase>();
            enemy.Init(school, enemyMoveSpeed, hp, enemyScoreValue);

            if (isElite)
            {
                enemy.InitElite(school);
            }

            Enemy.EnemyAI ai = obj.AddComponent<Enemy.EnemyAI>();
            if (_playerTransform != null) ai.SetTarget(_playerTransform);

            obj.tag = "Enemy";

            Core.GameManager.Instance.OnEnemySpawned();
        }

        /// <summary>根据波次学派百分比随机选取学派（仅儒/法/道）。</summary>
        private ESchool PickRandomSchool(WaveEntry wave)
        {
            float total = wave.ruPercent + wave.faPercent + wave.daoPercent;
            if (total <= 0f) return ESchool.Taoist;

            float roll = Random.Range(0f, total);
            float cumulative = 0f;

            cumulative += wave.ruPercent;
            if (roll < cumulative) return ESchool.Confucian;
            cumulative += wave.faPercent;
            if (roll < cumulative) return ESchool.Legalist;
            return ESchool.Taoist;
        }

        /// <summary>波次3开始有概率出现精英弟子。</summary>
        private bool ShouldSpawnElite(WaveEntry wave)
        {
            if (wave.waveNumber < 3) return false;
            return Random.value < eliteChance;
        }

        // ==================== Boss 波次生成 ====================

        private IEnumerator SpawnBossWave(WaveEntry wave)
        {
            // 确定 Boss 学派
            ESchool bossSchool = DetermineBossSchool(wave.bossSchool);

            // 先生成随从弟子
            for (int i = 0; i < wave.discipleCount; i++)
            {
                SpawnDisciple(bossSchool, wave.waveNumber);
                _spawnedInWave++;
                yield return new WaitForSeconds(0.3f);
            }

            // 生成 Boss
            SpawnBoss(bossSchool, wave.bossHp);
            _spawnedInWave++;

            _allSpawned = true;
            Debug.Log($"[WaveSpawner] Boss波{wave.waveNumber}：Boss({bossSchool}) + {wave.discipleCount}弟子 已全部生成");
        }

        private ESchool DetermineBossSchool(string rule)
        {
            switch (rule)
            {
                case "player":
                {
                    // Boss1: 必须打本派宗师（GDD §8）
                    ESchool s = GetPlayerSchool();
                    _encounteredBossSchools.Add(s);
                    return s;
                }
                case "random":
                {
                    // Boss2: 随机，不与已出现的Boss重复
                    var pool = new List<ESchool> { ESchool.Confucian, ESchool.Legalist, ESchool.Taoist };
                    pool.RemoveAll(s => _encounteredBossSchools.Contains(s));
                    if (pool.Count == 0) return ESchool.Taoist; // 兜底
                    ESchool chosen = pool[Random.Range(0, pool.Count)];
                    _encounteredBossSchools.Add(chosen);
                    return chosen;
                }
                case "remaining":
                {
                    // Boss3: 剩余未遇到的宗师
                    var pool = new List<ESchool> { ESchool.Confucian, ESchool.Legalist, ESchool.Taoist };
                    pool.RemoveAll(s => _encounteredBossSchools.Contains(s));
                    ESchool last = pool.Count > 0 ? pool[0] : ESchool.Taoist;
                    _encounteredBossSchools.Add(last);
                    return last;
                }
                default:
                    return ESchool.Taoist;
            }
        }

        private ESchool GetPlayerSchool()
        {
            if (_playerTransform != null)
            {
                var pm = _playerTransform.GetComponent<Player.PlayerMovement>();
                if (pm != null) return pm.CurrentSchool;
            }
            return ESchool.Taoist;
        }

        private void SpawnDisciple(ESchool school, int waveNumber)
        {
            Vector2 pos = GetRandomSpawnPosition();
            GameObject obj = new GameObject($"Disciple_{school}_W{waveNumber}");
            obj.transform.position = pos;

            Enemy.EnemyBase enemy = obj.AddComponent<Enemy.EnemyBase>();
            enemy.Init(school, enemyMoveSpeed, enemyBaseHp + _currentWaveIndex * enemyHpGrowth, enemyScoreValue);

            Enemy.EnemyAI ai = obj.AddComponent<Enemy.EnemyAI>();
            if (_playerTransform != null) ai.SetTarget(_playerTransform);

            obj.tag = "Enemy";
            Core.GameManager.Instance.OnEnemySpawned();
        }

        private void SpawnBoss(ESchool school, int bossHp)
        {
            Vector2 pos = GetRandomSpawnPosition();
            GameObject obj = new GameObject($"Boss_{school}");
            obj.transform.position = pos;

            Enemy.DaoBoss boss = obj.AddComponent<Enemy.DaoBoss>();
            boss.Init(school, 3f, bossHp, 100);
            boss.knowledgeValue = 200; // Boss 学识掉落（GDD: 200×学派系数+100固定）
            boss.ActivateBoss();

            obj.tag = "Enemy";
            Core.GameManager.Instance.OnEnemySpawned();

            Debug.Log($"[WaveSpawner] Boss({school}) 已生成 @ {pos}, HP={bossHp}");
        }

        // ==================== 生成位置 ====================

        private Vector2 GetRandomSpawnPosition()
        {
            Vector2 center = _playerTransform != null ? (Vector2)_playerTransform.position : Vector2.zero;
            Vector2 halfRoom = Core.GameManager.Instance != null
                ? Core.GameManager.Instance.roomSize * 0.5f
                : new Vector2(10f, 7f);

            for (int attempt = 0; attempt < 10; attempt++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float dist = Random.Range(minSpawnRadius, maxSpawnRadius);
                Vector2 candidate = center + new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
                candidate.x = Mathf.Clamp(candidate.x, -halfRoom.x, halfRoom.x);
                candidate.y = Mathf.Clamp(candidate.y, -halfRoom.y, halfRoom.y);

                var nearby = Physics2D.OverlapCircleAll(candidate, 1.5f);
                bool crowded = false;
                foreach (var col in nearby)
                {
                    if (col.CompareTag("Enemy") || col.CompareTag("Player"))
                    { crowded = true; break; }
                }
                if (!crowded) return candidate;
            }

            // 兜底
            float fbA = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float fbD = Random.Range(minSpawnRadius, maxSpawnRadius);
            Vector2 fallback = center + new Vector2(Mathf.Cos(fbA) * fbD, Mathf.Sin(fbA) * fbD);
            fallback.x = Mathf.Clamp(fallback.x, -halfRoom.x, halfRoom.x);
            fallback.y = Mathf.Clamp(fallback.y, -halfRoom.y, halfRoom.y);
            return fallback;
        }
    }
}
