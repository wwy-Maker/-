using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using HundredSchools.Core;
using HundredSchools.Economy;

namespace HundredSchools.UI
{
    public class ItemShop : MonoBehaviour
    {
        private GameObject _canvasObj;
        private Button _freezeBtn;
        private Text _freezeLabel;
        private Button _healBtn;
        private Text _healLabel;
        private bool _isShown;
        private int _freezeUses;
        private const int MaxFreezeUses = 3;
        private const int FreezeCost = 30;
        private const int HealCost = 20;
        private const int HealAmount = 40;

        private void Awake()
        {
            CreateUI();
            _canvasObj.SetActive(false);
            _freezeUses = MaxFreezeUses;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            bool shouldShow = gm != null && gm.IsWaitingForContinue && !gm.IsInUpgradePhase;
            if (shouldShow != _isShown)
            {
                _isShown = shouldShow;
                if (shouldShow) Show();
                else Hide();
            }
        }

        private void Show()
        {
            _canvasObj.SetActive(true);
            RefreshButtons();
        }

        private void Hide() { _canvasObj.SetActive(false); }

        private void RefreshButtons()
        {
            var km = KnowledgeManager.Instance;
            int k = km != null ? km.CurrentKnowledge : 0;

            bool canFreeze = k >= FreezeCost && _freezeUses > 0;
            _freezeBtn.interactable = true;
            _freezeBtn.GetComponent<Image>().color = canFreeze
                ? new Color(0.2f, 0.35f, 0.55f, 1f)
                : new Color(0.12f, 0.12f, 0.12f, 0.4f);
            _freezeLabel.text = _freezeUses > 0
                ? string.Format("定身符 x{0} | {1}学识", _freezeUses, FreezeCost)
                : "定身符 已售罄";

            bool canHeal = k >= HealCost;
            _healBtn.interactable = true;
            _healBtn.GetComponent<Image>().color = canHeal
                ? new Color(0.25f, 0.45f, 0.25f, 1f)
                : new Color(0.12f, 0.12f, 0.12f, 0.4f);
            _healLabel.text = string.Format("仁义之心 | {0}学识", HealCost);
        }

        // ==================== 购买回调 ====================

        private void BuyFreeze()
        {
            var km = KnowledgeManager.Instance;
            if (km == null || km.CurrentKnowledge < FreezeCost || _freezeUses <= 0)
            {
                StartCoroutine(ShakeButton(_freezeBtn));
                return;
            }

            if (!km.SpendKnowledge(FreezeCost)) return;
            _freezeUses--;

            Combat.ProjectileBase.GlobalFreeze = true;
            float dur = 3f;
            foreach (var e in FindObjectsOfType<Enemy.EnemyBase>())
                e.Freeze(e is Enemy.DaoBoss ? 1.5f : dur);
            StartCoroutine(Unfreeze(dur));

            StartCoroutine(BounceButton(_freezeBtn));
            StartCoroutine(ScreenFlash());
            RefreshButtons();
        }

        private void BuyHeal()
        {
            var km = KnowledgeManager.Instance;
            if (km == null || km.CurrentKnowledge < HealCost)
            {
                StartCoroutine(ShakeButton(_healBtn));
                return;
            }

            if (!km.SpendKnowledge(HealCost)) return;
            var p = FindObjectOfType<Player.PlayerMovement>();
            if (p != null) p.Heal(HealAmount);

            StartCoroutine(BounceButton(_healBtn));
            ShowHealRing();
            FindObjectOfType<HUD>()?.FlashHpBarGreen();
            RefreshButtons();
        }

        // ==================== 视觉效果 ====================

        private System.Collections.IEnumerator Unfreeze(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Combat.ProjectileBase.GlobalFreeze = false;
        }

        private System.Collections.IEnumerator BounceButton(Button btn)
        {
            var rt = btn.GetComponent<RectTransform>();
            float duration = 0.1f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                rt.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, t);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        private System.Collections.IEnumerator ShakeButton(Button btn)
        {
            var rt = btn.GetComponent<RectTransform>();
            Vector2 origPos = rt.anchoredPosition;
            float duration = 0.2f;
            float elapsed = 0f;
            float amplitude = 6f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float x = Mathf.Sin(elapsed * 50f) * amplitude * (1f - elapsed / duration);
                rt.anchoredPosition = origPos + new Vector2(x, 0);
                yield return null;
            }
            rt.anchoredPosition = origPos;
        }

        private void ShowHealRing()
        {
            var player = FindObjectOfType<Player.PlayerMovement>();
            if (player == null) return;

            var ring = new GameObject("HealRing");
            ring.transform.position = player.transform.position;

            var sr = ring.AddComponent<SpriteRenderer>();
            sr.sprite = Combat.WeaponUtils.GetOrCreateRingSprite();
            sr.color = new Color(0.1f, 1f, 0.3f, 0.8f);
            sr.sortingOrder = 5;
            ring.transform.localScale = Vector3.one * 0.5f;

            StartCoroutine(ExpandFadeRing(ring));
        }

        private System.Collections.IEnumerator ExpandFadeRing(GameObject ring)
        {
            var sr = ring.GetComponent<SpriteRenderer>();
            float duration = 0.3f;
            float elapsed = 0f;
            Color c0 = sr.color;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                ring.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 3f, t);
                sr.color = new Color(c0.r, c0.g, c0.b, Mathf.Lerp(c0.a, 0f, t));
                yield return null;
            }

            Destroy(ring);
        }

        private System.Collections.IEnumerator ScreenFlash()
        {
            var flash = new GameObject("FreezeFlash");
            var sr = flash.AddComponent<SpriteRenderer>();
            sr.sprite = Combat.WeaponUtils.GetOrCreateSquareSprite();
            sr.color = new Color(1f, 1f, 1f, 0.4f);
            sr.sortingOrder = 999;

            var cam = Camera.main;
            if (cam != null)
            {
                float h = cam.orthographicSize * 2f;
                float w = h * cam.aspect;
                flash.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0);
                flash.transform.localScale = new Vector3(w, h, 1f);
            }

            float duration = 0.15f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                sr.color = new Color(1f, 1f, 1f, 0.4f * (1f - elapsed / duration));
                yield return null;
            }

            Destroy(flash);
        }

        // ==================== UI 构建 ====================

        private void CreateUI()
        {
            _canvasObj = new GameObject("ItemShopCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObj.transform.SetParent(transform, false);
            var c = _canvasObj.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 110;
            var cr = _canvasObj.GetComponent<RectTransform>();
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.sizeDelta = Vector2.zero;
            var sc = _canvasObj.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("ShopPanel", typeof(Image));
            panel.transform.SetParent(_canvasObj.transform, false);
            panel.GetComponent<Image>().color = new Color(0.14f, 0.14f, 0.18f, 0.95f);
            var pr = panel.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(520, 80);
            pr.anchoredPosition = new Vector2(0, -260);

            var title = MkLabel(panel.transform, "器物坊", 18, new Color(0.95f, 0.85f, 0.55f, 1f), new Vector2(0, 22), new Vector2(480, 24));

            (_freezeBtn, _freezeLabel) = MkBtn(panel.transform, "FreezeBtn", new Vector2(-140, -14), new Vector2(250, 44), BuyFreeze);
            (_healBtn, _healLabel) = MkBtn(panel.transform, "HealBtn", new Vector2(140, -14), new Vector2(250, 44), BuyHeal);

            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
                es.transform.SetParent(null);
            }
        }

        private (Button, Text) MkBtn(Transform p, string n, Vector2 pos, Vector2 sz, UnityEngine.Events.UnityAction cb)
        {
            var go = new GameObject(n, typeof(Image), typeof(Button));
            go.transform.SetParent(p, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = sz;
            go.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.30f, 1f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.onClick.AddListener(cb);
            var l = MkLabel(go.transform, "", 16, new Color(0.9f, 0.9f, 0.92f, 1f), Vector2.zero, sz);
            l.alignment = TextAnchor.MiddleCenter;
            l.raycastTarget = false;
            return (btn, l);
        }

        private Text MkLabel(Transform p, string t, int fs, Color col, Vector2 pos, Vector2 sz)
        {
            var go = new GameObject("L", typeof(Text));
            go.transform.SetParent(p, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = sz;
            var tx = go.GetComponent<Text>();
            tx.text = t; tx.fontSize = fs; tx.alignment = TextAnchor.MiddleCenter;
            tx.color = col; tx.raycastTarget = false;
            tx.font = Font.CreateDynamicFontFromOSFont("Arial", 16);
            return tx;
        }
    }
}
