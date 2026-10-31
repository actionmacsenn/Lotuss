using UnityEngine;

// Global pooled particle systems. Everything is emitted manually with per-particle colour/velocity.
public class FX : MonoBehaviour
{
    public static FX I;

    ParticleSystem shards, petals, puffs, sparks;

    void Awake()
    {
        I = this;
        var plane = new GameObject("Particle Ground").transform;
        plane.SetParent(transform, false);

        shards = Make("Shards", LP.Shard, 2.2f, 4000, true, plane);
        petals = Make("Petals", LP.PetalFlake, 0.12f, 4000, false, null);
        var n = petals.noise;
        n.enabled = true; n.strength = 1.6f; n.frequency = 0.6f; n.scrollSpeed = 0.5f;
        puffs = Make("Puffs", LP.Ico(0.2f, 7), 0f, 2000, false, null);
        var lv = puffs.limitVelocityOverLifetime;
        lv.enabled = true; lv.dampen = 0.08f; lv.limit = 0.5f;
        var sz = puffs.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.3f), new Keyframe(0.25f, 1f), new Keyframe(1, 0f)));
        sparks = Make("Sparks", LP.Shard, 0.6f, 3000, false, null);
    }

    ParticleSystem Make(string name, Mesh mesh, float gravity, int max, bool collide, Transform plane)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity;
        main.startSize3D = false;
        main.startRotation3D = true;
        var em = ps.emission;
        em.rateOverTime = 0f;
        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1f), new Keyframe(0.7f, 0.85f), new Keyframe(1, 0f)));
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
        rot.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
        rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        if (collide)
        {
            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.Planes;
            col.SetPlane(0, plane);
            col.dampen = 0.45f;
            col.bounce = 0.35f;
            col.lifetimeLoss = 0f;
        }
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = mesh;
        r.sharedMaterial = LP.Particle;
        r.enableGPUInstancing = false;
        r.SetActiveVertexStreams(new System.Collections.Generic.List<ParticleSystemVertexStream>
            { ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal, ParticleSystemVertexStream.Color });
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.alignment = ParticleSystemRenderSpace.World;
        ps.Play();
        return ps;
    }

    static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, Color col, float life)
    {
        var ep = new ParticleSystem.EmitParams
        {
            position = pos,
            velocity = vel,
            startSize = size,
            startColor = col,
            startLifetime = life,
            rotation3D = new Vector3(Random.value * 360f, Random.value * 360f, Random.value * 360f),
        };
        ps.Emit(ep, 1);
    }

    // Chunky low-poly debris.
    public static void Shards(Vector3 pos, Color col, int count, float speed, float size = 0.35f, float up = 0.6f)
    {
        if (I == null) return;
        for (int i = 0; i < count; i++)
        {
            Vector3 v = Random.onUnitSphere; v.y = Mathf.Abs(v.y) + up;
            Emit(I.shards, pos + Random.insideUnitSphere * 0.3f, v.normalized * speed * Random.Range(0.4f, 1.1f),
                 size * Random.Range(0.5f, 1.3f), LP.Jitter(col, 0.08f), Random.Range(0.8f, 1.6f));
        }
    }

    // Fluttering flower petals.
    public static void Petals(Vector3 pos, Color col, int count, float speed, float size = 0.5f, float radius = 0.4f)
    {
        if (I == null) return;
        for (int i = 0; i < count; i++)
        {
            Vector3 v = Random.onUnitSphere; v.y = Mathf.Abs(v.y) * 1.5f + 0.3f;
            Color c = Color.Lerp(col, Color.white, Random.value * 0.35f);
            Emit(I.petals, pos + Random.insideUnitSphere * radius, v * speed * Random.Range(0.3f, 1f),
                 size * Random.Range(0.6f, 1.2f), c, Random.Range(1.6f, 3.2f));
        }
    }

    // Soft round puffs (dust, spores, pollen).
    public static void Puffs(Vector3 pos, Color col, int count, float speed, float size = 1f, float radius = 0.5f, float life = 1.2f)
    {
        if (I == null) return;
        for (int i = 0; i < count; i++)
        {
            Vector3 v = Random.onUnitSphere; v.y = Mathf.Abs(v.y) * 0.6f;
            Emit(I.puffs, pos + Random.insideUnitSphere * radius, v * speed * Random.Range(0.3f, 1f),
                 size * Random.Range(0.6f, 1.3f), LP.Jitter(col, 0.05f), life * Random.Range(0.7f, 1.3f));
        }
    }

    // Glowing sparks (alpha 0 makes the LowPoly shader render them emissive).
    public static void Sparks(Vector3 pos, Color col, int count, float speed, float size = 0.18f)
    {
        if (I == null) return;
        col.a = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector3 v = Random.onUnitSphere; v.y = Mathf.Abs(v.y) + 0.3f;
            Emit(I.sparks, pos, v.normalized * speed * Random.Range(0.3f, 1.1f), size * Random.Range(0.5f, 1.3f), col, Random.Range(0.4f, 0.9f));
        }
    }

    public static void Spark(Vector3 pos, Vector3 vel, Color col, float size, float life)
    {
        if (I == null) return;
        col.a = 0f;
        Emit(I.sparks, pos, vel, size, col, life);
    }

    public static void Dirt(Vector3 pos, int count, float speed = 7f)
    {
        Shards(pos, new Color(0.42f, 0.3f, 0.2f), count, speed, 0.3f, 0.9f);
        Puffs(pos, new Color(0.62f, 0.52f, 0.4f), count / 3 + 1, 2f, 1.2f, 0.8f, 0.9f);
    }

    public static void Leaves(Vector3 pos, int count, float speed, float radius = 0.5f)
    {
        Petals(pos, new Color(0.35f, 0.75f, 0.25f), count, speed, 0.55f, radius);
    }
}
