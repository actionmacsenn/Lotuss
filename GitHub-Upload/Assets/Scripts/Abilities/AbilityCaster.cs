using UnityEngine;

// Player input: aiming, casting, cooldowns, the ground reticle and the hold-right-click ability wheel.
public class AbilityCaster : MonoBehaviour
{
    public static AbilityCaster I;

    public int Selected { get; private set; }
    public int Page { get; set; }
    public bool WheelOpen { get; private set; }
    public int Hover { get; set; } = -1;      // ability index under the cursor in the wheel, -1 none
    public bool HoverCenter { get; set; }
    public Vector3 AimPoint { get; private set; }

    readonly float[] readyAt = new float[64];

    Renderer reticleRing, reticleFill, lineRend;
    Transform reticle, line;

    // Demo/automation hook: overrides the mouse aim when set.
    public Vector3? ForcedAim;

    void Awake()
    {
        I = this;
        reticle = new GameObject("Reticle").transform;
        reticle.SetParent(transform, false);
        reticleRing = LP.Obj("Ring", LP.Ring, LP.Add, reticle, false).GetComponent<Renderer>();
        reticleFill = LP.Obj("Fill", LP.DecalDisc, LP.Glow, reticle, false).GetComponent<Renderer>();
        line = LP.Obj("Line", LP.BeamTube, LP.Add, transform, false).transform;
        lineRend = line.GetComponent<Renderer>();
        ShowReticle(false);
    }

    void OnEnable() => ShowReticle(true);
    void OnDisable() => ShowReticle(false);

    void ShowReticle(bool on)
    {
        if (reticle) reticle.gameObject.SetActive(on);
        if (line) line.gameObject.SetActive(on);
    }

    public AbilityDef Current => Abilities.All[Selected];
    public float CooldownLeft(int i) => Mathf.Max(0f, readyAt[i] - Time.time);
    public float Cooldown01(int i) => Abilities.All[i].Cooldown <= 0f ? 0f : Mathf.Clamp01(CooldownLeft(i) / Abilities.All[i].Cooldown);

    public void Select(int i)
    {
        if (i < 0 || i >= Abilities.All.Count) return;
        Selected = i;
        Page = i / Abilities.PerPage;
        SFX.Play(SFX.S.UiSelect, 1f, 0.02f);
        Lotus.I?.Pulse(Abilities.All[i].color, 0.4f);
    }

    public void NextPage()
    {
        Page = (Page + 1) % Abilities.Pages;
        SFX.Play(SFX.S.UiPage, 1f, 0.05f);
    }

    public bool TryCast(int i, Vector3 at)
    {
        if (CooldownLeft(i) > 0f) return false;
        var a = Abilities.All[i];
        readyAt[i] = Time.time + a.Cooldown;
        Lotus.I?.Pulse(a.color);
        SFX.Play(SFX.S.Cast, 1f, 0.04f);
        a.cast(at);
        return true;
    }

    public void OpenWheel(bool open)
    {
        if (open == WheelOpen) return;
        WheelOpen = open;
        Time.timeScale = open ? 0.15f : 1f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        if (open) Page = Selected / Abilities.PerPage;
        SFX.Play(open ? SFX.S.UiOpen : SFX.S.UiClose, 1f, 0.03f);
    }

    void Update()
    {
        var cam = GameCamera.I;
        if (cam != null && cam.MouseGround(out var gp)) AimPoint = gp;
        if (ForcedAim.HasValue) AimPoint = ForcedAim.Value;
        if (Lotus.I) Lotus.I.Aim = AimPoint;

        if (ForcedAim == null) HandleInput();
        UpdateReticle();
    }

    void HandleInput()
    {
        if (Input.GetMouseButtonDown(1)) OpenWheel(true);

        if (WheelOpen)
        {
            if (Input.GetMouseButtonDown(0) && HoverCenter) NextPage();
            if (Input.mouseScrollDelta.y != 0f) NextPage();
            if (Input.GetMouseButtonUp(1))
            {
                if (Hover >= 0) Select(Hover);
                OpenWheel(false);
            }
        }
        else
        {
            if (Input.GetMouseButton(0) && !TryCast(Selected, AimPoint) && Input.GetMouseButtonDown(0))
                SFX.Play(SFX.S.UiDenied, 1f, 0.03f);
        }

        if (Input.GetKeyDown(KeyCode.Tab)) NextPage();
        for (int k = 0; k < Abilities.PerPage; k++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + k)) Select(Page * Abilities.PerPage + k);
    }

    void UpdateReticle()
    {
        var a = Current;
        Color c = a.color;
        bool ready = CooldownLeft(Selected) <= 0f;
        float pulse = 0.75f + Mathf.Sin(Time.unscaledTime * 6f) * 0.25f;
        float alpha = (ready ? 1f : 0.35f) * (WheelOpen ? 0.3f : 1f);

        bool circle = a.shape == AimShape.Circle;
        reticle.gameObject.SetActive(circle);
        line.gameObject.SetActive(a.shape == AimShape.Line);
        if (circle)
        {
            Vector3 p = AimPoint; p.y = 0.07f;
            reticle.position = p;
            reticle.localScale = new Vector3(a.Radius, 1f, a.Radius);
            reticle.Rotate(0f, Time.unscaledDeltaTime * 30f, 0f);
            LP.SetColor(reticleRing, new Color(c.r, c.g, c.b, alpha * pulse));
            LP.SetColor(reticleFill, new Color(c.r, c.g, c.b, alpha * 0.25f));
        }
        else if (a.shape == AimShape.Line)
        {
            Vector3 d = LP.Flat(AimPoint); d = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
            Vector3 s = d * WorldBuilder.PitOut + Vector3.up * 0.12f;
            float len = a.key == "lash" ? 32f * (1f + 0.15f * Save.Level("lash.len")) - WorldBuilder.PitOut : 70f;
            line.position = s;
            line.rotation = Quaternion.LookRotation(d);
            line.localScale = new Vector3(0.18f, 0.02f, len);
            LP.SetColor(lineRend, new Color(c.r, c.g, c.b, alpha * pulse * 0.8f));
        }
    }
}
