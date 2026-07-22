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
        public static GameManager Instance { get; private set; }

        public enum GameState { Playing, Paused, GameOver, GameWin }

        [Header("房间设置")]
        public Vector2 roomSize = new Vector2(20f, 14f);

        [Header("波间等待（秒）")]
        [SerializeField, Range(1f, 10f)] private float betweenWaveDelay = 3f;
        [SerializeField, Range(0f, 3f)]  private float firstWaveDelay = 1f;

        // ==================== 运行时状态 ====================

        public GameState CurrentState { get; private set; } = GameState.Playing;
        public int CurrentWave { get; set; }
        public int AliveEnemyCount { get; private set; }
        public int TotalKills { get; private set; }
        public int Score { get; private set; }

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
        }

        private void OnDisable()
        {
            EventBus.OnWaveCleared -= HandleWaveCleared;
            EventBus.OnEnemyKilled -= HandleEnemyKilled;
            EventBus.OnPlayerDied -= HandlePlayerDied;
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

            StartNewGame();
        }

        private void Update()
        {
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
                TriggerGameWin();
                return;
            }

            // 暂停游戏，弹出"继续"按钮，等待玩家手动推进
            Time.timeScale = 0f;
            IsWaitingForContinue = true;
            EventBus.TriggerWaveTransition();
        }

        /// <summary>玩家点击"继续"按钮后调用，进入下一波。</summary>
        public void ContinueToNextWave()
        {
            if (!IsWaitingForContinue) return;

            Time.timeScale = 1f;
            IsWaitingForContinue = false;
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

        /// <summary>玩家死亡 → GameOver。</summary>
        private void HandlePlayerDied()
        {
            if (!IsPlaying) return;
            Time.timeScale = 0f;
            SetState(GameState.GameOver);
            EventBus.TriggerGameOver();
            Debug.Log($"[GameManager] 游戏失败！得分={Score} 击杀={TotalKills}");
        }

        // ==================== 游戏流程 ====================

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

        public void TriggerGameWin()
        {
            if (!IsPlaying) return;
            SetState(GameState.GameWin);
            EventBus.TriggerGameWon();
            Debug.Log($"[GameManager] ★ 游戏胜利！得分={Score} 击杀={TotalKills}");
        }

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
