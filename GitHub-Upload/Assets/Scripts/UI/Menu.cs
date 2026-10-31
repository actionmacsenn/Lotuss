using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Start menu: title + Play / Upgrades / Settings / Quit, an upgrades shop paid in flowers, and volume sliders.
public class Menu : MonoBehaviour
{
    public static Menu I;

    Canvas canvas;
    CanvasGroup group;
    RectTransform main, upgrades, settings;
    Text balanceText, bestText, shopBalance;
    int selectedAbility;
    float fade = 1f;

    // Upgrades panel widgets.
    readonly Button[] abilityButtons = new Button[12];
    readonly Text[] abilityLevels = new Text[12];
    Text detailName, detailDesc;
    Image detailStripe;
    readonly Text[] rowName = new Text[3], rowDesc = new Text[3], rowCost = new Text[3];
    readonly Image[][] rowPips = new Image[3][];
    readonly Button[] rowBuy = new Button[3];
    readonly Image[] rowIcon = new Image[3];

    void Awake()
    {
        I = this;
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var go = new GameObject("Menu Canvas");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        group = go.AddComponent<CanvasGroup>();

        BuildMain(go.transform);
        BuildUpgrades(go.transform);
        BuildSettings(go.transform);
        Show(main);
    }

    // ------------------------------------------------------------------ main

    void BuildMain(Transform root)
    {
        main = UIKit.Rect(root, "Main", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIKit.Stretch(main);

        // Soft dark gradient on the left so text reads over the scene.
        var shade = UIKit.Image(main, new Color(0.05f, 0.02f, 0.06f, 0.6f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(1000, 2000));
        shade.rectTransform.pivot = new Vector2(0, 0.5f);
        shade.sprite = UIKit.FadeRight;

        var title = UIKit.Text(main, "LOTUS", 150, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(110, -250), new Vector2(800, 190), UIKit.Pink);
        title.rectTransform.pivot = new Vector2(0, 0.5f);
        var outline = title.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.05f, 0.2f, 0.8f);
        outline.effectDistance = new Vector2(4, -4);
        var sub = UIKit.Text(main, "bloom against the blight", 34, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(118, -345), new Vector2(700, 50), new Color(1f, 0.9f, 0.95f, 0.85f), false);
        sub.rectTransform.pivot = new Vector2(0, 0.5f);

        string[] labels = { "PLAY", "UPGRADES", "SETTINGS", "QUIT" };
        System.Action[] acts =
        {
            () => { Save.Flush(); Game.I.StartGame(); },
            () => { RefreshShop(); Show(upgrades); },
            () => Show(settings),
            () => { Save.Flush(); Application.Quit(); },
        };
        for (int i = 0; i < labels.Length; i++)
        {
            Color c = i == 0 ? UIKit.PinkDeep : new Color(0.16f, 0.12f, 0.18f, 0.92f);
            var b = UIKit.Button(main, labels[i], new Vector2(0, 1), new Vector2(110, -470 - i * 100), new Vector2(420, 80), c, acts[i], i == 0 ? 42 : 34);
            ((RectTransform)b.transform).pivot = new Vector2(0, 0.5f);
        }

        var icon = UIKit.Image(main, Color.white, new Vector2(1, 1), new Vector2(-300, -70), new Vector2(56, 56));
        icon.sprite = UIKit.Flower;
        balanceText = UIKit.Text(main, "0", 44, TextAnchor.MiddleLeft, new Vector2(1, 1), new Vector2(-265, -70), new Vector2(240, 60), Color.white);
        balanceText.rectTransform.pivot = new Vector2(0, 0.5f);

        bestText = UIKit.Text(main, "", 26, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(112, 40), new Vector2(700, 40), new Color(1, 1, 1, 0.7f), false);
        bestText.rectTransform.pivot = new Vector2(0, 0);
    }

    // ------------------------------------------------------------------ upgrades

    void BuildUpgrades(Transform root)
    {
        upgrades = UIKit.Rect(root, "Upgrades", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 900));
        var bg = UIKit.Image(upgrades, UIKit.Panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 900), true);
        bg.raycastTarget = true;

        var head = UIKit.Text(upgrades, "UPGRADES", 60, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(50, -60), new Vector2(600, 80), UIKit.Pink);
        head.rectTransform.pivot = new Vector2(0, 0.5f);
        var icon = UIKit.Image(upgrades, Color.white, new Vector2(1, 1), new Vector2(-290, -60), new Vector2(50, 50));
        icon.sprite = UIKit.Flower;
        shopBalance = UIKit.Text(upgrades, "0", 42, TextAnchor.MiddleLeft, new Vector2(1, 1), new Vector2(-255, -60), new Vector2(240, 60), Color.white);
        shopBalance.rectTransform.pivot = new Vector2(0, 0.5f);

        // Ability list: two columns of six.
        for (int i = 0; i < Abilities.All.Count && i < 12; i++)
        {
            int idx = i;
            var a = Abilities.All[i];
            float x = 50 + (i / 6) * 250;
            float y = -150 - (i % 6) * 112;
            var b = UIKit.Button(upgrades, "", new Vector2(0, 1), new Vector2(x, y), new Vector2(236, 98), new Color(0.15f, 0.11f, 0.17f, 1f), () => { selectedAbility = idx; RefreshShop(); }, 24);
            var rt = (RectTransform)b.transform;
            rt.pivot = new Vector2(0, 1);
            var lbl = b.GetComponentInChildren<Text>();
            lbl.text = a.name;
            lbl.alignment = TextAnchor.UpperLeft;
            lbl.rectTransform.offsetMin = new Vector2(20, 8); lbl.rectTransform.offsetMax = new Vector2(-8, -10);
            var stripe = UIKit.Image(rt, a.color, new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(10, 98));
            stripe.rectTransform.pivot = new Vector2(0, 0.5f);
            abilityLevels[i] = UIKit.Text(rt, "", 20, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(20, 8), new Vector2(200, 30), new Color(1, 1, 1, 0.65f), false);
            abilityLevels[i].rectTransform.pivot = new Vector2(0, 0);
            abilityButtons[i] = b;
        }

        // Detail area.
        float dx = 580;
        detailStripe = UIKit.Image(upgrades, Color.white, new Vector2(0, 1), new Vector2(dx, -150), new Vector2(10, 120));
        detailStripe.rectTransform.pivot = new Vector2(0, 1);
        detailName = UIKit.Text(upgrades, "", 46, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(dx + 30, -145), new Vector2(950, 60), Color.white);
        detailName.rectTransform.pivot = new Vector2(0, 1);
        detailDesc = UIKit.Text(upgrades, "", 24, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(dx + 30, -205), new Vector2(950, 80), new Color(1, 1, 1, 0.8f), false);
        detailDesc.rectTransform.pivot = new Vector2(0, 1);

        for (int r = 0; r < 3; r++)
        {
            int row = r;
            var card = UIKit.Image(upgrades, new Color(0.13f, 0.1f, 0.15f, 1f), new Vector2(0, 1), new Vector2(dx, -310 - r * 175), new Vector2(970, 155), true);
            card.rectTransform.pivot = new Vector2(0, 1);
            var ct = card.rectTransform;
            rowName[r] = UIKit.Text(ct, "", 32, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(26, -16), new Vector2(560, 44), Color.white);
            rowName[r].rectTransform.pivot = new Vector2(0, 1);
            rowDesc[r] = UIKit.Text(ct, "", 22, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(26, -62), new Vector2(600, 60), new Color(1, 1, 1, 0.75f), false);
            rowDesc[r].rectTransform.pivot = new Vector2(0, 1);
            rowPips[r] = new Image[5];
            for (int p = 0; p < 5; p++)
            {
                rowPips[r][p] = UIKit.Image(ct, Color.white, new Vector2(0, 0), new Vector2(26 + p * 40, 18), new Vector2(32, 14), true);
                rowPips[r][p].rectTransform.pivot = new Vector2(0, 0);
            }
            rowBuy[r] = UIKit.Button(ct, "", new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(250, 96), UIKit.PinkDeep, () => Buy(row), 30);
            ((RectTransform)rowBuy[r].transform).pivot = new Vector2(1, 0.5f);
            var bl = rowBuy[r].GetComponentInChildren<Text>();
            bl.alignment = TextAnchor.MiddleCenter;
            rowCost[r] = bl;
            rowIcon[r] = UIKit.Image(rowBuy[r].transform, Color.white, new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(40, 40));
            rowIcon[r].rectTransform.pivot = new Vector2(0, 0.5f);
            rowIcon[r].sprite = UIKit.Flower;
        }

        var back = UIKit.Button(upgrades, "BACK", new Vector2(1, 0), new Vector2(-50, 40), new Vector2(240, 70), new Color(0.2f, 0.15f, 0.22f, 1f), () => Show(main));
        ((RectTransform)back.transform).pivot = new Vector2(1, 0);
    }

    void RefreshShop()
    {
        shopBalance.text = Save.Flowers.ToString();
        for (int i = 0; i < 12 && i < Abilities.All.Count; i++)
        {
            var a = Abilities.All[i];
            int lv = 0, max = 0;
            foreach (var u in a.upgrades) { lv += u.Level; max += u.max; }
            abilityLevels[i].text = "Lv " + lv + " / " + max;
            var img = abilityButtons[i].GetComponent<Image>();
            var cb = abilityButtons[i].colors;
            cb.normalColor = i == selectedAbility ? Color.Lerp(new Color(0.15f, 0.11f, 0.17f), a.color, 0.35f) : new Color(0.15f, 0.11f, 0.17f, 1f);
            abilityButtons[i].colors = cb;
        }
        var sel = Abilities.All[selectedAbility];
        detailName.text = sel.name;
        detailName.color = Color.Lerp(sel.color, Color.white, 0.2f);
        detailStripe.color = sel.color;
        detailDesc.text = sel.desc;
        for (int r = 0; r < 3; r++)
        {
            var u = sel.upgrades[r];
            rowName[r].text = u.name;
            rowDesc[r].text = u.desc;
            for (int p = 0; p < 5; p++)
            {
                rowPips[r][p].gameObject.SetActive(p < u.max);
                rowPips[r][p].color = p < u.Level ? sel.color : new Color(1, 1, 1, 0.15f);
            }
            bool afford = Save.Flowers >= u.NextCost;
            rowBuy[r].interactable = !u.Maxed && afford;
            rowCost[r].text = u.Maxed ? "MAXED" : "    " + u.NextCost;
            rowIcon[r].gameObject.SetActive(!u.Maxed);
            rowCost[r].color = u.Maxed ? new Color(1, 1, 1, 0.6f) : afford ? Color.white : new Color(1f, 0.6f, 0.6f);
        }
    }

    void Buy(int row)
    {
        var u = Abilities.All[selectedAbility].upgrades[row];
        if (u.Maxed || Save.Flowers < u.NextCost) { SFX.Play(SFX.S.UiDenied); return; }
        Save.Flowers -= u.NextCost;
        Save.SetLevel(u.key, u.Level + 1);
        Save.Flush();
        SFX.Play(SFX.S.Bloom, 1f, 0f);
        SFX.Play(SFX.S.Combo, 0.6f, 0f);
        Lotus.I?.Pulse(Abilities.All[selectedAbility].color, 1f);
        RefreshShop();
    }

    // ------------------------------------------------------------------ settings

    void BuildSettings(Transform root)
    {
        settings = UIKit.Rect(root, "Settings", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 640));
        var bg = UIKit.Image(settings, UIKit.Panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 640), true);
        bg.raycastTarget = true;
        var head = UIKit.Text(settings, "SETTINGS", 60, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(50, -60), new Vector2(600, 80), UIKit.Pink);
        head.rectTransform.pivot = new Vector2(0, 0.5f);

        string[] names = { "Master", "Music", "Effects", "Ambience" };
        string[] keys = { "master", "music", "effects", "ambience" };
        float[] vals = { SFX.Master, SFX.MusicVol, SFX.EffectsVol, SFX.AmbienceVol };
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            float y = -170 - i * 100;
            var l = UIKit.Text(settings, names[i], 34, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(60, y), new Vector2(240, 50), Color.white);
            l.rectTransform.pivot = new Vector2(0, 0.5f);
            var pct = UIKit.Text(settings, Mathf.RoundToInt(vals[i] * 100) + "%", 30, TextAnchor.MiddleRight, new Vector2(1, 1), new Vector2(-60, y), new Vector2(120, 50), new Color(1, 1, 1, 0.8f));
            pct.rectTransform.pivot = new Vector2(1, 0.5f);
            var s = UIKit.Slider(settings, new Vector2(0, 1), new Vector2(300, y), new Vector2(400, 36), vals[i], UIKit.Pink, v =>
            {
                pct.text = Mathf.RoundToInt(v * 100) + "%";
                Save.SetVolume(keys[idx], v);
                switch (idx)
                {
                    case 0: SFX.Master = v; break;
                    case 1: SFX.MusicVol = v; break;
                    case 2: SFX.EffectsVol = v; SFX.Play(SFX.S.UiHover, 1f, 0f); break;
                    default: SFX.AmbienceVol = v; break;
                }
            });
            ((RectTransform)s.transform).pivot = new Vector2(0, 0.5f);
        }
        var back = UIKit.Button(settings, "BACK", new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(240, 70), new Color(0.2f, 0.15f, 0.22f, 1f), () => { Save.Flush(); Show(main); });
        ((RectTransform)back.transform).pivot = new Vector2(0.5f, 0);
    }

    // ------------------------------------------------------------------ flow

    public void Show(RectTransform panel)
    {
        main.gameObject.SetActive(panel == main);
        upgrades.gameObject.SetActive(panel == upgrades);
        settings.gameObject.SetActive(panel == settings);
    }

    public void OpenUpgrades() { RefreshShop(); Show(upgrades); }
    public void OpenSettings() => Show(settings);

    void Update()
    {
        bool menu = Game.I != null && Game.I.State == Game.Mode.Menu;
        fade = Mathf.MoveTowards(fade, menu ? 1f : 0f, Time.unscaledDeltaTime * 2.5f);
        group.alpha = fade;
        group.interactable = group.blocksRaycasts = menu;
        if (canvas.enabled != fade > 0.001f) canvas.enabled = fade > 0.001f;
        if (!menu) return;

        balanceText.text = Save.Flowers.ToString();
        bestText.text = Save.BestWave > 0 ? "Best wave reached:  " + Save.BestWave : "Hold back the blight. Earn flowers. Grow stronger.";
        if (upgrades.gameObject.activeSelf) shopBalance.text = Save.Flowers.ToString();

        if (Input.GetKeyDown(KeyCode.Escape) && !main.gameObject.activeSelf) { Save.Flush(); Show(main); }
    }
}
