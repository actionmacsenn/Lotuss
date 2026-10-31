using UnityEngine;
using UnityEngine.UI;

// Screen-space UI: kill counter, combo, surge timer, selected ability, and the radial ability wheel.
public class HUD : MonoBehaviour
{
    public static HUD I;

    Canvas canvas;
    CanvasGroup group;
    Font font;

    // Waves and currency.
    Text flowerText, announce, announceSub;
    RectTransform flowerIcon, announceRT;
    float announceT = -10f, flowerPulse;
    int shownFlowers;
    class Fly { public RectTransform rt; public Vector2 from, ctrl; public float t, dur; public int value; }
    readonly System.Collections.Generic.List<Fly> flies = new System.Collections.Generic.List<Fly>();
    readonly System.Collections.Generic.Stack<RectTransform> flyPool = new System.Collections.Generic.Stack<RectTransform>();
    int pendingFlowers;

    Text kills, combo, surge, abilityName, hint, centerText, descTitle, descBody, descMeta;
    Image abilitySwatch, cooldownFill, descPanel, descStripe, dim;
    RectTransform wheelRoot, comboRT, killsRT;
    WheelGraphic wheel;
    Text[] segLabels = new Text[Abilities.PerPage];
    Text[] segKeys = new Text[Abilities.PerPage];
    float wheelAnim, lastHover = -1, hoverAnim;

    void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var go = new GameObject("Canvas");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        I = this;

        var root = canvas.transform as RectTransform;

        // Flowers (currency) under the kill counter.
        var fi = MakeImage(root, Color.white, new Vector2(0, 1), new Vector2(40, -112), new Vector2(46, 46));
        fi.sprite = UIKit.Flower;
        fi.rectTransform.pivot = new Vector2(0, 0.5f);
        flowerIcon = fi.rectTransform;
        flowerText = MakeText(root, "0", 38, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(96, -112), new Vector2(400, 50), new Color(1f, 0.85f, 0.93f));
        flowerText.rectTransform.pivot = new Vector2(0, 0.5f);
        shownFlowers = Save.Flowers;

        // Big centre announcements: WAVE 3 / WAVE CLEARED.
        announce = MakeText(root, "", 110, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 180), new Vector2(1400, 150), new Color(1f, 0.6f, 0.82f));
        announceRT = announce.rectTransform;
        announceSub = MakeText(root, "", 34, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(1200, 60), Color.white);
        var ol = announce.gameObject.AddComponent<Outline>();
        ol.effectColor = new Color(0.3f, 0.03f, 0.15f, 0.8f);
        ol.effectDistance = new Vector2(4, -4);

        // Top-left: kills.
        kills = MakeText(root, "BLOOMED 0", 46, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(36, -26), new Vector2(700, 70), new Color(1f, 0.9f, 0.96f));
        killsRT = kills.rectTransform;
        // Top-centre: combo.
        combo = MakeText(root, "", 58, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(900, 90), new Color(1f, 0.6f, 0.82f));
        comboRT = combo.rectTransform;
        // Top-right: surge timer.
        surge = MakeText(root, "", 30, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-36, -32), new Vector2(700, 60), new Color(0.95f, 0.95f, 0.9f));

        // Bottom-right: selected ability.
        var panel = MakeImage(root, new Color(0.08f, 0.06f, 0.09f, 0.72f), new Vector2(1, 0), new Vector2(-30, 30), new Vector2(430, 120));
        panel.rectTransform.pivot = new Vector2(1, 0);
        abilitySwatch = MakeImage(panel.rectTransform, Color.white, new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(16, 84));
        abilitySwatch.rectTransform.pivot = new Vector2(0, 0.5f);
        abilityName = MakeText(panel.rectTransform, "", 36, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(52, -14), new Vector2(370, 54), Color.white);
        abilityName.rectTransform.pivot = new Vector2(0, 1);
        var barBg = MakeImage(panel.rectTransform, new Color(1, 1, 1, 0.12f), new Vector2(0, 0), new Vector2(52, 26), new Vector2(350, 14));
        barBg.rectTransform.pivot = new Vector2(0, 0);
        cooldownFill = MakeImage(barBg.rectTransform, Color.white, new Vector2(0, 0.5f), Vector2.zero, new Vector2(350, 14));
        cooldownFill.rectTransform.pivot = new Vector2(0, 0.5f);

        // Bottom-left: controls.
        hint = MakeText(root, "LMB  cast        Hold RMB  ability wheel        1-6 / Tab  quick select        M  music        Esc  menu", 22, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(34, 30), new Vector2(1100, 40), new Color(1, 1, 1, 0.75f));
        hint.rectTransform.pivot = new Vector2(0, 0);

        BuildWheel(root);
    }

    void BuildWheel(RectTransform root)
    {
        dim = MakeImage(root, new Color(0.05f, 0.02f, 0.06f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6000, 6000));
        dim.raycastTarget = false;

        wheelRoot = new GameObject("Wheel", typeof(RectTransform)).GetComponent<RectTransform>();
        wheelRoot.SetParent(root, false);
        wheelRoot.anchorMin = wheelRoot.anchorMax = new Vector2(0.5f, 0.5f);
        wheelRoot.anchoredPosition = new Vector2(-120, 40);
        wheelRoot.sizeDelta = new Vector2(700, 700);

        var wg = new GameObject("WheelGraphic", typeof(RectTransform));
        wg.transform.SetParent(wheelRoot, false);
        wheel = wg.AddComponent<WheelGraphic>();
        wheel.raycastTarget = false;
        wheel.rectTransform.sizeDelta = new Vector2(700, 700);

        float mid = (wheel.inner + wheel.outer) * 0.5f;
        for (int i = 0; i < Abilities.PerPage; i++)
        {
            float ang = (90f - i * 60f) * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * mid;
            segLabels[i] = MakeText(wheelRoot, "", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), pos, new Vector2(220, 100), Color.white);
            segLabels[i].lineSpacing = 0.85f;
            Vector2 kp = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (wheel.inner + 22f);
            segKeys[i] = MakeText(wheelRoot, (i + 1).ToString(), 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), kp, new Vector2(40, 30), new Color(1, 1, 1, 0.55f));
        }
        centerText = MakeText(wheelRoot, "", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 120), Color.white);

        // Description card to the right of the wheel.
        descPanel = MakeImage(wheelRoot, new Color(0.07f, 0.05f, 0.08f, 0.9f), new Vector2(0.5f, 0.5f), new Vector2(370, 0), new Vector2(420, 330));
        descPanel.rectTransform.pivot = new Vector2(0, 0.5f);
        descStripe = MakeImage(descPanel.rectTransform, Color.white, new Vector2(0, 0.5f), Vector2.zero, new Vector2(10, 330));
        descStripe.rectTransform.pivot = new Vector2(0, 0.5f);
        descTitle = MakeText(descPanel.rectTransform, "", 38, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(34, -22), new Vector2(370, 60), Color.white);
        descTitle.rectTransform.pivot = new Vector2(0, 1);
        descBody = MakeText(descPanel.rectTransform, "", 25, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(34, -86), new Vector2(360, 190), new Color(1, 1, 1, 0.88f));
        descBody.rectTransform.pivot = new Vector2(0, 1);
        descBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        descMeta = MakeText(descPanel.rectTransform, "", 22, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(34, 20), new Vector2(360, 40), new Color(1, 1, 1, 0.6f));
        descMeta.rectTransform.pivot = new Vector2(0, 0);

        wheelRoot.localScale = Vector3.zero;
    }

    Text MakeText(RectTransform parent, string s, int size, TextAnchor anchor, Vector2 anchorPt, Vector2 pos, Vector2 sizeDelta, Color col)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = s;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = anchor;
        t.color = col;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anchorPt;
        rt.pivot = anchorPt;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0.15f, 0.02f, 0.1f, 0.7f);
        sh.effectDistance = new Vector2(2, -3);
        return t;
    }

    Image MakeImage(RectTransform parent, Color col, Vector2 anchorPt, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Image", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = col;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = anchorPt;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return img;
    }

    void Update()
    {
        var game = Game.I;
        var caster = AbilityCaster.I;
        var em = EnemyManager.I;
        float dt = Time.unscaledDeltaTime;
        if (game == null || caster == null) return;

        // Gameplay HUD only shows once you are actually playing.
        bool playing = game.State == Game.Mode.Playing;
        group.alpha = Mathf.MoveTowards(group.alpha, playing ? 1f : 0f, dt * (playing ? 1.5f : 4f));
        if (group.alpha <= 0.001f && !playing) return;

        UpdateFlowers(dt);
        UpdateAnnounce();

        kills.text = "BLOOMED  " + game.Kills;
        killsRT.localScale = Vector3.one * (1f + game.KillPulse * 0.08f);

        if (game.Combo >= 5)
        {
            combo.text = "x" + game.Combo + "  " + ComboWord(game.Combo);
            float s = 1f + Mathf.Sin(Time.unscaledTime * 14f) * 0.03f + game.KillPulse * 0.12f;
            comboRT.localScale = Vector3.one * s;
            var c = Color.Lerp(new Color(1f, 0.6f, 0.82f), new Color(1f, 0.95f, 0.5f), Mathf.Clamp01((game.Combo - 5) / 60f));
            c.a = Mathf.Clamp01(game.ComboTimer * 2f);
            combo.color = c;
        }
        else combo.text = "";

        if (em != null)
        {
            if (em.Wave == 0) surge.text = "";
            else if (em.InBreak) surge.text = "WAVE " + (em.Wave + 1) + " in " + Mathf.CeilToInt(em.BreakLeft) + "s";
            else surge.text = "WAVE " + em.Wave + "      " + em.Remaining + " LEFT";
            surge.fontSize = 40;
            // Pink and pulsing once you're down to the stragglers.
            surge.color = em.Stragglers
                ? Color.Lerp(new Color(1f, 0.35f, 0.75f), Color.white, Mathf.PingPong(Time.unscaledTime * 3f, 1f) * 0.5f)
                : new Color(0.97f, 0.95f, 0.95f, 0.95f);
        }

        var cur = caster.Current;
        abilityName.text = cur.name;
        abilitySwatch.color = cur.color;
        float cd = caster.Cooldown01(caster.Selected);
        cooldownFill.rectTransform.sizeDelta = new Vector2(350f * (1f - cd), 14f);
        cooldownFill.color = cd > 0f ? new Color(cur.color.r, cur.color.g, cur.color.b, 0.5f) : cur.color;

        UpdateWheel(caster, dt);
    }

    void Start()
    {
        var em = EnemyManager.I;
        if (em == null) return;
        em.WaveStarted += w => Announce("WAVE " + w, w == 1 ? "Here they come" : "The blight grows stronger");
        em.WaveCleared += (w, bonus) => { Announce("WAVE " + w + " CLEARED", "+" + bonus + " flowers"); flowerPulse = 1f; };
    }

    void Announce(string big, string small)
    {
        announce.text = big;
        announceSub.text = small;
        announceT = Time.unscaledTime;
    }

    void UpdateAnnounce()
    {
        float a = Time.unscaledTime - announceT;
        float alpha = a < 0.25f ? a / 0.25f : a > 2.6f ? 1f - (a - 2.6f) / 0.5f : 1f;
        alpha = Mathf.Clamp01(alpha);
        announce.color = new Color(1f, 0.6f, 0.82f, alpha);
        announceSub.color = new Color(1f, 1f, 1f, alpha * 0.9f);
        announceRT.localScale = Vector3.one * (a < 0.35f ? LP.EaseOutBack(a / 0.35f) : 1f + (a - 0.35f) * 0.02f);
    }

    // Little flower icons fly from where an enemy died up into the counter.
    public void FlyFlowers(Vector3 world, int n)
    {
        var cam = GameCamera.I ? GameCamera.I.Cam : null;
        if (cam == null || flies.Count > 40) { pendingFlowers += n; return; }
        Vector2 screen = cam.WorldToScreenPoint(world);
        Vector2 local = screen / canvas.scaleFactor;
        int icons = Mathf.Clamp(n, 1, 3);
        for (int i = 0; i < icons; i++)
        {
            RectTransform rt = flyPool.Count > 0 ? flyPool.Pop() : null;
            if (rt == null)
            {
                var img = MakeImage((RectTransform)canvas.transform, Color.white, Vector2.zero, Vector2.zero, new Vector2(30, 30));
                img.sprite = UIKit.Flower;
                rt = img.rectTransform;
                rt.pivot = new Vector2(0.5f, 0.5f);
            }
            rt.gameObject.SetActive(true);
            Vector2 from = local + Random.insideUnitCircle * 20f;
            rt.anchoredPosition = from;
            int value = n / icons + (i < n % icons ? 1 : 0);
            flies.Add(new Fly { rt = rt, from = from, ctrl = from + new Vector2(Random.Range(-160f, 160f), Random.Range(80f, 220f)), dur = Random.Range(0.55f, 0.85f), value = value });
        }
    }

    void UpdateFlowers(float dt)
    {
        Vector2 target = FlowerTarget();
        for (int i = flies.Count - 1; i >= 0; i--)
        {
            var f = flies[i];
            f.t += dt / f.dur;
            float k = LP.EaseIn(f.t);
            Vector2 a = Vector2.Lerp(f.from, f.ctrl, k), b = Vector2.Lerp(f.ctrl, target, k);
            f.rt.anchoredPosition = Vector2.Lerp(a, b, k);
            f.rt.localScale = Vector3.one * (1f + Mathf.Sin(f.t * Mathf.PI) * 0.6f);
            f.rt.localRotation = Quaternion.Euler(0, 0, f.t * 360f);
            if (f.t >= 1f)
            {
                shownFlowers += f.value;
                flowerPulse = 1f;
                f.rt.gameObject.SetActive(false);
                flyPool.Push(f.rt);
                flies.RemoveAt(i);
            }
        }
        if (pendingFlowers > 0) { shownFlowers += pendingFlowers; pendingFlowers = 0; }
        // Never drift from the real balance (wave bonuses, purchases).
        int inFlight = 0; foreach (var f in flies) inFlight += f.value;
        int real = Save.Flowers - inFlight;
        if (shownFlowers < real) shownFlowers = Mathf.Min(real, shownFlowers + Mathf.Max(1, (real - shownFlowers) / 8));
        else if (shownFlowers > real) shownFlowers = real;
        flowerText.text = shownFlowers.ToString();
        flowerPulse = Mathf.MoveTowards(flowerPulse, 0f, dt * 4f);
        flowerIcon.localScale = Vector3.one * (1f + flowerPulse * 0.35f);
        flowerIcon.localRotation = Quaternion.Euler(0, 0, flowerPulse * 30f);
    }

    Vector2 FlowerTarget()
    {
        // Icon is anchored top-left; convert to bottom-left canvas coordinates used by the flyers.
        var size = ((RectTransform)canvas.transform).sizeDelta;
        return new Vector2(40f + 23f, size.y - 112f);
    }

    static string ComboWord(int n)
    {
        if (n >= 80) return "OVERGROWTH!!";
        if (n >= 50) return "FULL BLOOM!";
        if (n >= 30) return "WILDFLOWER!";
        if (n >= 15) return "BLOSSOMING";
        return "COMBO";
    }

    void UpdateWheel(AbilityCaster caster, float dt)
    {
        wheelAnim = Mathf.MoveTowards(wheelAnim, caster.WheelOpen ? 1f : 0f, dt * 7f);
        float s = caster.WheelOpen ? LP.EaseOutBack(wheelAnim) : LP.EaseOut(wheelAnim);
        wheelRoot.localScale = Vector3.one * s;
        dim.color = new Color(0.05f, 0.02f, 0.06f, 0.45f * wheelAnim);
        if (wheelAnim <= 0.001f) { caster.Hover = -1; caster.HoverCenter = false; return; }

        int page = caster.Page;
        int first = page * Abilities.PerPage;

        // Mouse relative to the wheel centre, in canvas units.
        Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(null, wheelRoot.position);
        Vector2 local = ((Vector2)Input.mousePosition - screenCenter) / canvas.scaleFactor;
        int hit = caster.WheelOpen ? wheel.HitTest(local) : -1;
        int hovered = hit >= 0 && first + hit < Abilities.All.Count ? first + hit : -1;
        caster.Hover = hovered;
        caster.HoverCenter = hit == -2;
        if (hovered != lastHover) { hoverAnim = 0f; if (hovered >= 0 || hit == -2) SFX.Play(SFX.S.UiHover, 1f, 0.1f); lastHover = hovered; }
        hoverAnim = Mathf.MoveTowards(hoverAnim, 1f, dt * 8f);

        wheel.count = Abilities.PerPage;
        wheel.hover = hit >= 0 ? hit : -1;
        wheel.hoverAnim = LP.EaseOutBack(hoverAnim);
        wheel.hoverCenter = hit == -2;
        wheel.selected = caster.Selected >= first && caster.Selected < first + Abilities.PerPage ? caster.Selected - first : -1;
        wheel.open = 1f;
        for (int i = 0; i < Abilities.PerPage; i++)
        {
            int idx = first + i;
            bool has = idx < Abilities.All.Count;
            wheel.colors[i] = has ? Abilities.All[idx].color : new Color(0.3f, 0.3f, 0.3f);
            wheel.cooldown[i] = has ? caster.Cooldown01(idx) : 0f;
            segLabels[i].text = has ? Abilities.All[idx].shortName.ToUpper() : "";
            float cdLeft = has ? caster.CooldownLeft(idx) : 0f;
            segKeys[i].text = cdLeft > 0f ? cdLeft.ToString("0.0") : (i + 1).ToString();
            segLabels[i].color = i == wheel.hover ? Color.white : new Color(1, 1, 1, 0.85f);
            segLabels[i].rectTransform.localScale = Vector3.one * (i == wheel.hover ? 1.12f : 1f);
        }
        wheel.Refresh();
        centerText.text = "NEXT\nPAGE\n" + (page + 1) + " / " + Abilities.Pages;
        centerText.color = caster.HoverCenter ? new Color(0.15f, 0.02f, 0.1f) : Color.white;

        int show = hovered >= 0 ? hovered : caster.Selected;
        var a = Abilities.All[show];
        descTitle.text = a.name;
        descTitle.color = Color.Lerp(a.color, Color.white, 0.25f);
        descStripe.color = a.color;
        descBody.text = a.desc;
        float left = caster.CooldownLeft(show);
        descMeta.text = (hovered >= 0 ? "Release to select" : "Selected") + "      Cooldown " + a.cooldown.ToString("0.#") + "s" + (left > 0f ? "  (" + left.ToString("0.0") + ")" : "");
        float slide = hovered >= 0 ? LP.EaseOut(hoverAnim) : 1f;
        descPanel.rectTransform.anchoredPosition = new Vector2(370f + (1f - slide) * 30f, 0f);
        descPanel.color = new Color(0.07f, 0.05f, 0.08f, 0.9f * slide);
    }
}
