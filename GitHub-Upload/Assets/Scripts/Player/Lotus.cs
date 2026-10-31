using System.Collections.Generic;
using UnityEngine;

// The player: a colossal pink lotus rooted on an island in the middle of a bramble pit.
// It turns toward the cursor, breathes, flares its petals when casting and keeps vines writhing around it.
public class Lotus : MonoBehaviour
{
    public static Lotus I;

    class Petal { public Transform t; public float tilt, yaw, phase; public int ring; }

    Transform head, core;
    readonly List<Petal> petals = new List<Petal>();
    readonly List<Vine> vines = new List<Vine>();
    readonly List<Vector3[]> vineRest = new List<Vector3[]>();
    Light glow;
    Renderer coreRenderer;

    float pulse, bloom, charge;
    Color pulseColor = Color.white, chargeColor = Color.yellow;
    float yaw, yawVel;

    public Vector3 CorePos => core ? core.position : new Vector3(0, 4f, 0);
    public Vector3 Aim { get; set; } = new Vector3(0, 0, 30);

    static readonly Color PinkDeep = new Color(0.93f, 0.27f, 0.58f);
    static readonly Color PinkMid = new Color(0.98f, 0.48f, 0.72f);
    static readonly Color PinkLight = new Color(1f, 0.78f, 0.9f);
    static readonly Color PinkPale = new Color(1f, 0.9f, 0.95f);

    // Overall size of the flower body (vines are placed in world space and scaled by hand).
    public const float S = 1.35f;
    Transform body;

    void Awake()
    {
        I = this;
        body = new GameObject("Body").transform;
        body.SetParent(transform, false);
        body.localScale = Vector3.one * S;
        BuildPads();
        head = new GameObject("Head").transform;
        head.SetParent(body, false);
        head.localPosition = new Vector3(0, 1.7f, 0);
        BuildStem();
        BuildPetals();
        BuildCore();
        BuildVines();
        BuildMenuVines();
    }

    // ------------------------------------------------------------- construction

    void BuildStem()
    {
        // The column is its own object so it can stretch when the lotus rises in the menu.
        var b = new MeshBuilder();
        Color g = new Color(0.22f, 0.5f, 0.24f);
        b.Cylinder(Vector3.zero, Vector3.up * 1.9f, 1.5f, 1.1f, 9, g, g * 0.9f);
        for (int i = 0; i < 4; i++)
        {
            // Ridges running up the stalk.
            Vector3 d = LP.Dir(i * 90f + 20f);
            b.Cylinder(d * 1.35f, d * 1.0f + Vector3.up * 1.9f, 0.28f, 0.2f, 4, g * 0.85f, null, false);
        }
        stemColumn = LP.Obj("Stem", b.ToMesh(), LP.Mat, body).transform;

        // Sepals: broad green leaves cupping the flower from below. They ride with the head.
        b = new MeshBuilder();
        for (int i = 0; i < 7; i++)
        {
            float a = i * 360f / 7 + 12f;
            Vector3 d = LP.Dir(a), s = Vector3.Cross(Vector3.up, d);
            Vector3 root = Vector3.up * -0.3f + d * 0.8f;
            Vector3 tip = root + d * 3.4f + Vector3.up * 0.9f;
            Vector3 mid = (root + tip) * 0.5f + Vector3.up * 0.35f;
            Color c = LP.Jitter(new Color(0.3f, 0.62f, 0.28f), 0.03f);
            b.TwoSided(root, mid + s * 0.9f, tip, c, c * 0.8f);
            b.TwoSided(root, tip, mid - s * 0.9f, c * 0.92f, c * 0.75f);
        }
        LP.Obj("Sepals", b.ToMesh(), LP.Mat, head);
    }

    Transform stemColumn;

    void BuildPads()
    {
        var b = new MeshBuilder();
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f + Random.Range(-10f, 10f);
            float r = Random.Range(3.7f, 4.5f);
            float size = Random.Range(1.9f, 2.4f);
            Vector3 c = LP.Dir(a) * r + Vector3.up * Random.Range(0.08f, 0.25f);
            Color col = LP.Jitter(new Color(0.24f, 0.56f, 0.26f), 0.04f);
            Color vein = col * 1.25f;
            int seg = 14;
            float notch = a + 180f;
            for (int k = 0; k < seg; k++)
            {
                float a0 = notch + 18f + k * (324f / seg), a1 = notch + 18f + (k + 1) * (324f / seg);
                Vector3 e0 = c + LP.Dir(a0) * size + Vector3.down * 0.15f * (1 + Mathf.Sin(k));
                Vector3 e1 = c + LP.Dir(a1) * size + Vector3.down * 0.15f * (1 + Mathf.Sin(k + 1));
                b.TwoSided(c + Vector3.up * 0.12f, e1, e0, k % 2 == 0 ? col : vein * 0.9f, col * 0.6f);
            }
        }
        LP.Obj("Lily Pads", b.ToMesh(), LP.Mat, body);
    }

    static Mesh PetalMesh(float len, float wid, float cup, float curl, Color baseCol, Color tipCol)
    {
        var b = new MeshBuilder();
        float[] w = { 0.28f, 0.72f, 0.95f, 1f, 0.86f, 0.55f, 0f };
        int rows = w.Length;
        Vector3[] L = new Vector3[rows], Mi = new Vector3[rows], R = new Vector3[rows];
        for (int i = 0; i < rows; i++)
        {
            float t = i / (float)(rows - 1);
            float y = t * len;
            float hw = w[i] * wid;
            float midZ = -curl * t * t;               // tip curls inward
            Mi[i] = new Vector3(0, y, midZ + 0.08f * Mathf.Sin(t * Mathf.PI));
            L[i] = new Vector3(-hw, y, midZ - cup * w[i]);
            R[i] = new Vector3(hw, y, midZ - cup * w[i]);
        }
        for (int i = 0; i < rows - 1; i++)
        {
            float t = (i + 0.5f) / (rows - 1);
            Color inner = Color.Lerp(baseCol, tipCol, t * t);
            Color outer = Color.Lerp(baseCol * 0.86f, tipCol * 0.9f, t);
            // Inner face points toward -Z (the flower centre); outer face +Z.
            AddQuadBoth(b, L[i], Mi[i], Mi[i + 1], L[i + 1], inner, outer);
            AddQuadBoth(b, Mi[i], R[i], R[i + 1], Mi[i + 1], Color.Lerp(inner, Color.white, 0.06f), outer * 0.95f);
        }
        return b.ToMesh();
    }

    static void AddQuadBoth(MeshBuilder b, Vector3 a, Vector3 c1, Vector3 c2, Vector3 d, Color inner, Color outer)
    {
        b.TriFacing(a, c1, c2, inner, Vector3.back);
        b.TriFacing(a, c2, d, inner, Vector3.back);
        b.TriFacing(a, c1, c2, outer, Vector3.forward);
        b.TriFacing(a, c2, d, outer, Vector3.forward);
    }

    void BuildPetals()
    {
        // ring: count, length, width, base tilt, radius offset, colours
        var rings = new (int n, float len, float wid, float tilt, float r, float cup, float curl, Color a, Color b)[]
        {
            (13, 5.6f, 1.55f, 66f, 1.3f, 0.55f, 0.4f, PinkDeep, PinkLight),
            (11, 4.8f, 1.4f, 46f, 1.1f, 0.5f, 0.6f, PinkMid, PinkLight),
            (9, 3.9f, 1.15f, 26f, 0.9f, 0.45f, 0.8f, PinkMid, PinkPale),
            (7, 2.9f, 0.85f, 10f, 0.65f, 0.4f, 0.9f, PinkLight, PinkPale),
        };
        for (int ri = 0; ri < rings.Length; ri++)
        {
            var R = rings[ri];
            Mesh[] variants = { PetalMesh(R.len, R.wid, R.cup, R.curl, R.a, R.b), PetalMesh(R.len * 0.93f, R.wid * 1.05f, R.cup, R.curl, Color.Lerp(R.a, R.b, 0.12f), R.b) };
            for (int i = 0; i < R.n; i++)
            {
                float yawA = i * 360f / R.n + ri * 17f;
                var go = LP.Obj("Petal", variants[i % 2], LP.Mat, head);
                go.transform.localPosition = LP.Dir(yawA) * R.r + Vector3.up * (0.1f + ri * 0.12f);
                var p = new Petal { t = go.transform, tilt = R.tilt + Random.Range(-4f, 4f), yaw = yawA, phase = Random.value * 6.28f, ring = ri };
                petals.Add(p);
            }
        }
    }

    void BuildCore()
    {
        var b = new MeshBuilder();
        Color pod = new Color(0.88f, 0.86f, 0.35f);
        b.Cylinder(Vector3.zero, Vector3.up * 1.1f, 0.85f, 1.15f, 10, new Color(0.6f, 0.75f, 0.3f), pod);
        // Glowing seed dots on the pod top.
        for (int i = 0; i < 9; i++)
        {
            Vector3 p = (i == 0 ? Vector3.zero : LP.Dir(i * 45f) * 0.62f) + Vector3.up * 1.12f;
            b.Ico(p, 0.13f, LP.Emissive(new Color(1f, 0.75f, 0.3f)), 0f, 0, 0f, new Vector3(1, 0.5f, 1));
        }
        // Stamens.
        for (int i = 0; i < 30; i++)
        {
            float a = i * 12f + Random.Range(-3f, 3f);
            Vector3 d = LP.Dir(a);
            Vector3 root = d * 1.05f + Vector3.up * 0.7f;
            Vector3 tip = root + (d * 0.55f + Vector3.up * 1.0f) * Random.Range(0.85f, 1.15f);
            b.Cylinder(root, tip, 0.05f, 0.04f, 3, new Color(1f, 0.85f, 0.45f), null, false);
            b.Ico(tip, 0.1f, LP.Emissive(new Color(1f, 0.9f, 0.4f)), 0f, 0);
        }
        var go = LP.Obj("Core", b.ToMesh(), LP.Mat, head);
        go.transform.localPosition = Vector3.up * 0.15f;
        core = go.transform;
        coreRenderer = go.GetComponent<Renderer>();

        glow = new GameObject("Core Glow").AddComponent<Light>();
        glow.transform.SetParent(head, false);
        glow.transform.localPosition = Vector3.up * 2.4f;
        glow.type = LightType.Point;
        glow.range = 12f;
        glow.intensity = 0.6f;
        glow.color = new Color(1f, 0.55f, 0.75f);
    }

    void BuildVines()
    {
        // Vines draping over the island edge into the pit, plus a few reaching out across it.
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f + Random.Range(-8f, 8f);
            Vector3 d = LP.Dir(a);
            bool reach = i % 4 == 1;
            var v = Vine.Create(transform, 16, Random.Range(0.32f, 0.48f), 0.08f);
            v.wiggle = reach ? 0.5f : 0.3f;
            v.wiggleSpeed = Random.Range(1.2f, 2.2f);
            v.thorns = true;
            Vector3 p0 = (d * 1.2f + Vector3.up * 0.6f) * S;
            Vector3 p1 = (d * 4.2f + Vector3.up * 1.6f) * S;
            Vector3[] rest;
            if (reach)
            {
                Vector3 side = Vector3.Cross(Vector3.up, d) * Random.Range(-2f, 2f);
                rest = new[] { p0, p1, d * (WorldBuilder.PitIn + 2f) + Vector3.up * 2.5f + side, d * (WorldBuilder.PitOut + Random.Range(1.5f, 3.5f)) + Vector3.up * 0.25f + side * 1.4f };
            }
            else
            {
                rest = new[] { p0, p1, d * (WorldBuilder.PitIn + 0.6f) + Vector3.up * 1.0f, d * (WorldBuilder.PitIn + Random.Range(1.2f, 2.8f)) + Vector3.down * Random.Range(2.5f, 4.5f) };
            }
            v.Set(rest[0], rest[1], rest[2], rest[3]);
            vines.Add(v);
            vineRest.Add(rest);
        }
        // Coils wrapping the stem.
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f;
            var v = Vine.Create(transform, 18, 0.26f, 0.06f);
            v.wiggle = 0.08f;
            v.leaves = true;
            Vector3[] rest = { LP.Dir(a) * 1.6f * S, (LP.Dir(a + 120f) * 2.2f + Vector3.up * 0.6f) * S, (LP.Dir(a + 240f) * 2.0f + Vector3.up * 1.2f) * S, (LP.Dir(a + 330f) * 2.4f + Vector3.up * 2.1f) * S };
            v.Set(rest[0], rest[1], rest[2], rest[3]);
            vines.Add(v);
            vineRest.Add(rest);
        }
    }

    // ------------------------------------------------------------- menu pose

    // In the menu the lotus rises up on a tall stalk and great vines whirl slowly around it.
    float rise = 1f, riseTarget = 1f, riseVel;
    readonly List<Vine> menuVines = new List<Vine>();
    readonly List<float> menuPhase = new List<float>();
    const float StemStretch = 3.2f;

    public void SetMenu(bool menu) => riseTarget = menu ? 1f : 0f;
    public float Rise => rise;

    void BuildMenuVines()
    {
        for (int i = 0; i < 7; i++)
        {
            var v = Vine.Create(transform, 22, Random.Range(0.8f, 1.1f), 0.18f);
            v.thorns = true;
            v.leaves = true;
            v.wiggle = 0.6f;
            v.wiggleSpeed = 0.8f;
            v.wiggleFreq = 1.2f;
            v.colBase = new Color(0.15f, 0.36f, 0.18f);
            v.colTip = new Color(0.45f, 0.75f, 0.3f);
            menuVines.Add(v);
            // Spread across the back half (yaw 180 = directly behind, away from the menu camera).
            menuPhase.Add(Mathf.Lerp(95f, 265f, i / 6f) + Random.Range(-8f, 8f));
        }
    }

    void UpdateMenuVines(float t)
    {
        float g = LP.Smooth(rise);
        for (int i = 0; i < menuVines.Count; i++)
        {
            var v = menuVines[i];
            bool on = g > 0.01f;
            if (v.gameObject.activeSelf != on) v.gameObject.SetActive(on);
            if (!on) continue;
            // Rooted behind and beside the flower (away from the menu camera), swaying in slow arcs.
            float a = menuPhase[i] + Mathf.Sin(t * 0.18f + i * 1.3f) * 35f;
            float bob = Mathf.Sin(t * 0.5f + i) * 1.5f;
            float h = 1f + i % 3 * 0.12f;
            Vector3 root = LP.Dir(menuPhase[i]) * 5.5f + Vector3.down * 1f;
            v.Set(root,
                  root + LP.Dir(a - 20f) * 3f + Vector3.up * 9f * h * g,
                  LP.Dir(a + 25f) * (12f + bob) + Vector3.up * (16f + bob) * h * g,
                  LP.Dir(a + 50f) * (9f - bob * 0.5f) + Vector3.up * (22f + bob * 1.5f) * h * g);
            v.grow = g;
        }
    }

    // ------------------------------------------------------------- reactions

    public void Pulse(Color c, float amount = 1f)
    {
        pulse = Mathf.Max(pulse, amount);
        pulseColor = c;
    }

    public void SetCharge(Color c, float amount)
    {
        charge = amount;
        chargeColor = c;
    }

    public void Bloom(float amount) => bloom = Mathf.Max(bloom, amount);

    // ------------------------------------------------------------- animation

    void Update()
    {
        float dt = Time.deltaTime;
        float t = Time.time;
        pulse = Mathf.MoveTowards(pulse, 0f, dt * 2.2f);
        bloom = Mathf.MoveTowards(bloom, 0f, dt * 0.6f);

        // Rise up tall for the menu, sink back into the pit for play.
        rise = Mathf.SmoothDamp(rise, riseTarget, ref riseVel, 0.7f);
        float rs = LP.Smooth(rise);
        stemColumn.localScale = new Vector3(1f, 1f + rs * StemStretch, 1f);
        head.localPosition = new Vector3(0f, 1.7f + rs * 1.9f * StemStretch, 0f);
        UpdateMenuVines(t);

        // Turn toward the aim point (clamped so the flower never shows its back).
        // In the menu it gazes down at the viewer instead.
        float targetYaw = Mathf.Clamp(LP.Yaw(LP.Flat(Aim)), -42f, 42f);
        if (rs > 0.5f && GameCamera.I) targetYaw = LP.Yaw(LP.Flat(GameCamera.I.transform.position)) + Mathf.Sin(t * 0.3f) * 12f;
        yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVel, 0.8f);
        float sway = Mathf.Sin(t * 0.8f) * (2.2f + rs * 3f);
        head.localRotation = Quaternion.Euler(7f + rs * 38f + Mathf.Sin(t * 0.6f) * (1.5f + rs * 2f) - pulse * 4f, yaw, sway);
        head.localScale = Vector3.one * (1f + pulse * 0.06f + bloom * 0.08f + Mathf.Sin(t * 1.3f) * 0.012f);

        float open = pulse * 14f + bloom * 26f + charge * 8f;
        foreach (var p in petals)
        {
            float breathe = Mathf.Sin(t * 1.3f + p.phase) * 2.2f;
            float ringOpen = open * (1f - p.ring * 0.15f);
            p.t.localRotation = Quaternion.Euler(0f, p.yaw, 0f) * Quaternion.Euler(p.tilt + breathe + ringOpen, 0f, 0f);
        }

        // Core glow reacts to casting.
        Color c = Color.Lerp(new Color(1f, 0.55f, 0.75f), pulseColor, pulse);
        c = Color.Lerp(c, chargeColor, charge);
        glow.color = c;
        glow.intensity = 0.55f + rs * 1.1f + Mathf.Sin(t * 2f) * 0.1f + pulse * 1.0f + charge * 1.6f + bloom * 1.2f;
        glow.range = 12f + rs * 10f;
        var mpb = LP.MPB;
        coreRenderer.GetPropertyBlock(mpb);
        mpb.SetColor("_Flash", c * (pulse * 0.35f + charge * 0.6f + bloom * 0.4f));
        coreRenderer.SetPropertyBlock(mpb);

        // Vines lean toward the aim, lash during casts.
        Vector3 aimDir = LP.Flat(Aim).normalized;
        for (int i = 0; i < vines.Count; i++)
        {
            var r = vineRest[i];
            var v = vines[i];
            float lean = Mathf.Max(0f, Vector3.Dot(LP.Flat(r[3]).normalized, aimDir));
            Vector3 lift = Vector3.up * (pulse * 1.6f * lean + bloom * 2f);
            if (i >= vines.Count - 3)
            {
                // Stem coils stretch with the stalk.
                float k = 1f + rs * StemStretch;
                v.Set(Scale(r[0], k), Scale(r[1], k), Scale(r[2], k), Scale(r[3], k));
                continue;
            }
            v.Set(r[0], r[1] + lift * 0.5f, r[2] + lift, r[3] + lift * 0.4f);
            v.wiggleSpeed = 1.5f + pulse * 6f + bloom * 6f;
        }
    }

    static Vector3 Scale(Vector3 p, float yk) => new Vector3(p.x, p.y * yk, p.z);
}
