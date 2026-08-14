using UnityEngine;
using UnityEngine.UI;
using HundredSchools.Core;

namespace HundredSchools.UI
{
    /// <summary>
    /// SchoolSelectPanel —— 开局选择面板。
    ///
    /// 游戏启动后显示，玩家选择 学派→主武器→副技能，点击"开始"后
    /// 调用 GameManager.ConfirmSelectionAndStart() 进入游戏。
    ///
    /// 全部 UI 程序化生成，零编辑器拖拽。灰模风格（深灰底+白字）。
    /// 挂载到场景根级空 GameObject "SchoolSelectPanel" 上。
    /// </summary>
    public class SchoolSelectPanel : MonoBehaviour
    {
        // ==================== 选项数据 ====================

        private static readonly (ESchool school, string name, string desc)[] SchoolOptions =
        {
            (ESchool.Confucian, "儒家", "击杀治愈+5HP —— 杀越多越持久"),
            (ESchool.Legalist,  "法家", "攻击力+10% —— 打得更狠但活得更短"),
            (ESchool.Taoist,   "道家", "闪避无冷却+体力恢复+50% —— 循环不息"),
        };

        private static readonly (EWeapon weapon, string name, string desc)[] WeaponOptions =
        {
            (EWeapon.Archery, "射艺", "经典俯视角射击 —— 直线箭矢弹，可蓄力穿透箭"),
            (EWeapon.Chariot, "御艺", "移动即攻击 —— 冲刺轨迹产生伤害带，灵动飘逸"),
            (EWeapon.Ritual,  "礼艺", "防守反击 —— 礼击推力波+礼屏障反弹，攻守兼备"),
        };

        private static readonly (EDifficulty diff, string name)[] DifficultyOptions =
        {
            (EDifficulty.Easy,   "游学"),
            (EDifficulty.Normal, "论道"),
            (EDifficulty.Hard,   "伐交"),
        };

        // ==================== 当前选择 ====================

        private int _schoolIndex = 0;
        private int _weaponIndex = 0;
        private int _subIndex = 0;

        private ESchool SelectedSchool => SchoolOptions[_schoolIndex].school;
        private EWeapon SelectedWeapon => WeaponOptions[_weaponIndex].weapon;
        private EWeapon SelectedSubSkill
        {
            get
            {
                int idx = 0;
                for (int i = 0; i < WeaponOptions.Length; i++)
                {
                    if (i == _weaponIndex) continue;
                    if (idx == _subIndex) return WeaponOptions[i].weapon;
                    idx++;
                }
                return WeaponOptions[0].weapon == SelectedWeapon
                    ? WeaponOptions[1].weapon
                    : WeaponOptions[0].weapon;
            }
        }

        private int _difficultyIndex = 1; // 默认：论道（普通）

        // ==================== UI 引用 ====================

        private GameObject _canvasObj;
        private Text _schoolDescText;
        private Text _weaponDescText;
        private Text _subDescText;
        private Image[] _schoolBtnBgs = new Image[3];
        private Image[] _weaponBtnBgs = new Image[3];
        private Image[] _subBtnBgs = new Image[2];
        private Button[] _subButtons = new Button[2];
        private Text[] _subBtnTexts = new Text[2];
        private Image[] _difficultyBtnBgs = new Image[3];

        // ==================== 颜色常量（灰模风格） ====================

        private static readonly Color BgColor         = new Color(0.08f, 0.08f, 0.10f, 0.95f);
        private static readonly Color PanelBgColor    = new Color(0.14f, 0.14f, 0.17f, 1f);
        private static readonly Color BtnNormalColor  = new Color(0.22f, 0.22f, 0.26f, 1f);
        private static readonly Color BtnSelectedColor= new Color(0.45f, 0.45f, 0.50f, 1f);
        private static readonly Color BtnHoverColor   = new Color(0.33f, 0.33f, 0.38f, 1f);
        private static readonly Color StartBtnColor   = new Color(0.35f, 0.50f, 0.35f, 1f);
        private static readonly Color TextColor       = new Color(0.90f, 0.90f, 0.92f, 1f);
        private static readonly Color DescTextColor   = new Color(0.65f, 0.65f, 0.70f, 1f);
        private static readonly Color TitleColor      = new Color(0.95f, 0.85f, 0.55f, 1f);

        // ==================== Unity 生命周期 ====================

        private void Awake()
        {
            CreateUI();
            gameObject.SetActive(false);
        }

        // ==================== 公开接口 ====================

        public void Show()
        {
            gameObject.SetActive(true);
            RefreshAllHighlights();
            RefreshSubSkillButtons();
            RefreshDescriptions();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // ==================== UI 构建 ====================

        private void CreateUI()
        {
            // Canvas
            _canvasObj = new GameObject("SelectCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObj.transform.SetParent(transform, false);
            var canvas = _canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var cr = _canvasObj.GetComponent<RectTransform>();
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
            cr.anchoredPosition = Vector2.zero; cr.sizeDelta = Vector2.zero;

            var scaler = _canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // 全屏背景
            var bgGo = new GameObject("Background", typeof(Image));
            bgGo.transform.SetParent(_canvasObj.transform, false);
            bgGo.GetComponent<Image>().color = BgColor;
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;

            // === 中央主面板 ===
            var mainPanel = new GameObject("MainPanel", typeof(Image));
            mainPanel.transform.SetParent(_canvasObj.transform, false);
            mainPanel.GetComponent<Image>().color = PanelBgColor;
            var mpRect = mainPanel.GetComponent<RectTransform>();
            mpRect.anchorMin = mpRect.anchorMax = new Vector2(0.5f, 0.5f);
            mpRect.sizeDelta = new Vector2(720, 900);
            mpRect.anchoredPosition = Vector2.zero;

            float y = 340f;
            float panelW = 680f;

            // 标题
            y -= 55f;
            MakeLabel(mainPanel.transform, "诸子百家 · 口诛笔伐", 36, TitleColor,
                new Vector2(0, y), new Vector2(panelW, 50));

            // === 学派选择区 ===
            y -= 75f;
            MakeSectionLabel(mainPanel.transform, "学派", new Vector2(0, y));
            y -= 40f;
            for (int i = 0; i < 3; i++)
            {
                float bx = -180f + i * 180f;
                int idx = i;
                var btn = MakeSelectButton(mainPanel.transform, SchoolOptions[i].name, 22,
                    new Vector2(bx, y), new Vector2(160, 44), () => OnSchoolClicked(idx));
                _schoolBtnBgs[i] = btn.Item2;
            }
            y -= 30f;
            _schoolDescText = MakeLabel(mainPanel.transform, "", 18, DescTextColor,
                new Vector2(0, y), new Vector2(panelW - 40, 30));

            // === 主武器选择区 ===
            y -= 70f;
            MakeSectionLabel(mainPanel.transform, "主武器", new Vector2(0, y));
            y -= 40f;
            for (int i = 0; i < 3; i++)
            {
                float bx = -180f + i * 180f;
                int idx = i;
                var btn = MakeSelectButton(mainPanel.transform, WeaponOptions[i].name, 22,
                    new Vector2(bx, y), new Vector2(160, 44), () => OnWeaponClicked(idx));
                _weaponBtnBgs[i] = btn.Item2;
            }
            y -= 30f;
            _weaponDescText = MakeLabel(mainPanel.transform, "", 18, DescTextColor,
                new Vector2(0, y), new Vector2(panelW - 40, 30));

            // === 副技能选择区 ===
            y -= 70f;
            MakeSectionLabel(mainPanel.transform, "副技能", new Vector2(0, y));
            y -= 40f;
            for (int i = 0; i < 2; i++)
            {
                float bx = -95f + i * 190f;
                int idx = i;
                var btn = MakeSelectButton(mainPanel.transform, "", 22,
                    new Vector2(bx, y), new Vector2(170, 44), () => OnSubClicked(idx));
                _subBtnBgs[i] = btn.Item2;
                _subButtons[i] = btn.Item1;
                _subBtnTexts[i] = btn.Item3;
            }
            y -= 30f;
            _subDescText = MakeLabel(mainPanel.transform, "选择另一艺作为副技能", 18, DescTextColor,
                new Vector2(0, y), new Vector2(panelW - 40, 30));

            // === 开始按钮 ===
            y -= 80f;
            var startBtnGo = new GameObject("StartBtn", typeof(Image), typeof(Button));
            startBtnGo.transform.SetParent(mainPanel.transform, false);
            var sbRect = startBtnGo.GetComponent<RectTransform>();
            sbRect.anchorMin = sbRect.anchorMax = new Vector2(0.5f, 0.5f);
            sbRect.anchoredPosition = new Vector2(0, y);
            sbRect.sizeDelta = new Vector2(260, 56);

            var sbImg = startBtnGo.GetComponent<Image>();
            sbImg.color = StartBtnColor;
            var sbBtn = startBtnGo.GetComponent<Button>();
            sbBtn.targetGraphic = sbImg;
            var sbColors = sbBtn.colors;
            sbColors.normalColor = StartBtnColor;
            sbColors.highlightedColor = new Color(0.45f, 0.60f, 0.45f, 1f);
            sbColors.pressedColor = new Color(0.25f, 0.40f, 0.25f, 1f);
            sbBtn.colors = sbColors;
            sbBtn.onClick.AddListener(OnStartClicked);

            var sbLabel = MakeLabel(startBtnGo.transform, "开 始 修 行", 28, TextColor,
                Vector2.zero, new Vector2(260, 56));
            sbLabel.alignment = TextAnchor.MiddleCenter;
            sbLabel.raycastTarget = false;

            // === 难度选择区 ===
            y -= 80f;
            MakeSectionLabel(mainPanel.transform, "难度选择", new Vector2(0, y));
            y -= 45f;
            for (int i = 0; i < 3; i++)
            {
                float bx = -180f + i * 180f;
                int idx = i;
                var btn = MakeSelectButton(mainPanel.transform, DifficultyOptions[i].name, 22,
                    new Vector2(bx, y), new Vector2(160, 44), () => OnDifficultyClicked(idx));
                _difficultyBtnBgs[i] = btn.Item2;
            }

            // 确保 EventSystem
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                esGo.transform.SetParent(transform);
            }
        }

        // ==================== 按钮回调 ====================

        private void OnSchoolClicked(int index)
        {
            _schoolIndex = index;
            RefreshAllHighlights();
            RefreshDescriptions();
        }

        private void OnWeaponClicked(int index)
        {
            _weaponIndex = index;
            _subIndex = 0;
            RefreshAllHighlights();
            RefreshSubSkillButtons();
            RefreshDescriptions();
        }

        private void OnSubClicked(int index)
        {
            _subIndex = index;
            RefreshAllHighlights();
        }

        private void OnDifficultyClicked(int index)
        {
            _difficultyIndex = index;
            RefreshAllHighlights();
        }

        private void OnStartClicked()
        {
            Hide();
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.SetDifficulty(DifficultyOptions[_difficultyIndex].diff);
                gm.ConfirmSelectionAndStart(
                    SelectedSchool, SelectedWeapon, SelectedSubSkill);
            }
        }

        // ==================== UI 刷新 ====================

        private void RefreshAllHighlights()
        {
            for (int i = 0; i < 3; i++)
                _schoolBtnBgs[i].color = (i == _schoolIndex) ? BtnSelectedColor : BtnNormalColor;

            for (int i = 0; i < 3; i++)
                _weaponBtnBgs[i].color = (i == _weaponIndex) ? BtnSelectedColor : BtnNormalColor;

            for (int i = 0; i < 2; i++)
                _subBtnBgs[i].color = (i == _subIndex) ? BtnSelectedColor : BtnNormalColor;

            for (int i = 0; i < 3; i++)
                _difficultyBtnBgs[i].color = (i == _difficultyIndex) ? BtnSelectedColor : BtnNormalColor;
        }

        private void RefreshSubSkillButtons()
        {
            int[] subIndices = new int[2];
            int pos = 0;
            for (int i = 0; i < WeaponOptions.Length; i++)
            {
                if (i != _weaponIndex)
                    subIndices[pos++] = i;
            }

            for (int i = 0; i < 2; i++)
            {
                int wIdx = subIndices[i];
                _subBtnTexts[i].text = WeaponOptions[wIdx].name;
                _subButtons[i].gameObject.SetActive(true);
            }
        }

        private void RefreshDescriptions()
        {
            if (_schoolDescText != null)
                _schoolDescText.text = SchoolOptions[_schoolIndex].desc;

            if (_weaponDescText != null)
                _weaponDescText.text = WeaponOptions[_weaponIndex].desc;
        }

        // ==================== UI 工厂方法 ====================

        private void MakeSectionLabel(Transform parent, string text, Vector2 anchoredPos)
        {
            var go = new GameObject("Section_" + text, typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(640, 24);
            var t = go.GetComponent<Text>();
            t.text = "──  " + text + "  ──";
            t.fontSize = 16;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.4f, 0.4f, 0.45f, 1f);
            t.raycastTarget = false;
            t.font = GetFont();
        }

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

        private (Button, Image, Text) MakeSelectButton(Transform parent, string label, int fontSize,
            Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.color = BtnNormalColor;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var cols = btn.colors;
            cols.normalColor = BtnNormalColor;
            cols.highlightedColor = BtnHoverColor;
            cols.pressedColor = BtnSelectedColor;
            cols.selectedColor = BtnSelectedColor;
            btn.colors = cols;
            btn.onClick.AddListener(onClick);

            var labelGo = new GameObject("Text", typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var lr = labelGo.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
            var lt = labelGo.GetComponent<Text>();
            lt.text = label;
            lt.fontSize = fontSize;
            lt.alignment = TextAnchor.MiddleCenter;
            lt.color = TextColor;
            lt.raycastTarget = false;
            lt.font = GetFont();

            return (btn, img, lt);
        }

        // ==================== 字体 ====================

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
    }
}
