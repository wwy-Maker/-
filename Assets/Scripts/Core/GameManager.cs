using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace HundredSchools.Core
{
    /// <summary>
    /// GameManager —— 全局状态管理器（单例），事件驱动版本。
    ///
    /// 状态机：Playing → Paused / GameOver / GameWin
    /// 波次推进：通过 EventBus.OnWaveCleared 驱动，不再轮询存活敌人数量。
    /// 波次生成：通过 WaveSpawner.StartWave(index) 触发。
    ///
    /// 初始化顺序（Awake）：
    ///   1. ConfigLoader.Init() —— 加载全部 JSON 配置
    ///   2. 单例注册
    ///   3. 订阅 EventBus 事件
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        /// <summary>难度参数配置（乘数作用于基础值）。</summary>
        public struct DifficultyConfig
        {
            public float hpMult;
            public float spdMult;
            public float fireRateMult;
            public int playerInitialHp;
            public float knowledgeMult;
        }

        private static readonly System.Collections.Generic.Dictionary<EDifficulty, DifficultyConfig> _difficultyConfigs =
            new System.Collections.Generic.Dictionary<EDifficulty, DifficultyConfig>
            {
                { EDifficulty.Easy,   new DifficultyConfig { hpMult = 0.7f, spdMult = 0.8f, fireRateMult = 0.7f, playerInitialHp = 120, knowledgeMult = 1.3f } },
                { EDifficulty.Normal, new DifficultyConfig { hpMult = 1.0f, spdMult = 1.0f, fireRateMult = 1.0f, playerInitialHp = 100, knowledgeMult = 1.0f } },
                { EDifficulty.Hard,   new DifficultyConfig { hpMult = 1.4f, spdMult = 1.2f, fireRateMult = 1.3f, playerInitialHp = 80,  knowledgeMult = 0.8f } },
            };
        public static GameManager Instance { get; private set; }

        public enum GameState { Playing, Paused, GameOver, GameWin }

        [Header("房间设置")]
        public Vector2 roomSize = new Vector2(20f, 14f);

        [Header("波间等待（秒）")]
        [SerializeField, Range(0f, 3f)]  private float firstWaveDelay = 1f;

        // ==================== 运行时状态 ====================

        public GameState CurrentState { get; private set; } = GameState.Playing;
        public int CurrentWave { get; set; }
        public int AliveEnemyCount { get; private set; }
        public int TotalKills { get; private set; }
        public int Score { get; private set; }

        /// <summary>游戏是否正在运行（死亡/胜利后为 false）</summary>
        public bool IsGameRunning { get; private set; } = true;

        /// <summary>本局存活时间（秒）</summary>
        public float SurviveTime { get; private set; }

        private int _bossesKilled;
        private float _gameStartTime;

        /// <summary>当前连杀数（3秒内无击杀则重置）</summary>
        public int CurrentCombo { get; private set; }

        /// <summary>本局最高连杀数</summary>
        public int MaxCombo { get; private set; }

        private float _comboTimer;
        private const float ComboTimeout = 3f;

        public bool IsPaused   => CurrentState == GameState.Paused;
        public bool IsGameOver => CurrentState == GameState.GameOver;
        public bool IsGameWin  => CurrentState == GameState.GameWin;
        public bool IsPlaying  => CurrentState == GameState.Playing;

        // ==================== P0-1: 开局选择 ====================

        /// <summary>是否正在角色选择阶段（此时禁止玩家操作）</summary>
        public bool IsSelectingCharacter { get; private set; } = true;

        /// <summary>玩家选择的学派</summary>
        public ESchool SelectedSchool { get; private set; } = ESchool.Confucian;

        /// <summary>玩家选择的主武器</summary>
        public EWeapon SelectedMainWeapon { get; private set; } = EWeapon.Archery;

        /// <summary>玩家选择的副技能（P0-3 接入）</summary>
        public EWeapon SelectedSubSkill { get; set; } = EWeapon.Chariot;

        // ==================== UnityEvent（保留向后兼容） ====================

        public UnityEvent<GameState> OnGameStateChanged = new UnityEvent<GameState>();
        public UnityEvent<int> OnWaveChanged = new UnityEvent<int>();
        public UnityEvent OnWaveCleared = new UnityEvent();
        public UnityEvent OnEnemyKilledEvent = new UnityEvent();
        public UnityEvent<int> OnScoreChanged = new UnityEvent<int>();

        // ==================== 运行时引用 ====================

        private Flow.WaveSpawner _waveSpawner;
        private int _nextWaveIndex;

        /// <summary>是否正在等待玩家点击"继续"进入下一波。</summary>
        public bool IsWaitingForContinue { get; private set; }

        /// <summary>是否处于波间升级阶段（升级面板打开时）。</summary>
        public bool IsInUpgradePhase { get; private set; }

        /// <summary>当前难度等级。由 SchoolSelectPanel 在开局前设置。</summary>
        public EDifficulty CurrentDifficulty { get; private set; } = EDifficulty.Normal;

        /// <summary>获取当前难度参数配置。</summary>
        public DifficultyConfig GetDifficultyConfig() => _difficultyConfigs[CurrentDifficulty];

        /// <summary>由 SchoolSelectPanel 在开局前调用，设置难度等级。</summary>
        public void SetDifficulty(EDifficulty d) => CurrentDifficulty = d;

        // ==================== Unity 生命周期 ====================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 地基层：加载全部 JSON 配置
            ConfigLoader.Init();
            var errors = ConfigLoader.Validate();
            if (errors.Count > 0)
            {
                foreach (var e in errors)
                    Debug.LogError($"[GameManager] 配置校验失败: {e}");
            }

            // 注册到服务定位器
            ServiceLocator.Register(this);
        }

        private void OnEnable()
        {
            EventBus.OnWaveCleared += HandleWaveCleared;
            EventBus.OnEnemyKilled += HandleEnemyKilled;
            EventBus.OnPlayerDied += HandlePlayerDied;
            EventBus.OnBossKilled += HandleBossKilled;
        }

        private void OnDisable()
        {
            EventBus.OnWaveCleared -= HandleWaveCleared;
            EventBus.OnEnemyKilled -= HandleEnemyKilled;
            EventBus.OnPlayerDied -= HandlePlayerDied;
            EventBus.OnBossKilled -= HandleBossKilled;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            _waveSpawner = FindObjectOfType<Flow.WaveSpawner>();
            if (_waveSpawner == null)
            {
                Debug.LogError("[GameManager] 场景中找不到 WaveSpawner！请在场景中添加 WaveSpawner 组件。");
                return;
            }

            // 通知 HUD 总波数
            var hud = FindObjectOfType<UI.HUD>();
            if (hud != null) hud.SetTotalWaves(_waveSpawner.WaveCount);

            // 确保 GameOverPanel 存在
            if (FindObjectOfType<UI.GameOverPanel>() == null)
            {
                var goPanel = new GameObject("GameOverPanel");
                goPanel.AddComponent<UI.GameOverPanel>();
            }

            if (FindObjectOfType<UI.ItemShop>() == null)
            {
                var goShop = new GameObject("ItemShop");
                goShop.AddComponent<UI.ItemShop>();
            }

            // P0-1: 显示开局选择面板，等待玩家确认后再 StartNewGame
            IsSelectingCharacter = true;
            var selectPanel = FindObjectOfType<UI.SchoolSelectPanel>(true);
            if (selectPanel != null)
            {
                selectPanel.Show();
            }
            else
            {
                // 兜底：如果没有选择面板，直接用默认值开始
                Debug.LogWarning("[GameManager] 未找到 SchoolSelectPanel，使用默认选择直接开始");
                ConfirmSelectionAndStart(SelectedSchool, SelectedMainWeapon, SelectedSubSkill);
            }
        }

        private void Update()
        {
            if (!IsGameRunning) return;

            SurviveTime += Time.unscaledDeltaTime;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (CurrentState == GameState.Playing) PauseGame();
                else if (CurrentState == GameState.Paused) ResumeGame();
            }

            // 连杀超时重置
            if (IsPlaying && CurrentCombo > 0)
            {
                _comboTimer -= Time.unscaledDeltaTime;
                if (_comboTimer <= 0f)
                    CurrentCombo = 0;
            }
        }

        // ==================== EventBus 事件处理 ====================

        /// <summary>波次清空 → 暂停并显示"继续"按钮，等待玩家手动推进。</summary>
        private void HandleWaveCleared()
        {
            if (!IsPlaying) return;

            _nextWaveIndex++;
            Debug.Log($"[GameManager] 波次清空！下一波索引={_nextWaveIndex}, 总波数={_waveSpawner.WaveCount}");

            if (_nextWaveIndex >= _waveSpawner.WaveCount)
            {
                // All waves done — victory now determined by _bossesKilled in HandleBossKilled()
                return;
            }

            // 暂停游戏，弹出"继续"按钮，等待玩家手动推进
            Time.timeScale = 0f;
            IsWaitingForContinue = true;
            IsInUpgradePhase = false;
            EventBus.TriggerWaveTransition();
        }

        /// <summary>玩家点击"继续"后调用，关闭波间等待状态，进入升级阶段。</summary>
        public void EnterUpgradePhase()
        {
            IsWaitingForContinue = false;
            IsInUpgradePhase = true;
        }

        /// <summary>玩家在升级面板点击"继续下一波"后调用，开始下一波。</summary>
        public void ContinueToNextWave()
        {
            if (!IsInUpgradePhase && !IsWaitingForContinue) return;

            Time.timeScale = 1f;
            IsWaitingForContinue = false;
            IsInUpgradePhase = false;
            _waveSpawner.StartWave(_nextWaveIndex);
        }

        /// <summary>敌人击杀 → 更新统计。</summary>
        private void HandleEnemyKilled(Vector3 position, int knowledgeValue)
        {
            if (!IsPlaying) return;

            TotalKills++;
            AliveEnemyCount = Mathf.Max(0, AliveEnemyCount - 1);
            AddScore(10);
            OnEnemyKilledEvent.Invoke();

            // 连杀追踪
            CurrentCombo++;
            _comboTimer = ComboTimeout;
            if (CurrentCombo > MaxCombo)
                MaxCombo = CurrentCombo;
        }

        /// <summary>Boss 击杀 → 计数，≥3 触发胜利。</summary>
        private void HandleBossKilled()
        {
            if (!IsPlaying || !IsGameRunning) return;
            _bossesKilled++;
            Debug.Log($"[GameManager] Boss 击杀！{_bossesKilled}/3");
            if (_bossesKilled >= 3)
            {
                IsGameRunning = false;
                Time.timeScale = 0f;
                SetState(GameState.GameWin);
                EventBus.TriggerGameOver(true);
                Debug.Log($"[GameManager] ★ 胜利！得分={Score} 击杀={TotalKills} 存活={SurviveTime:F1}s");
            }
        }

        /// <summary>玩家死亡 → GameOver。</summary>
        private void HandlePlayerDied()
        {
            if (!IsPlaying || !IsGameRunning) return;
            IsGameRunning = false;
            Time.timeScale = 0f;
            SetState(GameState.GameOver);
            EventBus.TriggerGameOver(false);
            Debug.Log($"[GameManager] 游戏失败！得分={Score} 击杀={TotalKills} 存活={SurviveTime:F1}s");
        }

        // ==================== 游戏流程 ====================

        /// <summary>
        /// P0-1: 玩家确认选择后调用。应用学派/武器到 Player，然后开始游戏。
        /// </summary>
        public void ConfirmSelectionAndStart(ESchool school, EWeapon mainWeapon, EWeapon subSkill)
        {
            SelectedSchool = school;
            SelectedMainWeapon = mainWeapon;
            SelectedSubSkill = subSkill;

            // 应用到玩家
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                var pm = playerObj.GetComponent<Player.PlayerMovement>();
                if (pm != null)
                {
                    pm.SwitchSchool(school);
                }

                var pc = playerObj.GetComponent<Player.PlayerCombat>();
                if (pc != null)
                {
                    pc.SwitchWeapon(mainWeapon);
                }
            }

            IsSelectingCharacter = false;

            // P0-2: 初始化 UpgradeManager 的升级路径
            var um = Economy.UpgradeManager.Instance;
            if (um != null) um.Init(mainWeapon, subSkill);

            StartNewGame();
        }

        public void StartNewGame()
        {
            CurrentWave = 0;
            AliveEnemyCount = 0;
            TotalKills = 0;
            Score = 0;
            CurrentCombo = 0;
            MaxCombo = 0;
            _comboTimer = 0f;
            _nextWaveIndex = 0;
            _bossesKilled = 0;
            IsGameRunning = true;
            SurviveTime = 0f;
            _gameStartTime = Time.unscaledTime;
            Time.timeScale = 1f;
            SetState(GameState.Playing);

            StartCoroutine(StartFirstWave());
        }

        private IEnumerator StartFirstWave()
        {
            yield return new WaitForSeconds(firstWaveDelay);
            if (_waveSpawner != null)
                _waveSpawner.StartWave(0);
        }

        public void PauseGame()
        {
            if (!IsPlaying) return;
            Time.timeScale = 0f;
            SetState(GameState.Paused);
        }

        public void ResumeGame()
        {
            if (!IsPaused) return;
            Time.timeScale = 1f;
            SetState(GameState.Playing);
        }

        // TriggerGameWin removed — victory now triggered by HandleBossKilled() when _bossesKilled >= 3

        // ==================== 波次计数（保持旧接口兼容） ====================

        public void OnEnemySpawned()
        {
            AliveEnemyCount++;
        }

        public void OnEnemyKilled(int scoreValue = 10)
        {
            AliveEnemyCount = Mathf.Max(0, AliveEnemyCount - 1);
            AddScore(scoreValue);
            OnEnemyKilledEvent.Invoke();
            // 波次完成判定已迁移到 WaveSpawner + EventBus，此处仅维护统计
        }

        public void OnPlayerDied()
        {
            EventBus.TriggerPlayerDied();
        }

        public void AddScore(int amount)
        {
            if (amount <= 0) return;
            Score += amount;
            OnScoreChanged.Invoke(Score);
            EventBus.TriggerScoreChanged(Score);
        }

        // ==================== 内部方法 ====================

        private void SetState(GameState newState)
        {
            if (CurrentState == newState) return;
            CurrentState = newState;
            OnGameStateChanged.Invoke(newState);
            EventBus.TriggerGameStateChanged(newState);
        }
    }
}
