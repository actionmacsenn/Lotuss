using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Shared helpers for ability effects.
public abstract class Effect : MonoBehaviour
{
    protected static readonly List<Enemy> buf = new List<Enemy>();

    protected static T Make<T>(Vector3 p) where T : Effect
    {
        var go = new GameObject(typeof(T).Name);
        go.transform.position = p;
        return go.AddComponent<T>();
    }

    protected Renderer Decal(Mesh m, Material mat, Color c, Vector3 pos, float scale, float y = 0.06f)
    {
        var go = LP.Obj("Decal", m, mat, transform, false);
        pos.y = y;
        go.transform.position = pos;
        go.transform.localScale = new Vector3(scale, 1f, scale);
        var r = go.GetComponent<Renderer>();
        LP.SetColor(r, c);
        return r;
    }

    protected static Vector3 Ground(Vector3 p) { p.y = WorldBuilder.Height(p.x, p.z); return p; }

    protected static Vector3 AimDir(Vector3 target)
    {
        Vector3 d = LP.Flat(target);
        return d.sqrMagnitude < 0.01f ? Vector3.forward : d.normalized;
    }
}

// ============================================================================ Root Abyss
public class RootAbyss : Effect
{
    static float R => 8f * (1f + 0.12f * Abilities.Lv("root.size"));
    static int Hunger => Abilities.Lv("root.hunger");
    static Mesh holeMesh;

    class Grab { public Enemy e; public Vine v; public float t; public int phase; public Vector3 start; public float sink; }

    public static void Spawn(Vector3 p) => Make<RootAbyss>(Ground(p));

    static Mesh HoleMesh()
    {
        if (holeMesh) return holeMesh;
        var b = new MeshBuilder();
        int seg = 18;
        Color dark = new Color(0.05f, 0.03f, 0.03f), dirt = new Color(0.36f, 0.25f, 0.16f);
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * 360f / seg, a1 = (i + 1) * 360f / seg;
            float j0 = 1f + Mathf.Sin(i * 2.3f) * 0.12f, j1 = 1f + Mathf.Sin((i + 1) * 2.3f) * 0.12f;
            Vector3 e0 = LP.Dir(a0) * j0, e1 = LP.Dir(a1) * j1;
            b.TriFacing(Vector3.down * 0.4f, e0 * 0.85f, e1 * 0.85f, dark, Vector3.up);
            // Raised dirt lip.
            Vector3 lip0 = e0 * 1.05f + Vector3.up * 0.25f, lip1 = e1 * 1.05f + Vector3.up * 0.25f;
            b.TriFacing(e0 * 0.85f, lip0, lip1, LP.Jitter(dirt * 0.7f, 0.03f), Vector3.up);
            b.TriFacing(e0 * 0.85f, lip1, e1 * 0.85f, LP.Jitter(dirt * 0.7f, 0.03f), Vector3.up);
            b.TriFacing(lip0, e0 * 1.35f, e1 * 1.35f, LP.Jitter(dirt, 0.04f), Vector3.up);
            b.TriFacing(lip0, e1 * 1.35f, lip1, LP.Jitter(dirt, 0.04f), Vector3.up);
        }
        holeMesh = b.ToMesh();
        return holeMesh;
    }

    IEnumerator Start()
    {
        Vector3 p = transform.position;
        var zone = Decal(LP.DecalDisc, LP.Glow, new Color(0.3f, 1f, 0.35f, 0.9f), p, 0.01f);
        var rim = Decal(LP.Ring, LP.Add, new Color(0.5f, 1f, 0.45f, 1f), p, 0.01f, 0.08f);
        SFX.At(SFX.S.ZoneHum, p);
        FX.Leaves(p + Vector3.up, 20, 6f, R * 0.6f);

        for (float t = 0; t < 0.35f; t += Time.deltaTime)
        {
            float s = LP.EaseOutBack(t / 0.35f) * R;
            zone.transform.localScale = new Vector3(s, 1, s);
            rim.transform.localScale = new Vector3(s, 1, s);
            yield return null;
        }
        yield return new WaitForSeconds(0.25f);

        // The ground tears open.
        var hole = LP.Obj("Hole", HoleMesh(), LP.Mat, transform, false);
        hole.transform.position = p + Vector3.up * 0.04f;
        FX.Dirt(p, 30, 9f);
        SFX.At(SFX.S.GroundTear, p, 1f, 0.04f);
        GameCamera.Shake(0.35f);
        for (float t = 0; t < 0.25f; t += Time.deltaTime)
        {
            hole.transform.localScale = Vector3.one * LP.EaseOutBack(t / 0.25f) * 2.6f;
            yield return null;
        }

        var grabs = new List<Grab>();
        var grabbed = new HashSet<Enemy>();
        float window = 1.8f + 0.4f * Hunger, elapsed = 0f, scan = 0f;
        while (elapsed < window || grabs.Count > 0)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            scan -= dt;
            float pulse = 0.75f + Mathf.Sin(Time.time * 12f) * 0.25f;
            LP.SetColor(rim, new Color(0.5f, 1f, 0.45f, pulse));

            if (elapsed < window && scan <= 0f)
            {
                scan = 0.06f;
                EnemyManager.Query(p, R, buf, true);
                int added = 0;
                foreach (var e in buf)
                {
                    if (grabbed.Contains(e) || !e.TryHold()) continue;
                    grabbed.Add(e);
                    var v = Vine.Create(transform, 16, Random.Range(0.3f, 0.42f), 0.1f);
                    v.thorns = true;
                    v.wiggle = 0.35f;
                    v.wiggleSpeed = 6f;
                    v.grow = 0f;
                    grabs.Add(new Grab { e = e, v = v, start = e.transform.position });
                    SFX.At(SFX.S.VineGrab, e.transform.position, 0.8f, 0.15f);
                    if (++added > 6) break;
                }
            }

            for (int i = grabs.Count - 1; i >= 0; i--)
            {
                var g = grabs[i];
                g.t += dt;
                bool alive = g.e != null && g.e.Alive;
                Vector3 ep = alive ? g.e.transform.position : g.start;
                Vector3 basePt = p + Vector3.down * 1.5f;
                float d = LP.Flat(ep - p).magnitude;

                if (g.phase == 0)
                {
                    // Vine reaches out of the hole.
                    g.v.grow = LP.EaseOut(g.t / 0.22f);
                    if (alive) g.e.transform.position = g.start + Vector3.up * 0.3f * g.v.grow;
                    if (g.t >= 0.22f) { g.phase = 1; g.t = 0f; FX.Leaves(ep + Vector3.up, 4, 3f); SFX.At(SFX.S.Drag, ep, 0.7f, 0.15f); }
                }
                else if (g.phase == 1 && alive)
                {
                    // Drag toward the hole, accelerating; the victim bumps along the ground.
                    float spd = (3f + g.t * g.t * 40f) * (1f + 0.2f * Hunger);
                    Vector3 flat = LP.Flat(ep - p);
                    float nd = Mathf.Max(0f, d - spd * dt);
                    Vector3 np = p + (flat.sqrMagnitude > 0.001f ? flat.normalized : Vector3.forward) * nd;
                    np.y = 0.3f + Mathf.Abs(Mathf.Sin(g.t * 18f)) * 0.35f * Mathf.Clamp01(nd / 3f);
                    g.e.transform.position = np;
                    g.e.transform.rotation = Quaternion.LookRotation(-flat.normalized + Vector3.down * 0.3f) * Quaternion.Euler(Mathf.Sin(g.t * 30f) * 20f, 0, Mathf.Sin(g.t * 23f) * 25f);
                    if (Random.value < 0.4f) FX.Puffs(Ground(np), new Color(0.55f, 0.45f, 0.32f), 1, 1f, 0.6f, 0.3f, 0.5f);
                    if (nd < 0.8f) { g.phase = 2; g.t = 0f; g.sink = np.y; }
                }
                else if (g.phase == 2 && alive)
                {
                    // Sucked down into the dark.
                    float k = g.t / 0.28f;
                    g.e.transform.position = p + Vector3.up * Mathf.Lerp(g.sink, -3f, LP.EaseIn(k));
                    g.e.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, k);
                    if (k >= 1f)
                    {
                        FX.Dirt(p + Vector3.up * 0.3f, 6, 5f);
                        FX.Petals(p + Vector3.up * 0.5f, new Color(0.45f, 0.85f, 0.35f), 4, 4f);
                        g.e.Kill(Death.Swallow);
                        GameCamera.Shake(0.06f);
                        g.phase = 3; g.t = 0f;
                    }
                }
                else
                {
                    // Victim gone: retract.
                    if (g.phase != 3) { g.phase = 3; g.t = 0f; }
                    g.v.grow = 1f - g.t / 0.2f;
                    if (g.v.grow <= 0f) { Destroy(g.v.gameObject); grabs.RemoveAt(i); continue; }
                }

                if (alive || g.phase == 3)
                {
                    Vector3 tip = (g.phase == 3) ? g.v.p3 : ep + Vector3.up * 0.5f;
                    float arc = 2f + d * 0.25f;
                    g.v.Set(basePt, p + Vector3.up * arc, Vector3.Lerp(p, tip, 0.6f) + Vector3.up * arc, tip);
                }
            }
            yield return null;
        }

        // Close up and leave a ring of flowers.
        SFX.At(SFX.S.Bloom, p, 1f, 0.05f);
        SFX.At(SFX.S.Land, p, 0.8f, 0.05f);
        for (int i = 0; i < 8; i++) BloomFlowers.SpawnRandom(p + LP.Dir(i * 45f + Random.Range(-15f, 15f)) * Random.Range(2.5f, R * 0.8f), 1.2f);
        for (float t = 0; t < 0.4f; t += Time.deltaTime)
        {
            float k = t / 0.4f;
            hole.transform.localScale = Vector3.one * (1f - LP.EaseIn(k)) * 2.6f;
            LP.SetColor(zone, new Color(0.3f, 1f, 0.35f, 0.9f * (1f - k)));
            LP.SetColor(rim, new Color(0.5f, 1f, 0.45f, 1f - k));
            yield return null;
        }
        Destroy(gameObject);
    }
}

// ============================================================================ Bramble Rupture
public class BrambleRupture : Effect
{
    static Mesh[] thornMeshes;

    public static void Spawn(Vector3 target)
    {
        // Forked Rupture adds extra lines fanning out alternately left and right.
        int lines = 1 + Abilities.Lv("bramble.lines");
        float yaw = LP.Yaw(AimDir(target));
        for (int i = 0; i < lines; i++)
        {
            float off = i == 0 ? 0f : (i % 2 == 1 ? 1f : -1f) * 14f * ((i + 1) / 2);
            var e = Make<BrambleRupture>(Vector3.zero);
            e.dir = LP.Dir(yaw + off);
            e.quiet = i > 0;
        }
    }

    Vector3 dir;
    bool quiet;

    static Mesh ThornMesh(int seed)
    {
        var st = Random.state; Random.InitState(seed);
        var b = new MeshBuilder();
        Color bark = new Color(0.38f, 0.26f, 0.2f), tip = new Color(0.62f, 0.85f, 0.4f);
        for (int i = 0; i < 4; i++)
        {
            Vector3 basePt = new Vector3(Random.Range(-0.7f, 0.7f), -0.3f, Random.Range(-0.7f, 0.7f));
            Vector3 d = (Vector3.up + new Vector3(basePt.x, 0, basePt.z) * 0.6f + Random.insideUnitSphere * 0.25f).normalized;
            float len = Random.Range(2.2f, 3.6f) * (i == 0 ? 1.3f : 1f);
            b.Cylinder(basePt, basePt + d * len * 0.7f, Random.Range(0.35f, 0.5f), 0.18f, 5, LP.Jitter(bark, 0.04f), null, false);
            b.Cylinder(basePt + d * len * 0.7f, basePt + d * len, 0.18f, 0f, 5, tip, null, false);
            // Side leaf.
            Vector3 lb = basePt + d * len * 0.4f;
            Vector3 side = Vector3.Cross(d, Vector3.up).normalized;
            b.TwoSided(lb, lb + side * 0.9f + Vector3.up * 0.2f, lb + side * 0.5f + d * 0.6f, new Color(0.38f, 0.72f, 0.28f));
        }
        b.Ico(Vector3.zero, 0.9f, new Color(0.35f, 0.26f, 0.18f), 0.3f, 0, 0.04f, new Vector3(1.2f, 0.35f, 1.2f));
        Random.state = st;
        return b.ToMesh();
    }

    IEnumerator Start()
    {
        if (thornMeshes == null) thornMeshes = new[] { ThornMesh(1), ThornMesh(2), ThornMesh(3) };
        Lotus.I?.Pulse(new Color(0.75f, 0.45f, 0.3f));
        if (!quiet) SFX.Play(SFX.S.RootCreak, 0.8f);
        int power = Abilities.Lv("bramble.power");
        var hit = new HashSet<Enemy>();
        var spikes = new List<(Transform t, float born, float scale)>();
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        float start = WorldBuilder.PitOut + 1.5f, end = 95f, step = 1.7f;
        float nextStep = start;
        float time = 0f;
        while (nextStep < end || spikes.Count > 0)
        {
            time += Time.deltaTime;
            // Advance the eruption front.
            float front = start + time * 55f;
            int made = 0;
            while (nextStep < Mathf.Min(front, end))
            {
                float wobble = Mathf.Sin(nextStep * 0.35f) * 1.2f;
                Vector3 c = dir * nextStep + side * wobble;
                for (int k = -1; k <= 1; k++)
                {
                    Vector3 sp = Ground(c + side * k * 1.5f + Random.insideUnitSphere.normalized * 0.4f);
                    var go = LP.Obj("Thorn", thornMeshes[Random.Range(0, 3)], LP.Mat, transform);
                    go.transform.position = sp;
                    go.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.value * 360f, Random.Range(-10f, 10f));
                    go.transform.localScale = Vector3.zero;
                    spikes.Add((go.transform, Time.time, Random.Range(0.8f, 1.25f) * (k == 0 ? 1.25f : 0.9f)));
                }
                FX.Dirt(Ground(c), 3, 6f);
                EnemyManager.Query(c, 2.8f, buf);
                foreach (var e in buf)
                {
                    if (!hit.Add(e)) continue;
                    Vector3 away = LP.Flat(e.transform.position - c).normalized;
                    e.Launch(Vector3.up * (Random.Range(13f, 17f) + power * 2f) + dir * 4f + away * Random.Range(2f, 5f));
                    e.Damage(3.5f * (1f + 0.4f * power), c, Death.Shatter);
                }
                nextStep += step;
                made++;
                SFX.At(SFX.S.ThornErupt, c, 0.9f, 0.12f);
            }
            if (made > 0) GameCamera.Shake(0.04f);

            for (int i = spikes.Count - 1; i >= 0; i--)
            {
                var s = spikes[i];
                float age = Time.time - s.born;
                float k = age < 0.18f ? LP.EaseOutBack(age / 0.18f) : age > 1.4f ? 1f - (age - 1.4f) / 0.35f : 1f;
                if (k <= 0f) { Destroy(s.t.gameObject); spikes.RemoveAt(i); continue; }
                s.t.localScale = Vector3.one * (s.scale * Mathf.Max(0.01f, k));
                if (age > 1.4f) s.t.position += Vector3.down * Time.deltaTime * 4f;
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}

// ============================================================================ Vine Lash
public class VineLash : Effect
{
    public static void Spawn(Vector3 target)
    {
        float sgn = Random.value < 0.5f ? 1f : -1f;
        var e = Make<VineLash>(Vector3.zero);
        e.aim = LP.Yaw(AimDir(target));
        e.sgn = sgn;
        if (Abilities.Lv("lash.twin") > 0)
        {
            var twin = Make<VineLash>(Vector3.zero);
            twin.aim = e.aim;
            twin.sgn = -sgn;
            twin.quiet = true;
        }
    }

    float aim, sgn = 1f;
    bool quiet;

    IEnumerator Start()
    {
        Lotus.I?.Pulse(new Color(0.3f, 0.8f, 0.35f));
        var v = Vine.Create(transform, 26, 1.1f, 0.22f);
        v.thorns = true;
        v.wiggle = 0.2f;
        v.colBase = new Color(0.16f, 0.38f, 0.18f);
        v.colTip = new Color(0.42f, 0.75f, 0.28f);
        float L = 32f * (1f + 0.15f * Abilities.Lv("lash.len"));
        float sweep = 75f;
        var hit = new HashSet<Enemy>();
        if (!quiet) SFX.At(SFX.S.WhipSwoosh, LP.Dir(aim) * 15f, 1f, 0.05f);

        float total = 0.75f;
        for (float t = 0; t < total; t += Time.deltaTime)
        {
            float grow = LP.EaseOut(t / 0.14f);
            float k = Mathf.Clamp01((t - 0.06f) / 0.5f);
            float ang = aim + sgn * Mathf.Lerp(-sweep, sweep, LP.Smooth(k));
            float lag = sgn * 22f * Mathf.Sin(k * Mathf.PI);
            Vector3 root = new Vector3(0, 1.5f, 0);
            v.Set(root,
                  LP.Dir(ang + lag * 0.2f) * 9f + Vector3.up * 6f,
                  LP.Dir(ang - lag * 0.6f) * 20f + Vector3.up * 2.5f,
                  LP.Dir(ang - lag) * L + Vector3.up * 0.8f);
            v.grow = t > 0.6f ? 1f - (t - 0.6f) / 0.15f : grow;

            if (k > 0f && k < 1f)
            {
                Vector3 tangent = LP.Dir(ang + sgn * 90f);
                foreach (var e in EnemyManager.All.ToArray())
                {
                    if (e == null || !e.Alive || hit.Contains(e)) continue;
                    float r = LP.Flat(e.transform.position).magnitude;
                    if (r < WorldBuilder.PitOut || r > L + 2f) continue;
                    if (v.FlatDistance(e.transform.position, out Vector3 cp) < 1.9f + e.radius)
                    {
                        hit.Add(e);
                        e.Launch(tangent * Random.Range(16f, 22f) + Vector3.up * Random.Range(10f, 14f) + LP.Flat(e.transform.position).normalized * 4f);
                        e.Damage(4f, cp, Death.Shatter);
                        FX.Leaves(e.Center, 3, 5f);
                        SFX.At(SFX.S.WhipCrack, cp, 0.9f, 0.15f);
                    }
                }
                Vector3 tip = v.Point(1f);
                if (Random.value < 0.6f) FX.Dirt(Ground(tip), 2, 5f);
            }
            yield return null;
        }
        if (hit.Count > 5) GameCamera.Shake(0.25f);
        Destroy(gameObject);
    }
}

// ============================================================================ Petal Cyclone
public class PetalCyclone : Effect
{
    static float R => 11f * (1f + 0.15f * Abilities.Lv("cyclone.size"));
    static float Duration => 5.5f + 1.5f * Abilities.Lv("cyclone.time");
    class Lift { public Enemy e; public float t, ang, r; }

    public static void Spawn(Vector3 p) => Make<PetalCyclone>(Ground(p));

    IEnumerator Start()
    {
        Vector3 p = transform.position;
        Lotus.I?.Pulse(new Color(1f, 0.5f, 0.78f));
        SFX.At(SFX.S.Cyclone, p, 1f, 0.04f);

        // Funnel made of rotating petal rings.
        var rings = new List<Transform>();
        Mesh petal = LP.PetalFlake;
        for (int r = 0; r < 6; r++)
        {
            var ring = new GameObject("Ring").transform;
            ring.SetParent(transform, false);
            ring.localPosition = Vector3.up * (0.4f + r * 1.6f);
            float rad = 1.6f + r * 0.9f;
            int n = 9 + r * 2;
            for (int i = 0; i < n; i++)
            {
                var go = LP.Obj("Petal", petal, LP.Mat, ring, false);
                go.transform.localPosition = LP.Dir(i * 360f / n) * rad;
                go.transform.localRotation = Quaternion.Euler(Random.Range(-30f, 30f), i * 360f / n + 90f, Random.Range(20f, 60f));
                go.transform.localScale = Vector3.one * Random.Range(0.9f, 1.6f);
                LP.SetColor(go.GetComponent<Renderer>(), Color.Lerp(new Color(1f, 0.45f, 0.72f), Color.white, Random.value * 0.4f));
            }
            rings.Add(ring);
        }
        var funnel = LP.Obj("Funnel", LP.Wall, LP.Glow, transform, false);
        funnel.transform.localScale = new Vector3(5f, 10f, 5f);
        LP.SetColor(funnel.GetComponent<Renderer>(), new Color(1f, 0.6f, 0.82f, 0.25f));
        var zone = Decal(LP.Ring, LP.Add, new Color(1f, 0.5f, 0.78f, 0.6f), p, R);

        var lifts = new List<Lift>();
        float t = 0f;
        while (t < Duration || lifts.Count > 0)
        {
            float dt = Time.deltaTime;
            t += dt;
            float grow = Mathf.Min(LP.EaseOutBack(t / 0.5f), t > Duration ? Mathf.Max(0f, 1f - (t - Duration) / 0.6f) : 1f);
            for (int r = 0; r < rings.Count; r++)
            {
                rings[r].Rotate(0f, (500f - r * 40f) * dt, 0f);
                rings[r].localScale = Vector3.one * grow * (1f + Mathf.Sin(t * 5f + r) * 0.06f);
            }
            funnel.transform.Rotate(0f, 300f * dt, 0f);
            funnel.transform.localScale = new Vector3(5f * grow, 10f * grow, 5f * grow);
            zone.transform.localScale = new Vector3(R * grow, 1f, R * grow);
            if (Random.value < 0.8f) FX.Petals(p + Vector3.up * Random.Range(0f, 8f) + Random.insideUnitSphere * 3f, new Color(1f, 0.5f, 0.75f), 2, 6f, 0.5f);

            if (t < Duration)
            {
                EnemyManager.Query(p, R, buf);
                foreach (var e in buf)
                {
                    Vector3 to = LP.Flat(p - e.transform.position);
                    float d = to.magnitude;
                    if (d < 2.8f && e.TryHold())
                    {
                        lifts.Add(new Lift { e = e, ang = LP.Yaw(-to), r = Mathf.Max(d, 1.5f) });
                        continue;
                    }
                    if (e.state == EState.Walk && d > 0.01f)
                    {
                        Vector3 swirl = Vector3.Cross(Vector3.up, to / d);
                        e.push += (to / d * (5f + 9f * (1f - d / R)) + swirl * 5f) * dt * 4f;
                        e.Slow(0.3f, 0.2f);
                    }
                }
            }

            for (int i = lifts.Count - 1; i >= 0; i--)
            {
                var l = lifts[i];
                if (l.e == null || !l.e.Alive) { lifts.RemoveAt(i); continue; }
                l.t += dt;
                l.ang += (540f + l.t * 360f) * dt;
                l.r = Mathf.Lerp(l.r, 2.2f + l.t * 1.5f, dt * 3f);
                float h = 0.5f + l.t * l.t * 5f;
                l.e.transform.position = p + LP.Dir(l.ang) * l.r + Vector3.up * h;
                l.e.transform.Rotate(new Vector3(400f, 700f, 300f) * dt);
                l.e.Damage(4f * dt, p, Death.Petals);
                if (l.e == null || !l.e.Alive) { lifts.RemoveAt(i); continue; }
                if (h > 9f || t > Duration)
                {
                    // Spat out of the top - shredded into petals.
                    l.e.Kill(Death.Petals);
                    lifts.RemoveAt(i);
                }
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}

// ============================================================================ Snapjaw
public class Snapjaw : Effect
{
    const float Reach = 10f;
    static float Speed => 1f + 0.25f * Abilities.Lv("snap.speed");
    static float Life => 9f + 2f * Abilities.Lv("snap.speed");
    static Mesh jawMesh, bulbMesh;

    public static void Spawn(Vector3 p) => Make<Snapjaw>(Ground(p));

    // Snapjaw Patch: extra plants ring the target point.
    public static void SpawnGroup(Vector3 p)
    {
        int extra = Abilities.Lv("snap.count");
        Spawn(p);
        for (int i = 0; i < extra; i++) Spawn(p + LP.Dir(i * 360f / extra + 30f) * 5f);
    }

    static Mesh JawMesh()
    {
        if (jawMesh) return jawMesh;
        var b = new MeshBuilder();
        Color outer = new Color(0.4f, 0.72f, 0.25f), inner = new Color(0.85f, 0.25f, 0.35f), tooth = new Color(1f, 0.97f, 0.85f);
        int n = 12;
        Vector3 apex = new Vector3(0, 0.9f, 1.3f);
        Vector3[] rim = new Vector3[n + 1];
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Lerp(-100f, 100f, i / (float)n) * Mathf.Deg2Rad;
            rim[i] = new Vector3(Mathf.Sin(a) * 1.5f, 0f, 1.3f + Mathf.Cos(a) * 1.5f);
        }
        for (int i = 0; i < n; i++)
        {
            b.TriFacing(apex, rim[i], rim[i + 1], LP.Jitter(outer, 0.03f), Vector3.up);
            b.TriFacing(new Vector3(0, 0.05f, 1.3f), rim[i], rim[i + 1], inner, Vector3.down);
            // Teeth along the rim, pointing down.
            Vector3 m = (rim[i] + rim[i + 1]) * 0.5f;
            b.Cylinder(m, m + Vector3.down * 0.55f + (m - new Vector3(0, 0, 1.3f)).normalized * 0.1f, 0.11f, 0f, 3, tooth, null, false);
        }
        b.TriFacing(apex, rim[n], rim[0], outer * 0.9f, Vector3.back);
        jawMesh = b.ToMesh();
        return jawMesh;
    }

    static Mesh BulbMesh()
    {
        if (bulbMesh) return bulbMesh;
        var b = new MeshBuilder();
        Color leaf = new Color(0.3f, 0.6f, 0.24f);
        for (int i = 0; i < 6; i++)
        {
            Vector3 d = LP.Dir(i * 60f), s = Vector3.Cross(Vector3.up, d);
            b.TwoSided(Vector3.zero, d * 1.6f + s * 0.7f + Vector3.up * 0.3f, d * 3.2f + Vector3.up * 0.5f, LP.Jitter(leaf, 0.04f), leaf * 0.7f);
            b.TwoSided(Vector3.zero, d * 3.2f + Vector3.up * 0.5f, d * 1.6f - s * 0.7f + Vector3.up * 0.3f, LP.Jitter(leaf, 0.04f), leaf * 0.7f);
        }
        b.Ico(Vector3.up * 0.3f, 1f, new Color(0.28f, 0.5f, 0.22f), 0.2f, 0, 0.03f, new Vector3(1f, 0.6f, 1f));
        bulbMesh = b.ToMesh();
        return bulbMesh;
    }

    IEnumerator Start()
    {
        Vector3 p = transform.position;
        Lotus.I?.Pulse(new Color(0.55f, 0.85f, 0.25f));
        FX.Dirt(p, 20, 8f);
        SFX.At(SFX.S.PlantGrow, p);
        GameCamera.Shake(0.2f);

        var bulb = LP.Obj("Bulb", BulbMesh(), LP.Mat, transform);
        var stem = Vine.Create(transform, 14, 0.75f, 0.45f);
        stem.leaves = true;
        stem.wiggle = 0.12f;
        stem.colBase = new Color(0.25f, 0.5f, 0.22f);
        stem.colTip = new Color(0.4f, 0.7f, 0.28f);
        var head = new GameObject("Head").transform;
        head.SetParent(transform, false);
        var upper = new GameObject("Upper").transform; upper.SetParent(head, false);
        var lower = new GameObject("Lower").transform; lower.SetParent(head, false);
        LP.Obj("Jaw", JawMesh(), LP.Mat, upper);
        var lj = LP.Obj("Jaw", JawMesh(), LP.Mat, lower);
        lj.transform.localRotation = Quaternion.Euler(0, 0, 180f);
        var mouth = new GameObject("Mouth").transform; mouth.SetParent(head, false); mouth.localPosition = new Vector3(0, 0, 1.4f);

        Vector3 restLocal = new Vector3(0, 6.5f, 0);
        Vector3 headPos = p + Vector3.up * 0.5f;
        Vector3 facing = AimDir(p);
        float open = 0.2f;
        float scale = 0f;

        Enemy target = null, eaten = null;
        int mode = 0; // 0 idle, 1 lunge, 2 retract, 3 chew
        float mt = 0f, cooldown = 0.3f;
        Vector3 lungeFrom = Vector3.zero;
        float t = 0f;
        while (true)
        {
            float dt = Time.deltaTime;
            t += dt;
            bool dying = t > Life;
            scale = dying ? Mathf.Max(0f, scale - dt * 2f) : Mathf.Min(1f, LP.EaseOutBack(t / 0.6f));
            if (dying && scale <= 0f) break;
            Vector3 rest = p + restLocal * scale + Vector3.up * Mathf.Sin(t * 2f) * 0.3f;
            mt += dt;

            if (mode == 0)
            {
                headPos = Vector3.Lerp(headPos, rest, dt * 6f);
                open = Mathf.Lerp(open, 0.35f + Mathf.Sin(t * 3f) * 0.1f, dt * 5f);
                cooldown -= dt;
                if (!dying && cooldown <= 0f && scale > 0.9f)
                {
                    target = EnemyManager.Nearest(p, Reach);
                    if (target != null) { mode = 1; mt = 0f; lungeFrom = headPos; SFX.At(SFX.S.Lunge, headPos, 1f, 0.12f); }
                }
                if (target == null && Random.value < 0.02f) facing = Quaternion.Euler(0, Random.Range(-30f, 30f), 0) * facing;
            }
            else if (mode == 1)
            {
                if (target == null || !target.Alive || target.state == EState.Held) { mode = 0; target = null; cooldown = 0.1f; }
                else
                {
                    Vector3 goal = target.Center + Vector3.up * 0.6f;
                    float k = mt / (0.16f / Speed);
                    headPos = Vector3.Lerp(lungeFrom, goal, LP.EaseIn(Mathf.Clamp01(k)));
                    open = Mathf.Lerp(open, 1f, dt * 20f);
                    facing = LP.Flat(goal - p).normalized;
                    if (k >= 1f)
                    {
                        if (target.TryHold())
                        {
                            eaten = target;
                            open = 0f;
                            SFX.At(SFX.S.Chomp, goal, 1f, 0.1f);
                            FX.Shards(goal, new Color(0.5f, 0.85f, 0.3f), 8, 6f, 0.2f);
                            GameCamera.Shake(0.12f);
                            mode = 2; mt = 0f;
                        }
                        else { mode = 0; cooldown = 0.1f; }
                        target = null;
                    }
                }
            }
            else if (mode == 2)
            {
                headPos = Vector3.Lerp(headPos, rest, dt * 9f);
                open = 0f;
                if (eaten != null && eaten.Alive)
                {
                    eaten.transform.position = mouth.position;
                    eaten.transform.localScale = Vector3.one * Mathf.Lerp(eaten.transform.localScale.x, 0.35f, dt * 8f);
                }
                if (mt > 0.25f / Speed) { mode = 3; mt = 0f; SFX.At(SFX.S.Chew, headPos, 1f, 0.1f); }
            }
            else if (mode == 3)
            {
                // Chew.
                open = Mathf.Abs(Mathf.Sin(mt * 22f)) * 0.25f;
                headPos = rest + Vector3.down * Mathf.Abs(Mathf.Sin(mt * 22f)) * 0.3f;
                if (eaten != null && eaten.Alive) eaten.transform.position = mouth.position;
                if (Random.value < 0.3f) FX.Shards(mouth.position, new Color(0.5f, 0.85f, 0.3f), 1, 4f, 0.15f);
                if (mt > 0.35f / Speed)
                {
                    if (eaten != null && eaten.Alive) eaten.Kill(Death.Swallow);
                    eaten = null;
                    mode = 0; cooldown = 0.12f;
                }
            }

            head.position = headPos;
            Vector3 look = facing;
            if (mode == 1 || mode == 2) look = (LP.Flat(headPos - p).sqrMagnitude > 0.5f ? LP.Flat(headPos - p).normalized : facing);
            head.rotation = Quaternion.LookRotation(look + Vector3.down * 0.35f);
            head.localScale = Vector3.one * Mathf.Max(0.01f, scale) * 1.15f;
            upper.localRotation = Quaternion.Euler(-55f * open, 0, 0);
            lower.localRotation = Quaternion.Euler(30f * open, 0, 0);
            bulb.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
            stem.baseRadius = 0.75f * scale; stem.tipRadius = 0.45f * scale;
            Vector3 hb = headPos - head.forward * 0.4f;
            stem.Set(p, p + Vector3.up * 3.5f * scale, hb - LP.Flat(hb - p) * 0.2f + Vector3.down * 1.5f, hb);
            yield return null;
        }
        FX.Leaves(p + Vector3.up, 12, 4f);
        Destroy(gameObject);
    }
}

// ============================================================================ Seed Barrage
public class SeedBarrage : Effect
{
    static Mesh seedMesh;
    const float G = 24f;

    public static void Spawn(Vector3 p) => Make<SeedBarrage>(Ground(p));

    IEnumerator Start()
    {
        if (!seedMesh)
        {
            var b = new MeshBuilder();
            b.Ico(Vector3.zero, 0.45f, new Color(0.5f, 0.38f, 0.2f), 0.1f, 0, 0f, new Vector3(0.8f, 0.8f, 1.3f));
            b.TwoSided(new Vector3(0, 0.2f, -0.3f), new Vector3(0.4f, 0.5f, -0.7f), new Vector3(-0.1f, 0.4f, -0.8f), new Color(0.4f, 0.75f, 0.3f));
            b.Ico(new Vector3(0, 0, 0.35f), 0.12f, LP.Emissive(new Color(0.7f, 1f, 0.4f)), 0f, 0);
            seedMesh = b.ToMesh();
        }
        Vector3 target = transform.position;
        int count = 16 + 6 * Abilities.Lv("seed.count");
        Lotus.I?.Pulse(new Color(0.85f, 0.75f, 0.35f));
        for (int i = 0; i < count; i++)
        {
            Vector2 r = Random.insideUnitCircle * 5.5f;
            StartCoroutine(Fly(target + new Vector3(r.x, 0f, r.y), Random.Range(0.55f, 0.85f)));
            SFX.Play(SFX.S.SeedShoot, 0.9f, 0.15f);
            if (i % 3 == 0) Lotus.I?.Pulse(new Color(0.85f, 0.75f, 0.35f), 0.6f);
            yield return new WaitForSeconds(0.035f);
        }
        yield return new WaitForSeconds(1.2f);
        Destroy(gameObject);
    }

    IEnumerator Fly(Vector3 to, float time)
    {
        Vector3 from = Lotus.I ? Lotus.I.CorePos + Vector3.up * 1.5f : new Vector3(0, 6, 0);
        to = Ground(to);
        Vector3 v = (to - from) / time + 0.5f * G * time * Vector3.up;
        var seed = LP.Obj("Seed", seedMesh, LP.Mat, transform).transform;
        seed.position = from;
        for (float t = 0; t < time; t += Time.deltaTime)
        {
            Vector3 pos = from + v * t + 0.5f * G * t * t * Vector3.down;
            Vector3 vel = v + G * t * Vector3.down;
            seed.position = pos;
            seed.rotation = Quaternion.LookRotation(vel) * Quaternion.Euler(0, 0, t * 900f);
            if (Random.value < 0.5f) FX.Spark(pos, Random.insideUnitSphere, new Color(0.6f, 1f, 0.4f), 0.12f, 0.35f);
            yield return null;
        }
        Destroy(seed.gameObject);
        FX.Dirt(to, 8, 7f);
        FX.Leaves(to + Vector3.up * 0.5f, 5, 5f);
        SFX.At(SFX.S.SeedImpact, to, 0.9f, 0.12f);
        GameCamera.Shake(0.05f);
        BloomFlowers.Spawn(to, BloomFlowers.Kind.Sprout, 1.6f, 4f);
        int power = Abilities.Lv("seed.power");
        EnemyManager.Query(to, 2.6f * (1f + 0.2f * power), buf);
        foreach (var e in buf.ToArray())
        {
            Vector3 away = LP.Flat(e.transform.position - to);
            away = away.sqrMagnitude > 0.01f ? away.normalized : Random.onUnitSphere;
            e.Launch(Vector3.up * Random.Range(7f, 10f) + away * 6f);
            e.Damage(3.5f * (1f + 0.3f * power), to, Death.Bloom);
        }
    }
}
