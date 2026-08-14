using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using HundredSchools.Core;

namespace HundredSchools.UI
{
    /// <summary>
    /// GameOverPanel —— 死亡/胜利结算面板。
    /// 程序化生成全部 UI，零编辑器拖拽。
    /// </summary>
    public class GameOverPanel : MonoBehaviour
    {
        private GameObject _canvasObj;
        private GameObject _panel;
        private Text _titleText;
        private Text _statsText;
        private Button _restartButton;

        private static readonly Color BgColor      = new Color(0f, 0f, 0f, 0.85f);
        private static readonly Color PanelBgColor = new Color(0.12f, 0.12f, 0.15f, 0.95f);
        private static readonly Color BtnColor     = new Color(0.35f, 0.50f, 0.35f, 1f);
        private static readonly Color TextWhite    = new Color(0.90f, 0.90f, 0.92f, 1f);
        private static readonly Color TextGold     = new Color(0.95f, 0.85f, 0.55f, 1f);
        private static readonly Color TextGray     = new Color(0.65f, 0.65f, 0.70f, 1f);

        private void Awake()
        {
            CreateUI();
            _panel.SetActive(false);
            _canvasObj.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus.OnGameOver += Show;
        }

        private void OnDisable()
        {
            EventBus.OnGameOver -= Show;
        }

        private void Show(bool isVictory)
        {
            _canvasObj.SetActive(true);
            _panel.SetActive(true);
            Time.timeScale = 0f;

            _titleText.text = isVictory ? "百家归一" : "道消身殒";
            _titleText.color = isVictory ? TextGold : new Color(1f, 0.3f, 0.3f, 1f);

            var gm = GameManager.Instance;
            if (gm != null)
            {
                _statsText.text = string.Format(
                    "存活 {0:F0} 秒\n击杀 {1}\n学识 {2}\n连杀最高 {3}",
                    gm.SurviveTime,
                    gm.TotalKills,
                    Economy.KnowledgeManager.Instance?.CurrentKnowledge ?? 0,
                    gm.MaxCombo);
            }
        }

        private void Restart()
        {
            Time.timeScale = 1f;
            var gm = GameManager.Instance;
            if (gm != null) Destroy(gm.gameObject);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ==================== UI 构建 ====================

        private void CreateUI()
        {
            BuildCanvas();
            BuildPanel();
            EnsureEventSystem();
        }

        private void BuildCanvas()
        {
            _canvasObj = new GameObject("GameOverCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObj.transform.SetParent(transform, false);
            var canvas = _canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;

            var cr = _canvasObj.GetComponent<RectTransform>();
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
            cr.sizeDelta = Vector2.zero;

            var scaler = _canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var bg = new GameObject("Overlay", typeof(Image));
            bg.transform.SetParent(_canvasObj.transform, false);
            bg.GetComponent<Image>().color = BgColor;
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
        }

        private void BuildPanel()
        {
            _panel = new GameObject("Panel", typeof(Image));
            _panel.transform.SetParent(_canvasObj.transform, false);
            _panel.GetComponent<Image>().color = PanelBgColor;
            var pr = _panel.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(500, 420);
            pr.anchoredPosition = Vector2.zero;

            float y = 160f;

            // 标题
            _titleText = MakeLabel(_panel.transform, "", 42, TextGold,
                new Vector2(0, y), new Vector2(460, 56));
            y -= 60f;

            // 分隔线
            var sep = new GameObject("Separator", typeof(Image));
            sep.transform.SetParent(_panel.transform, false);
            sep.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f, 0.6f);
            var sr = sep.GetComponent<RectTransform>();
            sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
            sr.anchoredPosition = new Vector2(0, y);
            sr.sizeDelta = new Vector2(400, 2);
            y -= 20f;

            // 统计文本
            _statsText = MakeLabel(_panel.transform, "", 24, TextGray,
                new Vector2(0, y - 80f), new Vector2(460, 160));
            _statsText.alignment = TextAnchor.MiddleCenter;
            y -= 200f;

            // 重开按钮
            var btnGo = new GameObject("RestartBtn", typeof(Image), typeof(Button));
            btnGo.transform.SetParent(_panel.transform, false);
            var br = btnGo.GetComponent<RectTransform>();
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
            br.anchoredPosition = new Vector2(0, y);
            br.sizeDelta = new Vector2(240, 52);

            var img = btnGo.GetComponent<Image>();
            img.color = BtnColor;
            _restartButton = btnGo.GetComponent<Button>();
            _restartButton.targetGraphic = img;
            var cols = _restartButton.colors;
            cols.normalColor = BtnColor;
            cols.highlightedColor = new Color(0.45f, 0.60f, 0.45f, 1f);
            _restartButton.colors = cols;
            _restartButton.onClick.AddListener(Restart);

            var btnLabel = MakeLabel(btnGo.transform, "再 来 一 局", 26, TextWhite,
                Vector2.zero, new Vector2(240, 52));
            btnLabel.alignment = TextAnchor.MiddleCenter;
            btnLabel.raycastTarget = false;
        }

        // ==================== 工厂方法 ====================

        private Text MakeLabel(Transform parent, string content, int fontSize, Color color,
            Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject("Label", typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.text = content;
            t.fontSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;
            t.font = GetFont();
            return t;
        }

        private static Font _cachedFont;

        private static Font GetFont()
        {
            if (_cachedFont != null) return _cachedFont;
            _cachedFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 16);
            if (_cachedFont == null)
                _cachedFont = Font.CreateDynamicFontFromOSFont("SimHei", 16);
            if (_cachedFont == null)
                _cachedFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
            if (_cachedFont == null)
                _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _cachedFont;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                esGo.transform.SetParent(null);
            }
        }
    }
}
