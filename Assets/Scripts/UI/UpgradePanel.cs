using UnityEngine;
using UnityEngine.UI;
using HundredSchools.Core;
using HundredSchools.Economy;

namespace HundredSchools.UI
{
    /// <summary>
    /// UpgradePanel —— 波间升级面板（P0-2 重写）。
    ///
    /// 在每个波次结束后弹出，展示：
    ///   1. 主武器升级（含分支选择）
    ///   2. 副技能升级（含分支选择）
    ///   3. 切换副技能
    ///   4. 继续下一波
    ///
    /// 玩家可执行多次操作（升主武→升副技→继续），面板实时刷新状态。
    /// 全部 UI 程序化生成，零编辑器拖拽。
    ///
    /// 挂载到：场景根级空 GameObject "UpgradePanel" 上。
    /// </summary>
    public class UpgradePanel : MonoBehaviour
    {
        // ==================== 颜色常量（灰模风格） ====================

        private static readonly Color OverlayColor     = new Color(0f, 0f, 0f, 0.85f);
        private static readonly Color SectionBgColor   = new Color(0.12f, 0.12f, 0.15f, 0.95f);
        private static readonly Color BtnNormalColor   = new Color(0.22f, 0.22f, 0.26f, 1f);
        private static readonly Color BtnHoverColor    = new Color(0.33f, 0.33f, 0.38f, 1f);
        private static readonly Color BtnDisabledColor = new Color(0.12f, 0.12f, 0.12f, 0.5f);
        private static readonly Color ContinueColor    = new Color(0.35f, 0.50f, 0.35f, 1f);
        private static readonly Color TextWhite        = new Color(0.90f, 0.90f, 0.92f, 1f);
        private static readonly Color TextYellow       = new Color(1f, 0.85f, 0.3f, 1f);
        private static readonly Color TextGray         = new Color(0.55f, 0.55f, 0.60f, 1f);
        private static readonly Color TextGold         = new Color(0.95f, 0.85f, 0.55f, 1f);

        // ==================== UI 引用 ====================

        private GameObject _canvasObj;
        private Text _knowledgeText;

        // 主武器
        private GameObject _mainSection;
        private Text _mainHeader;
        private Button _mainBtn1;
        private Text _mainBtn1Label;
        private Text _mainBtn1Desc;
        private Button _mainBtn2;
        private Text _mainBtn2Label;
        private Text _mainBtn2Desc;
        private GameObject _mainMaxed;

        // 副技能
        private GameObject _subSection;
        private Text _subHeader;
        private Button _subBtn1;
        private Text _subBtn1Label;
        private Text _subBtn1Desc;
        private Button _subBtn2;
        private Text _subBtn2Label;
        private Text _subBtn2Desc;
        private GameObject _subMaxed;

        // 切换副技能
        private GameObject _switchSection;
        private Button _switchBtn;
        private Text _switchBtnLabel;

        // 继续
        private Button _continueBtn;

        // ==================== 缓存的分支ID（按钮回调用） ====================

        private string _mainBranchIdA;
        private string _mainBranchIdB;
        private string _subBranchIdA;
        private string _subBranchIdB;

        // ==================== Unity 生命周期 ====================

        private void Awake()
        {
            CreateUI();
            gameObject.SetActive(false);
        }

        // ==================== 公开接口 ====================

        public void ShowPanel()
        {
            gameObject.SetActive(true);
            Debug.Log($"[UpgradePanel] 面板激活, UpgradeManager={(UpgradeManager.Instance != null ? "OK" : "NULL")}, KnowledgeManager={(KnowledgeManager.Instance != null ? "OK" : "NULL")}");
            Refresh();
        }

        public void HidePanel()
        {
            gameObject.SetActive(false);
        }

        // ==================== 刷新全部 UI ====================

        private void Refresh()
        {
            var um = UpgradeManager.Instance;
            var km = KnowledgeManager.Instance;
            int knowledge = km != null ? km.CurrentKnowledge : 0;

            _knowledgeText.text = $"当前学识: {knowledge}";

            if (um == null)
            {
                Debug.LogError("[UpgradePanel] UpgradeManager.Instance 为 null！请确保场景中有 UpgradeManager GameObject");
                return;
            }

            Debug.Log($"[UpgradePanel] Refresh: 学识={knowledge}, 主武={um.MainWeaponName} Lv{um.MainLevel} CanUpgrade={um.CanUpgradeMain}, 副技={um.SubSkillName} Lv{um.SubLevel} CanUpgrade={um.CanUpgradeSub}");
            RefreshMainSection(um, knowledge);
            RefreshSubSection(um, knowledge);
            RefreshSwitchSection(um);
        }

        // ==================== 主武器区 ====================

        private void RefreshMainSection(UpgradeManager um, int knowledge)
        {
            if (!um.CanUpgradeMain)
            {
                // 已满级
                _mainHeader.text = $"主武器: {um.MainWeaponName}  Lv.Max";
                _mainBtn1.gameObject.SetActive(false);
                _mainBtn2.gameObject.SetActive(false);
                _mainMaxed.SetActive(true);
                return;
            }

            _mainMaxed.SetActive(false);
            int nextLv = um.MainLevel + 1;
            var (isBranch, branches, cost) = um.GetMainNextOptions();

            _mainHeader.text = $"主武器: {um.MainWeaponName}  Lv{um.MainLevel} → Lv{nextLv}";
            bool canAfford = knowledge >= cost;

            if (isBranch && branches != null && branches.Length >= 2)
            {
                // 分支升级：两个按钮
                _mainBranchIdA = branches[0].id;
                _mainBranchIdB = branches[1].id;

                SetupBranchButton(_mainBtn1, _mainBtn1Label, _mainBtn1Desc,
                    branches[0].name, branches[0].description, cost, canAfford);
                SetupBranchButton(_mainBtn2, _mainBtn2Label, _mainBtn2Desc,
                    branches[1].name, branches[1].description, cost, canAfford);

                _mainBtn1.gameObject.SetActive(true);
                _mainBtn2.gameObject.SetActive(true);
            }
            else
            {
                // 线性升级：单个按钮
                _mainBranchIdA = null;
                _mainBranchIdB = null;

                SetupLinearButton(_mainBtn1, _mainBtn1Label, _mainBtn1Desc,
                    cost, canAfford);
                RectTransform rt1 = _mainBtn1.GetComponent<RectTransform>();
                rt1.anchoredPosition = new Vector2(0f, rt1.anchoredPosition.y);

                _mainBtn1.gameObject.SetActive(true);
                _mainBtn2.gameObject.SetActive(false);
            }
        }

        // ==================== 副技能区 ====================

        private void RefreshSubSection(UpgradeManager um, int knowledge)
        {
            if (!um.CanUpgradeSub)
            {
                _subHeader.text = $"副技能: {um.SubSkillName}  Lv.Max";
                _subBtn1.gameObject.SetActive(false);
                _subBtn2.gameObject.SetActive(false);
                _subMaxed.SetActive(true);
                return;
            }

            _subMaxed.SetActive(false);
            int nextLv = um.SubLevel + 1;
            var (isBranch, branches, cost) = um.GetSubNextOptions();

            _subHeader.text = $"副技能: {um.SubSkillName}  Lv{um.SubLevel} → Lv{nextLv}";
            bool canAfford = knowledge >= cost;

            if (isBranch && branches != null && branches.Length >= 2)
            {
                _subBranchIdA = branches[0].id;
                _subBranchIdB = branches[1].id;

                SetupBranchButton(_subBtn1, _subBtn1Label, _subBtn1Desc,
                    branches[0].name, branches[0].description, cost, canAfford);
                SetupBranchButton(_subBtn2, _subBtn2Label, _subBtn2Desc,
                    branches[1].name, branches[1].description, cost, canAfford);

                _subBtn1.gameObject.SetActive(true);
                _subBtn2.gameObject.SetActive(true);
            }
            else
            {
                _subBranchIdA = null;
                _subBranchIdB = null;

                SetupLinearButton(_subBtn1, _subBtn1Label, _subBtn1Desc,
                    cost, canAfford);
                RectTransform rt1 = _subBtn1.GetComponent<RectTransform>();
                rt1.anchoredPosition = new Vector2(0f, rt1.anchoredPosition.y);

                _subBtn1.gameObject.SetActive(true);
                _subBtn2.gameObject.SetActive(false);
            }
        }

        // ==================== 切换副技能区 ====================

        private void RefreshSwitchSection(UpgradeManager um)
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                _switchSection.SetActive(false);
                return;
            }

            EWeapon target = GetSwitchTarget(gm.SelectedMainWeapon, gm.SelectedSubSkill);
            _switchBtnLabel.text = $"切换到 {WeaponName(target)}（等级继承）";
            _switchSection.SetActive(true);
        }

        // ==================== 按钮回调 ====================

        private void OnMainUpgrade(string branchId)
        {
            var um = UpgradeManager.Instance;
            if (um == null) return;

            bool ok = um.UpgradeMain(branchId);
            if (ok)
                Refresh();
        }

        private void OnSubUpgrade(string branchId)
        {
            var um = UpgradeManager.Instance;
            if (um == null) return;

            bool ok = um.UpgradeSub(branchId);
            if (ok)
                Refresh();
        }

        private void OnSwitchSub()
        {
            var um = UpgradeManager.Instance;
            var gm = GameManager.Instance;
            if (um == null || gm == null) return;

            EWeapon target = GetSwitchTarget(gm.SelectedMainWeapon, gm.SelectedSubSkill);
            um.SwitchSubSkill(target);
            Refresh();
        }

        private void OnContinue()
        {
            HidePanel();
            GameManager.Instance?.ContinueToNextWave();
        }

        // ==================== UI 构建 ====================

        private void CreateUI()
        {
            BuildCanvas();
            BuildContent();
            EnsureEventSystem();
        }

        private void BuildCanvas()
        {
            _canvasObj = new GameObject("UpgradeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObj.transform.SetParent(transform, false);
            var canvas = _canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var cr = _canvasObj.GetComponent<RectTransform>();
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
            cr.anchoredPosition = Vector2.zero; cr.sizeDelta = Vector2.zero;
            cr.pivot = new Vector2(0.5f, 0.5f);

            var scaler = _canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 半透明遮罩
            var bgGo = new GameObject("Overlay", typeof(Image));
            bgGo.transform.SetParent(_canvasObj.transform, false);
            bgGo.GetComponent<Image>().color = OverlayColor;
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
        }

        private void BuildContent()
        {
            const float panelWidth = 680f;
            const float twoColBtnW = 290f;
            const float oneColBtnW = 440f;
            const float btnH = 72f;
            const float smallBtnH = 52f;

            // 内容根（居中）
            var root = new GameObject("ContentRoot", typeof(Image));
            root.transform.SetParent(_canvasObj.transform, false);
            root.GetComponent<Image>().color = SectionBgColor;
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(panelWidth, 620f);
            rootRect.anchoredPosition = Vector2.zero;

            float y = 280f;
            const float margin = 30f;

            // 标题
            MakeLabel(root.transform, "波 间 修 炼", 36, TextGold,
                new Vector2(0, y), new Vector2(panelWidth, 48));
            y -= 46f;

            // 学识余额
            _knowledgeText = MakeLabel(root.transform, "当前学识: 0", 26, TextYellow,
                new Vector2(0, y), new Vector2(panelWidth, 34));
            y -= 28f;

            // ─── 主武器区 ───
            _mainSection = new GameObject("MainSection", typeof(Image));
            _mainSection.transform.SetParent(root.transform, false);
            _mainSection.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.22f, 0.8f);
            var msRect = _mainSection.GetComponent<RectTransform>();
            msRect.anchorMin = msRect.anchorMax = new Vector2(0.5f, 0.5f);
            msRect.sizeDelta = new Vector2(panelWidth - margin * 2, 155f);
            msRect.anchoredPosition = new Vector2(0, y - 60f);
            y -= 170f;

            _mainHeader = MakeLabel(_mainSection.transform, "", 22, TextWhite,
                new Vector2(0, 55), new Vector2(panelWidth - 60, 28));

            (_mainBtn1, _mainBtn1Label, _mainBtn1Desc) = MakeUpgradeButton(
                _mainSection.transform, "MainBtn1", twoColBtnW, btnH,
                new Vector2(-twoColBtnW / 2f - 8f, -16f),
                () => OnMainUpgrade(_mainBranchIdA));

            (_mainBtn2, _mainBtn2Label, _mainBtn2Desc) = MakeUpgradeButton(
                _mainSection.transform, "MainBtn2", twoColBtnW, btnH,
                new Vector2(twoColBtnW / 2f + 8f, -16f),
                () => OnMainUpgrade(_mainBranchIdB));

            _mainMaxed = MakeSimpleText(_mainSection.transform, "已满级", 20, TextGray,
                new Vector2(0, -16f), new Vector2(panelWidth - 60, 30));
            _mainMaxed.SetActive(false);

            // ─── 副技能区 ───
            _subSection = new GameObject("SubSection", typeof(Image));
            _subSection.transform.SetParent(root.transform, false);
            _subSection.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.22f, 0.8f);
            var ssRect = _subSection.GetComponent<RectTransform>();
            ssRect.anchorMin = ssRect.anchorMax = new Vector2(0.5f, 0.5f);
            ssRect.sizeDelta = new Vector2(panelWidth - margin * 2, 155f);
            ssRect.anchoredPosition = new Vector2(0, y - 60f);
            y -= 170f;

            _subHeader = MakeLabel(_subSection.transform, "", 22, TextWhite,
                new Vector2(0, 55), new Vector2(panelWidth - 60, 28));

            (_subBtn1, _subBtn1Label, _subBtn1Desc) = MakeUpgradeButton(
                _subSection.transform, "SubBtn1", twoColBtnW, btnH,
                new Vector2(-twoColBtnW / 2f - 8f, -16f),
                () => OnSubUpgrade(_subBranchIdA));

            (_subBtn2, _subBtn2Label, _subBtn2Desc) = MakeUpgradeButton(
                _subSection.transform, "SubBtn2", twoColBtnW, btnH,
                new Vector2(twoColBtnW / 2f + 8f, -16f),
                () => OnSubUpgrade(_subBranchIdB));

            _subMaxed = MakeSimpleText(_subSection.transform, "已满级", 20, TextGray,
                new Vector2(0, -16f), new Vector2(panelWidth - 60, 30));
            _subMaxed.SetActive(false);

            // ─── 切换副技能区 ───
            _switchSection = new GameObject("SwitchSection", typeof(Image));
            _switchSection.transform.SetParent(root.transform, false);
            _switchSection.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.22f, 0.6f);
            var swRect = _switchSection.GetComponent<RectTransform>();
            swRect.anchorMin = swRect.anchorMax = new Vector2(0.5f, 0.5f);
            swRect.sizeDelta = new Vector2(panelWidth - margin * 2, 70f);
            swRect.anchoredPosition = new Vector2(0, y - 20f);
            y -= 85f;

            (_switchBtn, _switchBtnLabel, _) = MakeUpgradeButton(
                _switchSection.transform, "SwitchBtn", oneColBtnW, smallBtnH,
                new Vector2(0, 0),
                OnSwitchSub);

            // ─── 继续按钮 ───
            var continueGo = new GameObject("ContinueBtn", typeof(Image), typeof(Button));
            continueGo.transform.SetParent(root.transform, false);
            var cbRect = continueGo.GetComponent<RectTransform>();
            cbRect.anchorMin = cbRect.anchorMax = new Vector2(0.5f, 0.5f);
            cbRect.anchoredPosition = new Vector2(0, y - 20f);
            cbRect.sizeDelta = new Vector2(280, 56);

            var cbImg = continueGo.GetComponent<Image>();
            cbImg.color = ContinueColor;
            _continueBtn = continueGo.GetComponent<Button>();
            _continueBtn.targetGraphic = cbImg;
            var cbColors = _continueBtn.colors;
            cbColors.normalColor = ContinueColor;
            cbColors.highlightedColor = new Color(0.45f, 0.60f, 0.45f, 1f);
            cbColors.pressedColor = new Color(0.25f, 0.40f, 0.25f, 1f);
            _continueBtn.colors = cbColors;
            _continueBtn.onClick.AddListener(OnContinue);

            var cbLabel = MakeLabel(continueGo.transform, "继续下一波", 26, TextWhite,
                Vector2.zero, new Vector2(280, 56));
            cbLabel.alignment = TextAnchor.MiddleCenter;
            cbLabel.raycastTarget = false;
        }

        // ==================== 按钮辅助方法 ====================

        private void SetupBranchButton(Button btn, Text label, Text desc,
            string name, string description, int cost, bool canAfford)
        {
            label.text = $"{name}";
            desc.text = $"{description}\n消耗 {cost} 学识";

            var img = btn.GetComponent<Image>();
            img.color = canAfford ? BtnNormalColor : BtnDisabledColor;
            btn.interactable = canAfford;

            var colors = btn.colors;
            colors.normalColor = canAfford ? BtnNormalColor : BtnDisabledColor;
            colors.highlightedColor = canAfford ? BtnHoverColor : BtnDisabledColor;
            btn.colors = colors;

            label.color = canAfford ? TextWhite : TextGray;
            desc.color = canAfford ? TextYellow : TextGray;
        }

        private void SetupLinearButton(Button btn, Text label, Text desc,
            int cost, bool canAfford)
        {
            label.text = "升级";
            desc.text = $"消耗 {cost} 学识";

            var img = btn.GetComponent<Image>();
            img.color = canAfford ? BtnNormalColor : BtnDisabledColor;
            btn.interactable = canAfford;

            var colors = btn.colors;
            colors.normalColor = canAfford ? BtnNormalColor : BtnDisabledColor;
            colors.highlightedColor = canAfford ? BtnHoverColor : BtnDisabledColor;
            btn.colors = colors;

            label.color = canAfford ? TextWhite : TextGray;
            desc.color = canAfford ? TextYellow : TextGray;
        }

        // ==================== UI 工厂方法 ====================

        /// <summary>创建一个升级按钮，返回(Button, 主Label, 描述Label)。</summary>
        private (Button, Text, Text) MakeUpgradeButton(Transform parent, string name,
            float width, float height, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(width, height);

            var img = go.GetComponent<Image>();
            img.color = BtnNormalColor;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var cols = btn.colors;
            cols.normalColor = BtnNormalColor;
            cols.highlightedColor = BtnHoverColor;
            cols.pressedColor = new Color(0.15f, 0.15f, 0.18f, 1f);
            btn.colors = cols;
            btn.onClick.AddListener(onClick);

            // 主标签（上方居中）
            var labelGo = new GameObject("Label", typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var lr = labelGo.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0f, 0.5f); lr.anchorMax = new Vector2(1f, 1f);
            lr.offsetMin = new Vector2(8, 0); lr.offsetMax = new Vector2(-8, -4);
            var label = labelGo.GetComponent<Text>();
            label.fontSize = 20;
            label.alignment = TextAnchor.LowerCenter;
            label.color = TextWhite;
            label.raycastTarget = false;
            label.font = GetFont();

            // 描述标签（下方居中）
            var descGo = new GameObject("Desc", typeof(Text));
            descGo.transform.SetParent(go.transform, false);
            var dr = descGo.GetComponent<RectTransform>();
            dr.anchorMin = new Vector2(0f, 0f); dr.anchorMax = new Vector2(1f, 0.5f);
            dr.offsetMin = new Vector2(8, 4); dr.offsetMax = new Vector2(-8, 2);
            var desc = descGo.GetComponent<Text>();
            desc.fontSize = 15;
            desc.alignment = TextAnchor.UpperCenter;
            desc.color = TextYellow;
            desc.raycastTarget = false;
            desc.font = GetFont();

            return (btn, label, desc);
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

        private GameObject MakeSimpleText(Transform parent, string content, int fontSize, Color color,
            Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject("Text", typeof(Text));
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
            return go;
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

        // ==================== 工具方法 ====================

        private static EWeapon GetSwitchTarget(EWeapon main, EWeapon sub)
        {
            foreach (EWeapon w in new[] { EWeapon.Archery, EWeapon.Chariot, EWeapon.Ritual })
            {
                if (w != main && w != sub)
                    return w;
            }
            return EWeapon.Archery; // fallback
        }

        private static string WeaponName(EWeapon w) => w switch
        {
            EWeapon.Archery => "射艺",
            EWeapon.Chariot => "御艺",
            EWeapon.Ritual  => "礼艺",
            _ => "???"
        };

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
