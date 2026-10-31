using UnityEngine;

// Persistent progress and settings (PlayerPrefs). Values are cached in memory and flushed on menu/quit.
public static class Save
{
    static bool loaded;
    static int flowers, bestWave;

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        flowers = PlayerPrefs.GetInt("flowers", 0);
        bestWave = PlayerPrefs.GetInt("bestWave", 0);
    }

    public static int Flowers
    {
        get { Load(); return flowers; }
        set { Load(); flowers = Mathf.Max(0, value); PlayerPrefs.SetInt("flowers", flowers); }
    }

    public static int BestWave
    {
        get { Load(); return bestWave; }
        set { Load(); if (value > bestWave) { bestWave = value; PlayerPrefs.SetInt("bestWave", bestWave); } }
    }

    public static int Level(string key) => PlayerPrefs.GetInt("up." + key, 0);
    public static void SetLevel(string key, int lv) => PlayerPrefs.SetInt("up." + key, lv);

    public static float Volume(string channel, float def = 0.8f) => PlayerPrefs.GetFloat("vol." + channel, def);
    public static void SetVolume(string channel, float v) => PlayerPrefs.SetFloat("vol." + channel, Mathf.Clamp01(v));

    public static void Flush() => PlayerPrefs.Save();
}
