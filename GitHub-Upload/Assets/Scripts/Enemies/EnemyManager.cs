using System.Collections.Generic;
using UnityEngine;

// Runs the horde in numbered waves. Each wave streams a fixed number of enemies out of the treeline;
// once only a handful are left they light up pink so you can hunt them down. Clearing a wave pays out flowers.
public class EnemyManager : MonoBehaviour
{
    public static EnemyManager I;
    public static readonly List<Enemy> All = new List<Enemy>();

    public const int HighlightAt = 10;
    public int MaxAlive = 230;

    public bool Running { get; private set; }
    public int Wave { get; private set; }
    public int WaveSize { get; private set; }
    public int ToSpawn { get; private set; }
    public int Remaining => ToSpawn + All.Count;
    public bool InBreak => breakTimer > 0f;
    public float BreakLeft => breakTimer;
    public int LastBonus { get; private set; }
    public bool Stragglers => Running && !InBreak && ToSpawn == 0 && All.Count > 0 && All.Count <= HighlightAt;

    public event System.Action<int> WaveStarted;
    public event System.Action<int, int> WaveCleared;   // wave, bonus

    float spawnRate, spawnAcc, breakTimer;

    void Awake()
    {
        I = this;
        All.Clear();
    }

    public void Begin()
    {
        Running = true;
        Wave = 0;
        breakTimer = 1.5f;
    }

    public static void Remove(Enemy e) => All.Remove(e);

    public static void Query(Vector3 c, float r, List<Enemy> results, bool onlyGrabbable = false)
    {
        results.Clear();
        float r2 = r * r;
        for (int i = 0; i < All.Count; i++)
        {
            var e = All[i];
            if (e == null || !e.Alive) continue;
            if (onlyGrabbable && !e.Grabbable) continue;
            Vector3 d = e.transform.position - c; d.y = 0f;
            if (d.sqrMagnitude <= r2) results.Add(e);
        }
    }

    public static Enemy Nearest(Vector3 c, float maxR, bool onlyGrabbable = true)
    {
        Enemy best = null; float bd = maxR * maxR;
        foreach (var e in All)
        {
            if (e == null || !e.Alive || (onlyGrabbable && !e.Grabbable)) continue;
            Vector3 d = e.transform.position - c; d.y = 0f;
            float s = d.sqrMagnitude;
            if (s < bd) { bd = s; best = e; }
        }
        return best;
    }

    void NextWave()
    {
        Wave++;
        WaveSize = Mathf.Min(24 + Wave * 10 + Wave * Wave, 400);
        ToSpawn = WaveSize;
        spawnRate = Mathf.Min(2.2f + Wave * 0.55f, 13f);
        spawnAcc = 0f;
        SFX.Play(SFX.S.Surge, 1f, 0f);
        WaveStarted?.Invoke(Wave);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (Running)
        {
            if (breakTimer > 0f)
            {
                breakTimer -= dt;
                if (breakTimer <= 0f) NextWave();
            }
            else
            {
                if (ToSpawn > 0)
                {
                    spawnAcc += spawnRate * dt;
                    while (spawnAcc >= 1f && ToSpawn > 0 && All.Count < MaxAlive)
                    {
                        int group = Mathf.Min(Random.Range(2, 6), ToSpawn);
                        spawnAcc -= group;
                        SpawnGroup(group, Random.Range(-WorldBuilder.FieldHalf + 3f, WorldBuilder.FieldHalf - 3f));
                    }
                }
                else if (All.Count == 0)
                {
                    // Wave cleared.
                    LastBonus = 15 + Wave * 6;
                    Save.Flowers += LastBonus;
                    Save.BestWave = Wave;
                    Save.Flush();
                    SFX.Play(SFX.S.Combo, 1f, 0f);
                    SFX.Play(SFX.S.Bloom, 1f, 0f);
                    WaveCleared?.Invoke(Wave, LastBonus);
                    breakTimer = 5f;
                }
            }
        }

        bool highlight = Stragglers;
        for (int i = 0; i < All.Count; i++) if (All[i]) All[i].Highlight = highlight;

        Separate();
    }

    public void SpawnGroup(int count, float cx)
    {
        for (int i = 0; i < count; i++)
        {
            float roll = Random.value;
            float bruteChance = Mathf.Min(0.03f + Wave * 0.012f, 0.16f);
            float miteChance = 0.5f;
            EnemyKind k = roll < bruteChance ? EnemyKind.Brute : roll < miteChance ? EnemyKind.Husk : EnemyKind.Mite;
            Vector3 p = new Vector3(cx + Random.Range(-4f, 4f), 0f, WorldBuilder.SpawnZ + Random.Range(0f, 9f));
            Spawn(k, p);
            if (ToSpawn > 0) ToSpawn--;
        }
    }

    static readonly Color[] miteCols = { new Color(0.3f, 0.18f, 0.36f), new Color(0.22f, 0.16f, 0.3f), new Color(0.36f, 0.2f, 0.3f) };
    static readonly Color[] huskCols = { new Color(0.47f, 0.43f, 0.4f), new Color(0.42f, 0.4f, 0.44f), new Color(0.5f, 0.42f, 0.36f) };

    public Enemy Spawn(EnemyKind k, Vector3 p)
    {
        var go = new GameObject(k.ToString());
        go.transform.SetParent(transform, false);
        p.y = WorldBuilder.Height(p.x, p.z);
        go.transform.position = p;
        go.transform.rotation = Quaternion.LookRotation(LP.Flat(-p));
        var e = go.AddComponent<Enemy>();
        Color col = k == EnemyKind.Mite ? miteCols[Random.Range(0, 3)] : k == EnemyKind.Husk ? huskCols[Random.Range(0, 3)] : new Color(0.36f, 0.24f, 0.26f);
        e.Init(k, col);
        // Later waves bring tougher blight.
        e.maxHp *= 1f + Mathf.Max(0, Wave - 1) * 0.08f;
        e.hp = e.maxHp;
        All.Add(e);
        return e;
    }

    // Debug/demo: cut the current wave short so only 'keep' enemies remain.
    public void Thin(int keep)
    {
        ToSpawn = 0;
        for (int i = All.Count - 1; i >= keep; i--) if (All[i]) All[i].Kill(Death.Bloom);
    }

    // Simple O(n^2) separation - fine for a couple hundred creatures.
    void Separate()
    {
        int n = All.Count;
        for (int i = 0; i < n; i++) if (All[i]) All[i].sep = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            var a = All[i];
            if (a == null || a.state != EState.Walk) continue;
            Vector3 pa = a.transform.position;
            for (int j = i + 1; j < n; j++)
            {
                var b = All[j];
                if (b == null || b.state != EState.Walk) continue;
                Vector3 d = pa - b.transform.position; d.y = 0f;
                float rr = a.radius + b.radius;
                float s = d.sqrMagnitude;
                if (s < rr * rr && s > 0.0001f)
                {
                    float dist = Mathf.Sqrt(s);
                    Vector3 f = d / dist * (rr - dist) * 4f;
                    a.sep += f; b.sep -= f;
                }
            }
        }
    }
}
