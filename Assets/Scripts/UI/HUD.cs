using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using HundredSchools.Core;

namespace HundredSchools.UI
{
    /// <summary>
    /// HUD —— 生存反馈 UI。
    ///
    /// P0：血条 + 体力条 + 波次文本
    /// P1：学识计数器（击杀即加，跳过拾取动画）
    /// P2：波间"继续"按钮面板
    ///
    /// 挂载到：场景中的 Canvas GameObject。
    /// 依赖：需要在 Canvas 下预先创建好 Slider/Text/Button 子物体并拖入 Inspector。
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [Header("血条")]
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Image hpFillImage;

        [Header("体力条")]
        [SerializeField] private Slider staminaSlider;
        [SerializeField] private Image staminaFillImage;

        [Header("波次文本")]
        [SerializeField] private Text waveText;

        [Header("学识计数器")]
        [SerializeField] private Text knowledgeText;

        [Header("波间过渡面板")]
        [SerializeField] private GameObject continuePanel;
        [SerializeField] private Button continueButton;
        [SerializeField] private Text continueWaveText;

        [Header("死亡面板")]
        [SerializeField] private GameObject deathPanel;
        [SerializeField] private Text deathWaveText;
        [SerializeField] private Text deathKillsText;
        [SerializeField] private Text deathComboText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button menuButton;

        private int _totalKnowledge;
        private int _totalWaves;

        // ==================== Unity 生命周期 ====================

        private void Start()
        {
            if (hpSlider != null) hpSlider.minValue = 0;
            if (staminaSlider != null) staminaSlider.minValue = 0;

            if (continuePanel != null) continuePanel.SetActive(false);
            if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);

            if (deathPanel != null) deathPanel.SetActive(false);
            if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
            if (menuButton != null) menuButton.onClick.AddListener(OnMenuClicked);
        }

        private void OnEnable()
        {
            EventBus.OnPlayerDamaged += HandlePlayerDamaged;
            EventBus.OnPlayerHealed += HandlePlayerHealed;
            EventBus.OnStaminaChanged += HandleStaminaChanged;
            EventBus.OnWaveChanged += HandleWaveChanged;
            EventBus.OnEnemyKilled += HandleEnemyKilled;
            EventBus.OnWaveTransition += HandleWaveTransition;
            EventBus.OnGameWon += HandleGameWon;
            EventBus.OnGameOver += HandleGameOver;
        }

        private void OnDisable()
        {
            EventBus.OnPlayerDamaged -= HandlePlayerDamaged;
            EventBus.OnPlayerHealed -= HandlePlayerHealed;
            EventBus.OnStaminaChanged -= HandleStaminaChanged;
            EventBus.OnWaveChanged -= HandleWaveChanged;
            EventBus.OnEnemyKilled -= HandleEnemyKilled;
            EventBus.OnWaveTransition -= HandleWaveTransition;
            EventBus.OnGameWon -= HandleGameWon;
            EventBus.OnGameOver -= HandleGameOver;
        }

        private void Update()
        {
            // 波间等待期间不轮询（timescale=0）
            if (Core.GameManager.Instance != null && Core.GameManager.Instance.IsWaitingForContinue)
                return;

            // 体力条：直接轮询 PlayerMovement（比事件更可靠，不漏帧）
            PollStamina();
        }

        // ==================== 事件处理 ====================

        private void HandlePlayerDamaged(float currentHp, float maxHp)
        {
            UpdateHpSlider(currentHp, maxHp);
        }

        private void HandlePlayerHealed(float amount)
        {
            // HP 变化由 OnPlayerDamaged 统一刷新（Heal 也会触发它）
        }

        private void HandleStaminaChanged(float current, float max)
        {
            UpdateStaminaSlider(current, max);
        }

        private void HandleWaveChanged(int waveNumber)
        {
            if (waveText != null)
                waveText.text = $"第 {waveNumber} / {_totalWaves} 波";
        }

        private void HandleEnemyKilled(Vector3 position, int knowledgeValue)
        {
            _totalKnowledge += knowledgeValue;
            UpdateKnowledgeText();
        }

        private void HandleWaveTransition()
        {
            if (continuePanel != null)
            {
                continuePanel.SetActive(true);
                if (continueWaveText != null && Core.GameManager.Instance != null)
                {
                    int nextWave = Core.GameManager.Instance.CurrentWave + 1;
                    continueWaveText.text = $"第 {Core.GameManager.Instance.CurrentWave} 波 完成！\n准备进入第 {nextWave} 波";
                }
            }
        }

        private void HandleGameWon()
        {
            if (waveText != null)
                waveText.text = "通关！";

            if (continuePanel != null)
            {
                continuePanel.SetActive(true);
                if (continueWaveText != null)
                    continueWaveText.text = $"★ 诸子百家，口诛笔伐 ★\n\n总学识: {_totalKnowledge}\n得分: {Core.GameManager.Instance?.Score ?? 0}";
                if (continueButton != null)
                {
                    var label = continueButton.GetComponentInChildren<Text>();
                    if (label != null) label.text = "再来一局";
                }
            }
        }

        // ==================== UI 更新 ====================

        private void UpdateHpSlider(float current, float max)
        {
            if (hpSlider != null)
            {
                hpSlider.maxValue = max;
                hpSlider.value = current;
            }

            if (hpFillImage != null)
            {
                float ratio = max > 0 ? current / max : 0f;
                hpFillImage.fillAmount = ratio;

                // 低血量变红
                if (ratio <= 0.3f)
                    hpFillImage.color = Color.red;
                else if (ratio <= 0.6f)
                    hpFillImage.color = Color.yellow;
                else
                    hpFillImage.color = Color.green;
            }
        }

        private void UpdateStaminaSlider(float current, float max)
        {
            if (staminaSlider != null)
            {
                staminaSlider.maxValue = max;
                staminaSlider.value = current;
            }

            if (staminaFillImage != null)
            {
                float ratio = max > 0 ? current / max : 0f;
                staminaFillImage.fillAmount = ratio;
            }
        }

        private void UpdateKnowledgeText()
        {
            if (knowledgeText != null)
                knowledgeText.text = $"学识: {_totalKnowledge}";
        }

        private void PollStamina()
        {
            if (staminaSlider == null) return;

            var player = FindObjectOfType<Player.PlayerMovement>();
            if (player != null)
                UpdateStaminaSlider(player.CurrentStamina, player.MaxStamina);
        }

        // ==================== 按钮回调 ====================

        private void OnContinueClicked()
        {
            if (continuePanel != null)
                continuePanel.SetActive(false);

            if (Core.GameManager.Instance != null)
                Core.GameManager.Instance.ContinueToNextWave();
        }

        private void OnRestartClicked()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void OnMenuClicked()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // ==================== 死亡面板 ====================

        private void HandleGameOver()
        {
            if (deathPanel != null) deathPanel.SetActive(true);

            var gm = Core.GameManager.Instance;
            if (gm == null) return;

            if (deathWaveText != null)
                deathWaveText.text = $"打到了第 {gm.CurrentWave} 波";

            if (deathKillsText != null)
                deathKillsText.text = $"总击杀 {gm.TotalKills}";

            if (deathComboText != null)
                deathComboText.text = $"最高连杀 {gm.MaxCombo}";
        }

        // ==================== 公开接口 ====================

        /// <summary>由外部（如 GameManager.Start）调用，设置总波数。</summary>
        public void SetTotalWaves(int total)
        {
            _totalWaves = total;
            if (waveText != null)
                waveText.text = $"第 0 / {_totalWaves} 波";
        }
    }
}
