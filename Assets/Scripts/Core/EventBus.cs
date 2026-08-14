using System;
using UnityEngine;

namespace HundredSchools.Core
{
    /// <summary>
    /// 全局事件总线。解耦组件间通信，替代 FindObjectOfType 和直接引用。
    ///
    /// 所有事件通过静态方法订阅/发布，组件在 OnEnable 订阅、OnDisable 取消订阅。
    ///
    /// 用法：
    ///   EventBus.OnEnemyKilled += HandleEnemyKilled;
    ///   EventBus.TriggerEnemyKilled(25);
    /// </summary>
    public static class EventBus
    {
        // === 游戏流程 ===
        public static event Action<GameManager.GameState> OnGameStateChanged;
        public static event Action<int> OnWaveChanged;
        public static event Action OnWaveCleared;
        public static event Action OnWaveTransition;     // 波间过渡（显示"继续"按钮）
        public static event Action<bool> OnGameOver;     // (isVictory)
        public static event Action OnBossKilled;          // Boss 被击杀

        // === 玩家 ===
        public static event Action<float, float> OnPlayerDamaged;     // (currentHp, maxHp)
        public static event Action<float> OnPlayerHealed;
        public static event Action OnPlayerDied;
        public static event Action<float, float> OnStaminaChanged;     // (currentStamina, maxStamina)
        public static event Action<ESchool> OnSchoolChanged;
        public static event Action<EWeapon> OnWeaponChanged;

        // === 战斗 ===
        public static event Action<Vector3, int> OnEnemyKilled;       // (position, knowledgeValue)
        public static event Action<int> OnScoreChanged;

        // === 经济 ===
        public static event Action<int> OnKnowledgeChanged;            // (totalKnowledge)

        // === Boss ===
        public static event Action<int> OnBossPhaseChange;              // (newPhase)

        // === 升级 ===
        public static event Action<string, int> OnWeaponUpgraded;      // (weaponId, newLevel)

        // ==================== 发布方法 ====================

        public static void TriggerGameStateChanged(GameManager.GameState state) =>
            OnGameStateChanged?.Invoke(state);

        public static void TriggerWaveChanged(int waveNumber) =>
            OnWaveChanged?.Invoke(waveNumber);

        public static void TriggerWaveCleared() =>
            OnWaveCleared?.Invoke();

        public static void TriggerWaveTransition() =>
            OnWaveTransition?.Invoke();

        public static void TriggerGameOver(bool isVictory) =>
            OnGameOver?.Invoke(isVictory);

        public static void TriggerBossKilled() =>
            OnBossKilled?.Invoke();

        public static void TriggerPlayerDamaged(float currentHp, float maxHp) =>
            OnPlayerDamaged?.Invoke(currentHp, maxHp);

        public static void TriggerPlayerHealed(float amount) =>
            OnPlayerHealed?.Invoke(amount);

        public static void TriggerPlayerDied() =>
            OnPlayerDied?.Invoke();

        public static void TriggerStaminaChanged(float current, float max) =>
            OnStaminaChanged?.Invoke(current, max);

        public static void TriggerSchoolChanged(ESchool school) =>
            OnSchoolChanged?.Invoke(school);

        public static void TriggerWeaponChanged(EWeapon weapon) =>
            OnWeaponChanged?.Invoke(weapon);

        public static void TriggerEnemyKilled(Vector3 position, int knowledgeValue) =>
            OnEnemyKilled?.Invoke(position, knowledgeValue);

        public static void TriggerScoreChanged(int score) =>
            OnScoreChanged?.Invoke(score);

        public static void TriggerKnowledgeChanged(int total) =>
            OnKnowledgeChanged?.Invoke(total);

        public static void TriggerBossPhaseChange(int phase) =>
            OnBossPhaseChange?.Invoke(phase);

        public static void TriggerWeaponUpgraded(string weaponId, int newLevel) =>
            OnWeaponUpgraded?.Invoke(weaponId, newLevel);

        /// <summary>清除所有订阅（场景切换时调用，防止内存泄漏）。</summary>
        public static void ClearAll()
        {
            OnGameStateChanged = null;
            OnWaveChanged = null;
            OnWaveCleared = null;
            OnWaveTransition = null;
            OnGameOver = null;
            OnBossKilled = null;
            OnPlayerDamaged = null;
            OnPlayerHealed = null;
            OnPlayerDied = null;
            OnStaminaChanged = null;
            OnSchoolChanged = null;
            OnWeaponChanged = null;
            OnEnemyKilled = null;
            OnScoreChanged = null;
            OnKnowledgeChanged = null;
            OnBossPhaseChange = null;
            OnWeaponUpgraded = null;
        }
    }
}
