using UnityEngine;

// Builds the static forest clearing: grassy field, bramble pit around the lotus, tree lines, tiny flowers.
public class WorldBuilder : MonoBehaviour
{
    public const float PitIn = 7.8f;      // edge of the lotus island
    public const float PitOut = 11.8f;    // outer lip of the pit
    public const float PitDepth = 5.5f;
    public const float FieldHalf = 25f;   // half-width of the open field
    public const float SpawnZ = 96f;

    public static readonly Color FogColor = new Color(0.66f, 0.82f, 0.84f);

    static readonly Color GrassA = new Color(0.46f, 0.66f, 0.3f);
    static readonly Color GrassB = new Color(0.38f, 0.57f, 0.27f);
    static readonly Color HillCol = new Color(0.29f, 0.52f, 0.22f);

    // Field is flat; hills rise under the tree lines on the sides and behind the spawn edge.
    public static float Height(float x, float z)
    {
        float side = Mathf.InverseLerp(FieldHalf + 1f, FieldHalf + 26f, Mathf.Abs(x));
        float back = Mathf.InverseLerp(SpawnZ + 4f, SpawnZ + 40f, z);
        float behind = Mathf.InverseLerp(-38f, -80f, z);
        float h = Mathf.Max(Mathf.Max(LP.Smooth(side), LP.Smooth(back)), LP.Smooth(behind));
        if (h <= 0f) return 0f;
        float n = Mathf.PerlinNoise(x * 0.05f + 13f, z * 0.05f + 7f);
        return h * (2f + n * 7f);
    }

    void Awake()
    {
        SetupLighting();
        var st = Random.state;
        Random.InitState(1337);
        BuildGround();
        BuildPit();
        BuildTrees();
        BuildFieldDecor();
        Random.state = st;
    }

    void SetupLighting()
    {
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.transform.SetParent(transform, false);
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
        sun.color = new Color(1f, 0.95f, 0.86f);
        sun.intensity = 0.95f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.55f;
        sun.shadowBias = 0.06f;
        sun.shadowNormalBias = 0.3f;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.52f, 0.58f, 0.7f);
        RenderSettings.ambientEquatorColor = new Color(0.44f, 0.48f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.28f, 0.26f, 0.24f);
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = FogColor;
        RenderSettings.fogStartDistance = 75f;
        RenderSettings.fogEndDistance = 185f;
    }

    void BuildGround()
    {
        var b = new MeshBuilder();
        float cell = 3f;
        float fill = PitOut + 4f;
        for (float x = -100f; x < 100f; x += cell)
        for (float z = -140f; z < 190f; z += cell)
        {
            Vector3 a = V(x, z), bb = V(x, z + cell), c = V(x + cell, z + cell), d = V(x + cell, z);
            // Skip quads fully inside the pit area; the island and rim rings cover it.
            float far = Mathf.Max(Mathf.Max(R(a), R(bb)), Mathf.Max(R(c), R(d)));
            if (far < fill) continue;
            b.Tri(a, bb, c, GroundColor(a, bb, c));
            b.Tri(a, c, d, GroundColor(a, c, d));
        }
        // Lotus island.
        int seg = 36;
        for (int i = 0; i < seg; i++)
        {
            Vector3 p0 = LP.Dir(i * 360f / seg), p1 = LP.Dir((i + 1) * 360f / seg);
            b.Tri(Vector3.zero, p0 * PitIn * 0.6f, p1 * PitIn * 0.6f, LP.Jitter(GrassA * 0.95f, 0.03f));
            b.Quad(p0 * PitIn * 0.6f, p0 * PitIn, p1 * PitIn, p1 * PitIn * 0.6f, LP.Jitter(GrassB, 0.03f));
        }
        // Outer lip ring (sits a hair above the grid to cover its jagged inner edge).
        int rseg = 48;
        for (int i = 0; i < rseg; i++)
        {
            Vector3 p0 = LP.Dir(i * 360f / rseg), p1 = LP.Dir((i + 1) * 360f / rseg);
            Vector3 lift = Vector3.up * 0.02f;
            b.Quad(p0 * PitOut + lift, p0 * (PitOut + 2.2f) + lift, p1 * (PitOut + 2.2f) + lift, p1 * PitOut + lift, LP.Jitter(GrassB * 0.92f, 0.03f));
            b.Quad(p0 * (PitOut + 2.2f) + lift, p0 * (fill + 1.5f) + lift, p1 * (fill + 1.5f) + lift, p1 * (PitOut + 2.2f) + lift, LP.Jitter(GrassA, 0.04f));
        }
        var go = LP.Obj("Ground", b.ToMesh(), LP.Mat, transform);
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Vector3 V(float x, float z) => new Vector3(x, Height(x, z), z);
    static float R(Vector3 p) => Mathf.Sqrt(p.x * p.x + p.z * p.z);

    static Color GroundColor(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 m = (a + b + c) / 3f;
        float n = Mathf.PerlinNoise(m.x * 0.07f, m.z * 0.07f);
        Color col = Color.Lerp(GrassA, GrassB, n);
        col = Color.Lerp(col, HillCol, Mathf.Clamp01(m.y / 5f));
        return LP.Jitter(col, 0.025f);
    }

    void BuildPit()
    {
        var b = new MeshBuilder();
        int seg = 56;
        Color top = new Color(0.36f, 0.25f, 0.17f), bot = new Color(0.1f, 0.06f, 0.07f);
        int rows = 4;
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * 360f / seg, a1 = (i + 1) * 360f / seg;
            Vector3 d0 = LP.Dir(a0), d1 = LP.Dir(a1);
            for (int r = 0; r < rows; r++)
            {
                float y0 = -PitDepth * r / rows, y1 = -PitDepth * (r + 1) / rows;
                Color c = LP.Jitter(Color.Lerp(top, bot, (r + 0.5f) / rows), 0.03f);
                float wob0 = Mathf.Sin(i * 1.7f + r) * 0.25f, wob1 = Mathf.Sin((i + 1) * 1.7f + r) * 0.25f;
                // Inner wall faces outward (away from the lotus), outer wall faces inward.
                b.TriFacing(d0 * (PitIn + wob0) + Vector3.up * y0, d1 * (PitIn + wob1) + Vector3.up * y0, d1 * (PitIn + wob1) + Vector3.up * y1, c, d0 + d1);
                b.TriFacing(d0 * (PitIn + wob0) + Vector3.up * y0, d1 * (PitIn + wob1) + Vector3.up * y1, d0 * (PitIn + wob0) + Vector3.up * y1, c, d0 + d1);
                b.TriFacing(d0 * (PitOut - wob0) + Vector3.up * y0, d1 * (PitOut - wob1) + Vector3.up * y0, d1 * (PitOut - wob1) + Vector3.up * y1, c, -(d0 + d1));
                b.TriFacing(d0 * (PitOut - wob0) + Vector3.up * y0, d1 * (PitOut - wob1) + Vector3.up * y1, d0 * (PitOut - wob0) + Vector3.up * y1, c, -(d0 + d1));
            }
            Vector3 fy = Vector3.down * PitDepth;
            b.TriFacing(d0 * PitIn + fy, d0 * PitOut + fy, d1 * PitOut + fy, bot, Vector3.up);
            b.TriFacing(d0 * PitIn + fy, d1 * PitOut + fy, d1 * PitIn + fy, bot, Vector3.up);
        }
        // Thorn spikes filling the pit.
        Color thorn = new Color(0.22f, 0.16f, 0.14f), tip = new Color(0.75f, 0.22f, 0.32f);
        for (int i = 0; i < 150; i++)
        {
            float ang = Random.value * 360f;
            float rr = Random.Range(PitIn + 0.4f, PitOut - 0.4f);
            Vector3 basePt = LP.Dir(ang) * rr + Vector3.down * (PitDepth - Random.Range(0f, 0.6f));
            Vector3 dir = (Vector3.up + Random.insideUnitSphere * 0.45f).normalized;
            float len = Random.Range(1.2f, 3.4f);
            b.Cylinder(basePt, basePt + dir * len * 0.75f, Random.Range(0.18f, 0.32f), 0.08f, 4, LP.Jitter(thorn, 0.04f), null, false);
            b.Cylinder(basePt + dir * len * 0.75f, basePt + dir * len, 0.08f, 0f, 4, tip, null, false);
        }
        // Wall thorns jutting sideways.
        for (int i = 0; i < 70; i++)
        {
            float ang = Random.value * 360f;
            bool inner = Random.value < 0.5f;
            Vector3 radial = LP.Dir(ang);
            Vector3 p = radial * (inner ? PitIn : PitOut) + Vector3.down * Random.Range(1f, PitDepth - 1f);
            Vector3 dir = ((inner ? radial : -radial) + Vector3.up * 0.4f + Random.insideUnitSphere * 0.3f).normalized;
            b.Cylinder(p, p + dir * Random.Range(0.8f, 1.6f), 0.14f, 0f, 4, LP.Jitter(thorn, 0.04f), null, false);
        }
        // Faint glowing bulbs deep in the pit.
        for (int i = 0; i < 26; i++)
        {
            Vector3 p = LP.Dir(Random.value * 360f) * Random.Range(PitIn + 0.6f, PitOut - 0.6f) + Vector3.down * (PitDepth - 0.3f);
            b.Ico(p, Random.Range(0.18f, 0.32f), LP.Emissive(new Color(1f, 0.45f, 0.7f)), 0.15f, 0);
        }
        // Rocks and roots along both lips.
        for (int i = 0; i < 64; i++)
        {
            float ang = i * 360f / 64 + Random.Range(-2f, 2f);
            bool inner = i % 2 == 0;
            Vector3 p = LP.Dir(ang) * (inner ? PitIn + 0.1f : PitOut - 0.1f);
            b.Ico(p, Random.Range(0.35f, 0.7f), LP.Jitter(new Color(0.48f, 0.44f, 0.4f), 0.05f), 0.25f, 0, 0f, new Vector3(1f, 0.6f, 1f));
        }
        LP.Obj("Bramble Pit", b.ToMesh(), LP.Mat, transform);
    }

    void BuildTrees()
    {
        var b = new MeshBuilder();
        int placed = 0, tries = 0;
        while (placed < 680 && tries < 9000)
        {
            tries++;
            float x = Random.Range(-95f, 95f);
            float z = Random.Range(-130f, 175f);
            bool sideZone = Mathf.Abs(x) > FieldHalf + 2f + Random.Range(0f, 5f);
            bool backZone = z > SpawnZ + 6f + Random.Range(0f, 4f);
            bool behindLotus = z < -36f - Random.Range(0f, 6f);   // seen from the menu camera
            if (!sideZone && !backZone && !behindLotus) continue;
            if (x * x + z * z < 30f * 30f && z < 0f) continue; // keep the area behind the camera clear
            Tree(b, new Vector3(x, Height(x, z), z), Random.Range(0.8f, 1.5f));
            placed++;
        }
        // A few sparse trees at the field edge break up the line.
        for (int i = 0; i < 26; i++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            float x = side * Random.Range(FieldHalf - 3f, FieldHalf + 3f);
            float z = Random.Range(18f, SpawnZ);
            Tree(b, new Vector3(x, Height(x, z), z), Random.Range(0.7f, 1.1f));
        }
        LP.Obj("Forest", b.ToMesh(), LP.Mat, transform);
    }

    static void Tree(MeshBuilder b, Vector3 p, float s)
    {
        Color trunk = LP.Jitter(new Color(0.42f, 0.29f, 0.2f), 0.04f);
        float kind = Random.value;
        if (kind < 0.45f)
        {
            // Pine: stacked cones.
            Color g = LP.Jitter(new Color(0.18f, 0.42f, 0.25f), 0.05f);
            b.Cylinder(p, p + Vector3.up * 2f * s, 0.35f * s, 0.25f * s, 5, trunk);
            for (int k = 0; k < 3; k++)
            {
                float y = (1.6f + k * 1.7f) * s;
                float r = (2.6f - k * 0.65f) * s;
                b.Cylinder(p + Vector3.up * y, p + Vector3.up * (y + 2.6f * s), r, 0f, 6, Color.Lerp(g, g * 1.2f, k * 0.3f), g * 0.8f);
            }
        }
        else if (kind < 0.92f)
        {
            // Round leafy tree.
            Color g = LP.Jitter(Random.value < 0.5f ? new Color(0.32f, 0.6f, 0.26f) : new Color(0.26f, 0.52f, 0.24f), 0.05f);
            b.Cylinder(p, p + Vector3.up * 2.6f * s, 0.4f * s, 0.28f * s, 5, trunk);
            b.Ico(p + Vector3.up * 4.2f * s, 2.3f * s, g, 0.18f, 0, 0.04f);
            b.Ico(p + new Vector3(1.2f, 3.4f, 0.4f) * s, 1.5f * s, g * 1.08f, 0.2f, 0, 0.04f);
            b.Ico(p + new Vector3(-1.1f, 3.6f, -0.5f) * s, 1.6f * s, g * 0.95f, 0.2f, 0, 0.04f);
        }
        else
        {
            // Pink blossom tree - echoes the lotus.
            Color g = LP.Jitter(new Color(0.96f, 0.6f, 0.78f), 0.04f);
            b.Cylinder(p, p + Vector3.up * 2.4f * s, 0.38f * s, 0.26f * s, 5, trunk);
            b.Ico(p + Vector3.up * 4f * s, 2.1f * s, g, 0.2f, 0, 0.04f);
            b.Ico(p + new Vector3(1.1f, 3.3f, 0.3f) * s, 1.3f * s, g * 1.03f, 0.2f, 0, 0.04f);
        }
    }

    void BuildFieldDecor()
    {
        var b = new MeshBuilder();
        Color[] petalCols =
        {
            new Color(1f, 1f, 0.96f), new Color(1f, 0.86f, 0.3f), new Color(1f, 0.6f, 0.78f),
            new Color(0.7f, 0.78f, 1f), new Color(0.95f, 0.5f, 0.6f), new Color(0.85f, 0.65f, 1f),
        };
        // Tiny flowers.
        for (int i = 0; i < 1100; i++)
        {
            Vector3 p = FieldPoint(); if (p.x > 900f) continue;
            float s = Random.Range(0.18f, 0.32f);
            Color pc = petalCols[Random.Range(0, petalCols.Length)];
            Vector3 top = p + Vector3.up * s * 1.6f;
            b.Cylinder(p, top, 0.03f, 0.03f, 3, new Color(0.3f, 0.55f, 0.2f), null, false);
            int n = 5; float off = Random.value * 360f;
            for (int k = 0; k < n; k++)
            {
                Vector3 d0 = LP.Dir(off + k * 72f - 22f) * s, d1 = LP.Dir(off + k * 72f + 22f) * s, dm = LP.Dir(off + k * 72f) * s * 1.6f;
                b.TwoSided(top, top + d0 + Vector3.up * 0.05f, top + dm + Vector3.up * 0.1f, pc);
                b.TwoSided(top, top + dm + Vector3.up * 0.1f, top + d1 + Vector3.up * 0.05f, pc);
            }
            b.Ico(top + Vector3.up * 0.06f, s * 0.35f, new Color(1f, 0.8f, 0.2f), 0f, 0);
        }
        // Grass tufts.
        for (int i = 0; i < 2600; i++)
        {
            Vector3 p = FieldPoint(); if (p.x > 900f) continue;
            Color g = LP.Jitter(Random.value < 0.5f ? new Color(0.5f, 0.8f, 0.32f) : new Color(0.32f, 0.6f, 0.24f), 0.04f);
            for (int k = 0; k < 3; k++)
            {
                Vector3 d = LP.Dir(Random.value * 360f);
                Vector3 side = Vector3.Cross(d, Vector3.up) * 0.08f;
                float h = Random.Range(0.35f, 0.75f);
                b.TwoSided(p - side, p + side, p + d * 0.25f + Vector3.up * h, g);
            }
        }
        // Mossy rocks.
        for (int i = 0; i < 60; i++)
        {
            Vector3 p = FieldPoint(); if (p.x > 900f) continue;
            float s = Random.Range(0.3f, 1.1f);
            b.Ico(p + Vector3.up * s * 0.2f, s, LP.Jitter(new Color(0.55f, 0.55f, 0.52f), 0.04f), 0.3f, 0, 0.03f, new Vector3(1.2f, 0.7f, 1f));
            b.Ico(p + Vector3.up * s * 0.55f, s * 0.7f, LP.Jitter(new Color(0.4f, 0.6f, 0.28f), 0.04f), 0.3f, 0, 0f, new Vector3(1.1f, 0.35f, 0.9f));
        }
        var go = LP.Obj("Field Decor", b.ToMesh(), LP.Mat, transform);
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Vector3 FieldPoint()
    {
        float x = Random.Range(-FieldHalf - 6f, FieldHalf + 6f);
        float z = Random.Range(-20f, SpawnZ + 10f);
        float r = Mathf.Sqrt(x * x + z * z);
        if (r > PitIn - 1.5f && r < PitOut + 0.8f) return new Vector3(999f, 0, 0);
        if (r < 6.5f) return new Vector3(999f, 0, 0); // under the lotus
        return new Vector3(x, Height(x, z), z);
    }
}
