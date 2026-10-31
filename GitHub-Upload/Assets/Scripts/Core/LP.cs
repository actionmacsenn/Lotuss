using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// "Low Poly" toolkit: materials, colour helpers and a flat-shaded mesh builder.
public static class LP
{
    static Material _mat, _glow, _add;
    static MaterialPropertyBlock _mpb;

    public static Material Mat
    {
        get
        {
            if (_mat == null)
            {
                _mat = new Material(Shader.Find("Lotus/LowPoly")) { name = "LowPoly" };
            }
            return _mat;
        }
    }

    static Material _particle;
    public static Material Particle
    {
        get
        {
            if (_particle == null) _particle = new Material(Shader.Find("Lotus/Particle")) { name = "Particle" };
            return _particle;
        }
    }

    // Alpha-blended unlit material (decals, clouds).
    public static Material Glow
    {
        get
        {
            if (_glow == null)
            {
                _glow = new Material(Shader.Find("Lotus/Glow")) { name = "Glow" };
                _glow.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                _glow.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }
            return _glow;
        }
    }

    // Additive unlit material (beams, shockwaves).
    public static Material Add
    {
        get
        {
            if (_add == null)
            {
                _add = new Material(Shader.Find("Lotus/Glow")) { name = "Additive" };
                _add.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                _add.SetFloat("_DstBlend", (float)BlendMode.One);
                _add.renderQueue = 3100;
            }
            return _add;
        }
    }

    public static MaterialPropertyBlock MPB => _mpb ??= new MaterialPropertyBlock();

    public static void SetColor(Renderer r, Color c)
    {
        var b = MPB;
        r.GetPropertyBlock(b);
        b.SetColor("_Color", c);
        r.SetPropertyBlock(b);
    }

    public static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }

    public static Color Jitter(Color c, float amount)
    {
        float j = Random.Range(-amount, amount);
        return new Color(Mathf.Clamp01(c.r + j + Random.Range(-amount, amount) * 0.3f),
                         Mathf.Clamp01(c.g + j + Random.Range(-amount, amount) * 0.3f),
                         Mathf.Clamp01(c.b + j + Random.Range(-amount, amount) * 0.3f), c.a);
    }

    // Alpha 0 = glows in the LowPoly shader.
    public static Color Emissive(Color c) { c.a = 0f; return c; }

    public static GameObject Obj(string name, Mesh mesh, Material mat, Transform parent = null, bool shadows = true)
    {
        var go = new GameObject(name);
        if (parent) go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        r.receiveShadows = shadows;
        return go;
    }

    public static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        t = Mathf.Clamp01(t);
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    public static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
    public static float EaseIn(float t) { t = Mathf.Clamp01(t); return t * t; }
    public static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    public static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    public static Vector3 Dir(float yawDeg) { float a = yawDeg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)); }
    public static float Yaw(Vector3 v) { return Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg; }

    public static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    // ---------------------------------------------------------------- shared meshes

    static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

    public static Mesh Cached(string key, System.Func<Mesh> make)
    {
        if (!cache.TryGetValue(key, out var m) || m == null) { m = make(); m.name = key; cache[key] = m; }
        return m;
    }

    // Flat disc on XZ, radius 1, alpha fading from centre to a bright rim.
    public static Mesh DecalDisc => Cached("decal", () =>
    {
        var b = new MeshBuilder();
        int seg = 40;
        var cIn = new Color(1, 1, 1, 0.12f); var cMid = new Color(1, 1, 1, 0.3f); var cRim = new Color(1, 1, 1, 0.85f);
        for (int i = 0; i < seg; i++)
        {
            Vector3 a = Dir(i * 360f / seg), c = Dir((i + 1) * 360f / seg);
            b.TriColors(Vector3.zero, a * 0.85f, c * 0.85f, cIn, cMid, cMid);
            b.TriColors(a * 0.85f, a, c, cMid, cRim, cRim);
            b.TriColors(a * 0.85f, c, c * 0.85f, cMid, cRim, cMid);
        }
        return b.ToMesh();
    });

    // Thin ring radius 1 for reticles and shockwaves.
    public static Mesh Ring => Cached("ring", () =>
    {
        var b = new MeshBuilder();
        int seg = 48;
        var o = new Color(1, 1, 1, 0f); var w = Color.white;
        for (int i = 0; i < seg; i++)
        {
            Vector3 a = Dir(i * 360f / seg), c = Dir((i + 1) * 360f / seg);
            b.TriColors(a * 0.86f, a * 0.95f, c * 0.95f, o, w, w);
            b.TriColors(a * 0.86f, c * 0.95f, c * 0.86f, o, w, o);
            b.TriColors(a * 0.95f, a, c, w, o, o);
            b.TriColors(a * 0.95f, c, c * 0.95f, w, o, w);
        }
        return b.ToMesh();
    });

    // Open cylinder wall radius 1 height 1, fading to transparent at the top.
    public static Mesh Wall => Cached("wall", () =>
    {
        var b = new MeshBuilder();
        int seg = 48;
        var top = new Color(1, 1, 1, 0f); var bot = Color.white;
        for (int i = 0; i < seg; i++)
        {
            Vector3 a = Dir(i * 360f / seg), c = Dir((i + 1) * 360f / seg);
            b.TriColors(a, a + Vector3.up, c + Vector3.up, bot, top, top);
            b.TriColors(a, c + Vector3.up, c, bot, top, bot);
        }
        return b.ToMesh();
    });

    // Cylinder along +Z from 0 to 1, radius 1 (beams).
    public static Mesh BeamTube => Cached("beam", () =>
    {
        var b = new MeshBuilder();
        int seg = 8;
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
            Vector3 p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0), p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0);
            b.Quad(p0, p0 + Vector3.forward, p1 + Vector3.forward, p1, Color.white);
        }
        return b.ToMesh();
    });

    public static Mesh Shard => Cached("shard", () =>
    {
        var b = new MeshBuilder();
        b.Ico(Vector3.zero, 0.5f, Color.white, 0f, 0);
        return b.ToMesh();
    });

    public static Mesh PetalFlake => Cached("petalflake", () =>
    {
        var b = new MeshBuilder();
        Vector3 a = new Vector3(0, 0, -0.5f), l = new Vector3(-0.35f, 0.05f, 0.05f), r = new Vector3(0.35f, 0.05f, 0.05f), t = new Vector3(0, 0, 0.6f);
        b.TwoSided(a, l, t, Color.white);
        b.TwoSided(a, t, r, Color.white);
        return b.ToMesh();
    });

    public static Mesh Ico(float jitter, int seed) => Cached("ico" + seed, () =>
    {
        var st = Random.state; Random.InitState(seed);
        var b = new MeshBuilder();
        b.Ico(Vector3.zero, 1f, Color.white, jitter, 1);
        Random.state = st;
        return b.ToMesh();
    });
}

// Accumulates flat-shaded triangles. Every triangle owns its vertices so normals stay faceted.
public class MeshBuilder
{
    public readonly List<Vector3> verts = new List<Vector3>();
    public readonly List<Color> cols = new List<Color>();
    public readonly List<int> tris = new List<int>();
    readonly List<Color32> cols32 = new List<Color32>();
    public Matrix4x4 M = Matrix4x4.identity;

    public int Count => verts.Count;

    public void Clear() { verts.Clear(); cols.Clear(); tris.Clear(); M = Matrix4x4.identity; }

    public void TriColors(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
    {
        int i = verts.Count;
        verts.Add(M.MultiplyPoint3x4(a)); verts.Add(M.MultiplyPoint3x4(b)); verts.Add(M.MultiplyPoint3x4(c));
        cols.Add(ca); cols.Add(cb); cols.Add(cc);
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
    }

    public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col) => TriColors(a, b, c, col, col, col);

    // Triangle wound so that its face points away from 'center' (local space).
    public void TriOut(Vector3 a, Vector3 b, Vector3 c, Color col, Vector3 center)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        Vector3 mid = (a + b + c) / 3f;
        if (Vector3.Dot(n, mid - center) < 0) Tri(a, c, b, col); else Tri(a, b, c, col);
    }

    // Triangle wound so its face points along 'dir'.
    public void TriFacing(Vector3 a, Vector3 b, Vector3 c, Color col, Vector3 dir)
    {
        Vector3 n = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(n, dir) < 0) Tri(a, c, b, col); else Tri(a, b, c, col);
    }

    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
    {
        Tri(a, b, c, col); Tri(a, c, d, col);
    }

    public void TwoSided(Vector3 a, Vector3 b, Vector3 c, Color front, Color? back = null)
    {
        Tri(a, b, c, front); Tri(a, c, b, back ?? front);
    }

    // Frustum / cone between two points. rTop 0 makes a cone.
    public void Cylinder(Vector3 bottom, Vector3 top, float rBottom, float rTop, int sides, Color col, Color? capCol = null, bool caps = true)
    {
        Vector3 axis = top - bottom;
        Vector3 up = axis.normalized;
        Vector3 side = Vector3.Cross(up, Mathf.Abs(up.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
        Vector3 fwd = Vector3.Cross(side, up);
        Vector3 center = (bottom + top) * 0.5f;
        float off = Mathf.Repeat(bottom.x * 7.13f + bottom.z * 3.71f + bottom.y * 1.3f, 6.28f);
        for (int i = 0; i < sides; i++)
        {
            float a0 = off + i * Mathf.PI * 2f / sides, a1 = off + (i + 1) * Mathf.PI * 2f / sides;
            Vector3 d0 = side * Mathf.Cos(a0) + fwd * Mathf.Sin(a0);
            Vector3 d1 = side * Mathf.Cos(a1) + fwd * Mathf.Sin(a1);
            Vector3 b0 = bottom + d0 * rBottom, b1 = bottom + d1 * rBottom;
            Vector3 t0 = top + d0 * rTop, t1 = top + d1 * rTop;
            Vector3 sideMid = (b0 + b1 + t0 + t1) * 0.25f;
            Vector3 c = sideMid - (d0 + d1).normalized; // point "inside" this face
            if (rTop > 0.0001f) { TriOut(b0, t0, t1, col, c); TriOut(b0, t1, b1, col, c); }
            else TriOut(b0, top, b1, col, c);
            if (caps)
            {
                Color cc = capCol ?? col;
                if (rBottom > 0.0001f) TriFacing(bottom, b0, b1, cc, -up);
                if (rTop > 0.0001f) TriFacing(top, t0, t1, cc, up);
            }
        }
    }

    public void Box(Vector3 center, Vector3 size, Color col)
    {
        Vector3 h = size * 0.5f;
        Vector3[] p =
        {
            center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3(h.x, -h.y, -h.z),
            center + new Vector3(h.x, -h.y, h.z), center + new Vector3(-h.x, -h.y, h.z),
            center + new Vector3(-h.x, h.y, -h.z), center + new Vector3(h.x, h.y, -h.z),
            center + new Vector3(h.x, h.y, h.z), center + new Vector3(-h.x, h.y, h.z),
        };
        int[,] f = { { 0, 1, 2, 3 }, { 4, 5, 6, 7 }, { 0, 1, 5, 4 }, { 1, 2, 6, 5 }, { 2, 3, 7, 6 }, { 3, 0, 4, 7 } };
        for (int i = 0; i < 6; i++)
        {
            TriOut(p[f[i, 0]], p[f[i, 1]], p[f[i, 2]], col, center);
            TriOut(p[f[i, 0]], p[f[i, 2]], p[f[i, 3]], col, center);
        }
    }

    static readonly Vector3[] icoV;
    static readonly int[] icoF =
    {
        0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
        3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
    };

    static MeshBuilder()
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        icoV = new[]
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        for (int i = 0; i < icoV.Length; i++) icoV[i] = icoV[i].normalized;
    }

    // Lumpy icosphere. subdiv 0 = 20 faces, 1 = 80 faces.
    public void Ico(Vector3 center, float radius, Color col, float jitter, int subdiv, float colJitter = 0f, Vector3? scale = null)
    {
        var pts = new List<Vector3>(icoV);
        var faces = new List<int>(icoF);
        if (subdiv > 0)
        {
            var mid = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out int idx)) return idx;
                pts.Add(((pts[a] + pts[b]) * 0.5f).normalized);
                mid[key] = pts.Count - 1;
                return pts.Count - 1;
            }
            var nf = new List<int>();
            for (int i = 0; i < faces.Count; i += 3)
            {
                int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            faces = nf;
        }
        Vector3 s = scale ?? Vector3.one;
        var w = new Vector3[pts.Count];
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 p = pts[i] * (radius * (1f + Random.Range(-jitter, jitter)));
            w[i] = center + Vector3.Scale(p, s);
        }
        for (int i = 0; i < faces.Count; i += 3)
        {
            Color c = colJitter > 0 ? LP.Jitter(col, colJitter) : col;
            TriOut(w[faces[i]], w[faces[i + 1]], w[faces[i + 2]], c, center);
        }
    }

    public Mesh ToMesh(Mesh m = null)
    {
        if (m == null) m = new Mesh();
        else m.Clear();
        if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
        m.SetVertices(verts);
        // Color32, not float colours: float vertex colours break particle tinting on mesh particles.
        cols32.Clear();
        for (int i = 0; i < cols.Count; i++) cols32.Add(cols[i]);
        m.SetColors(cols32);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
