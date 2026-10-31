using UnityEngine;

public enum EnemyKind { Mite, Husk, Brute }
public enum EState { Walk, Air, Held, Falling, Dead }
public enum Death { Bloom, Shatter, Swallow, Pit, Sun, Petals }

// A blight creature marching on the lotus. Movement is fully scripted (no rigidbodies) so abilities
// can grab, fling and drag them around freely.
public class Enemy : MonoBehaviour
{
    public EnemyKind kind;
    public EState state = EState.Walk;
    public float hp, maxHp, speed, radius, height;
    public Vector3 vel;            // used while airborne / falling
    public Vector3 push;           // external shove while walking
    public Vector3 sep;            // separation from neighbours (EnemyManager)
    public Color bodyColor;

    Transform body;
    Transform[] legs;
    Transform[] arms;
    Renderer[] rends;
    float animT, seed, flash, slowTimer, slowMul = 1f, tintTimer;
    Color tint = Color.white;
    Vector3 spin;
    bool propsDirty = true;

    // Stragglers at the end of a wave glow pink and get a floating marker.
    bool highlight;
    Transform marker;
    public bool Highlight
    {
        get => highlight;
        set
        {
            if (highlight == value) return;
            highlight = value;
            propsDirty = true;
            if (value && marker == null)
            {
                marker = LP.Obj("Marker", MarkerMesh, LP.Mat, transform, false).transform;
                marker.localPosition = Vector3.up * (height + 1.2f);
            }
            if (marker) marker.gameObject.SetActive(value);
        }
    }

    static Mesh markerMesh;
    static Mesh MarkerMesh
    {
        get
        {
            if (markerMesh) return markerMesh;
            var b = new MeshBuilder();
            Color pink = LP.Emissive(new Color(1f, 0.35f, 0.75f));
            Vector3 top = Vector3.up * 0.9f, bot = Vector3.down * 0.9f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = LP.Dir(i * 90f) * 0.5f, c = LP.Dir(i * 90f + 90f) * 0.5f;
                b.TriOut(top, a, c, pink, Vector3.zero);
                b.TriOut(bot, a, c, pink, Vector3.zero);
            }
            markerMesh = b.ToMesh();
            return markerMesh;
        }
    }

    public int Reward => kind == EnemyKind.Brute ? 8 : kind == EnemyKind.Husk ? 2 : 1;

    public bool Alive => state != EState.Dead && this != null;
    public bool Grabbable => state == EState.Walk || state == EState.Air;
    public Vector3 Center => transform.position + Vector3.up * height * 0.5f;

    public void Init(EnemyKind k, Color col)
    {
        kind = k;
        bodyColor = col;
        seed = Random.value * 100f;
        switch (k)
        {
            case EnemyKind.Mite: maxHp = 2f; speed = Random.Range(5f, 6.5f); radius = 0.6f; height = 0.8f; break;
            case EnemyKind.Husk: maxHp = 6f; speed = Random.Range(3.2f, 4f); radius = 0.8f; height = 2.3f; break;
            default: maxHp = 30f; speed = Random.Range(2f, 2.5f); radius = 1.6f; height = 4.2f; break;
        }
        hp = maxHp;
        EnemyParts.Build(this, out body, out legs, out arms);
        rends = GetComponentsInChildren<Renderer>();
        tint = Color.white;
        propsDirty = true;
    }

    // ------------------------------------------------------------- interaction API

    public void Damage(float amount, Vector3 from, Death style = Death.Bloom, float knock = 0f)
    {
        if (state == EState.Dead) return;
        hp -= amount;
        flash = 1f;
        // Only discrete blows get a thwack; damage-over-time would spam it.
        if (amount >= 1f && hp > 0f) SFX.At(SFX.S.Hit, Center, kind == EnemyKind.Brute ? 1f : 0.7f, 0.15f);
        propsDirty = true;
        if (knock > 0f && state == EState.Walk)
        {
            Vector3 d = LP.Flat(transform.position - from).normalized;
            push += d * knock;
        }
        if (hp <= 0f) Kill(style);
    }

    public void Launch(Vector3 v)
    {
        if (state == EState.Dead || state == EState.Held || state == EState.Falling) return;
        // Heavies fly less far.
        float m = kind == EnemyKind.Brute ? 0.45f : kind == EnemyKind.Husk ? 0.85f : 1f;
        vel = v * m;
        state = EState.Air;
        spin = Random.insideUnitSphere * 720f;
    }

    public bool TryHold()
    {
        if (!Grabbable) return false;
        state = EState.Held;
        push = Vector3.zero;
        return true;
    }

    public void Release(Vector3 v)
    {
        if (state != EState.Held) return;
        state = EState.Air;
        vel = v;
        spin = Random.insideUnitSphere * 540f;
    }

    public void Slow(float mul, float time)
    {
        slowMul = Mathf.Min(slowMul, mul);
        slowTimer = Mathf.Max(slowTimer, time);
    }

    public void Tint(Color c, float time)
    {
        tint = c;
        tintTimer = time;
        propsDirty = true;
    }

    SFX.S DeathSound => kind == EnemyKind.Brute ? SFX.S.BruteDie : kind == EnemyKind.Husk ? SFX.S.HuskDie : SFX.S.MiteDie;

    public void Kill(Death style)
    {
        if (state == EState.Dead) return;
        state = EState.Dead;
        Vector3 c = Center;
        float s = kind == EnemyKind.Brute ? 2f : kind == EnemyKind.Husk ? 1.2f : 0.7f;
        int n = Mathf.RoundToInt(10 * s);
        switch (style)
        {
            case Death.Bloom:
                FX.Shards(c, bodyColor, n, 7f, 0.3f * s);
                FX.Petals(c, new Color(1f, 0.55f, 0.78f), Mathf.RoundToInt(14 * s), 5f);
                BloomFlowers.SpawnRandom(transform.position, 0.9f + s * 0.4f);
                if (kind != EnemyKind.Mite) BloomFlowers.SpawnRandom(transform.position + Random.insideUnitSphere * 1.2f, 0.8f + s * 0.2f);
                SFX.At(DeathSound, c);
                SFX.At(SFX.S.Bloom, c, 0.6f, 0.1f);
                break;
            case Death.Petals:
                FX.Petals(c, new Color(1f, 0.5f, 0.75f), Mathf.RoundToInt(26 * s), 8f, 0.6f);
                FX.Shards(c, bodyColor, n / 2, 6f, 0.25f * s);
                SFX.At(DeathSound, c);
                SFX.At(SFX.S.Bloom, c, 0.8f, 0.15f);
                break;
            case Death.Sun:
                FX.Sparks(c, new Color(1f, 0.85f, 0.3f), Mathf.RoundToInt(16 * s), 9f);
                FX.Shards(c, new Color(0.15f, 0.12f, 0.1f), n, 6f, 0.28f * s);
                BloomFlowers.Spawn(transform.position, BloomFlowers.Kind.Sun, 1f + s * 0.3f);
                SFX.At(DeathSound, c, 0.8f);
                SFX.At(SFX.S.Sizzle, c, 0.8f);
                break;
            case Death.Shatter:
                FX.Shards(c, bodyColor, n * 2, 10f, 0.35f * s);
                FX.Puffs(c, new Color(0.5f, 0.85f, 0.35f), 4, 3f, 0.8f * s);
                SFX.At(DeathSound, c);
                SFX.At(SFX.S.Hit, c, 0.8f);
                break;
            case Death.Pit:
                FX.Shards(c + Vector3.up, bodyColor, n, 9f, 0.3f * s, 2f);
                FX.Shards(c + Vector3.up, new Color(0.7f, 0.15f, 0.25f), n / 2, 8f, 0.2f * s, 2f);
                SFX.At(SFX.S.PitDeath, c, kind == EnemyKind.Brute ? 1f : 0.75f, 0.12f);
                break;
            case Death.Swallow:
                SFX.At(SFX.S.Swallow, c);
                break;
        }
        if (Game.I) { Game.I.OnKill(); Game.I.AddFlowers(Reward, c); }
        EnemyManager.Remove(this);
        Destroy(gameObject);
    }

    // ------------------------------------------------------------- simulation

    void Update()
    {
        float dt = Time.deltaTime;
        if (state == EState.Dead) return;
        Vector3 pos = transform.position;

        if (slowTimer > 0f) { slowTimer -= dt; if (slowTimer <= 0f) slowMul = 1f; }
        if (tintTimer > 0f) { tintTimer -= dt; if (tintTimer <= 0f) { tint = Color.white; propsDirty = true; } }

        float animRate = 1f;
        switch (state)
        {
            case EState.Walk:
            {
                Vector3 to = -LP.Flat(pos);
                float d = to.magnitude;
                Vector3 dir = d > 0.01f ? to / d : Vector3.forward;
                float wander = Mathf.Sin(Time.time * 0.9f + seed) * 22f * Mathf.Clamp01((d - 16f) / 20f);
                dir = Quaternion.Euler(0f, wander, 0f) * dir;
                float spd = speed * slowMul;
                Vector3 move = dir * spd + push + sep;
                pos += move * dt;
                push *= Mathf.Exp(-3.5f * dt);
                pos.y = WorldBuilder.Height(pos.x, pos.z);
                if (move.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(LP.Flat(move)), dt * 8f);
                animRate = spd / 3.5f;

                float r = LP.Flat(pos).magnitude;
                if (r < WorldBuilder.PitOut - radius * 0.4f && r > WorldBuilder.PitIn - 0.5f)
                {
                    state = EState.Falling;
                    vel = dir * spd * 0.6f + Vector3.up * 1.5f;
                    spin = new Vector3(Random.Range(200f, 500f), 0f, Random.Range(-200f, 200f));
                }
                else if (r <= WorldBuilder.PitIn - 0.5f)
                {
                    Kill(Death.Bloom); // the lotus devours anything that reaches its island
                    return;
                }
                break;
            }
            case EState.Air:
            {
                vel.y -= 26f * dt;
                pos += vel * dt;
                transform.Rotate(spin * dt, Space.World);
                animRate = 4f;
                float r = LP.Flat(pos).magnitude;
                bool overPit = r > WorldBuilder.PitIn && r < WorldBuilder.PitOut;
                if (overPit && pos.y < 0.5f)
                {
                    state = EState.Falling;
                    break;
                }
                float ground = WorldBuilder.Height(pos.x, pos.z);
                if (pos.y <= ground && vel.y < 0f)
                {
                    pos.y = ground;
                    float impact = -vel.y;
                    push = LP.Flat(vel) * 0.25f;
                    vel = Vector3.zero;
                    state = EState.Walk;
                    transform.rotation = Quaternion.LookRotation(LP.Flat(-pos).sqrMagnitude > 0.01f ? LP.Flat(-pos) : Vector3.forward);
                    FX.Puffs(pos, new Color(0.62f, 0.55f, 0.42f), 3, 2f, 0.7f, 0.4f, 0.6f);
                    SFX.At(SFX.S.Land, pos, Mathf.Clamp01(impact / 16f) * (kind == EnemyKind.Brute ? 1f : 0.7f), 0.15f);
                    if (impact > 13f) { Damage(impact * 0.25f, pos, Death.Shatter); if (state == EState.Dead) return; }
                }
                if (Mathf.Abs(pos.x) > 120f || pos.z > 220f || pos.z < -60f) { Kill(Death.Shatter); return; }
                break;
            }
            case EState.Falling:
            {
                vel.y -= 26f * dt;
                vel.x *= Mathf.Exp(-2f * dt); vel.z *= Mathf.Exp(-2f * dt);
                pos += vel * dt;
                // Keep inside the pit ring.
                Vector3 f = LP.Flat(pos);
                float r = f.magnitude;
                float clamped = Mathf.Clamp(r, WorldBuilder.PitIn + radius * 0.5f, WorldBuilder.PitOut - radius * 0.5f);
                if (r > 0.01f && pos.y < 0f) { f = f / r * clamped; pos.x = f.x; pos.z = f.z; }
                transform.Rotate(spin * dt, Space.World);
                animRate = 5f;
                if (pos.y < -WorldBuilder.PitDepth + 1.2f) { transform.position = pos; Kill(Death.Pit); return; }
                break;
            }
            case EState.Held:
                animRate = 5f;
                break;
        }
        if (state != EState.Held) transform.position = pos;
        Animate(dt, animRate);
    }

    void Animate(float dt, float rate)
    {
        animT += dt * 9f * rate;
        bool flail = state != EState.Walk;
        float swing = flail ? 55f : 32f;
        if (legs != null)
            for (int i = 0; i < legs.Length; i++)
                legs[i].localRotation = Quaternion.Euler(Mathf.Sin(animT + i * Mathf.PI + (flail ? i : 0)) * swing, 0f, 0f);
        if (arms != null)
            for (int i = 0; i < arms.Length; i++)
                arms[i].localRotation = flail
                    ? Quaternion.Euler(-120f + Mathf.Sin(animT * 1.7f + i * 2f) * 50f, 0f, (i == 0 ? -1 : 1) * 30f)
                    : Quaternion.Euler(Mathf.Sin(animT + i * Mathf.PI + Mathf.PI) * 28f, 0f, (i == 0 ? -1 : 1) * 8f);
        if (body)
        {
            float bob = Mathf.Abs(Mathf.Sin(animT)) * 0.12f * height * 0.3f;
            body.localPosition = new Vector3(0f, bob, 0f);
            body.localRotation = Quaternion.Euler(flail ? Mathf.Sin(animT * 1.3f) * 15f : 6f, 0f, Mathf.Sin(animT * 0.5f) * 5f);
        }

        if (flash > 0f) { flash = Mathf.MoveTowards(flash, 0f, dt * 7f); propsDirty = true; }
        if (highlight)
        {
            propsDirty = true;
            if (marker)
            {
                marker.localPosition = Vector3.up * (height + 1.3f + Mathf.Sin(Time.time * 4f) * 0.3f);
                marker.rotation = Quaternion.Euler(0f, Time.time * 180f, 0f);
                marker.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 8f) * 0.15f) * (kind == EnemyKind.Brute ? 1.6f : 1f) / transform.localScale.x;
            }
        }
        if (propsDirty)
        {
            propsDirty = flash > 0f || highlight;
            var mpb = LP.MPB;
            Color f = new Color(1f, 0.85f, 0.9f) * (flash * 0.55f);
            if (highlight) f += new Color(1f, 0.25f, 0.65f) * (0.45f + 0.3f * Mathf.Sin(Time.time * 8f));
            Color tnt = highlight ? Color.Lerp(tint, new Color(1f, 0.55f, 0.8f), 0.6f) : tint;
            foreach (var r in rends)
            {
                if (!r) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", tnt);
                mpb.SetColor("_Flash", f);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}

// Builds the low-poly creature bodies. Meshes are shared per kind.
public static class EnemyParts
{
    static Mesh[] bodyM = new Mesh[3], legM = new Mesh[3], armM = new Mesh[3];

    public static void Build(Enemy e, out Transform body, out Transform[] legs, out Transform[] arms)
    {
        int k = (int)e.kind;
        if (bodyM[k] == null) MakeMeshes(e.kind);
        body = new GameObject("Body").transform;
        body.SetParent(e.transform, false);
        LP.Obj("BodyMesh", bodyM[k], LP.Mat, body);

        switch (e.kind)
        {
            case EnemyKind.Mite:
                legs = new Transform[4];
                for (int i = 0; i < 4; i++)
                    legs[i] = Limb(legM[k], e.transform, new Vector3(i % 2 == 0 ? -0.35f : 0.35f, 0.32f, i < 2 ? 0.2f : -0.2f));
                arms = null;
                break;
            case EnemyKind.Husk:
                legs = new[] { Limb(legM[k], e.transform, new Vector3(-0.3f, 0.75f, 0f)), Limb(legM[k], e.transform, new Vector3(0.3f, 0.75f, 0f)) };
                arms = new[] { Limb(armM[k], body, new Vector3(-0.7f, 1.6f, 0.1f)), Limb(armM[k], body, new Vector3(0.7f, 1.6f, 0.1f)) };
                break;
            default:
                legs = new[] { Limb(legM[k], e.transform, new Vector3(-0.7f, 1.4f, 0f)), Limb(legM[k], e.transform, new Vector3(0.7f, 1.4f, 0f)) };
                arms = new[] { Limb(armM[k], body, new Vector3(-1.6f, 3.0f, 0.3f)), Limb(armM[k], body, new Vector3(1.6f, 3.0f, 0.3f)) };
                break;
        }
    }

    static Transform Limb(Mesh m, Transform parent, Vector3 pos)
    {
        var t = new GameObject("Limb").transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        LP.Obj("LimbMesh", m, LP.Mat, t);
        return t;
    }

    static void MakeMeshes(EnemyKind kind)
    {
        int k = (int)kind;
        var b = new MeshBuilder();
        Color white = Color.white;
        switch (kind)
        {
            case EnemyKind.Mite:
            {
                // Low beetle-like blight mite with a glowing back.
                Color shell = new Color(0.3f, 0.18f, 0.36f);
                b.Ico(new Vector3(0, 0.45f, -0.05f), 0.5f, shell, 0.12f, 0, 0.03f, new Vector3(1f, 0.6f, 1.25f));
                b.Ico(new Vector3(0, 0.62f, -0.1f), 0.32f, LP.Emissive(new Color(0.75f, 0.3f, 1f)), 0.2f, 0, 0f, new Vector3(1f, 0.5f, 1.3f));
                b.Ico(new Vector3(0, 0.45f, 0.55f), 0.27f, shell * 0.8f, 0.1f, 0);
                b.Ico(new Vector3(-0.12f, 0.52f, 0.76f), 0.07f, LP.Emissive(new Color(1f, 0.25f, 0.2f)), 0f, 0);
                b.Ico(new Vector3(0.12f, 0.52f, 0.76f), 0.07f, LP.Emissive(new Color(1f, 0.25f, 0.2f)), 0f, 0);
                b.Cylinder(new Vector3(-0.1f, 0.4f, 0.75f), new Vector3(-0.18f, 0.3f, 1.05f), 0.05f, 0f, 3, shell * 0.6f, null, false);
                b.Cylinder(new Vector3(0.1f, 0.4f, 0.75f), new Vector3(0.18f, 0.3f, 1.05f), 0.05f, 0f, 3, shell * 0.6f, null, false);
                bodyM[k] = b.ToMesh(); b.Clear();
                b.Cylinder(Vector3.zero, new Vector3(0, -0.32f, 0.05f), 0.07f, 0.04f, 3, shell * 0.6f);
                legM[k] = b.ToMesh();
                break;
            }
            case EnemyKind.Husk:
            {
                // Hunched rot-husk: lumpy grey-brown torso, small head, ember eyes.
                Color skin = new Color(0.47f, 0.43f, 0.4f);
                Color dark = new Color(0.32f, 0.28f, 0.27f);
                b.Ico(new Vector3(0, 1.35f, 0), 0.72f, skin, 0.2f, 0, 0.04f, new Vector3(1f, 1.05f, 0.85f));
                b.Ico(new Vector3(0, 1.75f, -0.25f), 0.5f, dark, 0.25f, 0, 0.03f);
                b.Ico(new Vector3(0, 2.05f, 0.35f), 0.42f, skin * 1.05f, 0.15f, 0, 0.03f);
                b.Ico(new Vector3(-0.15f, 2.1f, 0.7f), 0.09f, LP.Emissive(new Color(1f, 0.55f, 0.15f)), 0f, 0);
                b.Ico(new Vector3(0.15f, 2.1f, 0.7f), 0.09f, LP.Emissive(new Color(1f, 0.55f, 0.15f)), 0f, 0);
                b.Box(new Vector3(0, 1.88f, 0.68f), new Vector3(0.3f, 0.06f, 0.1f), new Color(0.15f, 0.1f, 0.1f));
                // Blight growths on the back.
                b.Cylinder(new Vector3(-0.2f, 1.9f, -0.5f), new Vector3(-0.35f, 2.4f, -0.8f), 0.12f, 0f, 4, new Color(0.45f, 0.25f, 0.5f));
                b.Cylinder(new Vector3(0.2f, 1.8f, -0.5f), new Vector3(0.4f, 2.25f, -0.75f), 0.1f, 0f, 4, new Color(0.45f, 0.25f, 0.5f));
                bodyM[k] = b.ToMesh(); b.Clear();
                b.Cylinder(Vector3.zero, new Vector3(0, -0.75f, 0), 0.17f, 0.13f, 5, dark);
                b.Box(new Vector3(0, -0.72f, 0.1f), new Vector3(0.26f, 0.12f, 0.4f), dark * 0.8f);
                legM[k] = b.ToMesh(); b.Clear();
                b.Cylinder(Vector3.zero, new Vector3(0, -1.0f, 0.15f), 0.14f, 0.1f, 5, skin * 0.95f);
                b.Ico(new Vector3(0, -1.05f, 0.18f), 0.16f, skin * 0.85f, 0.2f, 0);
                armM[k] = b.ToMesh();
                break;
            }
            default:
            {
                // Hulking blight brute with horns and a glowing chest crack.
                Color hide = new Color(0.36f, 0.24f, 0.26f);
                Color plate = new Color(0.24f, 0.2f, 0.22f);
                b.Ico(new Vector3(0, 2.6f, 0), 1.45f, hide, 0.18f, 1, 0.03f, new Vector3(1.1f, 1f, 0.9f));
                b.Ico(new Vector3(0, 3.3f, -0.6f), 1.1f, plate, 0.2f, 0, 0.03f);
                b.Ico(new Vector3(0, 3.5f, 0.95f), 0.6f, hide * 1.1f, 0.15f, 0, 0.03f);
                b.Ico(new Vector3(-0.25f, 3.6f, 1.45f), 0.12f, LP.Emissive(new Color(1f, 0.9f, 0.25f)), 0f, 0);
                b.Ico(new Vector3(0.25f, 3.6f, 1.45f), 0.12f, LP.Emissive(new Color(1f, 0.9f, 0.25f)), 0f, 0);
                b.Cylinder(new Vector3(-0.45f, 3.85f, 1f), new Vector3(-1.1f, 4.6f, 0.9f), 0.2f, 0f, 4, new Color(0.85f, 0.8f, 0.7f));
                b.Cylinder(new Vector3(0.45f, 3.85f, 1f), new Vector3(1.1f, 4.6f, 0.9f), 0.2f, 0f, 4, new Color(0.85f, 0.8f, 0.7f));
                b.Box(new Vector3(0, 2.6f, 1.3f), new Vector3(0.22f, 0.9f, 0.1f), LP.Emissive(new Color(1f, 0.4f, 0.15f)));
                for (int i = 0; i < 5; i++)
                    b.Cylinder(new Vector3(Random.Range(-0.8f, 0.8f), 3.6f, -1f + Random.Range(-0.3f, 0.3f)), new Vector3(Random.Range(-1f, 1f), 4.4f, -1.5f), 0.18f, 0f, 4, new Color(0.45f, 0.25f, 0.5f));
                bodyM[k] = b.ToMesh(); b.Clear();
                b.Cylinder(Vector3.zero, new Vector3(0, -1.4f, 0), 0.4f, 0.32f, 6, plate);
                b.Box(new Vector3(0, -1.35f, 0.2f), new Vector3(0.6f, 0.2f, 0.8f), plate * 0.8f);
                legM[k] = b.ToMesh(); b.Clear();
                b.Cylinder(Vector3.zero, new Vector3(0, -1.9f, 0.25f), 0.35f, 0.3f, 6, hide);
                b.Ico(new Vector3(0, -2.0f, 0.3f), 0.48f, plate, 0.2f, 0);
                armM[k] = b.ToMesh();
                break;
            }
        }
    }
}
