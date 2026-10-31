using System.Collections.Generic;
using UnityEngine;

// Flowers that sprout wherever enemies are defeated. Pooled, they pop in, linger, then shrink away.
public class BloomFlowers : MonoBehaviour
{
    public static BloomFlowers I;

    public enum Kind { Lotus, Sun, Daisy, Sprout, Bell }

    class Entry { public Transform t; public float born, life, scale; }

    readonly List<Entry> live = new List<Entry>();
    readonly Stack<Transform>[] free = new Stack<Transform>[5];
    Mesh[] meshes;
    const int Max = 450;

    void Awake()
    {
        I = this;
        meshes = new[] { BuildLotusBud(), BuildSun(), BuildDaisy(), BuildSprout(), BuildBell() };
        for (int i = 0; i < free.Length; i++) free[i] = new Stack<Transform>();
    }

    public static void Spawn(Vector3 pos, Kind kind, float scale = 1f, float life = 22f)
    {
        if (I == null) return;
        I.SpawnInternal(pos, kind, scale, life);
    }

    public static void SpawnRandom(Vector3 pos, float scale = 1f)
    {
        float r = Random.value;
        Spawn(pos, r < 0.55f ? Kind.Lotus : r < 0.8f ? Kind.Daisy : Kind.Bell, scale);
    }

    void SpawnInternal(Vector3 pos, Kind kind, float scale, float life)
    {
        // Don't plant inside the pit.
        float r = Mathf.Sqrt(pos.x * pos.x + pos.z * pos.z);
        if (r > WorldBuilder.PitIn - 0.3f && r < WorldBuilder.PitOut + 0.3f) return;
        if (live.Count >= Max) live[0].life = Mathf.Min(live[0].life, Time.time - live[0].born + 0.4f);

        int k = (int)kind;
        Transform t = free[k].Count > 0 ? free[k].Pop() : null;
        if (t == null)
        {
            t = LP.Obj(kind.ToString(), meshes[k], LP.Mat, transform, false).transform;
            t.name = k.ToString();
        }
        t.gameObject.SetActive(true);
        pos.y = WorldBuilder.Height(pos.x, pos.z);
        t.position = pos;
        t.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.value * 360f, Random.Range(-8f, 8f));
        t.localScale = Vector3.zero;
        live.Add(new Entry { t = t, born = Time.time, life = life * Random.Range(0.8f, 1.2f), scale = scale * Random.Range(0.8f, 1.2f) });
    }

    void Update()
    {
        float now = Time.time;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var e = live[i];
            float age = now - e.born;
            float s;
            if (age < 0.4f) s = LP.EaseOutBack(age / 0.4f);
            else if (age > e.life) s = 1f - (age - e.life) / 0.5f;
            else s = 1f + Mathf.Sin(age * 2f + i) * 0.03f;
            if (s <= 0f)
            {
                e.t.gameObject.SetActive(false);
                free[int.Parse(e.t.name)].Push(e.t);
                live.RemoveAt(i);
                continue;
            }
            e.t.localScale = Vector3.one * (s * e.scale);
        }
    }

    // ------------------------------------------------------------- flower meshes

    static void Stem(MeshBuilder b, float h)
    {
        b.Cylinder(Vector3.zero, Vector3.up * h, 0.07f, 0.05f, 4, new Color(0.3f, 0.6f, 0.22f), null, false);
        Vector3 l = new Vector3(0.45f, h * 0.35f, 0.1f);
        b.TwoSided(Vector3.up * h * 0.2f, l, Vector3.up * h * 0.45f + new Vector3(0.1f, 0, 0), new Color(0.36f, 0.7f, 0.26f));
        b.TwoSided(Vector3.up * h * 0.25f, new Vector3(-0.4f, h * 0.45f, -0.1f), Vector3.up * h * 0.5f, new Color(0.33f, 0.66f, 0.24f));
    }

    static void PetalRing(MeshBuilder b, Vector3 c, int n, float len, float wid, float lift, Color col, Color tip, float off = 0f)
    {
        for (int k = 0; k < n; k++)
        {
            float a = off + k * 360f / n;
            Vector3 d = LP.Dir(a), s = Vector3.Cross(Vector3.up, d) * wid;
            Vector3 mid = c + d * len * 0.5f + Vector3.up * lift * 0.4f;
            Vector3 end = c + d * len + Vector3.up * lift;
            b.TwoSided(c, mid - s, end, col, col * 0.85f);
            b.TwoSided(c, end, mid + s, tip, tip * 0.85f);
        }
    }

    static Mesh BuildLotusBud()
    {
        var b = new MeshBuilder();
        Stem(b, 0.9f);
        Vector3 c = Vector3.up * 0.9f;
        PetalRing(b, c, 6, 0.55f, 0.22f, 0.45f, new Color(0.98f, 0.45f, 0.7f), new Color(1f, 0.75f, 0.88f));
        PetalRing(b, c, 5, 0.38f, 0.17f, 0.55f, new Color(1f, 0.62f, 0.82f), new Color(1f, 0.85f, 0.93f), 36f);
        b.Ico(c + Vector3.up * 0.1f, 0.12f, LP.Emissive(new Color(1f, 0.85f, 0.35f)), 0f, 0);
        return b.ToMesh();
    }

    static Mesh BuildSun()
    {
        var b = new MeshBuilder();
        Stem(b, 1.3f);
        Vector3 c = Vector3.up * 1.3f;
        PetalRing(b, c, 12, 0.6f, 0.13f, 0.25f, new Color(1f, 0.82f, 0.15f), new Color(1f, 0.92f, 0.4f));
        b.Ico(c + Vector3.up * 0.12f, 0.24f, new Color(0.45f, 0.28f, 0.12f), 0.1f, 0, 0f, new Vector3(1f, 0.5f, 1f));
        return b.ToMesh();
    }

    static Mesh BuildDaisy()
    {
        var b = new MeshBuilder();
        Stem(b, 0.7f);
        Vector3 c = Vector3.up * 0.7f;
        PetalRing(b, c, 9, 0.45f, 0.1f, 0.12f, new Color(1f, 1f, 0.97f), new Color(0.95f, 0.95f, 1f));
        b.Ico(c + Vector3.up * 0.05f, 0.12f, new Color(1f, 0.8f, 0.2f), 0f, 0);
        return b.ToMesh();
    }

    static Mesh BuildSprout()
    {
        var b = new MeshBuilder();
        b.Cylinder(Vector3.zero, Vector3.up * 0.6f, 0.06f, 0.04f, 4, new Color(0.36f, 0.66f, 0.24f), null, false);
        b.TwoSided(Vector3.up * 0.55f, new Vector3(0.5f, 0.85f, 0.1f), new Vector3(0.2f, 0.95f, -0.15f), new Color(0.45f, 0.82f, 0.3f));
        b.TwoSided(Vector3.up * 0.55f, new Vector3(-0.5f, 0.8f, -0.1f), new Vector3(-0.15f, 1f, 0.15f), new Color(0.4f, 0.78f, 0.28f));
        return b.ToMesh();
    }

    static Mesh BuildBell()
    {
        var b = new MeshBuilder();
        Stem(b, 0.8f);
        Vector3 c = Vector3.up * 0.8f;
        Color bl = new Color(0.62f, 0.55f, 1f);
        b.Cylinder(c + Vector3.up * 0.35f, c - Vector3.up * 0.05f, 0.08f, 0.28f, 6, bl, LP.Emissive(new Color(0.85f, 0.8f, 1f)));
        return b.ToMesh();
    }
}
