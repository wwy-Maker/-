using UnityEngine;

namespace HundredSchools.Core
{
    /// <summary>
    /// 轻量级存档系统。基于 PlayerPrefs 实现，用于保存玩家设置和游戏统计。
    ///
    /// 存档内容：
    ///   - 难度选择（低/中/高）
    ///   - 最高分
    ///   - 总击杀数
    ///   - 灰阶模式开关
    ///   - 色盲模式开关
    ///
    /// 用法：
    ///   SaveSystem.SetDifficulty(1);
    ///   int diff = SaveSystem.GetDifficulty();
    /// </summary>
    public static class SaveSystem
    {
        private const string KEY_DIFFICULTY = "Difficulty";
        private const string KEY_HIGH_SCORE = "HighScore";
        private const string KEY_TOTAL_KILLS = "TotalKills";
        private const string KEY_GRAYSCALE = "GrayscaleMode";
        private const string KEY_COLORBLIND = "ColorBlindMode";
        private const string KEY_TOTAL_RUNS = "TotalRuns";

        public static int GetDifficulty(int defaultValue = 1) =>
            PlayerPrefs.GetInt(KEY_DIFFICULTY, defaultValue);

        public static void SetDifficulty(int value) =>
            PlayerPrefs.SetInt(KEY_DIFFICULTY, Mathf.Clamp(value, 0, 2));

        public static int GetHighScore() =>
            PlayerPrefs.GetInt(KEY_HIGH_SCORE, 0);

        public static void SetHighScore(int score)
        {
            if (score > GetHighScore())
                PlayerPrefs.SetInt(KEY_HIGH_SCORE, score);
        }

        public static int GetTotalKills() =>
            PlayerPrefs.GetInt(KEY_TOTAL_KILLS, 0);

        public static void AddTotalKills(int kills)
        {
            PlayerPrefs.SetInt(KEY_TOTAL_KILLS, GetTotalKills() + kills);
        }

        public static bool GetGrayscaleMode() =>
            PlayerPrefs.GetInt(KEY_GRAYSCALE, 0) == 1;

        public static void SetGrayscaleMode(bool enabled) =>
            PlayerPrefs.SetInt(KEY_GRAYSCALE, enabled ? 1 : 0);

        public static bool GetColorBlindMode() =>
            PlayerPrefs.GetInt(KEY_COLORBLIND, 0) == 1;

        public static void SetColorBlindMode(bool enabled) =>
            PlayerPrefs.SetInt(KEY_COLORBLIND, enabled ? 1 : 0);

        public static int GetTotalRuns() =>
            PlayerPrefs.GetInt(KEY_TOTAL_RUNS, 0);

        public static void IncrementTotalRuns() =>
            PlayerPrefs.SetInt(KEY_TOTAL_RUNS, GetTotalRuns() + 1);

        public static void Save() => PlayerPrefs.Save();

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(KEY_DIFFICULTY);
            PlayerPrefs.DeleteKey(KEY_HIGH_SCORE);
            PlayerPrefs.DeleteKey(KEY_TOTAL_KILLS);
            PlayerPrefs.DeleteKey(KEY_GRAYSCALE);
            PlayerPrefs.DeleteKey(KEY_COLORBLIND);
            PlayerPrefs.DeleteKey(KEY_TOTAL_RUNS);
            PlayerPrefs.Save();
        }
    }
}
