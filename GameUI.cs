using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChinaFishing
{
    public class GameUI : MonoBehaviour
    {
        public static GameUI I;
        Canvas canvas; RectTransform root, safe; Rect lastSafe;
        RectTransform menuR, hudR, shopR, invR, colR, mapR, setR, resR, tutR;
        RectTransform shopList, invList, colGrid, shopTabs, invTabs, mapSpots;
        RectTransform currentPanel;
        ItemCategory shopCat = ItemCategory.Наживки, invCat = ItemCategory.Наживки;
        RectTransform craftR, craftList; Text craftMsg;
        string shopMaker;                       // фильтр производителя (null — все)
        RectTransform shopDlg;                  // окно товара
        ItemDef dlgItem; int dlgQtyV = 1;
        RawImage dlgIcon; Text dlgName, dlgType, dlgStats, dlgDesc, dlgCompat, dlgQty, dlgTotal, dlgMsg;
        Button dlgBuy, dlgMinus, dlgPlus;
        Text hudMoney, hudLevel, hudMsg, hudHint, hudBait, hudDist, hudDir, hudShopMoney, hudInvInfo, colHeader, setQualityInfo;
        Image xpFill, tensionMarker, staminaFill, powerFill, biteFill;
        Text menuMoney;
        RectTransform tensionBox, staminaBox, powerBox, biteBox, joyArea;
        Button btnFish, btnCast, btnHook, btnReel, btnPull, btnStop, btnBait;
        RectTransform mapImgR, mapPlayer; RawImage mapImage;
        float msgTimer;
        bool hudVisible;
        Button[] qualBtns = new Button[3];
        Slider[] sliders = new Slider[3];

        // ---------------- ПОСТРОЕНИЕ ----------------
        public void Build()
        {
            if (I != null && I != this) { Destroy(gameObject); return; } // интерфейс создаётся один раз
            I = this;
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();
            }
            var cg = new GameObject("Холст");
            cg.transform.SetParent(transform, false);
            canvas = cg.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var sc = cg.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; sc.matchWidthOrHeight = 1f; // подгонка по высоте: вертикальные размеры одинаковы на всех телефонах
            cg.AddComponent<GraphicRaycaster>();
            var canvasRt = cg.GetComponent<RectTransform>();
            safe = UIKit.R("Безопасная область", canvasRt); UIKit.Stretch(safe);
            root = safe; ApplySafeArea();

            BuildHud(); BuildMenu(); BuildShop(); BuildInventory(); BuildCollection(); BuildMap(); BuildSettings(); BuildResult(); BuildTutorial();
            HideAllPanels();
            Game.Changed += Refresh;
            var f = FishingController.I;
            f.OnMessage += Toast; f.OnCatch += ShowResult; f.OnStateChanged += RefreshHud;
        }

        void OnDestroy()
        {
            Game.Changed -= Refresh;
            var f = FishingController.I;
            if (f != null) { f.OnMessage -= Toast; f.OnCatch -= ShowResult; f.OnStateChanged -= RefreshHud; }
            if (I == this) I = null;
        }

        public bool HasPanel { get { return currentPanel != null; } }

        void ApplySafeArea()
        {
            var sa = Screen.safeArea;
            if (safe == null || sa.width <= 0f || Screen.width <= 0 || Screen.height <= 0) return;
            safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            lastSafe = sa;
        }

        RectTransform Full(string n)
        {
            var r = UIKit.R(n, root); UIKit.Stretch(r); return r;
        }

        Text Label(Transform p, string s, int size, Vector2 anchor, Vector2 pos, Vector2 sz, TextAnchor a = TextAnchor.MiddleLeft, Color? col = null, FontStyle st = FontStyle.Normal)
        {
            var t = UIKit.Txt(p, s, size, col ?? Color.white, a, st);
            UIKit.At(t.rectTransform, anchor, pos, sz); return t;
        }

        Button B(Transform p, string txt, Color c, Vector2 anchor, Vector2 pos, Vector2 sz, UnityEngine.Events.UnityAction act, int fs = 36)
        {
            var b = UIKit.Btn(p, txt, c, act, fs);
            UIKit.At(b.GetComponent<RectTransform>(), anchor, pos, sz); return b;
        }

        // ---------------- HUD ----------------
        void BuildHud()
        {
            hudR = Full("HUD");
            var look = UIKit.R("Свайп камеры", hudR); UIKit.Stretch(look);
            var li = look.gameObject.AddComponent<Image>(); li.color = new Color(0, 0, 0, 0.002f);
            look.gameObject.AddComponent<LookPad>();

            joyArea = UIKit.R("Зона джойстика", hudR);
            joyArea.anchorMin = new Vector2(0, 0); joyArea.anchorMax = new Vector2(0.42f, 0.62f); joyArea.offsetMin = joyArea.offsetMax = Vector2.zero;
            var ji = joyArea.gameObject.AddComponent<Image>(); ji.color = new Color(0, 0, 0, 0.002f);
            var jb = UIKit.Img(joyArea, new Color(1, 1, 1, 0.18f)); jb.rectTransform.sizeDelta = new Vector2(300, 300);
            jb.rectTransform.anchorMin = jb.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            var jk = UIKit.Img(jb.transform, new Color(1, 1, 1, 0.55f)); jk.rectTransform.sizeDelta = new Vector2(130, 130);
            jk.rectTransform.anchorMin = jk.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            joyArea.gameObject.AddComponent<VirtualJoystick>().Setup(joyArea, jb.rectTransform, jk.rectTransform);
            // подсказка где джойстик
            var jh = UIKit.Img(joyArea, new Color(1, 1, 1, 0.1f)); UIKit.At(jh.rectTransform, new Vector2(0, 0), new Vector2(120, 120), new Vector2(240, 240));
            jh.gameObject.name = "Подсказка джойстика";

            // верхняя панель
            var top = UIKit.Img(hudR, new Color(0, 0, 0, 0.45f)); UIKit.At(top.rectTransform, new Vector2(0, 1), new Vector2(20, -20), new Vector2(520, 130));
            hudMoney = Label(top.transform, "", 40, new Vector2(0, 1), new Vector2(24, -12), new Vector2(480, 56), TextAnchor.MiddleLeft, UIKit.Accent, FontStyle.Bold);
            hudLevel = Label(top.transform, "", 30, new Vector2(0, 1), new Vector2(24, -64), new Vector2(480, 40));
            var xpBg = UIKit.Img(top.transform, new Color(0, 0, 0, 0.5f)); UIKit.At(xpBg.rectTransform, new Vector2(0, 0), new Vector2(24, 14), new Vector2(472, 16));
            xpFill = UIKit.Img(xpBg.transform, UIKit.Green); xpFill.rectTransform.anchorMin = Vector2.zero; xpFill.rectTransform.anchorMax = new Vector2(0.5f, 1); xpFill.rectTransform.offsetMin = xpFill.rectTransform.offsetMax = Vector2.zero;

            B(hudR, "Меню", UIKit.PanelC, new Vector2(1, 1), new Vector2(-20, -20), new Vector2(210, 90), () => GameManager.I.ToMenu(), 34);
            B(hudR, "Рюкзак", UIKit.Panel2, new Vector2(1, 1), new Vector2(-250, -20), new Vector2(210, 90), () => Open(invR, true), 34);
            B(hudR, "Карта", UIKit.Panel2, new Vector2(1, 1), new Vector2(-480, -20), new Vector2(210, 90), () => Open(mapR, true), 34);
            B(hudR, "Настройки", UIKit.Panel2, new Vector2(1, 1), new Vector2(-710, -20), new Vector2(260, 90), () => Open(setR, true), 34);

            // сообщения
            var mb = UIKit.Img(hudR, new Color(0, 0, 0, 0.55f)); UIKit.At(mb.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(1100, 90));
            mb.rectTransform.anchoredPosition = new Vector2(0, -170); mb.rectTransform.pivot = new Vector2(0.5f, 1);
            hudMsg = Label(mb.transform, "", 34, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060, 80), TextAnchor.MiddleCenter);
            mb.gameObject.name = "Сообщение";
            hudHint = Label(hudR, "", 34, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1100, 60), TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.95f), FontStyle.Bold);
            var sh = hudHint.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.9f);

            // кнопки рыбалки
            btnFish = B(hudR, "Рыбалка", UIKit.Blue, new Vector2(1, 0), new Vector2(-40, 60), new Vector2(300, 130), () => FishingController.I.ToggleFishing(), 42);
            btnStop = B(hudR, "Закончить", UIKit.Gray, new Vector2(1, 0), new Vector2(-40, 60), new Vector2(300, 100), () => FishingController.I.ToggleFishing(), 34);
            btnCast = B(hudR, "Заброс\n(удерживать)", UIKit.Accent, new Vector2(1, 0), new Vector2(-40, 200), new Vector2(300, 190), null, 38);
            var hc = btnCast.gameObject.AddComponent<HoldButton>();
            hc.down = () => FishingController.I.BeginCharge(); hc.up = () => FishingController.I.ReleaseCast();
            btnHook = B(hudR, "ПОДСЕЧКА!", UIKit.Red, new Vector2(1, 0), new Vector2(-40, 200), new Vector2(340, 230), () => FishingController.I.SetHook(), 46);
            btnPull = B(hudR, "Вытащить снасть", UIKit.Gray, new Vector2(1, 0), new Vector2(-40, 330), new Vector2(300, 90), () => FishingController.I.Reclaim(), 28);
            btnReel = B(hudR, "Тянуть\n(удерживать)", UIKit.Green, new Vector2(1, 0), new Vector2(-40, 200), new Vector2(340, 240), null, 44);
            var hr = btnReel.gameObject.AddComponent<HoldButton>();
            hr.down = () => FishingController.I.SetReeling(true); hr.up = () => FishingController.I.SetReeling(false);
            btnBait = B(hudR, "Наживка", UIKit.Panel2, new Vector2(0.5f, 0), new Vector2(-130, 110), new Vector2(700, 80), () => CycleBait(), 28);
            btnBait.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0); btnBait.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 100);
            hudBait = btnBait.GetComponentInChildren<Text>();

            // натяжение
            tensionBox = UIKit.R("Натяжение", hudR); UIKit.At(tensionBox, new Vector2(1, 0.5f), new Vector2(-440, 0), new Vector2(70, 520));
            tensionBox.pivot = new Vector2(1, 0.5f);
            var tbg = UIKit.Img(tensionBox, new Color(0, 0, 0, 0.55f)); UIKit.Stretch(tbg.rectTransform);
            var zone = UIKit.Img(tensionBox, new Color(0.2f, 0.75f, 0.4f, 0.55f)); zone.rectTransform.anchorMin = new Vector2(0, 0.25f / 1.5f); zone.rectTransform.anchorMax = new Vector2(1, 0.8f / 1.5f); zone.rectTransform.offsetMin = zone.rectTransform.offsetMax = Vector2.zero;
            var danger = UIKit.Img(tensionBox, new Color(0.9f, 0.2f, 0.15f, 0.55f)); danger.rectTransform.anchorMin = new Vector2(0, 1f / 1.5f); danger.rectTransform.anchorMax = new Vector2(1, 1); danger.rectTransform.offsetMin = danger.rectTransform.offsetMax = Vector2.zero;
            tensionMarker = UIKit.Img(tensionBox, Color.white, false); tensionMarker.rectTransform.anchorMin = new Vector2(-0.2f, 0); tensionMarker.rectTransform.anchorMax = new Vector2(1.2f, 0);
            tensionMarker.rectTransform.sizeDelta = new Vector2(0, 12);
            Label(tensionBox, "Леска", 26, new Vector2(0.5f, 1), new Vector2(0, 40), new Vector2(160, 34), TextAnchor.MiddleCenter).rectTransform.pivot = new Vector2(0.5f, 0);

            staminaBox = UIKit.R("Силы рыбы", hudR); UIKit.At(staminaBox, new Vector2(0.5f, 1), new Vector2(0, -280), new Vector2(700, 36)); staminaBox.pivot = new Vector2(0.5f, 1);
            var sbg = UIKit.Img(staminaBox, new Color(0, 0, 0, 0.55f)); UIKit.Stretch(sbg.rectTransform);
            staminaFill = UIKit.Img(staminaBox, new Color(0.9f, 0.45f, 0.2f)); staminaFill.rectTransform.anchorMin = Vector2.zero; staminaFill.rectTransform.anchorMax = Vector2.one; staminaFill.rectTransform.offsetMin = staminaFill.rectTransform.offsetMax = Vector2.zero;
            Label(staminaBox, "Силы рыбы", 24, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 34), TextAnchor.MiddleCenter);
            hudDist = Label(hudR, "", 34, new Vector2(0.5f, 1), new Vector2(0, -325), new Vector2(700, 46), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            hudDist.rectTransform.pivot = new Vector2(0.5f, 1);
            hudDir = Label(hudR, "", 44, new Vector2(0.5f, 1), new Vector2(0, -375), new Vector2(1300, 60), TextAnchor.MiddleCenter, UIKit.Accent, FontStyle.Bold);
            hudDir.rectTransform.pivot = new Vector2(0.5f, 1);

            powerBox = UIKit.R("Сила заброса", hudR); UIKit.At(powerBox, new Vector2(0.5f, 0), new Vector2(0, 190), new Vector2(620, 44)); powerBox.pivot = new Vector2(0.5f, 0);
            var pbg = UIKit.Img(powerBox, new Color(0, 0, 0, 0.55f)); UIKit.Stretch(pbg.rectTransform);
            powerFill = UIKit.Img(powerBox, UIKit.Accent); powerFill.rectTransform.anchorMin = Vector2.zero; powerFill.rectTransform.anchorMax = new Vector2(0.5f, 1); powerFill.rectTransform.offsetMin = powerFill.rectTransform.offsetMax = Vector2.zero;
            Label(powerBox, "Сила заброса", 26, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 40), TextAnchor.MiddleCenter);

            biteBox = UIKit.R("Окно подсечки", hudR); UIKit.At(biteBox, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(520, 30));
            var bbg = UIKit.Img(biteBox, new Color(0, 0, 0, 0.55f)); UIKit.Stretch(bbg.rectTransform);
            biteFill = UIKit.Img(biteBox, UIKit.Red); biteFill.rectTransform.anchorMin = Vector2.zero; biteFill.rectTransform.anchorMax = Vector2.one; biteFill.rectTransform.offsetMin = biteFill.rectTransform.offsetMax = Vector2.zero;
        }

        void RefreshHud()
        {
            var f0 = FishingController.I;
            if (resR != null && resR.gameObject.activeSelf && f0 != null && f0.state != FState.Итог)
            { resR.gameObject.SetActive(false); FishPreview.SetAllActive(false); Inp.UiBlocking = false; }
            if (!hudVisible) return;
            var f = FishingController.I; var s = f.state;
            btnFish.gameObject.SetActive(s == FState.Нет);
            btnStop.gameObject.SetActive(s == FState.Готов || s == FState.Ожидание || s == FState.Поклёвка);
            btnCast.gameObject.SetActive(s == FState.Готов || s == FState.Замах);
            btnHook.gameObject.SetActive(s == FState.Поклёвка);
            btnPull.gameObject.SetActive(s == FState.Ожидание);
            btnReel.gameObject.SetActive(s == FState.Вываживание);
            btnBait.gameObject.SetActive(s == FState.Готов || s == FState.Ожидание);
            tensionBox.gameObject.SetActive(s == FState.Вываживание);
            staminaBox.gameObject.SetActive(s == FState.Вываживание);
            hudDist.gameObject.SetActive(s == FState.Вываживание);
            hudDir.gameObject.SetActive(s == FState.Вываживание);
            powerBox.gameObject.SetActive(s == FState.Замах);
            biteBox.gameObject.SetActive(s == FState.Поклёвка);
            joyArea.gameObject.SetActive(true);
            RefreshStats();
        }

        void RefreshStats()
        {
            if (hudMoney == null) return;
            var S = Game.S;
            hudMoney.text = "Монеты: " + S.money;
            hudLevel.text = "Уровень " + S.level + "   Опыт " + S.xp + "/" + GameData.XpForLevel(S.level);
            xpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(S.xp / (float)GameData.XpForLevel(S.level)), 1);
            var b = GameData.GetItem(S.bait);
            if (b != null) hudBait.text = (b.cat == ItemCategory.Приманки ? "Приманка: " : "Наживка: ") + b.name + (b.consumable ? "  ×" + Game.Count(b.id) : "");
            if (b != null && Game.S.groundbaitLeft > 0) hudBait.text += "  · прикормка: " + Game.S.groundbaitLeft;
            if (menuMoney != null) menuMoney.text = "Монеты: " + S.money + "    Уровень " + S.level;
        }

        void CycleBait()
        {
            var list = new List<ItemDef>();
            foreach (var it in GameData.Items)
                if ((it.cat == ItemCategory.Наживки || it.cat == ItemCategory.Приманки) && Game.Owns(it.id)) list.Add(it);
            if (list.Count == 0) { Toast("Нет наживок. Купите их в магазине."); return; }
            int idx = list.FindIndex(x => x.id == Game.S.bait);
            Game.Equip(list[(idx + 1) % list.Count]);
            AudioManager.I?.Click();
        }

        public void Toast(string s) { if (hudMsg == null) return; hudMsg.text = s; msgTimer = 4.5f; hudMsg.transform.parent.gameObject.SetActive(true); }

        void Update()
        {
            if (Screen.safeArea != lastSafe) ApplySafeArea();
            if (!hudVisible) return;
            var f = FishingController.I;
            if (msgTimer > 0f) { msgTimer -= Time.deltaTime; if (msgTimer <= 0f) hudMsg.transform.parent.gameObject.SetActive(false); }
            hudHint.text = f.HintForState();
            if (f.state == FState.Замах) powerFill.rectTransform.anchorMax = new Vector2(f.power, 1);
            if (f.state == FState.Поклёвка) biteFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(f.biteWindowLeft / 2f), 1);
            if (f.state == FState.Вываживание)
            {
                float t = Mathf.Clamp01(f.tension / 1.5f);
                tensionMarker.rectTransform.anchorMin = new Vector2(-0.2f, t); tensionMarker.rectTransform.anchorMax = new Vector2(1.2f, t);
                tensionMarker.color = f.tension > 1f ? UIKit.Red : (f.tension < 0.25f ? Color.yellow : Color.white);
                staminaFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(f.stamina), 1);
                hudDist.text = "До рыбы: " + Mathf.Max(0, f.dist).ToString("0.0") + " м";
                hudDir.text = f.pullDir < 0 ? "◄  РЫБА ТЯНЕТ ВЛЕВО" : "РЫБА ТЯНЕТ ВПРАВО  ►";
            }
        }

        // ---------------- ГЛАВНОЕ МЕНЮ ----------------
        void BuildMenu()
        {
            menuR = Full("Главное меню");
            var shade = UIKit.R("Затемнение", menuR); UIKit.Stretch(shade);
            var si = shade.gameObject.AddComponent<Image>(); si.color = new Color(0, 0.05f, 0.1f, 0.35f);
            var strip = UIKit.Img(menuR, new Color(0.03f, 0.1f, 0.14f, 0.78f), false); strip.rectTransform.anchorMin = new Vector2(0, 0); strip.rectTransform.anchorMax = new Vector2(0.42f, 1); strip.rectTransform.offsetMin = strip.rectTransform.offsetMax = Vector2.zero;
            Label(menuR, "China Fishing", 96, new Vector2(0, 1), new Vector2(70, -70), new Vector2(760, 120), TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            Label(menuR, "Рыбалка у деревенского озера", 34, new Vector2(0, 1), new Vector2(76, -190), new Vector2(760, 50), TextAnchor.MiddleLeft, UIKit.Dim);
            string[] names = { "Играть", "Магазин", "Инвентарь", "Коллекция", "Карта", "Настройки" };
            Color[] cols = { UIKit.Green, UIKit.Accent, UIKit.Blue, UIKit.Blue, UIKit.Blue, UIKit.Gray };
            System.Action[] acts = {
                () => GameManager.I.Play(),
                () => Open(shopR, false), () => Open(invR, false), () => Open(colR, false), () => Open(mapR, false), () => Open(setR, false) };
            for (int i = 0; i < names.Length; i++)
            {
                var act = acts[i];
                B(menuR, names[i], cols[i], new Vector2(0, 1), new Vector2(70, -290 - i * 112), new Vector2(620, 98), () => act(), i == 0 ? 50 : 42);
            }
            menuMoney = Label(menuR, "", 34, new Vector2(0, 0), new Vector2(76, 40), new Vector2(760, 50), TextAnchor.MiddleLeft, UIKit.Accent, FontStyle.Bold);
        }

        // ---------------- ОБЩИЕ ПАНЕЛИ ----------------
        RectTransform MakePanel(string title, out RectTransform content)
        {
            var p = Full(title);
            var dim = UIKit.R("Фон", p); UIKit.Stretch(dim); var di = dim.gameObject.AddComponent<Image>(); di.color = new Color(0, 0.04f, 0.08f, 0.82f);
            var box = UIKit.Img(p, UIKit.PanelC); UIKit.Stretch(box.rectTransform, 50, 40, 50, 40);
            Label(box.transform, title, 54, new Vector2(0, 1), new Vector2(40, -20), new Vector2(900, 80), TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            B(box.transform, "Назад", UIKit.Red, new Vector2(1, 1), new Vector2(-30, -22), new Vector2(240, 80), () => Back(), 36);
            content = UIKit.R("Область", box.transform); UIKit.Stretch(content, 30, 20, 30, 120);
            return p;
        }

        void Open(RectTransform panel, bool fromHud)
        {
            HideAllPanels();
            menuR.gameObject.SetActive(false);
            panel.gameObject.SetActive(true);
            currentPanel = panel;
            Time.timeScale = GameManager.I.InMenu ? 1f : 0f; // из игры — пауза
            Inp.UiBlocking = true; Inp.Move = Vector2.zero;
            hudR.gameObject.SetActive(false); hudVisible = false;
            if (panel == shopR) { if (shopDlg != null) shopDlg.gameObject.SetActive(false); RebuildShop(); }
            else if (panel == invR) RebuildInv();
            else if (panel == colR) { RebuildCol(); }
            else if (panel == mapR) RebuildMap();
            else if (panel == setR) { RefreshSettings(); SetSettingsTab(setTab); }
        }

        public void Back()
        {
            if (currentPanel == null) return;
            if (currentPanel == shopR && shopDlg != null && shopDlg.gameObject.activeSelf) { shopDlg.gameObject.SetActive(false); return; }
            if (currentPanel == setR) Game.Save();
            Time.timeScale = 1f;
            HideAllPanels();
            FishPreview.SetAllActive(false);
            if (GameManager.I.InMenu) ShowMenu(); else ShowHud();
        }

        void HideAllPanels()
        {
            foreach (var r in new[] { shopR, invR, colR, mapR, setR, resR, tutR }) if (r != null) r.gameObject.SetActive(false);
            currentPanel = null;
        }

        public void ShowMenu()
        {
            HideAllPanels(); hudR.gameObject.SetActive(false); hudVisible = false; menuR.gameObject.SetActive(true);
            RefreshStats(); Inp.UiBlocking = true;
            if (PlayerPrefs.GetInt("tut_done", 0) == 0) { }
        }

        public void ShowHud()
        {
            HideAllPanels(); Time.timeScale = 1f; menuR.gameObject.SetActive(false); hudR.gameObject.SetActive(true); hudVisible = true;
            Inp.UiBlocking = false; hudMsg.transform.parent.gameObject.SetActive(false);
            RefreshHud();
            if (PlayerPrefs.GetInt("tut_done", 0) == 0) { tutR.gameObject.SetActive(true); Inp.UiBlocking = true; }
        }

        void Refresh()
        {
            RefreshStats();
            if (currentPanel == shopR) RebuildShop();
            else if (currentPanel == invR) RebuildInv();
            else if (currentPanel == setR) RefreshSettingsTab();
            if (hudVisible) RefreshHud();
        }

        // ---------------- ВКЛАДКИ КАТЕГОРИЙ ----------------
        void BuildTabs(RectTransform parent, RectTransform tabsHolder, ItemCategory current, System.Action<ItemCategory> pick)
        {
            UIKit.Clear(tabsHolder);
            var cats = (ItemCategory[])System.Enum.GetValues(typeof(ItemCategory));
            for (int i = 0; i < cats.Length; i++)
            {
                var c = cats[i];
                var b = UIKit.Btn(tabsHolder, c.ToString(), c == current ? UIKit.Accent : UIKit.Panel2, () => pick(c), 34);
                var le = b.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 72;
            }
        }

        RectTransform TabHolder(RectTransform content)
        {
            var t = UIKit.R("Вкладки", content);
            t.anchorMin = new Vector2(0, 0); t.anchorMax = new Vector2(0, 1); t.pivot = new Vector2(0, 1);
            t.offsetMin = new Vector2(0, 0); t.offsetMax = new Vector2(320, 0);
            var v = t.gameObject.AddComponent<VerticalLayoutGroup>(); v.spacing = 8; v.childForceExpandWidth = true; v.childForceExpandHeight = false; v.childControlHeight = true; v.childControlWidth = true;
            return t;
        }

        // ---------------- МАГАЗИН ----------------
        void BuildShop()
        {
            RectTransform c; shopR = MakePanel("Магазин", out c);
            shopTabs = TabHolder(c);
            hudShopMoney = Label(shopR.GetChild(1), "", 40, new Vector2(1, 1), new Vector2(-300, -22), new Vector2(520, 80), TextAnchor.MiddleRight, UIKit.Accent, FontStyle.Bold);
            var sr = UIKit.Scroll(c, false, Vector2.zero, 1);
            var r = sr.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(340, 0); r.offsetMax = Vector2.zero;
            shopList = sr.content;
            BuildShopDialog();
        }

        // Магазин строится из данных: категория -> секции (подкатегории) -> карточки. Пустые секции не показываются.
        void RebuildShop()
        {
            hudShopMoney.text = "Монеты: " + Game.S.money + "   Ур. " + Game.S.level;
            BuildTabs(shopR, shopTabs, shopCat, c => { shopCat = c; shopMaker = null; RebuildShop(); });
            UIKit.Clear(shopList);

            var makers = GameData.MakersIn(shopCat);
            if (shopMaker != null && !makers.Contains(shopMaker)) shopMaker = null;
            if (makers.Count > 0)
            {
                var row = UIKit.R("Фильтр производителя", shopList);
                var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.spacing = 10; hl.childForceExpandWidth = false; hl.childForceExpandHeight = true; hl.childControlWidth = true; hl.childControlHeight = true;
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;
                var all = UIKit.Btn(row, "Все производители", shopMaker == null ? UIKit.Accent : UIKit.Panel2, () => { shopMaker = null; RebuildShop(); }, 28);
                all.gameObject.AddComponent<LayoutElement>().preferredWidth = 340;
                foreach (var m in makers)
                {
                    var m_ = m;
                    var mb = UIKit.Btn(row, m_, shopMaker == m_ ? UIKit.Accent : UIKit.Panel2, () => { shopMaker = m_; RebuildShop(); }, 28);
                    mb.gameObject.AddComponent<LayoutElement>().preferredWidth = 260;
                }
            }

            Canvas.ForceUpdateCanvases();
            var pr = shopList.parent as RectTransform; float cw = pr != null ? pr.rect.width : 0f; if (cw < 300f) cw = 1500f;
            int cols = Mathf.Clamp(Mathf.FloorToInt((cw - 20f) / 380f), 2, 4);
            float gap = 16f;
            float cellW = Mathf.Floor((cw - 36f - gap * (cols - 1)) / cols);

            var sections = GameData.SectionsFor(shopCat, shopMaker);
            if (sections.Count == 0)
            {
                var t = UIKit.Txt(shopList, "В этом разделе пока нет товаров.", 36, UIKit.Dim);
                t.gameObject.AddComponent<LayoutElement>().preferredHeight = 120;
                return;
            }

            string lastGroup = null;
            foreach (var kv in sections)
            {
                var sd = kv.Key;
                if (!string.IsNullOrEmpty(sd.group) && sd.group != lastGroup)
                {
                    var gt = UIKit.Txt(shopList, sd.group, 46, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
                    gt.gameObject.AddComponent<LayoutElement>().preferredHeight = 66;
                }
                lastGroup = sd.group;

                var ht = UIKit.Txt(shopList, sd.name, 38, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                ht.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
                var line = UIKit.Img(shopList, new Color(0.72f, 0.84f, 0.88f, 0.55f), false);
                line.gameObject.AddComponent<LayoutElement>().preferredHeight = 3;

                var grid = UIKit.R("Сетка " + sd.name, shopList);
                var g = grid.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(cellW, 360); g.spacing = new Vector2(gap, gap);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = cols;
                g.padding = new RectOffset(0, 0, 6, 14); g.childAlignment = TextAnchor.UpperLeft;

                foreach (var d in kv.Value) BuildShopCard(grid, d, cellW);
            }
        }

        void BuildShopCard(Transform grid, ItemDef d, float cellW)
        {
            var d_ = d;
            var card = UIKit.Img(grid, UIKit.Panel2); card.raycastTarget = true; card.gameObject.name = "Товар " + d.id;
            var cb = card.gameObject.AddComponent<Button>(); cb.targetGraphic = card;
            cb.onClick.AddListener(() => OpenShopDialog(d_));
            float w = cellW - 24f;

            var icon = UIKit.R("Иконка", card.transform); UIKit.At(icon, new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(150, 150));
            var ri = icon.gameObject.AddComponent<RawImage>(); ri.texture = IconFactory.Get(d); ri.raycastTarget = false;

            Label(card.transform, d.name, 30, new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(w, 66), TextAnchor.UpperCenter, Color.white, FontStyle.Bold);
            string typeLine = GameData.TypeName(d) + (d.consumable && d.packSize > 1 ? "  ·  " + d.packSize + " шт" : "");
            Label(card.transform, typeLine, 24, new Vector2(0.5f, 1), new Vector2(0, -238), new Vector2(w, 32), TextAnchor.UpperCenter, UIKit.Dim);
            if (!string.IsNullOrEmpty(d.maker))
                Label(card.transform, d.maker, 24, new Vector2(0.5f, 1), new Vector2(0, -270), new Vector2(w, 32), TextAnchor.UpperCenter, UIKit.Dim);

            bool lockedLvl = Game.S.level < d.minLevel;
            bool has = !d.consumable && Game.Owns(d.id);
            string price = has ? "Куплено" : lockedLvl ? "Нужен ур. " + d.minLevel : d.price == 0 ? "Бесплатно" : d.price + " монет";
            Label(card.transform, price, 34, new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(w, 48), TextAnchor.MiddleCenter, has ? UIKit.Green : (lockedLvl ? UIKit.Dim : UIKit.Accent), FontStyle.Bold);

            if (d.consumable && Game.Count(d.id) > 0)
                Label(card.transform, "В запасе: " + Game.Count(d.id), 24, new Vector2(0, 1), new Vector2(10, -10), new Vector2(w - 60, 34), TextAnchor.MiddleLeft, UIKit.Green);

            // индикатор «информация о товаре»
            var info = UIKit.Img(card.transform, UIKit.Blue); UIKit.At(info.rectTransform, new Vector2(1, 1), new Vector2(-10, -10), new Vector2(44, 44));
            var it = UIKit.Txt(info.transform, "i", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold); UIKit.Stretch(it.rectTransform);
        }

        // ---------- окно товара ----------
        void BuildShopDialog()
        {
            shopDlg = UIKit.R("Окно товара", shopR); UIKit.Stretch(shopDlg);
            var dim = UIKit.R("Затемнение", shopDlg); UIKit.Stretch(dim);
            var di = dim.gameObject.AddComponent<Image>(); di.color = new Color(0, 0.03f, 0.06f, 0.78f);
            var box = UIKit.Img(shopDlg, UIKit.PanelC); UIKit.Stretch(box.rectTransform, 140, 80, 140, 80);
            var bt = box.transform;

            var icon = UIKit.R("Иконка", bt); UIKit.At(icon, new Vector2(0, 1), new Vector2(40, -40), new Vector2(260, 260));
            dlgIcon = icon.gameObject.AddComponent<RawImage>(); dlgIcon.raycastTarget = false;

            dlgName = Label(bt, "", 50, new Vector2(0, 1), new Vector2(330, -36), new Vector2(900, 64), TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            dlgType = Label(bt, "", 30, new Vector2(0, 1), new Vector2(330, -104), new Vector2(900, 44), TextAnchor.MiddleLeft, UIKit.Dim);
            dlgStats = Label(bt, "", 34, new Vector2(0, 1), new Vector2(330, -152), new Vector2(900, 50), TextAnchor.MiddleLeft, UIKit.Accent);
            dlgDesc = Label(bt, "", 30, new Vector2(0, 1), new Vector2(330, -210), new Vector2(900, 110), TextAnchor.UpperLeft, Color.white);
            dlgCompat = Label(bt, "", 28, new Vector2(0, 1), new Vector2(330, -330), new Vector2(900, 100), TextAnchor.UpperLeft, UIKit.Dim);
            dlgMsg = Label(bt, "", 32, new Vector2(0, 0), new Vector2(40, 170), new Vector2(1200, 50), TextAnchor.MiddleLeft, UIKit.Green, FontStyle.Bold);

            B(bt, "Закрыть", UIKit.Red, new Vector2(1, 1), new Vector2(-30, -24), new Vector2(240, 80), () => shopDlg.gameObject.SetActive(false), 36);

            dlgMinus = B(bt, "−", UIKit.Blue, new Vector2(0, 0), new Vector2(40, 40), new Vector2(100, 100), () => { dlgQtyV = Mathf.Clamp(dlgQtyV - 1, 1, 99); RefreshShopDialog(); }, 56);
            dlgQty = Label(bt, "1", 44, new Vector2(0, 0), new Vector2(150, 40), new Vector2(150, 100), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            dlgPlus = B(bt, "+", UIKit.Blue, new Vector2(0, 0), new Vector2(310, 40), new Vector2(100, 100), () => { dlgQtyV = Mathf.Clamp(dlgQtyV + 1, 1, 99); RefreshShopDialog(); }, 56);
            dlgTotal = Label(bt, "", 34, new Vector2(0, 0), new Vector2(440, 40), new Vector2(520, 100), TextAnchor.MiddleLeft, UIKit.Accent, FontStyle.Bold);
            dlgBuy = B(bt, "Купить", UIKit.Green, new Vector2(1, 0), new Vector2(-40, 40), new Vector2(380, 100), BuyFromDialog, 36);
            shopDlg.gameObject.SetActive(false);
        }

        void OpenShopDialog(ItemDef d)
        {
            dlgItem = d; dlgQtyV = 1; dlgMsg.text = "";
            shopDlg.gameObject.SetActive(true);
            RefreshShopDialog();
        }

        static string CompatText(ItemDef d)
        {
            if (!string.IsNullOrEmpty(d.compat)) return "Совместимость: " + d.compat;
            var names = new List<string>();
            foreach (var f in GameData.Fish) if (GameData.Likes(f, d)) names.Add(f.name);
            return names.Count > 0 ? "Хорошо клюют: " + string.Join(", ", names.ToArray()) : "";
        }

        void RefreshShopDialog()
        {
            var d = dlgItem; if (d == null) return;
            dlgIcon.texture = IconFactory.Get(d);
            dlgName.text = d.name;
            dlgType.text = GameData.TypeName(d) + (d.consumable && d.packSize > 1 ? "  ·  В упаковке: " + d.packSize + " шт" : "") + (string.IsNullOrEmpty(d.maker) ? "" : "  ·  " + d.maker);
            dlgStats.text = d.statText;
            dlgDesc.text = d.desc;
            dlgCompat.text = CompatText(d);
            bool lockedLvl = Game.S.level < d.minLevel;
            bool has = !d.consumable && Game.Owns(d.id);
            if (!d.consumable) dlgQtyV = 1;
            dlgQty.text = d.consumable ? dlgQtyV + " уп." : "1";
            dlgMinus.interactable = dlgPlus.interactable = d.consumable;
            int total = d.price * dlgQtyV;
            dlgTotal.text = "Итого: " + total + " монет" + (d.consumable ? "\nПолучите: " + (dlgQtyV * Mathf.Max(1, d.packSize)) + " шт" : "");
            UIKit.SetLabel(dlgBuy, has ? "Куплено" : lockedLvl ? "Нужен ур. " + d.minLevel : "Купить");
            dlgBuy.interactable = !has && !lockedLvl;
        }

        void BuyFromDialog()
        {
            var d = dlgItem; if (d == null) return;
            int bought = 0; string err = null;
            for (int i = 0; i < dlgQtyV; i++) { err = Game.Buy(d); if (err != null) break; bought++; }
            if (bought > 0)
            {
                AudioManager.I?.Catch();
                dlgMsg.text = "Куплено: " + d.name + (d.consumable ? " ×" + bought * Mathf.Max(1, d.packSize) : "") + (err != null ? "  (" + err + ")" : "");
            }
            else dlgMsg.text = err ?? "";
            RefreshShopDialog();
        }

        // ---------------- КРАФТ ПРИКОРМКИ ----------------
        void BuildCraft(RectTransform page)
        {
            craftR = page;
            craftMsg = Label(page, "", 30, new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(1400, 50), TextAnchor.MiddleCenter, UIKit.Green, FontStyle.Bold);
            var sr = UIKit.Scroll(page, false, Vector2.zero, 1);
            var r = sr.GetComponent<RectTransform>(); UIKit.Stretch(r, 0, 60, 0, 0);
            craftList = sr.content;
        }

        void RebuildCraft()
        {
            UIKit.Clear(craftList);
            var hint = UIKit.Txt(craftList, "Материалы покупаются в магазине (вкладка «Материалы»). Готовая прикормка выбирается в рюкзаке и расходуется при забросе.", 28, UIKit.Dim, TextAnchor.MiddleLeft);
            hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 90;
            foreach (var rc in GameData.Recipes)
            {
                var rc_ = rc;
                var o = GameData.GetItem(rc.outId); if (o == null) continue;
                var card = UIKit.Img(craftList, UIKit.Panel2);
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 200;
                var icon = UIKit.R("Иконка", card.transform); UIKit.At(icon, new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(150, 150));
                var ri = icon.gameObject.AddComponent<RawImage>(); ri.texture = IconFactory.Get(o); ri.raycastTarget = false;
                Label(card.transform, o.name + "  ×" + rc.outCount, 40, new Vector2(0, 1), new Vector2(200, -14), new Vector2(1000, 54), TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
                Label(card.transform, o.statText, 28, new Vector2(0, 1), new Vector2(200, -68), new Vector2(1000, 40), TextAnchor.MiddleLeft, UIKit.Accent);
                bool ok = true; string need = "";
                for (int i = 0; i < rc.ids.Length; i++)
                {
                    var m = GameData.GetItem(rc.ids[i]); int have = Game.Count(rc.ids[i]);
                    bool enough = have >= rc.counts[i]; if (!enough) ok = false;
                    need += (i > 0 ? "   " : "") + "<color=" + (enough ? "#7FD08A" : "#E8705F") + ">" + (m != null ? m.name : rc.ids[i]) + " " + have + "/" + rc.counts[i] + "</color>";
                }
                var nt = Label(card.transform, need, 28, new Vector2(0, 0), new Vector2(200, 14), new Vector2(1100, 80), TextAnchor.UpperLeft, Color.white); nt.supportRichText = true;
                bool locked = Game.S.level < rc.minLevel;
                var btn = B(card.transform, locked ? "Нужен ур. " + rc.minLevel : "Изготовить", ok && !locked ? UIKit.Green : UIKit.Gray, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(320, 100), () =>
                {
                    string err = Game.Craft(rc_);
                    craftMsg.text = err != null ? err : "Готово: " + o.name + " ×" + rc_.outCount;
                    if (err == null) AudioManager.I?.Catch();
                }, 34);
                btn.interactable = ok && !locked;
            }
        }

        // ---------------- НАВЫКИ ----------------
        RectTransform skillList;
        void BuildSkills(RectTransform page)
        {
            var sr = UIKit.Scroll(page, false, Vector2.zero, 1);
            UIKit.Stretch(sr.GetComponent<RectTransform>());
            skillList = sr.content;
        }

        void RebuildSkills()
        {
            UIKit.Clear(skillList);
            foreach (var sk in GameData.Skills)
            {
                int count = sk.index >= 0 ? Game.S.skillCatches[sk.index] : Game.S.crafted;
                float pct = sk.index >= 0 ? Game.SkillPct(sk.index) : Game.CookPct();
                int toNext = sk.perStep - (count % sk.perStep);
                var card = UIKit.Img(skillList, UIKit.Panel2);
                card.gameObject.AddComponent<LayoutElement>().preferredHeight = 190;
                Label(card.transform, sk.name, 42, new Vector2(0, 1), new Vector2(24, -12), new Vector2(900, 56), TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
                Label(card.transform, "+" + pct.ToString("0.0").Replace('.', ',') + "%", 46, new Vector2(1, 1), new Vector2(-30, -10), new Vector2(300, 60), TextAnchor.MiddleRight, UIKit.Accent, FontStyle.Bold);
                Label(card.transform, sk.how, 28, new Vector2(0, 1), new Vector2(24, -70), new Vector2(1500, 40), TextAnchor.MiddleLeft, UIKit.Dim);
                string prog = (sk.index >= 0 ? "Поймано рыб: " : "Замесов: ") + count + "   ·   до следующего +0,3%: " + toNext;
                Label(card.transform, prog, 28, new Vector2(0, 1), new Vector2(24, -108), new Vector2(1500, 40), TextAnchor.MiddleLeft, UIKit.Green);
                Label(card.transform, sk.note, 26, new Vector2(0, 1), new Vector2(24, -144), new Vector2(1500, 36), TextAnchor.MiddleLeft, UIKit.Dim);
                var bg = UIKit.Img(card.transform, new Color(0, 0, 0, 0.45f)); UIKit.At(bg.rectTransform, new Vector2(0, 0), new Vector2(24, 8), new Vector2(1000, 10));
                var fl = UIKit.Img(bg.transform, UIKit.Green); fl.rectTransform.anchorMin = Vector2.zero; fl.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((count % sk.perStep) / (float)sk.perStep), 1); fl.rectTransform.offsetMin = fl.rectTransform.offsetMax = Vector2.zero;
            }
        }

        // ---------- вкладки окна «Настройки»: Настройки · Крафт · Навыки ----------
        RectTransform setPage, skillPage; Button[] setTabBtns = new Button[3]; int setTab;
        void BuildSettingsTabs(RectTransform box)
        {
            string[] t = { "Настройки", "Крафт", "Навыки" };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                setTabBtns[i] = B(box, t[i], UIKit.Panel2, new Vector2(0, 1), new Vector2(430 + i * 270, -24), new Vector2(255, 80), () => SetSettingsTab(k), 34);
            }
        }

        void SetSettingsTab(int i)
        {
            setTab = Mathf.Clamp(i, 0, 2);
            setPage.gameObject.SetActive(setTab == 0); craftR.gameObject.SetActive(setTab == 1); skillPage.gameObject.SetActive(setTab == 2);
            for (int k = 0; k < 3; k++) setTabBtns[k].GetComponent<Image>().color = k == setTab ? UIKit.Accent : UIKit.Panel2;
            if (setTab == 1) { craftMsg.text = ""; RebuildCraft(); }
            else if (setTab == 2) RebuildSkills();
        }

        void RefreshSettingsTab()
        {
            if (setTab == 1) RebuildCraft(); else if (setTab == 2) RebuildSkills();
        }

        // ---------------- ИНВЕНТАРЬ ----------------
        void BuildInventory()
        {
            RectTransform c; invR = MakePanel("Инвентарь", out c);
            invTabs = TabHolder(c);
            hudInvInfo = Label(c, "", 28, new Vector2(1, 1), new Vector2(0, 0), new Vector2(1200, 90), TextAnchor.UpperLeft, UIKit.Dim);
            var rt = hudInvInfo.rectTransform; rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1); rt.offsetMin = new Vector2(340, -110); rt.offsetMax = new Vector2(0, 0);
            var sr = UIKit.Scroll(c, false, Vector2.zero, 1);
            var r = sr.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(340, 0); r.offsetMax = new Vector2(0, -120);
            invList = sr.content;
        }

        string Eq(string id) { var d = GameData.GetItem(id); return d == null ? "—" : d.name; }

        void RebuildInv()
        {
            var S = Game.S;
            hudInvInfo.text = "Надето: " + Eq(S.rod) + " · " + Eq(S.reel) + " · " + Eq(S.line) + " · " + Eq(S.hook) + "\n" +
                              Eq(S.floatId) + " · " + Eq(S.sinker) + " · " + Eq(S.rig) + " · Наживка: " + Eq(S.bait);
            BuildTabs(invR, invTabs, invCat, c => { invCat = c; RebuildInv(); });
            UIKit.Clear(invList);
            int shown = 0;
            foreach (var d in GameData.ItemsIn(invCat))
            {
                if (!Game.Owns(d.id)) continue;
                shown++;
                var d_ = d;
                var card = UIKit.Img(invList, UIKit.Panel2);
                var le = card.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 170;
                var icon = UIKit.R("Иконка", card.transform); UIKit.At(icon, new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(130, 130));
                var ri = icon.gameObject.AddComponent<RawImage>(); ri.texture = IconFactory.Get(d); ri.raycastTarget = false;
                Label(card.transform, d.name + (d.consumable ? "   ×" + Game.Count(d.id) : ""), 38, new Vector2(0, 1), new Vector2(180, -14), new Vector2(950, 52), TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
                Label(card.transform, d.statText, 30, new Vector2(0, 1), new Vector2(180, -66), new Vector2(950, 44), TextAnchor.MiddleLeft, UIKit.Accent);
                Label(card.transform, d.desc, 28, new Vector2(0, 0), new Vector2(180, 14), new Vector2(950, 60), TextAnchor.UpperLeft, UIKit.Dim);
                bool equippable = GameData.IsEquippable(d.cat);
                if (equippable)
                {
                    bool eq = Game.IsEquipped(d.id);
                    var b = B(card.transform, eq ? "Выбрано" : "Выбрать", eq ? UIKit.Gray : UIKit.Green, new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(260, 90), () => { Game.Equip(d_); Toast("Выбрано: " + d_.name); }, 34);
                    b.interactable = !eq;
                }
            }
            if (shown == 0)
            {
                var t = UIKit.Txt(invList, "В этой категории пока ничего нет. Загляните в магазин.", 36, UIKit.Dim);
                t.gameObject.AddComponent<LayoutElement>().preferredHeight = 120;
            }
        }

        // ---------------- КОЛЛЕКЦИЯ ----------------
        void BuildCollection()
        {
            RectTransform c; colR = MakePanel("Коллекция рыб", out c);
            colHeader = Label(c, "", 34, new Vector2(0, 1), new Vector2(0, 0), new Vector2(1400, 50), TextAnchor.MiddleLeft, UIKit.Accent, FontStyle.Bold);
            var sr = UIKit.Scroll(c, true, new Vector2(520, 560), 3);
            var r = sr.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = new Vector2(0, -60);
            colGrid = sr.content;
        }

        void RebuildCol()
        {
            UIKit.Clear(colGrid);
            Canvas.ForceUpdateCanvases();
            var pr = colGrid.parent as RectTransform; float cw = pr != null ? pr.rect.width : 0f; if (cw < 300f) cw = 1760f;
            var gl = colGrid.GetComponent<GridLayoutGroup>();
            if (gl != null) gl.constraintCount = Mathf.Clamp(Mathf.FloorToInt((cw - 20f) / 540f), 1, 3);
            int have = 0;
            foreach (var f in GameData.Fish) if (Game.Record(f.id) != null) have++;
            colHeader.text = "Открыто видов: " + have + " из " + GameData.Fish.Count + "      Всего поймано: " + Game.S.totalCaught;
            foreach (var f in GameData.Fish)
            {
                var rec = Game.Record(f.id);
                var card = UIKit.Img(colGrid, UIKit.Panel2);
                var pv = UIKit.R("Просмотр", card.transform); UIKit.At(pv, new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(480, 210)); pv.pivot = new Vector2(0.5f, 1);
                if (rec != null)
                {
                    var raw = pv.gameObject.AddComponent<RawImage>(); var p = FishPreview.Get(f); raw.texture = p.Texture; p.SetActive(true); raw.raycastTarget = false;
                    Label(card.transform, f.name, 40, new Vector2(0.5f, 1), new Vector2(0, -232), new Vector2(480, 50), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold).rectTransform.pivot = new Vector2(0.5f, 1);
                    Label(card.transform, f.rarity.ToString(), 28, new Vector2(0.5f, 1), new Vector2(0, -282), new Vector2(480, 36), TextAnchor.MiddleCenter, IconFactory.RarityColor(f.rarity), FontStyle.Bold).rectTransform.pivot = new Vector2(0.5f, 1);
                    string info = "Макс. вес: " + rec.maxWeight.ToString("0.0") + " кг   Макс. размер: " + rec.maxLength.ToString("0") + " см\nПоймано: " + rec.caught + "\nМеста: " + f.habitat + "\n" + f.desc;
                    var t = Label(card.transform, info, 24, new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(480, 250), TextAnchor.UpperLeft, UIKit.Dim);
                    t.rectTransform.pivot = new Vector2(0.5f, 0);
                }
                else
                {
                    var bg = UIKit.Img(pv, new Color(0, 0, 0, 0.45f)); UIKit.Stretch(bg.rectTransform);
                    var q = UIKit.Txt(pv, "?", 120, new Color(1, 1, 1, 0.35f)); UIKit.Stretch(q.rectTransform);
                    Label(card.transform, "Неизвестный вид", 38, new Vector2(0.5f, 1), new Vector2(0, -232), new Vector2(480, 50), TextAnchor.MiddleCenter, UIKit.Gray, FontStyle.Bold).rectTransform.pivot = new Vector2(0.5f, 1);
                    Label(card.transform, "Поймайте эту рыбу впервые,\nчтобы узнать о ней больше", 28, new Vector2(0.5f, 1), new Vector2(0, -300), new Vector2(480, 100), TextAnchor.UpperCenter, UIKit.Dim).rectTransform.pivot = new Vector2(0.5f, 1);
                }
            }
        }

        // ---------------- КАРТА ----------------
        void BuildMap()
        {
            RectTransform c; mapR = MakePanel("Карта", out c);
            var holder = UIKit.R("Карта-рамка", c); UIKit.At(holder, new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(700, 700));
            mapImgR = holder;
            var raw = holder.gameObject.AddComponent<RawImage>(); mapImage = raw; raw.raycastTarget = false;
            var p = UIKit.Img(holder, UIKit.Accent); p.rectTransform.sizeDelta = new Vector2(26, 26);
            p.rectTransform.anchorMin = p.rectTransform.anchorMax = Vector2.zero; mapPlayer = p.rectTransform;
            mapSpots = UIKit.R("Места", c); mapSpots.anchorMin = new Vector2(0, 0); mapSpots.anchorMax = new Vector2(1, 1); mapSpots.offsetMin = new Vector2(740, 0); mapSpots.offsetMax = Vector2.zero;
            var v = mapSpots.gameObject.AddComponent<VerticalLayoutGroup>(); v.spacing = 14; v.childForceExpandHeight = false; v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandWidth = true;
        }

        Vector2 MapPos(Vector3 w)
        {
            return new Vector2((w.x / WorldBuilder.Size + 0.5f) * 700f, (w.z / WorldBuilder.Size + 0.5f) * 700f);
        }

        void RebuildMap()
        {
            if (mapImage.texture == null) mapImage.texture = GameManager.I.world.MakeMap(256);
            mapPlayer.anchoredPosition = MapPos(GameManager.I.player.transform.position);
            mapPlayer.SetAsLastSibling();
            // убрать старые маркеры (кроме игрока)
            for (int i = mapImgR.childCount - 1; i >= 0; i--) { var ch = mapImgR.GetChild(i); if (ch != mapPlayer) Destroy(ch.gameObject); }
            UIKit.Clear(mapSpots);
            var hdr = UIKit.Txt(mapSpots, "Места для рыбалки (нажмите, чтобы перейти)", 34, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            hdr.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            int n = 1;
            foreach (var sp in GameManager.I.world.spots)
            {
                var sp_ = sp;
                var m = UIKit.Img(mapImgR, sp.fishing ? UIKit.Blue : UIKit.Green); m.rectTransform.sizeDelta = new Vector2(34, 34);
                m.rectTransform.anchorMin = m.rectTransform.anchorMax = Vector2.zero; m.rectTransform.anchoredPosition = MapPos(sp.pos);
                var mt = UIKit.Txt(m.transform, n.ToString(), 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold); UIKit.Stretch(mt.rectTransform);
                var b = UIKit.Btn(mapSpots, n + ". " + sp.name, UIKit.Panel2, () =>
                {
                    if (FishingController.I != null && FishingController.I.state != FState.Нет) FishingController.I.StopFishing();
                    GameManager.I.player.Teleport(sp_.pos + Vector3.up * 0.3f, sp_.yaw);
                    GameManager.I.rig.yaw = sp_.yaw; GameManager.I.rig.Snap();
                    GameManager.I.SavePosition(); Toast("Вы пришли: " + sp_.name);
                    mapPlayer.anchoredPosition = MapPos(sp_.pos);
                }, 36);
                b.gameObject.AddComponent<LayoutElement>().preferredHeight = 90;
                n++;
            }
            var note = UIKit.Txt(mapSpots, "Жёлтая точка — вы. Синие — места для рыбалки, зелёная — деревня. Красные пятна — дома деревни, тёмно-синий цвет — глубокая вода.", 28, UIKit.Dim, TextAnchor.UpperLeft);
            note.gameObject.AddComponent<LayoutElement>().preferredHeight = 120;
        }

        // ---------------- НАСТРОЙКИ ----------------
        void BuildSettings()
        {
            RectTransform area; setR = MakePanel("Настройки", out area);
            setPage = UIKit.R("Страница настроек", area); UIKit.Stretch(setPage);
            var cr = UIKit.R("Страница крафта", area); UIKit.Stretch(cr); BuildCraft(cr);
            skillPage = UIKit.R("Страница навыков", area); UIKit.Stretch(skillPage); BuildSkills(skillPage);
            BuildSettingsTabs(setR.GetChild(1) as RectTransform);
            RectTransform c = setPage;
            string[] names = { "Общая громкость", "Музыка", "Звуки природы и рыбалки" };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                Label(c, names[i], 38, new Vector2(0, 1), new Vector2(0, -20 - i * 110), new Vector2(540, 70), TextAnchor.MiddleLeft);
                var s = UIKit.MakeSlider(c, 0.5f, v => { if (k == 0) Game.S.volMaster = v; else if (k == 1) Game.S.volMusic = v; else Game.S.volEffects = v; AudioManager.I?.ApplyVolumes(); });
                UIKit.At(s.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(560, -20 - i * 110), new Vector2(700, 70));
                sliders[i] = s;
            }
            Label(c, "Качество графики", 38, new Vector2(0, 1), new Vector2(0, -370), new Vector2(540, 70), TextAnchor.MiddleLeft);
            string[] q = { "Низкое", "Среднее", "Высокое" };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                qualBtns[i] = B(c, q[i], UIKit.Panel2, new Vector2(0, 1), new Vector2(560 + i * 240, -370), new Vector2(225, 80), () => { Game.S.quality = k; Game.ApplyQuality(); Game.Save(); RefreshSettings(); }, 34);
            }
            setQualityInfo = Label(c, "", 28, new Vector2(0, 1), new Vector2(0, -470), new Vector2(1500, 90), TextAnchor.UpperLeft, UIKit.Dim);
            B(c, "Сбросить прогресс", UIKit.Red, new Vector2(0, 1), new Vector2(0, -600), new Vector2(520, 90), () => ConfirmReset(), 36);
            Label(c, "China Fishing · версия 0.6", 28, new Vector2(0, 0), new Vector2(0, 0), new Vector2(900, 50), TextAnchor.MiddleLeft, UIKit.Gray);
        }

        bool resetArmed;
        void ConfirmReset()
        {
            if (!resetArmed) { resetArmed = true; Toast("Нажмите «Сбросить прогресс» ещё раз, чтобы стереть сохранение."); StartCoroutine(Disarm()); return; }
            if (FishingController.I != null && FishingController.I.state != FState.Нет) FishingController.I.StopFishing();
            Game.ResetProgress(); Game.ApplyQuality(); AudioManager.I?.ApplyVolumes();
            resetArmed = false; Toast("Прогресс сброшен (старое сохранение сохранено копией на устройстве)."); RefreshSettings(); RefreshStats();
        }
        System.Collections.IEnumerator Disarm() { yield return new WaitForSecondsRealtime(4f); resetArmed = false; }
        void GiveStarterAfterReset()
        {
            foreach (var id in new[] { "rod_start", "reel_basic", "line_015", "hook_s", "float_basic", "sinker_s", "rig_float" }) Game.S.owned.Add(id);
            Game.AddItem("worm", 15); Game.AddItem("bread", 10);
        }

        void RefreshSettings()
        {
            sliders[0].SetValueWithoutNotify(Game.S.volMaster); sliders[1].SetValueWithoutNotify(Game.S.volMusic); sliders[2].SetValueWithoutNotify(Game.S.volEffects);
            for (int i = 0; i < 3; i++) qualBtns[i].GetComponent<Image>().color = i == Game.S.quality ? UIKit.Accent : UIKit.Panel2;
            string[] info = { "Низкое: без теней, минимум травы и деревьев, 30 кадров/с — для слабых телефонов.", "Среднее: жёсткие тени, умеренная трава, 45 кадров/с.", "Высокое: мягкие тени, густая трава, 60 кадров/с." };
            setQualityInfo.text = info[Mathf.Clamp(Game.S.quality, 0, 2)] + "\nИзменение растительности применится при следующем запуске.";
        }

        // ---------------- ДОБЫЧА ----------------
        Text resTitle, resInfo; RawImage resPreview; Button resSell, resRelease;
        void BuildResult()
        {
            resR = Full("Улов");
            var dim = UIKit.R("Фон", resR); UIKit.Stretch(dim); dim.gameObject.AddComponent<Image>().color = new Color(0, 0.04f, 0.08f, 0.55f);
            var box = UIKit.Img(resR, UIKit.PanelC); UIKit.At(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 760)); box.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            resTitle = Label(box.transform, "", 56, new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(940, 80), TextAnchor.MiddleCenter, UIKit.Accent, FontStyle.Bold); resTitle.rectTransform.pivot = new Vector2(0.5f, 1);
            var pv = UIKit.R("Просмотр", box.transform); UIKit.At(pv, new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(560, 280)); pv.pivot = new Vector2(0.5f, 1);
            resPreview = pv.gameObject.AddComponent<RawImage>(); resPreview.raycastTarget = false;
            resInfo = Label(box.transform, "", 34, new Vector2(0.5f, 1), new Vector2(0, -400), new Vector2(940, 200), TextAnchor.UpperCenter); resInfo.rectTransform.pivot = new Vector2(0.5f, 1);
            resSell = B(box.transform, "Продать", UIKit.Green, new Vector2(0, 0), new Vector2(40, 30), new Vector2(440, 110), () => { FishPreview.SetAllActive(false); resR.gameObject.SetActive(false); Inp.UiBlocking = false; FishingController.I.SellCatch(); }, 36);
            resRelease = B(box.transform, "Отпустить", UIKit.Blue, new Vector2(1, 0), new Vector2(-40, 30), new Vector2(440, 110), () => { FishPreview.SetAllActive(false); resR.gameObject.SetActive(false); Inp.UiBlocking = false; FishingController.I.ReleaseCatch(); }, 36);
        }

        void ShowResult(CatchResult r)
        {
            var p = FishPreview.Get(r.fish); p.SetActive(true); resPreview.texture = p.Texture;
            resTitle.text = r.firstTime ? "Новый вид: " + r.fish.name + "!" : "Поймана: " + r.fish.name + (r.record ? "  (рекорд!)" : "");
            resInfo.text = "Вес: " + r.weight.ToString("0.00") + " кг     Размер: " + r.length.ToString("0") + " см\nРедкость: " + r.fish.rarity +
                           "\nОпыт: +" + r.xp + (r.levelUp ? "     НОВЫЙ УРОВЕНЬ " + Game.S.level + "! Награда выдана" : "") +
                           "\nЦена продажи: " + r.value + " монет";
            resR.gameObject.SetActive(true); Inp.UiBlocking = true; Inp.Move = Vector2.zero;
            resR.SetAsLastSibling();
        }

        // ---------------- ОБУЧЕНИЕ ----------------
        void BuildTutorial()
        {
            tutR = Full("Обучение");
            var dim = UIKit.R("Фон", tutR); UIKit.Stretch(dim); dim.gameObject.AddComponent<Image>().color = new Color(0, 0.04f, 0.08f, 0.8f);
            var box = UIKit.Img(tutR, UIKit.PanelC); UIKit.At(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300, 820)); box.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Label(box.transform, "Как играть", 60, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(1200, 80), TextAnchor.MiddleCenter, UIKit.Accent, FontStyle.Bold).rectTransform.pivot = new Vector2(0.5f, 1);
            string t = "• Левый палец: джойстик — вверх вперёд, вниз назад, вбок в сторону.\n" +
                       "• Свайп по экрану (правая часть) — поворот камеры. Двумя пальцами — приближение.\n" +
                       "• Подойдите к воде (мостки на юге озера) и нажмите «Рыбалка».\n" +
                       "• Выберите направление свайпом, удерживайте «Заброс» и отпустите в нужный момент.\n" +
                       "• Когда поплавок уйдёт под воду — сразу «ПОДСЕЧКА!».\n" +
                       "• Удерживайте «Тянуть», следите за шкалой лески (держите зелёную зону). Когда рыба тянет в сторону — отклоняйте джойстик в обратную.\n" +
                       "• Улов можно продать или отпустить. На монеты покупайте снасти и наживки в магазине.";
            var tx = Label(box.transform, t, 34, new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(1200, 560), TextAnchor.UpperLeft); tx.rectTransform.pivot = new Vector2(0.5f, 1);
            B(box.transform, "Понятно, начать!", UIKit.Green, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(600, 110), () => { PlayerPrefs.SetInt("tut_done", 1); PlayerPrefs.Save(); tutR.gameObject.SetActive(false); Inp.UiBlocking = false; }, 42).GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0);
        }
    }
}
