using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChinaFishing
{
    public static class UIKit
    {
        public static Font Font;
        public static Sprite Round;
        public static readonly Color PanelC = new Color(0.05f, 0.14f, 0.18f, 0.95f);
        public static readonly Color Panel2 = new Color(0.10f, 0.25f, 0.31f, 1f);
        public static readonly Color Accent = new Color(0.96f, 0.64f, 0.16f);
        public static readonly Color Green = new Color(0.20f, 0.70f, 0.42f);
        public static readonly Color Red = new Color(0.85f, 0.26f, 0.20f);
        public static readonly Color Blue = new Color(0.20f, 0.50f, 0.80f);
        public static readonly Color Dim = new Color(0.72f, 0.84f, 0.88f);
        public static readonly Color Gray = new Color(0.35f, 0.42f, 0.45f);

        public static void Init()
        {
            if (Font != null) return;
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            const int n = 48, r = 16;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x, r, n - 1 - r), cy = Mathf.Clamp(y, r, n - 1 - r);
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f)));
                }
            t.Apply();
            Round = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        public static RectTransform R(string n, Transform parent)
        {
            var g = new GameObject(n, typeof(RectTransform));
            g.transform.SetParent(parent, false);
            return g.GetComponent<RectTransform>();
        }

        public static void Stretch(RectTransform r, float l = 0, float b = 0, float rt = 0, float t = 0)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rt, -t);
        }

        public static void At(RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            r.anchorMin = anchor; r.anchorMax = anchor; r.pivot = anchor;
            r.anchoredPosition = pos; r.sizeDelta = size;
        }

        public static Image Img(Transform p, Color c, bool round = true)
        {
            var r = R("Рамка", p);
            var i = r.gameObject.AddComponent<Image>();
            i.color = c;
            if (round) { i.sprite = Round; i.type = Image.Type.Sliced; }
            i.raycastTarget = false;
            return i;
        }

        public static Text Txt(Transform p, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter, FontStyle st = FontStyle.Normal)
        {
            var r = R("Текст", p);
            var t = r.gameObject.AddComponent<Text>();
            t.font = Font; t.text = s; t.fontSize = size; t.color = c; t.alignment = a; t.fontStyle = st;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Btn(Transform p, string label, Color c, UnityAction click, int fs = 36)
        {
            var img = Img(p, c); img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1, 1, 1, 1);
            cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f); cb.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
            b.colors = cb;
            var t = Txt(img.transform, label, fs, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform, 8, 4, 8, 4);
            if (click != null) b.onClick.AddListener(click);
            b.onClick.AddListener(() => { if (AudioManager.I != null) AudioManager.I?.Click(); });
            return b;
        }

        public static void SetLabel(Button b, string s) { b.GetComponentInChildren<Text>().text = s; }

        public static ScrollRect Scroll(Transform p, bool grid, Vector2 cell, int cols)
        {
            var root = R("Список", p);
            var bg = root.gameObject.AddComponent<Image>(); bg.color = new Color(0, 0, 0, 0.001f);
            root.gameObject.AddComponent<RectMask2D>();
            var content = R("Содержимое", root);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0, 0); content.offsetMax = new Vector2(0, 0);
            if (grid)
            {
                var g = content.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = cell; g.spacing = new Vector2(20, 20); g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = cols;
                g.padding = new RectOffset(10, 10, 10, 10); g.childAlignment = TextAnchor.UpperCenter;
            }
            else
            {
                var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
                v.spacing = 16; v.padding = new RectOffset(8, 8, 8, 20);
                v.childForceExpandWidth = true; v.childForceExpandHeight = false; v.childControlHeight = true; v.childControlWidth = true;
            }
            var f = content.gameObject.AddComponent<ContentSizeFitter>(); f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = root.gameObject.AddComponent<ScrollRect>();
            sr.content = content; sr.horizontal = false; sr.vertical = true; sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 40f; sr.viewport = root;
            return sr;
        }

        public static void Clear(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject); }

        public static Slider MakeSlider(Transform p, float v, UnityAction<float> on)
        {
            var root = R("Слайдер", p);
            var sl = root.gameObject.AddComponent<Slider>();
            var bg = Img(root, new Color(0, 0, 0, 0.45f));
            bg.rectTransform.anchorMin = new Vector2(0, 0.35f); bg.rectTransform.anchorMax = new Vector2(1, 0.65f);
            bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;
            var fa = R("Область", root); fa.anchorMin = new Vector2(0, 0.35f); fa.anchorMax = new Vector2(1, 0.65f); fa.offsetMin = fa.offsetMax = Vector2.zero;
            var fill = Img(fa, Accent); Stretch(fill.rectTransform);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0, 1);
            var ha = R("Ручка", root); Stretch(ha, 24, 0, 24, 0);
            var h = Img(ha, Color.white); h.raycastTarget = true;
            h.rectTransform.sizeDelta = new Vector2(48, 0);
            h.rectTransform.anchorMin = new Vector2(0, 0); h.rectTransform.anchorMax = new Vector2(0, 1);
            sl.fillRect = fill.rectTransform; sl.handleRect = h.rectTransform; sl.targetGraphic = h;
            sl.direction = Slider.Direction.LeftToRight; sl.minValue = 0; sl.maxValue = 1; sl.value = v;
            sl.onValueChanged.AddListener(on);
            return sl;
        }
    }

    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public System.Action down, up;
        bool held;
        public void OnPointerDown(PointerEventData e) { held = true; if (down != null) down(); }
        public void OnPointerUp(PointerEventData e) { if (held) { held = false; if (up != null) up(); } }
        void OnDisable() { if (held) { held = false; if (up != null) up(); } }
    }

    // Плавающий виртуальный джойстик в левой части экрана
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform baseR, knob;
        RectTransform area; Vector2 origin; float radius = 130f; int pid = -99;

        public void Setup(RectTransform areaRect, RectTransform b, RectTransform k) { area = areaRect; baseR = b; knob = k; Hide(); }
        void Hide() { if (baseR != null) baseR.gameObject.SetActive(false); }

        Vector2 Local(PointerEventData e)
        {
            Vector2 lp; RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out lp); return lp;
        }

        public void OnPointerDown(PointerEventData e)
        {
            pid = e.pointerId; origin = Local(e);
            baseR.gameObject.SetActive(true); baseR.anchoredPosition = origin + area.rect.size * 0.0f;
            baseR.anchorMin = baseR.anchorMax = new Vector2(0.5f, 0.5f);
            baseR.localPosition = new Vector3(origin.x, origin.y, 0);
            knob.anchoredPosition = Vector2.zero;
            Inp.Move = Vector2.zero;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != pid) return;
            var d = Local(e) - origin;
            float m = Mathf.Min(1f, d.magnitude / radius);
            var v = d.sqrMagnitude > 0.001f ? d.normalized * m : Vector2.zero;
            knob.anchoredPosition = v * radius * 0.8f;
            Inp.Move = m < 0.12f ? Vector2.zero : v; // вверх = вперёд, вправо = вправо
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != pid) return;
            pid = -99; Inp.Move = Vector2.zero; Hide();
        }

        void OnDisable() { Inp.Move = Vector2.zero; }
    }

    // Свайп по экрану вращает камеру (без отдельной кнопки LOOK)
    public class LookPad : MonoBehaviour, IDragHandler
    {
        public void OnDrag(PointerEventData e) { Inp.LookDelta += e.delta; }
    }
}
