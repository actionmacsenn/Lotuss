using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================ Pollen Haze
public class PollenHaze : Effect
{
    const float Duration = 7f;
    static float R => 9f * (1f + 0.15f * Abilities.Lv("pollen.size"));
    static float Dps => 2.6f * (1f + 0.5f * Abilities.Lv("pollen.power"));

    public static void Spawn(Vector3 p) => Make<PollenHaze>(Ground(p));

    IEnumerator Start()
    {
        Vector3 p = transform.position;
        Lotus.I?.Pulse(new Color(1f, 0.88f, 0.3f));
        SFX.At(SFX.S.Pollen, p);
        var clouds = new List<(Transform t, float s, float ph)>();
        for (int i = 0; i < 12; i++)
        {
            Vector2 o = Random.insideUnitCircle * R * 0.7f;
            var go = LP.Obj("Cloud", LP.Ico(0.25f, 20 + i % 4), LP.Glow, transform, false);
            go.transform.position = p + new Vector3(o.x, Random.Range(0.8f, 2.5f), o.y);
            float s = Random.Range(2.2f, 4f);
            LP.SetColor(go.GetComponent<Renderer>(), new Color(1f, 0.85f, 0.3f, 0.22f));
            clouds.Add((go.transform, s, Random.value * 6f));
        }
        var zone = Decal(LP.DecalDisc, LP.Glow, new Color(1f, 0.85f, 0.25f, 0.6f), p, R);

        for (float t = 0; t < Duration + 0.8f; t += Time.deltaTime)
        {
            float k = Mathf.Min(LP.EaseOutBack(t / 0.6f), t > Duration ? 1f - (t - Duration) / 0.8f : 1f);
            foreach (var c in clouds)
            {
                c.t.localScale = Vector3.one * c.s * k * (1f + Mathf.Sin(t * 1.5f + c.ph) * 0.12f);
                c.t.Rotate(10f * Time.deltaTime, 20f * Time.deltaTime, 0f);
            }
            zone.transform.localScale = new Vector3(R * k, 1f, R * k);
            if (Random.value < 0.9f)
            {
                Vector2 o = Random.insideUnitCircle * R;
                FX.Puffs(p + new Vector3(o.x, Random.Range(0.3f, 3f), o.y), new Color(1f, 0.88f, 0.35f), 1, 0.6f, 0.35f, 0.2f, 2f);
                FX.Spark(p + new Vector3(o.x, Random.Range(0.3f, 3f), o.y), Vector3.up * 0.6f + Random.insideUnitSphere * 0.4f, new Color(1f, 0.9f, 0.4f), 0.12f, 1.2f);
            }
            if (t < Duration)
            {
                EnemyManager.Query(p, R * k, buf);
                foreach (var e in buf.ToArray())
                {
                    e.Slow(0.2f, 0.3f);
                    e.Tint(new Color(1f, 0.92f, 0.45f), 0.3f);
                    e.Damage(Dps * Time.deltaTime, p, Death.Bloom);
                    if (e != null && e.Alive && Random.value < 0.03f) FX.Puffs(e.Center, new Color(1f, 0.85f, 0.3f), 1, 1f, 0.5f);
                }
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}

// ============================================================================ Strangleroot
public class Strangleroot : Effect
{
    static float R => 10f * (1f + 0.15f * Abilities.Lv("strangle.size"));
    static int Grip => Abilities.Lv("strangle.speed");

    public static void Spawn(Vector3 p) => Make<Strangleroot>(Ground(p));

    IEnumerator Start()
    {
        Vector3 p = transform.position;
        Lotus.I?.Pulse(new Color(0.4f, 0.65f, 0.3f));
        SFX.At(SFX.S.RootCreak, p);
        SFX.At(SFX.S.GroundTear, p, 0.45f, 0.1f);
        var zone = Decal(LP.DecalDisc, LP.Glow, new Color(0.25f, 0.5f, 0.2f, 0.7f), p, 0.1f);
        for (float t = 0; t < 0.3f; t += Time.deltaTime)
        {
            float s = LP.EaseOut(t / 0.3f) * R;
            zone.transform.localScale = new Vector3(s, 1f, s);
            yield return null;
        }
        EnemyManager.Query(p, R, buf, true);
        var victims = new List<Enemy>(buf);
        int running = 0;
        int n = 0;
        foreach (var e in victims)
        {
            if (n++ > 45 + 15 * Grip) break;
            running++;
            StartCoroutine(Squeeze(e, Random.Range(0f, 0.35f), () => running--));
        }
        FX.Dirt(p, 16, 6f);
        GameCamera.Shake(0.2f);
        while (running > 0) yield return null;
        for (float t = 0; t < 0.5f; t += Time.deltaTime)
        {
            LP.SetColor(zone, new Color(0.25f, 0.5f, 0.2f, 0.7f * (1f - t / 0.5f)));
            yield return null;
        }
        Destroy(gameObject);
    }

    IEnumerator Squeeze(Enemy e, float delay, System.Action done)
    {
        yield return new WaitForSeconds(delay);
        if (e == null || !e.TryHold()) { done(); yield break; }
        Vector3 ep = e.transform.position;
        float h = e.height;
        var vines = new List<(Vine v, Vector3 root, float ang)>();
        int count = e.kind == EnemyKind.Brute ? 4 : 3;
        for (int i = 0; i < count; i++)
        {
            float a = i * 360f / count + Random.Range(-20f, 20f);
            var v = Vine.Create(transform, 12, 0.22f + h * 0.04f, 0.06f);
            v.thorns = true;
            v.wiggle = 0.12f;
            v.wiggleSpeed = 8f;
            v.grow = 0f;
            Vector3 root = Ground(ep + LP.Dir(a) * (1.2f + h * 0.25f)) + Vector3.down * 0.3f;
            vines.Add((v, root, a));
            FX.Dirt(root, 3, 4f);
        }
        SFX.At(SFX.S.VineGrab, ep, 0.8f, 0.15f);
        SFX.At(SFX.S.Squeeze, ep, 1f, 0.1f);
        float dur = Mathf.Max(0.4f, 1.1f * (1f - 0.18f * Grip));
        Vector3 baseScale = e.transform.localScale;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            if (e == null || !e.Alive) break;
            float grow = LP.EaseOut(t / 0.2f);
            float sq = LP.EaseIn(Mathf.Clamp01((t - 0.25f) / (dur - 0.25f)));
            foreach (var (v, root, a) in vines)
            {
                v.grow = grow;
                // Coil: the vine wraps around the victim, spiralling tighter as it squeezes.
                float coil = a + t * 360f;
                Vector3 c1 = ep + LP.Dir(coil) * (1.4f - sq * 0.8f) + Vector3.up * h * 0.3f;
                Vector3 c2 = ep + LP.Dir(coil + 120f) * (0.9f - sq * 0.5f) + Vector3.up * h * 0.65f;
                v.Set(root, c1, c2, ep + Vector3.up * h * 0.85f + LP.Dir(coil + 200f) * 0.3f);
            }
            float shake = sq * 0.12f;
            e.transform.position = ep + Random.insideUnitSphere * shake + Vector3.up * sq * 0.3f;
            e.transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + sq * 0.25f, 1f - sq * 0.55f, 1f + sq * 0.25f));
            yield return null;
        }
        if (e != null && e.Alive)
        {
            e.Kill(Death.Bloom);
            FX.Petals(ep + Vector3.up * h * 0.5f, new Color(0.95f, 0.5f, 0.75f), 10, 6f);
            BloomFlowers.Spawn(ep, BloomFlowers.Kind.Lotus, 1.6f);
        }
        for (float t = 0; t < 0.3f; t += Time.deltaTime)
        {
            foreach (var (v, _, _) in vines) v.grow = 1f - t / 0.3f;
            yield return null;
        }
        foreach (var (v, _, _) in vines) Destroy(v.gameObject);
        done();
    }
}

// ============================================================================ Solar Lance
public class SolarLance : Effect
{
    public static void Spawn(Vector3 p) => Make<SolarLance>(Ground(p));

    IEnumerator Start()
    {
        var lotus = Lotus.I;
        Color gold = new Color(1f, 0.82f, 0.3f);
        SFX.Play(SFX.S.Charge, 1f, 0.02f);

        // Charge: light converges into the core.
        for (float t = 0; t < 0.55f; t += Time.deltaTime)
        {
            float k = t / 0.55f;
            lotus?.SetCharge(gold, k);
            Vector3 core = lotus ? lotus.CorePos + Vector3.up * 1.5f : Vector3.up * 5f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 from = core + Random.onUnitSphere * 5f;
                FX.Spark(from, (core - from) * 2.2f, gold, 0.2f, 0.45f);
            }
            yield return null;
        }

        var outer = LP.Obj("Beam", LP.BeamTube, LP.Add, transform, false);
        var inner = LP.Obj("BeamCore", LP.BeamTube, LP.Add, transform, false);
        var impact = LP.Obj("Impact", LP.Ico(0.1f, 3), LP.Add, transform, false);
        LP.SetColor(outer.GetComponent<Renderer>(), new Color(1f, 0.75f, 0.25f, 0.55f));
        LP.SetColor(inner.GetComponent<Renderer>(), new Color(1f, 1f, 0.85f, 0.95f));
        LP.SetColor(impact.GetComponent<Renderer>(), new Color(1f, 0.85f, 0.4f, 0.7f));
        SFX.Play(SFX.S.Beam, 1f, 0.02f);

        float dur = 1.6f + 0.6f * Abilities.Lv("solar.time");
        int flare = Abilities.Lv("solar.power");
        float yaw = LP.Yaw(AimDir(transform.position));
        float plant = 0f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float dt = Time.deltaTime;
            // Steer toward the live aim point.
            float targetYaw = LP.Yaw(AimDir(lotus ? lotus.Aim : transform.position));
            yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, 70f * dt);
            Vector3 dir = LP.Dir(yaw);
            Vector3 start = lotus ? lotus.CorePos + Vector3.up * 1.6f : Vector3.up * 5f;
            float len = 75f;
            Vector3 end = dir * len;
            Vector3 hitPt = Ground(dir * Mathf.Max(18f, LP.Flat(lotus ? lotus.Aim : transform.position).magnitude));
            Vector3 beamEnd = hitPt;
            // Beam hits the ground at the aim point then skims along it.
            float w = 1f + Mathf.Sin(t * 60f) * 0.12f;
            float fade = Mathf.Min(1f, t / 0.08f, (dur - t) / 0.2f);
            float wide = 1f + 0.15f * flare;
            Place(outer.transform, start, beamEnd, 1.4f * w * fade * wide);
            Place(inner.transform, start, beamEnd, 0.55f * w * fade * wide);
            if (t < 1.6f && t + dt >= 1.6f && dur > 2f) SFX.Play(SFX.S.Beam, 0.9f, 0.02f);
            impact.transform.position = beamEnd;
            impact.transform.localScale = Vector3.one * (2.2f + Mathf.Sin(t * 40f) * 0.4f) * fade;
            lotus?.SetCharge(gold, fade);
            GameCamera.Shake(0.02f);

            FX.Sparks(beamEnd, gold, 2, 10f, 0.2f);
            if (Random.value < 0.4f) FX.Puffs(beamEnd, new Color(0.35f, 0.3f, 0.25f), 1, 2f, 0.9f);

            // Burn along a swath from the pit lip to beyond the impact point.
            Vector3 a = dir * WorldBuilder.PitOut, bEnd = end;
            foreach (var e in EnemyManager.All.ToArray())
            {
                if (e == null || !e.Alive) continue;
                Vector3 q = LP.Flat(e.transform.position);
                float along = Vector3.Dot(q, dir);
                if (along < WorldBuilder.PitOut || along > LP.Flat(beamEnd).magnitude + 4f) continue;
                float off = (q - dir * along).magnitude;
                if (off < 2.4f * (1f + 0.15f * flare) + e.radius)
                {
                    e.Tint(new Color(1f, 0.6f, 0.3f), 0.2f);
                    SFX.At(SFX.S.Sizzle, e.Center, 0.6f, 0.2f);
                    e.Damage(26f * (1f + 0.4f * flare) * dt, Vector3.zero, Death.Sun, 0f);
                    if (e != null && e.Alive && e.state == EState.Walk) e.push += dir * 12f * dt;
                }
            }

            plant -= dt;
            if (plant <= 0f)
            {
                plant = 0.06f;
                BloomFlowers.Spawn(beamEnd + Random.insideUnitSphere * 1.5f, BloomFlowers.Kind.Sun, 1.3f, 12f);
            }
            yield return null;
        }
        lotus?.SetCharge(gold, 0f);
        Destroy(gameObject);
    }

    static void Place(Transform t, Vector3 a, Vector3 b, float r)
    {
        t.position = a;
        Vector3 d = b - a;
        t.rotation = Quaternion.LookRotation(d);
        t.localScale = new Vector3(Mathf.Max(0.001f, r), Mathf.Max(0.001f, r), d.magnitude);
    }
}

// ============================================================================ Sporecaps
public class Sporecaps : Effect
{
    static Mesh capMesh;

    class Cap { public Transform t; public float born, trigger = -1f; public bool done; public float size; }

    public static void Spawn(Vector3 p) => Make<Sporecaps>(Ground(p));

    static Mesh CapMesh()
    {
        if (capMesh) return capMesh;
        var b = new MeshBuilder();
        b.Cylinder(Vector3.zero, Vector3.up * 1.1f, 0.38f, 0.3f, 6, new Color(0.95f, 0.92f, 0.85f));
        Color cap = new Color(0.72f, 0.3f, 0.85f);
        b.Ico(Vector3.up * 1.25f, 1f, cap, 0.08f, 1, 0.03f, new Vector3(1.1f, 0.6f, 1.1f));
        for (int i = 0; i < 7; i++)
        {
            Vector3 d = (LP.Dir(i * 51f) * 0.8f + Vector3.up * 0.6f).normalized;
            b.Ico(Vector3.up * 1.25f + Vector3.Scale(d, new Vector3(1.1f, 0.6f, 1.1f)) * 0.98f, 0.16f, LP.Emissive(new Color(1f, 0.9f, 1f)), 0f, 0);
        }
        capMesh = b.ToMesh();
        return capMesh;
    }

    IEnumerator Start()
    {
        Vector3 p = transform.position;
        Lotus.I?.Pulse(new Color(0.75f, 0.4f, 0.9f));
        SFX.At(SFX.S.ShroomPlant, p);
        var caps = new List<Cap>();
        int count = 8 + 3 * Abilities.Lv("spore.count");
        for (int i = 0; i < count; i++)
        {
            // Inner ring of 7, then further mushrooms spill into an outer ring.
            bool outer = i >= 8;
            int ringN = outer ? count - 8 : 7;
            int ringI = outer ? i - 8 : i - 1;
            Vector3 cp = i == 0 ? p : p + LP.Dir(ringI * (360f / ringN) + (outer ? 25f : 0f) + Random.Range(-10f, 10f)) * (outer ? Random.Range(6f, 7.5f) : Random.Range(3f, 4.5f));
            cp = Ground(cp);
            var go = LP.Obj("Sporecap", CapMesh(), LP.Mat, transform);
            go.transform.position = cp;
            go.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.value * 360f, Random.Range(-8f, 8f));
            go.transform.localScale = Vector3.zero;
            caps.Add(new Cap { t = go.transform, born = Time.time + i * 0.05f, size = Random.Range(1f, 1.4f) });
            FX.Dirt(cp, 3, 4f);
        }
        float life = 25f, t0 = Time.time;
        while (true)
        {
            bool any = false;
            float now = Time.time;
            foreach (var c in caps)
            {
                if (c.done) continue;
                any = true;
                float age = now - c.born;
                if (age < 0f) continue;
                float s = LP.EaseOutBack(age / 0.35f) * c.size;
                if (c.trigger < 0f)
                {
                    s *= 1f + Mathf.Sin(now * 3f + c.size * 10f) * 0.04f;
                    if (age > 0.35f && (EnemyManager.Nearest(c.t.position, 2.6f, false) != null || now - t0 > life)) c.trigger = now;
                    if (c.trigger >= 0f && now - t0 <= life) SFX.At(SFX.S.Inflate, c.t.position, 0.8f, 0.15f);
                }
                if (c.trigger >= 0f)
                {
                    float k = (now - c.trigger) / 0.22f;
                    s = c.size * (1f + LP.EaseIn(k) * 0.6f);
                    var r = c.t.GetComponentInChildren<Renderer>();
                    var mpb = LP.MPB; r.GetPropertyBlock(mpb); mpb.SetColor("_Flash", new Color(0.8f, 0.4f, 1f) * k); r.SetPropertyBlock(mpb);
                    if (k >= 1f) { Explode(c, caps, now - t0 > life); continue; }
                }
                c.t.localScale = Vector3.one * Mathf.Max(0.01f, s);
            }
            if (!any) break;
            yield return null;
        }
        yield return new WaitForSeconds(0.5f);
        Destroy(gameObject);
    }

    void Explode(Cap c, List<Cap> caps, bool fizzle)
    {
        c.done = true;
        Vector3 p = c.t.position;
        Destroy(c.t.gameObject);
        if (fizzle) { FX.Puffs(p + Vector3.up, new Color(0.75f, 0.5f, 0.9f), 6, 2f); return; }
        FX.Puffs(p + Vector3.up, new Color(0.78f, 0.45f, 0.95f), 16, 7f, 1.4f, 1f, 1.1f);
        FX.Puffs(p + Vector3.up, new Color(0.65f, 0.9f, 0.4f), 6, 5f, 1f, 1f, 1f);
        FX.Shards(p + Vector3.up, new Color(0.72f, 0.3f, 0.85f), 14, 10f, 0.3f);
        FX.Sparks(p + Vector3.up, new Color(0.95f, 0.8f, 1f), 12, 10f);
        SFX.At(SFX.S.SporeBoom, p, 1f, 0.12f);
        GameCamera.Shake(0.15f);
        float boom = 1f + 0.25f * Abilities.Lv("spore.power");
        EnemyManager.Query(p, 4.2f * boom, buf);
        foreach (var e in buf.ToArray())
        {
            Vector3 away = LP.Flat(e.transform.position - p);
            away = away.sqrMagnitude > 0.01f ? away.normalized : LP.Dir(Random.value * 360f);
            e.Launch(Vector3.up * Random.Range(11f, 15f) + away * Random.Range(7f, 11f));
            e.Damage(8f * boom, p, Death.Bloom);
        }
        // Chain reaction.
        foreach (var o in caps)
            if (!o.done && o.trigger < 0f && (o.t.position - p).sqrMagnitude < 5.5f * 5.5f)
                o.trigger = Time.time + Random.Range(0.02f, 0.12f) - 0.0f;
    }
}

// ============================================================================ Elder Sapling
public class ElderSapling : Effect
{
    const float Life = 14f;
    static Mesh bodyMesh, legMesh;

    public static void Spawn(Vector3 p) => Make<ElderSapling>(Ground(p));

    public static void SpawnGroup(Vector3 p)
    {
        int extra = Abilities.Lv("sapling.count");
        Spawn(p);
        for (int i = 0; i < extra; i++) Spawn(p + LP.Dir(i * 180f + 90f) * 6f);
    }

    static void Meshes()
    {
        if (bodyMesh) return;
        var b = new MeshBuilder();
        Color bark = new Color(0.45f, 0.32f, 0.22f), leaf = new Color(0.35f, 0.66f, 0.28f), blossom = new Color(1f, 0.6f, 0.8f);
        b.Cylinder(new Vector3(0, 2f, 0), new Vector3(0, 5.5f, 0), 1.1f, 0.8f, 7, bark);
        b.Ico(new Vector3(0, 7f, 0), 2.6f, leaf, 0.2f, 0, 0.04f);
        b.Ico(new Vector3(1.6f, 6.3f, 0.3f), 1.7f, leaf * 1.1f, 0.2f, 0, 0.04f);
        b.Ico(new Vector3(-1.5f, 6.5f, -0.4f), 1.8f, leaf * 0.9f, 0.2f, 0, 0.04f);
        for (int i = 0; i < 9; i++) b.Ico(new Vector3(0, 7f, 0) + Random.onUnitSphere * 2.6f, 0.35f, blossom, 0f, 0);
        // Face: glowing eyes in the bark.
        b.Ico(new Vector3(-0.35f, 4.4f, 0.85f), 0.17f, LP.Emissive(new Color(0.7f, 1f, 0.5f)), 0f, 0);
        b.Ico(new Vector3(0.35f, 4.4f, 0.85f), 0.17f, LP.Emissive(new Color(0.7f, 1f, 0.5f)), 0f, 0);
        // Branch arms.
        b.Cylinder(new Vector3(-0.8f, 4.5f, 0), new Vector3(-2.6f, 3.2f, 0.6f), 0.35f, 0.15f, 5, bark);
        b.Cylinder(new Vector3(0.8f, 4.5f, 0), new Vector3(2.6f, 3.2f, 0.6f), 0.35f, 0.15f, 5, bark);
        b.Ico(new Vector3(-2.7f, 3.1f, 0.6f), 0.6f, leaf, 0.2f, 0);
        b.Ico(new Vector3(2.7f, 3.1f, 0.6f), 0.6f, leaf, 0.2f, 0);
        bodyMesh = b.ToMesh(); b.Clear();
        b.Cylinder(Vector3.zero, new Vector3(0, -2.1f, 0.2f), 0.55f, 0.4f, 6, bark * 0.9f);
        b.Cylinder(new Vector3(0, -2f, 0.2f), new Vector3(0, -2.2f, 0.9f), 0.4f, 0.1f, 5, bark * 0.8f);
        legMesh = b.ToMesh();
    }

    IEnumerator Start()
    {
        Meshes();
        Vector3 p = transform.position;
        Lotus.I?.Pulse(new Color(0.55f, 0.75f, 0.35f));
        FX.Dirt(p, 30, 9f);
        SFX.At(SFX.S.TreeRise, p, 1f, 0.03f);
        GameCamera.Shake(0.3f);

        var root = new GameObject("Treant").transform;
        root.SetParent(transform, false);
        root.position = p;
        var body = LP.Obj("Body", bodyMesh, LP.Mat, root).transform;
        var legs = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            legs[i] = new GameObject("Leg").transform;
            legs[i].SetParent(root, false);
            legs[i].localPosition = new Vector3(i == 0 ? -0.6f : 0.6f, 2.1f, 0);
            LP.Obj("LegMesh", legMesh, LP.Mat, legs[i]);
        }

        float t = 0f, stompTimer = 0.9f, walkT = 0f;
        Enemy target = null;
        float stompAnim = -1f;
        while (t < Life + 1f)
        {
            float dt = Time.deltaTime;
            t += dt;
            float grow = t < 0.7f ? LP.EaseOutBack(t / 0.7f) : t > Life ? 1f - (t - Life) : 1f;
            root.localScale = Vector3.one * Mathf.Max(0.01f, grow);
            if (t > Life) { root.position += Vector3.down * dt * 3f; yield return null; continue; }

            if (target == null || !target.Alive) target = EnemyManager.Nearest(root.position, 40f, false);
            Vector3 pos = root.position;
            bool moving = false;
            if (stompAnim < 0f && target != null && t > 0.7f)
            {
                Vector3 to = LP.Flat(target.transform.position - pos);
                if (to.magnitude > 2.5f)
                {
                    Vector3 step = to.normalized * 4.2f * dt;
                    Vector3 np = pos + step;
                    float r = LP.Flat(np).magnitude;
                    if (r > WorldBuilder.PitOut + 1.5f) { pos = np; moving = true; }
                }
                if (to.sqrMagnitude > 0.1f) root.rotation = Quaternion.Slerp(root.rotation, Quaternion.LookRotation(to), dt * 5f);
            }
            pos.y = WorldBuilder.Height(pos.x, pos.z);

            stompTimer -= dt;
            if (stompAnim < 0f && stompTimer <= 0f && t > 0.7f && EnemyManager.Nearest(pos, 5f, false) != null)
            {
                stompAnim = 0f;
            }
            float lift = 0f;
            if (stompAnim >= 0f)
            {
                stompAnim += dt;
                if (stompAnim < 0.3f) lift = LP.EaseOut(stompAnim / 0.3f) * 2.2f;
                else if (stompAnim < 0.4f) lift = (1f - (stompAnim - 0.3f) / 0.1f) * 2.2f;
                else
                {
                    Stomp(pos);
                    stompAnim = -1f;
                    stompTimer = 1.0f;
                }
            }
            if (moving)
            {
                // A heavy wooden footstep each time a foot comes down.
                float before = Mathf.Sin(walkT);
                walkT += dt * 7f;
                if (Mathf.Sign(before) != Mathf.Sign(Mathf.Sin(walkT))) SFX.At(SFX.S.Footstep, pos, 1f, 0.1f);
            }
            root.position = pos + Vector3.up * lift;
            body.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(walkT)) * 0.25f;
            body.localRotation = Quaternion.Euler(lift * -6f, 0, Mathf.Sin(walkT) * 4f);
            legs[0].localRotation = Quaternion.Euler(Mathf.Sin(walkT) * 30f, 0, 0);
            legs[1].localRotation = Quaternion.Euler(-Mathf.Sin(walkT) * 30f, 0, 0);
            if (moving && Mathf.Abs(Mathf.Sin(walkT)) < 0.1f) FX.Puffs(pos, new Color(0.6f, 0.5f, 0.38f), 1, 1f, 0.8f);
            yield return null;
        }
        FX.Leaves(p + Vector3.up * 3f, 20, 5f, 2f);
        Destroy(gameObject);
    }

    void Stomp(Vector3 pos)
    {
        FX.Dirt(pos, 24, 10f);
        FX.Leaves(pos + Vector3.up * 6f, 10, 5f, 2f);
        SFX.At(SFX.S.Stomp, pos, 1f, 0.08f);
        GameCamera.Shake(0.35f);
        StartCoroutine(Shockwave(pos));
        float might = 1f + 0.25f * Abilities.Lv("sapling.power");
        EnemyManager.Query(pos, 5f * might, buf);
        foreach (var e in buf.ToArray())
        {
            Vector3 away = LP.Flat(e.transform.position - pos);
            away = away.sqrMagnitude > 0.01f ? away.normalized : LP.Dir(Random.value * 360f);
            e.Launch(Vector3.up * Random.Range(10f, 14f) + away * Random.Range(8f, 12f));
            e.Damage(12f * might, pos, Death.Shatter);
        }
        for (int i = 0; i < 4; i++) BloomFlowers.SpawnRandom(pos + LP.Dir(Random.value * 360f) * Random.Range(1.5f, 4.5f), 1f);
    }

    IEnumerator Shockwave(Vector3 pos)
    {
        var r = Decal(LP.Ring, LP.Add, new Color(0.7f, 1f, 0.5f, 1f), pos, 1f, 0.1f);
        for (float t = 0; t < 0.4f; t += Time.deltaTime)
        {
            float k = t / 0.4f;
            r.transform.localScale = new Vector3(1f + k * 6f, 1f, 1f + k * 6f);
            LP.SetColor(r, new Color(0.7f, 1f, 0.5f, 1f - k));
            yield return null;
        }
        Destroy(r.gameObject);
    }
}

// ============================================================================ Grand Bloom
public class GrandBloom : Effect
{
    bool echo;

    public static void Spawn(Vector3 p) => Make<GrandBloom>(Vector3.zero);

    IEnumerator Start()
    {
        var lotus = Lotus.I;
        Color pink = new Color(1f, 0.45f, 0.72f);
        if (echo)
        {
            // Afterbloom: a quicker follow-up wave with no wind-up.
            lotus?.Bloom(1f);
            SFX.Play(SFX.S.Shockwave, 0.7f, 0.05f);
            GameCamera.Shake(0.4f);
            yield return Wave(lotus, new Color(1f, 0.7f, 0.88f), 1.6f);
            Destroy(gameObject);
            yield break;
        }
        SFX.Play(SFX.S.GrandChord, 1f, 0f);
        SFX.Play(SFX.S.Charge, 0.7f, 0f);
        lotus?.Bloom(1.4f);
        lotus?.Pulse(pink, 1f);

        // Wind-up: petals swirl into the flower.
        for (float t = 0; t < 0.6f; t += Time.deltaTime)
        {
            Vector3 core = lotus ? lotus.CorePos + Vector3.up : Vector3.up * 5f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 from = core + Random.onUnitSphere * 9f;
                FX.Spark(from, (core - from) * 1.8f, Color.Lerp(pink, Color.white, Random.value * 0.5f), 0.25f, 0.55f);
            }
            lotus?.SetCharge(pink, t / 0.6f);
            yield return null;
        }
        lotus?.SetCharge(pink, 0f);
        lotus?.Bloom(1.6f);
        GameCamera.Shake(0.9f);
        SFX.Play(SFX.S.Shockwave, 1f, 0f);
        FX.Petals((lotus ? lotus.CorePos : Vector3.up * 5f) + Vector3.up * 2f, pink, 160, 22f, 0.8f, 2f);
        int echoes = Abilities.Lv("bloom.echo");
        for (int i = 0; i < echoes; i++) StartCoroutine(Echo(1.3f + i * 1.3f));
        yield return Wave(lotus, pink, 2.2f);
        while (pending > 0) yield return null;
        yield return new WaitForSeconds(1f);
        Destroy(gameObject);
    }

    int pending;

    IEnumerator Echo(float delay)
    {
        pending++;
        yield return new WaitForSeconds(delay);
        var e = Make<GrandBloom>(Vector3.zero);
        e.echo = true;
        pending--;
    }

    IEnumerator Wave(Lotus lotus, Color pink, float dur)
    {
        var ring = Decal(LP.Ring, LP.Add, new Color(1f, 0.55f, 0.8f, 1f), Vector3.zero, 1f, 0.15f);
        var wall = LP.Obj("Wave", LP.Wall, LP.Add, transform, false);
        LP.SetColor(wall.GetComponent<Renderer>(), new Color(1f, 0.5f, 0.78f, 0.6f));
        var hit = new HashSet<Enemy>();
        float maxR = 150f;
        float lastR = WorldBuilder.PitOut;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float k = t / dur;
            float r = Mathf.Lerp(WorldBuilder.PitOut, maxR, LP.EaseOut(k));
            ring.transform.localScale = new Vector3(r, 1f, r);
            wall.transform.localScale = new Vector3(r, 4f * (1f - k * 0.5f), r);
            LP.SetColor(ring, new Color(1f, 0.55f, 0.8f, 1f - k * 0.6f));
            LP.SetColor(wall.GetComponent<Renderer>(), new Color(1f, 0.5f, 0.78f, 0.6f * (1f - k)));

            foreach (var e in EnemyManager.All.ToArray())
            {
                if (e == null || !e.Alive || hit.Contains(e)) continue;
                float d = LP.Flat(e.transform.position).magnitude;
                if (d < r)
                {
                    hit.Add(e);
                    Vector3 away = LP.Flat(e.transform.position).normalized;
                    e.Launch(Vector3.up * Random.Range(13f, 19f) + away * Random.Range(4f, 9f));
                    StartCoroutine(PopLater(e, Random.Range(0.35f, 0.8f)));
                }
            }
            // Meadow blooms behind the wave.
            int plants = Mathf.RoundToInt((r - lastR) * 1.2f);
            for (int i = 0; i < plants; i++)
            {
                float ang = Random.Range(-80f, 80f);
                Vector3 fp = LP.Dir(ang) * (r - Random.Range(0f, 4f));
                if (Mathf.Abs(fp.x) < WorldBuilder.FieldHalf + 4f && fp.z < WorldBuilder.SpawnZ) BloomFlowers.SpawnRandom(fp, 1.1f);
            }
            for (int i = 0; i < 6; i++)
            {
                Vector3 fp = LP.Dir(Random.Range(-70f, 70f)) * r;
                FX.Petals(fp + Vector3.up * 0.8f, pink, 1, 3f, 0.6f);
            }
            lastR = r;
            yield return null;
        }
        Destroy(ring.gameObject);
        Destroy(wall);
    }

    IEnumerator PopLater(Enemy e, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (e != null && e.Alive)
        {
            Vector3 pos = e.Center;
            e.Kill(Death.Petals);
            int fortune = Abilities.Lv("bloom.fortune");
            if (fortune > 0 && Game.I) Game.I.AddFlowers(fortune, pos);
        }
    }
}
