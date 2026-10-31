using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Code-built uGUI helpers shared by the menu and HUD.
public static class UIKit
{
    static Font font;
    public static Font Font => font ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

    public static readonly Color Panel = new Color(0.07f, 0.05f, 0.08f, 0.86f);
    public static readonly Color Pink = new Color(1f, 0.5f, 0.78f);
    public static readonly Color PinkDeep = new Color(0.86f, 0.25f, 0.56f);

    static Sprite flower, round;

    // Little five-petal flower used as the currency icon.
    public static Sprite Flower
    {
        get
        {
            if (flower) return flower;
            int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            Vector2 c = new Vector2(n / 2f, n / 2f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - c;
                float a = 0f; Color col = new Color(1f, 0.55f, 0.8f);
                for (int k = 0; k < 5; k++)
                {
                    float ang = k * Mathf.PI * 2f / 5f + Mathf.PI / 2f;
                    Vector2 pc = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * n * 0.24f;
                    float d = (p - pc).magnitude;
                    a = Mathf.Max(a, Mathf.Clamp01((n * 0.2f - d) * 1.2f));
                }
                float dc = p.magnitude;
                float center = Mathf.Clamp01((n * 0.13f - dc) * 1.2f);
                Color petal = Color.Lerp(new Color(1f, 0.85f, 0.93f), col, Mathf.Clamp01(dc / (n * 0.4f)));
                col = Color.Lerp(petal, new Color(1f, 0.82f, 0.3f), center);
                col.a = Mathf.Max(a, center);
                px[y * n + x] = col;
            }
            tex.SetPixels(px);
            tex.Apply();
            flower = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return flower;
        }
    }

    static Sprite fadeRight;

    // Horizontal gradient: opaque on the left, fading to clear on the right.
    public static Sprite FadeRight
    {
        get
        {
            if (fadeRight) return fadeRight;
            var tex = new Texture2D(128, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 128; x++)
            {
                float k = x / 127f;
                tex.SetPixel(x, 0, new Color(1, 1, 1, 1f - k * k * (3f - 2f * k)));
            }
            tex.Apply();
            fadeRight = Sprite.Create(tex, new Rect(0, 0, 128, 1), new Vector2(0.5f, 0.5f));
            return fadeRight;
        }
    }

    // Soft rounded rectangle (9-sliced) for panels and buttons.
    public static Sprite Round
    {
        get
        {
            if (round) return round;
            int n = 32, r = 10;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Max(0, Mathf.Max(r - x - 0.5f, x + 0.5f - (n - r)));
                float dy = Mathf.Max(0, Mathf.Max(r - y - 0.5f, y + 0.5f - (n - r)));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f));
            }
            tex.SetPixels(px);
            tex.Apply();
            round = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return round;
        }
    }

    public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot ?? anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Text Text(Transform parent, string s, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box, Color col, bool bold = true)
    {
        var rt = Rect(parent, "Text", anchor, pos, box);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.text = s;
        t.fontSize = size;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.alignment = align;
        t.color = col;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var sh = rt.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0.15f, 0.02f, 0.1f, 0.7f);
        sh.effectDistance = new Vector2(2, -3);
        return t;
    }

    public static Image Image(Transform parent, Color col, Vector2 anchor, Vector2 pos, Vector2 size, bool rounded = false)
    {
        var rt = Rect(parent, "Image", anchor, pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = col;
        img.raycastTarget = false;
        if (rounded) { img.sprite = Round; img.type = UnityEngine.UI.Image.Type.Sliced; }
        return img;
    }

    public static void Stretch(RectTransform rt, float pad = 0f)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
    }

    public static Button Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color col, System.Action onClick, int fontSize = 34)
    {
        var img = Image(parent, Color.white, anchor, pos, size, true);
        img.raycastTarget = true;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = col;
        cb.highlightedColor = Color.Lerp(col, Color.white, 0.25f);
        cb.selectedColor = col;
        cb.pressedColor = Color.Lerp(col, Color.black, 0.2f);
        cb.disabledColor = new Color(0.3f, 0.3f, 0.32f, 0.6f);
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        btn.onClick.AddListener(() => { SFX.Play(SFX.S.UiSelect, 1f, 0.03f); onClick?.Invoke(); });
        var t = Text(img.transform, label, fontSize, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size, Color.white);
        Stretch(t.rectTransform);
        img.gameObject.AddComponent<HoverPop>();
        return btn;
    }

    public static Slider Slider(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, float value, Color col, System.Action<float> onChange)
    {
        var root = Rect(parent, "Slider", anchor, pos, size);
        var bg = Image(root, new Color(1, 1, 1, 0.12f), new Vector2(0.5f, 0.5f), Vector2.zero, size, true);
        Stretch(bg.rectTransform);
        bg.rectTransform.offsetMin = new Vector2(0, size.y * 0.3f);
        bg.rectTransform.offsetMax = new Vector2(0, -size.y * 0.3f);

        var fillArea = Rect(root, "Fill Area", new Vector2(0.5f, 0.5f), Vector2.zero, size);
        Stretch(fillArea);
        fillArea.offsetMin = new Vector2(0, size.y * 0.3f);
        fillArea.offsetMax = new Vector2(0, -size.y * 0.3f);
        var fill = Image(fillArea, col, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero, true);
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0, 1);
        fill.rectTransform.sizeDelta = Vector2.zero;

        var handleArea = Rect(root, "Handle Slide Area", new Vector2(0.5f, 0.5f), Vector2.zero, size);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(size.y * 0.5f, 0); handleArea.offsetMax = new Vector2(-size.y * 0.5f, 0);
        var handle = Image(handleArea, Color.white, new Vector2(0, 0.5f), Vector2.zero, new Vector2(size.y, 0), true);
        handle.rectTransform.anchorMin = new Vector2(0, 0); handle.rectTransform.anchorMax = new Vector2(0, 1);
        handle.rectTransform.sizeDelta = new Vector2(size.y, 0);
        handle.raycastTarget = true;
        bg.raycastTarget = true;

        var s = root.gameObject.AddComponent<Slider>();
        s.fillRect = fill.rectTransform;
        s.handleRect = handle.rectTransform;
        s.targetGraphic = handle;
        s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        s.minValue = 0f; s.maxValue = 1f;
        s.value = value;
        s.onValueChanged.AddListener(v => onChange?.Invoke(v));
        root.gameObject.AddComponent<HoverPop>().scale = 1.02f;
        return s;
    }
}

// Small scale-up and a soft tick when the pointer enters a control.
public class HoverPop : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public float scale = 1.06f;
    float target = 1f, cur = 1f;

    public void OnPointerEnter(PointerEventData e)
    {
        var sel = GetComponent<Selectable>();
        if (sel && !sel.interactable) return;
        target = scale;
        SFX.Play(SFX.S.UiHover, 1f, 0.1f);
    }

    public void OnPointerExit(PointerEventData e) => target = 1f;

    void OnDisable() { target = cur = 1f; transform.localScale = Vector3.one; }

    void Update()
    {
        cur = Mathf.Lerp(cur, target, Time.unscaledDeltaTime * 14f);
        transform.localScale = Vector3.one * cur;
    }
}
