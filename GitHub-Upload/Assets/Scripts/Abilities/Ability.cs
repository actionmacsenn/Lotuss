using System.Collections.Generic;
using UnityEngine;

public enum AimShape { Circle, Line, Global }

public class UpgradeDef
{
    public string key, name, desc;
    public int max = 5, baseCost = 30;
    public int Level => Save.Level(key);
    public bool Maxed => Level >= max;
    public int NextCost => Mathf.RoundToInt(baseCost * Mathf.Pow(1.65f, Level));
}

public class AbilityDef
{
    public string key, name, shortName, desc;
    public Color color;
    public float cooldown, radius;
    public AimShape shape;
    public System.Action<Vector3> cast;
    public string sizeKey;          // upgrade that scales the aim radius
    public float sizeStep;
    public UpgradeDef[] upgrades;

    public float Cooldown => cooldown * (1f - 0.09f * Save.Level(key + ".cd"));
    public float Radius => radius * (1f + (sizeKey != null ? sizeStep * Save.Level(sizeKey) : 0f));
}

// The full plant arsenal. Six per wheel page. Every ability has a cooldown upgrade plus two of its own.
public static class Abilities
{
    public const int PerPage = 6;

    public static int Lv(string key) => Save.Level(key);

    static UpgradeDef U(string key, string name, string desc, int max = 5, int cost = 30)
        => new UpgradeDef { key = key, name = name, desc = desc, max = max, baseCost = cost };

    static AbilityDef A(string key, string name, string shortName, Color color, float cd, float radius, AimShape shape,
                        string desc, System.Action<Vector3> cast, string sizeKey, float sizeStep, params UpgradeDef[] specific)
    {
        var ups = new List<UpgradeDef> { U(key + ".cd", "Quick Regrowth", "Cooldown 9% shorter per level.", 5, 20) };
        ups.AddRange(specific);
        return new AbilityDef
        {
            key = key, name = name, shortName = shortName, color = color, cooldown = cd, radius = radius, shape = shape,
            desc = desc, cast = cast, sizeKey = sizeKey, sizeStep = sizeStep, upgrades = ups.ToArray(),
        };
    }

    public static readonly List<AbilityDef> All = new List<AbilityDef>
    {
        // ---------------- page 1
        A("root", "Root Abyss", "Root\nAbyss", new Color(0.35f, 0.95f, 0.4f), 5f, 8f, AimShape.Circle,
          "Marks a glowing green zone. A pit tears open in the middle and a mass of vines bursts out, seizes everything inside and drags them down into the dark.",
          p => RootAbyss.Spawn(p), "root.size", 0.12f,
          U("root.size", "Wider Abyss", "Zone radius +12% per level."),
          U("root.hunger", "Hungry Roots", "Vines keep grabbing 0.4s longer and drag 20% faster per level.")),
        A("bramble", "Bramble Rupture", "Bramble\nRupture", new Color(0.75f, 0.45f, 0.3f), 2.5f, 2.2f, AimShape.Line,
          "A ripple of giant thorns erupts from the earth in a line toward the cursor, skewering and launching anything in its path.",
          p => BrambleRupture.Spawn(p), null, 0f,
          U("bramble.lines", "Forked Rupture", "Each level adds another thorn line fanning out to the side.", 2, 90),
          U("bramble.power", "Wicked Thorns", "Thorns hit 40% harder and launch higher per level.")),
        A("lash", "Vine Lash", "Vine\nLash", new Color(0.3f, 0.8f, 0.35f), 2f, 28f, AimShape.Line,
          "One colossal vine whips across the field in a huge arc, swatting whole crowds into the sky.",
          p => VineLash.Spawn(p), null, 0f,
          U("lash.twin", "Twin Lash", "A second vine sweeps from the opposite side at the same time.", 1, 140),
          U("lash.len", "Long Reach", "Vine length +15% per level.")),
        A("cyclone", "Petal Cyclone", "Petal\nCyclone", new Color(1f, 0.5f, 0.78f), 8f, 11f, AimShape.Circle,
          "A roaring tornado of razor petals. Pulls enemies in, spins them up into the funnel and shreds them into blossoms.",
          p => PetalCyclone.Spawn(p), "cyclone.size", 0.15f,
          U("cyclone.size", "Bigger Storm", "Pull radius +15% per level."),
          U("cyclone.time", "Long Gale", "Lasts 1.5s longer per level.")),
        A("snap", "Snapjaw", "Snap\njaw", new Color(0.55f, 0.85f, 0.25f), 7f, 10f, AimShape.Circle,
          "Grows a giant carnivorous plant that lunges at anything nearby and swallows it whole. Keeps eating until it wilts.",
          p => Snapjaw.SpawnGroup(p), null, 0f,
          U("snap.count", "Snapjaw Patch", "Summons one more Snapjaw per level.", 3, 80),
          U("snap.speed", "Ravenous", "Lunges and chews 25% faster and lives 2s longer per level.")),
        A("seed", "Seed Barrage", "Seed\nBarrage", new Color(0.85f, 0.75f, 0.35f), 3f, 5.5f, AimShape.Circle,
          "The lotus spits a volley of explosive seeds. Each one bursts into a blast of dirt and sprouts on impact.",
          p => SeedBarrage.Spawn(p), null, 0f,
          U("seed.count", "Seed Storm", "+6 seeds per volley per level."),
          U("seed.power", "Volatile Seeds", "Blast radius +20% and damage +30% per level.")),
        // ---------------- page 2
        A("pollen", "Pollen Haze", "Pollen\nHaze", new Color(1f, 0.88f, 0.3f), 9f, 9f, AimShape.Circle,
          "A thick golden cloud. Enemies inside slow to a crawl, choke on pollen and sprout into flowers.",
          p => PollenHaze.Spawn(p), "pollen.size", 0.15f,
          U("pollen.size", "Wide Cloud", "Cloud radius +15% per level."),
          U("pollen.power", "Choking Pollen", "Pollen damage +50% per level.")),
        A("strangle", "Strangleroot", "Strangle\nroot", new Color(0.4f, 0.65f, 0.3f), 9f, 10f, AimShape.Circle,
          "Roots burst up beneath every enemy in a wide area, coil around them and squeeze until they pop into blooms.",
          p => Strangleroot.Spawn(p), "strangle.size", 0.15f,
          U("strangle.size", "Grove Reach", "Area radius +15% per level."),
          U("strangle.speed", "Crushing Grip", "Squeezes 18% faster and grabs 15 more enemies per level.")),
        A("solar", "Solar Lance", "Solar\nLance", new Color(1f, 0.8f, 0.25f), 6f, 2.4f, AimShape.Line,
          "The lotus drinks in sunlight and fires a searing beam. Steer it with the mouse. Sunflowers grow wherever it burns.",
          p => SolarLance.Spawn(p), null, 0f,
          U("solar.time", "Long Burn", "Beam lasts 0.6s longer per level."),
          U("solar.power", "Solar Flare", "Beam damage +40% and width +15% per level.")),
        A("spore", "Sporecap Field", "Spore\ncaps", new Color(0.75f, 0.4f, 0.9f), 5f, 5f, AimShape.Circle,
          "Plants a patch of puffy mushrooms. When anything wanders close they swell up and explode in chain reactions.",
          p => Sporecaps.Spawn(p), null, 0f,
          U("spore.count", "Fairy Ring", "+3 mushrooms per level."),
          U("spore.power", "Big Boom", "Explosion radius and damage +25% per level.")),
        A("sapling", "Elder Sapling", "Elder\nSapling", new Color(0.55f, 0.75f, 0.35f), 14f, 4.5f, AimShape.Circle,
          "Summons a walking tree guardian that stomps through the horde, sending shockwaves through the ground.",
          p => ElderSapling.SpawnGroup(p), null, 0f,
          U("sapling.count", "Grove Guard", "Summons one more guardian per level.", 2, 120),
          U("sapling.power", "Mighty Stomp", "Stomp radius and damage +25% per level.")),
        A("bloom", "Grand Bloom", "Grand\nBloom", new Color(1f, 0.4f, 0.7f), 25f, 0f, AimShape.Global,
          "The lotus opens completely and releases a tidal wave of petals across the entire field. Nothing survives. The meadow blooms behind it.",
          p => GrandBloom.Spawn(p), null, 0f,
          U("bloom.fortune", "Petal Fortune", "Every enemy caught by the wave drops +1 extra flower per level.", 5, 40),
          U("bloom.echo", "Afterbloom", "Another smaller petal wave follows per level.", 2, 150)),
    };

    public static int Pages => (All.Count + PerPage - 1) / PerPage;
}
