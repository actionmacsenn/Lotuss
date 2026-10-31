using System.Collections.Generic;
using UnityEngine;

// A writhing low-poly vine: a tapered tube rebuilt every frame along a cubic bezier.
public class Vine : MonoBehaviour
{
    public Vector3 p0, p1, p2, p3;
    public float grow = 1f;                 // 0..1 portion of the curve that exists
    public float baseRadius = 0.35f, tipRadius = 0.07f;
    public float wiggle = 0.25f, wiggleSpeed = 3f, wiggleFreq = 2.2f;
    public Color colBase = new Color(0.18f, 0.42f, 0.2f), colTip = new Color(0.45f, 0.78f, 0.3f);
    public int segments = 14, sides = 5;
    public bool leaves = true, thorns = false;
    public float alpha = 1f;

    readonly MeshBuilder b = new MeshBuilder();
    Mesh mesh;
    float seed;
    readonly List<Vector3> pts = new List<Vector3>();

    public static Vine Create(Transform parent, int segments = 14, float baseRadius = 0.35f, float tipRadius = 0.07f)
    {
        var go = new GameObject("Vine");
        if (parent) go.transform.SetParent(parent, false);
        var v = go.AddComponent<Vine>();
        v.segments = segments;
        v.baseRadius = baseRadius;
        v.tipRadius = tipRadius;
        return v;
    }

    void Awake()
    {
        mesh = new Mesh { name = "Vine" };
        mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = gameObject.AddComponent<MeshRenderer>();
        r.sharedMaterial = LP.Mat;
        seed = Random.value * 100f;
    }

    public void Set(Vector3 a, Vector3 b1, Vector3 c, Vector3 d) { p0 = a; p1 = b1; p2 = c; p3 = d; }

    public Vector3 Point(float t) => LP.Bezier(p0, p1, p2, p3, t);

    // World-space tip of the grown portion.
    public Vector3 Tip => Point(Mathf.Clamp01(grow));

    // Approximate distance from a point to the grown vine (XZ only).
    public float FlatDistance(Vector3 p, out Vector3 closest)
    {
        float best = float.MaxValue; closest = p0;
        int n = 16;
        Vector3 prev = Point(0f);
        for (int i = 1; i <= n; i++)
        {
            Vector3 cur = Point(grow * i / n);
            Vector3 a = LP.Flat(prev), bb = LP.Flat(cur), q = LP.Flat(p);
            Vector3 ab = bb - a;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
            Vector3 c = a + ab * t;
            float d = (q - c).sqrMagnitude;
            if (d < best) { best = d; closest = Vector3.Lerp(prev, cur, t); }
            prev = cur;
        }
        return Mathf.Sqrt(best);
    }

    void LateUpdate()
    {
        // Control points are world-space: cancel out whatever the parent transform does.
        if (transform.parent)
        {
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Vector3 s = transform.parent.lossyScale;
            transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, s.x), 1f / Mathf.Max(0.0001f, s.y), 1f / Mathf.Max(0.0001f, s.z));
        }
        Rebuild();
    }

    public void Rebuild()
    {
        b.Clear();
        float g = Mathf.Clamp01(grow);
        if (g <= 0.01f) { mesh.Clear(); return; }

        pts.Clear();
        float time = Time.time * wiggleSpeed + seed;
        for (int i = 0; i <= segments; i++)
        {
            float t = g * i / segments;
            Vector3 p = Point(t);
            // Wiggle grows toward the tip.
            float w = wiggle * Mathf.Sin(t * Mathf.PI);
            p += new Vector3(Mathf.Sin(time + t * wiggleFreq * 6f), Mathf.Sin(time * 0.7f + t * 5f) * 0.5f, Mathf.Cos(time * 1.1f + t * wiggleFreq * 5f)) * w;
            pts.Add(p);
        }

        Vector3 prevSide = Vector3.right;
        Vector3[] ringPrev = new Vector3[sides], ringCur = new Vector3[sides];
        Color dark = new Color(0.08f, 0.2f, 0.1f);
        for (int i = 0; i <= segments; i++)
        {
            Vector3 fwd = (i < segments ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.up;
            fwd.Normalize();
            Vector3 side = Vector3.Cross(fwd, Mathf.Abs(fwd.y) > 0.95f ? Vector3.forward : Vector3.up).normalized;
            if (Vector3.Dot(side, prevSide) < 0) side = -side;
            prevSide = side;
            Vector3 up = Vector3.Cross(side, fwd);
            float u = (float)i / segments;
            float rad = Mathf.Lerp(baseRadius, tipRadius, u) * (i == segments ? 0.3f : 1f);
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.PI * 2f / sides + u * 2.5f;
                ringCur[s] = pts[i] + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * rad;
            }
            if (i > 0)
            {
                Color c = Color.Lerp(colBase, colTip, u);
                for (int s = 0; s < sides; s++)
                {
                    int s1 = (s + 1) % sides;
                    Color cc = (s % 2 == 0) ? c : Color.Lerp(c, dark, 0.18f);
                    Vector3 center = (pts[i] + pts[i - 1]) * 0.5f;
                    b.TriOut(ringPrev[s], ringCur[s], ringCur[s1], cc, center);
                    b.TriOut(ringPrev[s], ringCur[s1], ringPrev[s1], cc, center);
                }
                if (leaves && i % 3 == 1 && i < segments - 1)
                {
                    Vector3 lside = (i % 6 == 1 ? side : -side);
                    Vector3 lb = pts[i];
                    Vector3 tip = lb + (lside * 1.3f + up * 0.6f + fwd * 0.4f) * (rad * 2.2f + 0.25f);
                    Vector3 w = Vector3.Cross(tip - lb, up).normalized * (rad + 0.12f);
                    Color lc = new Color(0.4f, 0.78f, 0.3f);
                    b.TwoSided(lb, (lb + tip) * 0.5f + w, tip, lc, lc * 0.8f);
                    b.TwoSided(lb, tip, (lb + tip) * 0.5f - w, lc * 0.9f, lc * 0.75f);
                }
                if (thorns && i % 2 == 0 && i < segments)
                {
                    Vector3 dir = (side * Mathf.Cos(i) + up * Mathf.Sin(i)).normalized;
                    Vector3 tb = pts[i] + dir * rad;
                    b.Cylinder(tb, tb + (dir + fwd * 0.3f) * (rad * 0.9f + 0.1f), rad * 0.35f + 0.03f, 0f, 3, new Color(0.55f, 0.2f, 0.25f), null, false);
                }
            }
            var tmp = ringPrev; ringPrev = ringCur; ringCur = tmp;
        }
        b.ToMesh(mesh);
    }
}
