using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

// Procedural audio: every sound effect, the forest ambience and the music are synthesized at startup
// (on a background thread) with oscillators, filters, distortion and reverb. No audio files needed.
public class SFX : MonoBehaviour
{
    public static SFX I;

    public enum S
    {
        // UI
        UiHover, UiSelect, UiOpen, UiClose, UiPage, UiDenied,
        // Lotus / general
        Cast, Charge, Bloom, Combo, Surge,
        // Enemies
        Hit, Land, MiteDie, HuskDie, BruteDie, PitDeath, Swallow,
        // Abilities
        ZoneHum, GroundTear, VineGrab, Drag,
        ThornErupt, RootCreak,
        WhipSwoosh, WhipCrack,
        Cyclone,
        PlantGrow, Lunge, Chomp, Chew,
        SeedShoot, SeedImpact,
        Pollen, Squeeze,
        Beam, Sizzle,
        ShroomPlant, Inflate, SporeBoom,
        TreeRise, Stomp, Footstep,
        GrandChord, Shockwave,
    }

    const int SR = 44100;
    public static bool MusicOn = true;

    // Volume channels (0..1), persisted by the settings menu.
    public static float Master = Save.Volume("master", 0.85f);
    public static float MusicVol = Save.Volume("music", 0.7f);
    public static float EffectsVol = Save.Volume("effects", 0.85f);
    public static float AmbienceVol = Save.Volume("ambience", 0.7f);

    bool menuMusic = true;
    AudioSource lobby;
    AudioClip lobbyClip;
    public void SetMenuMusic(bool on) => menuMusic = on;

    class Bank
    {
        public readonly List<AudioClip> clips = new List<AudioClip>();
        public float gain = 0.6f, last = -1f;
        public int maxVoices = 4, lastVariant = -1;
    }

    class Def
    {
        public S id; public float dur, gain; public int variants, maxVoices; public Action<Syn> make;
    }

    readonly Dictionary<S, Bank> banks = new Dictionary<S, Bank>();
    readonly List<Def> defs = new List<Def>();
    readonly ConcurrentQueue<(string key, S id, float[] data)> ready = new ConcurrentQueue<(string, S, float[])>();
    AudioClip musicClip, drumClip, ambientClip;

    AudioSource[] pool;
    S[] poolId;
    float[] poolStart;
    AudioSource ambient, music, drums;
    AudioLowPassFilter[] lowpass;
    bool loopsStarted;
    float lpCut = 22000f;

    void Awake()
    {
        I = this;
        pool = new AudioSource[40];
        poolId = new S[pool.Length];
        poolStart = new float[pool.Length];
        for (int i = 0; i < pool.Length; i++)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 0f;
            pool[i] = a;
        }
        ambient = MakeLoop("Ambience");
        music = MakeLoop("Music");
        drums = MakeLoop("Drums");
        lobby = MakeLoop("Lobby");
        lowpass = new[] { ambient.GetComponent<AudioLowPassFilter>(), music.GetComponent<AudioLowPassFilter>(), drums.GetComponent<AudioLowPassFilter>() };

        Define();
        foreach (var d in defs) banks[d.id] = new Bank { gain = d.gain, maxVoices = d.maxVoices };

        var list = defs.ToArray();
        Task.Run(() =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // The menu beat first: it is the first thing you hear.
                ready.Enqueue(("lobby", default, Lobby()));
                foreach (var d in list)
                    for (int v = 0; v < d.variants; v++)
                    {
                        var s = new Syn(d.dur, (int)d.id * 7919 + v * 104729 + 17);
                        d.make(s);
                        s.Norm(0.92f);
                        ready.Enqueue((null, d.id, s.Trimmed()));
                    }
                ready.Enqueue(("ambient", default, Ambient()));
                var m = Music(out float[] dr);
                ready.Enqueue(("drums", default, dr));
                ready.Enqueue(("music", default, m));
                Debug.Log("[SFX] synthesized all audio in " + sw.ElapsedMilliseconds + " ms");
            }
            catch (Exception e) { Debug.LogException(e); }
        });
    }

    public bool AllLoaded => loopsStarted && ambientClip != null;

    // Debug/demo helper: writes every clip to .wav and logs level stats.
    public void Dump(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        void Write(string name, AudioClip c)
        {
            var data = new float[c.samples];
            c.GetData(data, 0);
            float peak = 0f; int bad = 0;
            foreach (var x in data) { if (float.IsNaN(x) || float.IsInfinity(x)) bad++; else peak = Mathf.Max(peak, Mathf.Abs(x)); }
            Debug.Log($"[SFX] {name}: {c.length:0.00}s peak={peak:0.00} bad={bad}");
            using (var f = new System.IO.BinaryWriter(System.IO.File.Create(System.IO.Path.Combine(dir, name + ".wav"))))
            {
                int n = data.Length;
                f.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); f.Write(36 + n * 2); f.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                f.Write(16); f.Write((short)1); f.Write((short)1); f.Write(SR); f.Write(SR * 2); f.Write((short)2); f.Write((short)16);
                f.Write(System.Text.Encoding.ASCII.GetBytes("data")); f.Write(n * 2);
                foreach (var x in data) f.Write((short)(Mathf.Clamp(float.IsNaN(x) ? 0f : x, -1f, 1f) * 32767f));
            }
        }
        foreach (var kv in banks) if (kv.Value.clips.Count > 0) Write(kv.Key.ToString(), kv.Value.clips[0]);
        if (ambientClip) Write("_Ambience", ambientClip);
        if (musicClip) Write("_Music", musicClip);
        if (drumClip) Write("_MusicDrums", drumClip);
        if (lobbyClip) Write("_MenuBeat", lobbyClip);
    }

    AudioSource MakeLoop(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var a = go.AddComponent<AudioSource>();
        a.loop = true;
        a.playOnAwake = false;
        a.spatialBlend = 0f;
        go.AddComponent<AudioLowPassFilter>().cutoffFrequency = 22000f;
        return a;
    }

    void Update()
    {
        // Turn finished buffers into clips (a few per frame to avoid hitches).
        for (int n = 0; n < 12 && ready.TryDequeue(out var item); n++)
        {
            var clip = AudioClip.Create(item.key ?? item.id.ToString(), item.data.Length, 1, SR, false);
            clip.SetData(item.data, 0);
            if (item.key == null) banks[item.id].clips.Add(clip);
            else if (item.key == "ambient") { ambientClip = clip; ambient.clip = clip; ambient.volume = 0f; ambient.Play(); }
            else if (item.key == "lobby") { lobbyClip = clip; lobby.clip = clip; lobby.volume = 0f; lobby.Play(); }
            else if (item.key == "drums") drumClip = clip;
            else if (item.key == "music") musicClip = clip;
        }
        if (!loopsStarted && musicClip && drumClip)
        {
            loopsStarted = true;
            music.clip = musicClip; drums.clip = drumClip;
            double t = AudioSettings.dspTime + 0.2;
            music.PlayScheduled(t);
            drums.PlayScheduled(t);
            drums.volume = 0f;
        }

        if (Input.GetKeyDown(KeyCode.M)) MusicOn = !MusicOn;

        // Drums swell with the size of the horde and your combo.
        float intensity = 0f;
        if (EnemyManager.I != null) intensity += EnemyManager.All.Count / 140f;
        if (Game.I != null) intensity += Game.I.Combo / 50f;
        float dt = Time.unscaledDeltaTime;
        float mv = MusicOn ? Master * MusicVol : 0f;
        bool playing = Game.I != null && Game.I.State == Game.Mode.Playing;
        // Crossfade between the calm lobby beat (menu) and the battle music (game).
        lobby.volume = Mathf.MoveTowards(lobby.volume, menuMusic ? 0.5f * mv : 0f, dt * 0.35f);
        music.volume = Mathf.MoveTowards(music.volume, menuMusic ? 0f : 0.38f * mv, dt * 0.3f);
        drums.volume = Mathf.MoveTowards(drums.volume, playing ? Mathf.Clamp01(intensity) * 0.42f * mv : 0f, dt * 0.15f);
        ambient.volume = Mathf.MoveTowards(ambient.volume, 0.5f * Master * AmbienceVol, dt * 0.5f);

        // Muffle the background while the ability wheel slows time.
        bool wheel = AbilityCaster.I != null && AbilityCaster.I.WheelOpen;
        float target = wheel ? 700f : 22000f;
        lpCut = Mathf.Exp(Mathf.Lerp(Mathf.Log(lpCut), Mathf.Log(target), dt * 8f));
        foreach (var lp in lowpass) lp.cutoffFrequency = lpCut;
    }

    // ------------------------------------------------------------------ playback

    public static void Play(S id, float vol = 1f, float pitchVar = 0.06f) { if (I) I.PlayAt(id, null, vol, pitchVar); }
    public static void At(S id, Vector3 pos, float vol = 1f, float pitchVar = 0.06f) { if (I) I.PlayAt(id, pos, vol, pitchVar); }

    void PlayAt(S id, Vector3? pos, float vol, float pitchVar)
    {
        if (!banks.TryGetValue(id, out var bank) || bank.clips.Count == 0) return;
        float now = Time.unscaledTime;
        if (now - bank.last < 0.022f) return;

        int active = 0, free = -1, oldest = 0;
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i].isPlaying)
            {
                if (poolId[i] == id) active++;
                if (poolStart[i] < poolStart[oldest]) oldest = i;
            }
            else if (free < 0) free = i;
        }
        if (active >= bank.maxVoices) return;
        int slot = free >= 0 ? free : oldest;
        bank.last = now;

        float pan = 0f;
        var cam = GameCamera.I ? GameCamera.I.Cam : null;
        if (pos.HasValue && cam)
        {
            Vector3 vp = cam.WorldToViewportPoint(pos.Value);
            pan = Mathf.Clamp((vp.x - 0.5f) * 1.5f, -0.85f, 0.85f);
            vol *= Mathf.Lerp(1f, 0.55f, Mathf.Clamp01((vp.z - 30f) / 90f));
        }

        int v = UnityEngine.Random.Range(0, bank.clips.Count);
        if (bank.clips.Count > 1 && v == bank.lastVariant) v = (v + 1) % bank.clips.Count;
        bank.lastVariant = v;

        var src = pool[slot];
        src.Stop();
        src.clip = bank.clips[v];
        src.pitch = 1f + UnityEngine.Random.Range(-pitchVar, pitchVar);
        src.volume = Mathf.Clamp01(vol * bank.gain * Master * EffectsVol);
        src.panStereo = pan;
        src.Play();
        poolId[slot] = id;
        poolStart[slot] = now;
    }

    // ------------------------------------------------------------------ sound design

    static float Hz(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);
    static Func<float, float> Sw(float a, float b) => k => a * Mathf.Pow(b / a, k);
    static Func<float, float> Sw3(float a, float b, float c) => k => k < 0.5f ? a * Mathf.Pow(b / a, k * 2f) : b * Mathf.Pow(c / b, k * 2f - 1f);
    static Func<float, float> Fixed(float a) => k => a;

    void D(S id, float dur, int variants, float gain, int maxVoices, Action<Syn> make)
        => defs.Add(new Def { id = id, dur = dur, variants = variants, gain = gain, maxVoices = maxVoices, make = make });

    void Define()
    {
        // ------------------------------------------------ UI
        D(S.UiHover, 0.25f, 3, 0.28f, 2, s =>
        {
            s.Tone(0, 0.05f, s.R(1500, 1800), 1200, 1f, W.Sine, 0.001f, 70f);
            s.Noise(0, 0.02f, 0.25f, Fixed(4000), F.BP, 2f, 0.001f, 160f);
            s.Reverb(0.18f, 0.5f);
        });
        D(S.UiSelect, 0.9f, 2, 0.45f, 2, s =>
        {
            float b = s.R(0, 1) < 0.5f ? 76 : 79;
            s.Bell(0, Hz(b), 0.8f, 8f, 2f, 1.3f);
            s.Bell(0.07f, Hz(b + 7), 0.7f, 7f, 2f, 1.1f);
            s.Bell(0.14f, Hz(b + 12), 0.5f, 6f, 3f, 0.8f);
            s.Tone(0, 0.09f, 260, 520, 0.35f, W.Sine, 0.002f, 30f);
            s.Reverb(0.35f, 0.7f);
        });
        D(S.UiOpen, 0.7f, 2, 0.4f, 1, s =>
        {
            s.Noise(0, 0.3f, 1f, Sw(400, 3500), F.BP, 1.6f, 0f, -1.5f);
            s.Tone(0, 0.3f, 220, 520, 0.35f, W.Tri, 0f, -1.2f);
            s.Bell(0.18f, Hz(86), 0.25f, 9f, 3f, 0.6f);
            s.Reverb(0.35f, 0.65f);
        });
        D(S.UiClose, 0.5f, 2, 0.35f, 1, s =>
        {
            s.Noise(0, 0.2f, 1f, Sw(3000, 500), F.BP, 1.6f, 0f, -1.5f);
            s.Tone(0, 0.18f, 480, 240, 0.3f, W.Tri, 0f, -1.2f);
            s.Reverb(0.25f, 0.5f);
        });
        D(S.UiPage, 0.4f, 3, 0.4f, 2, s =>
        {
            s.Noise(0, 0.09f, 1f, Sw(1500, 6000), F.HP, 0.8f, 0.004f, 40f);
            s.Noise(0.03f, 0.06f, 0.6f, Fixed(2500), F.BP, 2f, 0.002f, 50f);
            s.Tone(0.02f, 0.06f, 900, 1300, 0.4f, W.Sine, 0.002f, 50f);
            s.Reverb(0.2f, 0.5f);
        });
        D(S.UiDenied, 0.35f, 2, 0.35f, 1, s =>
        {
            s.Tone(0, 0.16f, 190, 150, 1f, W.Square, 0.002f, 20f);
            s.LowPass(900);
            s.Tone(0.09f, 0.16f, 150, 120, 0.8f, W.Sine, 0.002f, 22f);
        });

        // ------------------------------------------------ Lotus / general
        D(S.Cast, 1.1f, 4, 0.32f, 3, s =>
        {
            int[] pent = { 74, 76, 79, 81, 83, 86, 88 };
            for (int i = 0; i < 3; i++) s.Bell(i * 0.035f, Hz(pent[s.I(0, pent.Length)]), 0.5f, s.R(5, 8), s.R(2, 4), 1.2f);
            s.Noise(0, 0.4f, 0.35f, Sw(2500, 7000), F.BP, 1.2f, 0f, -2f);
            s.Tone(0, 0.2f, 140, 90, 0.4f, W.Sine, 0.003f, 18f);
            s.Reverb(0.4f, 0.75f);
        });
        D(S.Charge, 1.0f, 2, 0.55f, 2, s =>
        {
            s.Tone(0, 0.65f, 110, 440, 0.6f, W.Saw, 0.1f, -0.8f, 6f, 0.01f, 1.6f);
            s.LowPassSweep(Sw(300, 4500));
            s.Tone(0, 0.65f, 220, 880, 0.4f, W.Sine, 0.1f, -0.8f, 0, 0, 1.6f);
            s.Noise(0, 0.6f, 0.25f, Sw(1000, 8000), F.BP, 2f, 0f, -1.2f);
            for (int i = 0; i < 4; i++) s.Bell(0.45f + i * 0.04f, Hz(86 + i * 3), 0.3f, 8f, 3f, 1f);
            s.Reverb(0.35f, 0.7f);
        });
        D(S.Bloom, 1.0f, 4, 0.3f, 4, s =>
        {
            s.Tone(0, 0.08f, s.R(450, 550), 1200, 0.7f, W.Sine, 0.002f, 25f);
            int[] notes = { 84, 86, 88, 91, 93 };
            s.Bell(0.04f, Hz(notes[s.I(0, 5)]), 0.5f, 7f, 3.5f, 1f);
            s.Bell(0.09f, Hz(notes[s.I(0, 5)] + 12), 0.25f, 9f, 3.5f, 0.6f);
            s.Reverb(0.4f, 0.7f);
        });
        D(S.Combo, 1.6f, 2, 0.45f, 1, s =>
        {
            int[] n = { 79, 83, 86, 91 };
            for (int i = 0; i < n.Length; i++) s.Bell(i * 0.07f, Hz(n[i]), 0.6f, 4f, 2f, 1.5f);
            s.Noise(0, 0.6f, 0.2f, Sw(3000, 9000), F.HP, 0.7f, 0f, -1.5f);
            s.Reverb(0.45f, 0.85f);
        });
        D(S.Surge, 2.6f, 1, 0.75f, 1, s =>
        {
            // Low horn swell + two drum hits: the horde is coming.
            s.Tone(0, 1.7f, Hz(43) * 0.97f, Hz(43), 0.7f, W.Saw, 0.25f, -0.6f, 4.5f, 0.006f, 0.15f);
            s.Tone(0, 1.7f, Hz(50) * 0.97f, Hz(50), 0.45f, W.Saw, 0.3f, -0.6f, 5f, 0.006f, 0.15f);
            s.LowPassSweep(Sw3(250, 1300, 500), 1.7f);
            s.Tone(0, 1.7f, Hz(31), Hz(31), 0.6f, W.Sine, 0.2f, -0.7f);
            for (int i = 0; i < 2; i++)
            {
                s.Tone(0.05f + i * 0.45f, 0.5f, 110, 45, 1f, W.Sine, 0.002f, 7f);
                s.Noise(0.05f + i * 0.45f, 0.2f, 0.5f, Sw(900, 200), F.LP, 0.8f, 0.001f, 15f);
            }
            s.Drive(1.6f);
            s.Reverb(0.45f, 0.85f);
        });

        // ------------------------------------------------ Enemies
        D(S.Hit, 0.25f, 4, 0.28f, 5, s =>
        {
            s.Noise(0, 0.08f, 1f, Sw(3000, 400), F.LP, 0.9f, 0.001f, 40f);
            s.Tone(0, 0.1f, s.R(150, 190), 70, 0.9f, W.Sine, 0.001f, 30f);
            s.Drive(2f);
        });
        D(S.Land, 0.3f, 3, 0.25f, 4, s =>
        {
            s.Tone(0, 0.14f, s.R(95, 115), 48, 1f, W.Sine, 0.002f, 24f);
            s.Noise(0, 0.12f, 0.6f, Sw(900, 200), F.LP, 0.8f, 0.001f, 30f);
            s.Clicks(0, 0.06f, 900, 0.3f, 2200);
        });
        D(S.MiteDie, 0.7f, 4, 0.4f, 6, s =>
        {
            s.Tone(0, 0.11f, s.R(650, 800), 180, 0.9f, W.Sine, 0.001f, 28f);              // squishy bloop
            s.Clicks(0, 0.07f, 500, 0.6f, 3600, 2.5f);                                      // chitter
            s.Noise(0, 0.14f, 0.5f, Sw(1500, 300), F.BP, 3f, 0.002f, 22f);                 // wet
            s.Bell(0.05f, Hz(88 + s.I(0, 3) * 2), 0.25f, 9f, 3.5f, 0.7f);                  // tiny bloom
            s.Reverb(0.25f, 0.6f);
        });
        D(S.HuskDie, 0.9f, 4, 0.5f, 6, s =>
        {
            s.Tone(0, 0.28f, s.R(120, 140), 48, 1f, W.Sine, 0.002f, 12f);
            s.Noise(0, 0.22f, 0.7f, Sw(1800, 200), F.LP, 0.9f, 0.002f, 16f);
            s.Noise(0.01f, 0.16f, 0.5f, Sw3(500, 1800, 400), F.BP, 4f, 0.002f, 14f);      // squelch
            s.Drive(1.8f);
            s.Bell(0.07f, Hz(84 + s.I(0, 4) * 2), 0.3f, 7f, 3.5f, 0.8f);
            s.Bell(0.12f, Hz(91 + s.I(0, 3) * 2), 0.18f, 9f, 3.5f, 0.6f);
            s.Reverb(0.28f, 0.65f);
        });
        D(S.BruteDie, 1.6f, 3, 0.75f, 3, s =>
        {
            s.Tone(0, 0.7f, s.R(70, 80), 28, 1f, W.Sine, 0.003f, 5f);
            s.Clicks(0, 0.25f, 1500, 0.7f, 1800, 1.2f, 0.006f, 10f);                       // bones cracking
            s.Noise(0, 0.55f, 0.8f, Sw(3000, 150), F.LP, 0.8f, 0.002f, 6f);
            s.Noise(0, 1.0f, 0.5f, Fixed(180), F.LP, 0.7f, 0.05f, 3f, 0, 0, true);         // rumble
            s.Drive(2.6f);
            for (int i = 0; i < 3; i++) s.Bell(0.12f + i * 0.06f, Hz(79 + i * 5), 0.25f, 5f, 3.5f, 0.9f);
            s.Reverb(0.32f, 0.8f);
        });
        D(S.PitDeath, 0.9f, 4, 0.42f, 5, s =>
        {
            s.Clicks(0, 0.24f, 1800, 0.9f, 2600, 1f, 0.005f, 10f);
            s.Noise(0, 0.1f, 0.5f, Fixed(4000), F.HP, 0.8f, 0.001f, 30f);
            s.Tone(0, 0.2f, 100, 40, 0.8f, W.Sine, 0.002f, 16f);
            s.Drive(1.6f);
            s.Reverb(0.45f, 0.75f);  // echo up out of the pit
        });
        D(S.Swallow, 0.6f, 3, 0.45f, 4, s =>
        {
            s.Tone(0, 0.3f, s.R(260, 300), 100, 0.9f, W.Tri, 0.01f, 7f, 18f, 0.15f);
            s.Noise(0, 0.28f, 0.5f, Sw3(300, 900, 200), F.BP, 5f, 0.01f, 8f);
            s.Tone(0.2f, 0.15f, 90, 50, 0.6f, W.Sine, 0.003f, 20f);
            s.Drive(1.4f);
            s.Reverb(0.2f, 0.5f);
        });

        // ------------------------------------------------ Root Abyss
        D(S.ZoneHum, 1.6f, 2, 0.5f, 2, s =>
        {
            float[] ch = { 50, 57, 62, 69 };
            foreach (float n in ch) s.Tone(0, 1.1f, Hz(n) * 0.99f, Hz(n), 0.35f, W.Tri, 0f, -1.3f, 5f, 0.004f);
            s.Noise(0, 1f, 0.4f, Sw(300, 2500), F.BP, 2.5f, 0f, -1.2f);
            for (int i = 0; i < 5; i++) s.Bell(0.1f + i * 0.09f, Hz(81 + i * 2), 0.25f, 6f, 3f, 1f);
            s.Reverb(0.5f, 0.85f);
        });
        D(S.GroundTear, 2.0f, 2, 0.85f, 2, s =>
        {
            s.Noise(0, 1.3f, 1f, Fixed(260), F.LP, 0.8f, 0.01f, 2.6f, 0, 0, true);
            s.Clicks(0, 0.6f, 1100, 0.8f, 1500, 1.1f, 0.008f, 4f);
            s.Noise(0, 0.25f, 0.8f, Sw(5000, 600), F.LP, 0.7f, 0.001f, 12f);
            s.Tone(0, 1.2f, 58, 30, 1f, W.Sine, 0.004f, 2.8f);
            s.Drive(2.4f);
            s.Reverb(0.3f, 0.8f);
        });
        D(S.VineGrab, 0.6f, 4, 0.38f, 5, s =>
        {
            s.Noise(0, 0.12f, 0.8f, Sw(700, 5000), F.BP, 1.4f, 0f, -1.5f);                 // swish
            s.Noise(0.11f, 0.02f, 1f, Fixed(3000), F.HP, 0.7f, 0.0005f, 120f);              // snap
            s.Tone(0.1f, 0.3f, 85, 70, 0.6f, W.Saw, 0.01f, 8f, 37f, 0.25f);                 // creak
            s.LowPass(1400);
            s.Reverb(0.2f, 0.5f);
        });
        D(S.Drag, 0.9f, 3, 0.35f, 3, s =>
        {
            s.Noise(0, 0.75f, 1f, Fixed(s.R(600, 900)), F.BP, 1.4f, 0f, -0.7f, 23f, 0.7f);
            s.Noise(0, 0.75f, 0.5f, Fixed(200), F.LP, 0.7f, 0f, -1f, 0, 0, true);
            s.Clicks(0, 0.7f, 140, 0.4f, 2000);
        });

        // ------------------------------------------------ Bramble Rupture
        D(S.ThornErupt, 0.5f, 5, 0.4f, 4, s =>
        {
            s.Noise(0, 0.03f, 1f, Fixed(2500), F.HP, 0.8f, 0.0005f, 90f);
            s.Clicks(0, 0.14f, 1400, 0.7f, s.R(1600, 2600), 1.2f, 0.004f, 18f);
            s.Tone(0, 0.18f, s.R(130, 160), 55, 0.9f, W.Sine, 0.001f, 16f);
            s.Drive(2f);
            s.Reverb(0.2f, 0.6f);
        });
        D(S.RootCreak, 1.2f, 3, 0.5f, 2, s =>
        {
            s.Tone(0, 0.9f, s.R(48, 56), s.R(60, 70), 1f, W.Saw, 0.05f, -0.8f, 29f, 0.35f);
            s.LowPass(650);
            s.Clicks(0, 0.9f, 60, 0.6f, 1300, 1.5f, 0.01f);
            s.Noise(0, 0.9f, 0.35f, Fixed(160), F.LP, 0.7f, 0f, -1f, 0, 0, true);
            s.Reverb(0.3f, 0.7f);
        });

        // ------------------------------------------------ Vine Lash
        D(S.WhipSwoosh, 1.0f, 3, 0.65f, 2, s =>
        {
            s.Noise(0, 0.6f, 1f, Sw3(300, 3200, 700), F.BP, 1.3f, 0f, -1.4f);
            s.Tone(0, 0.6f, 75, 55, 0.5f, W.Sine, 0f, -1f);
            s.Reverb(0.25f, 0.6f);
        });
        D(S.WhipCrack, 0.5f, 4, 0.35f, 5, s =>
        {
            s.Noise(0, 0.02f, 1f, Fixed(3500), F.HP, 0.7f, 0.0003f, 140f);
            s.Noise(0, 0.12f, 0.7f, Sw(2500, 300), F.LP, 0.9f, 0.001f, 30f);
            s.Tone(0, 0.15f, 160, 60, 0.8f, W.Sine, 0.001f, 22f);
            s.Drive(2.2f);
            s.Reverb(0.2f, 0.55f);
        });

        // ------------------------------------------------ Petal Cyclone
        D(S.Cyclone, 6.8f, 1, 0.6f, 2, s =>
        {
            float L = 6.4f;
            s.Noise(0, L, 1f, k => 700f + 450f * Mathf.Sin(k * L * Mathf.PI * 2f * 1.6f), F.BP, 2.2f, 0f, -0.35f);
            s.Noise(0, L, 0.45f, Fixed(6000), F.HP, 0.7f, 0f, -0.4f, 9f, 0.8f);         // petal flutter
            s.Noise(0, L, 0.6f, Fixed(140), F.LP, 0.7f, 0f, -0.5f, 0, 0, true);         // low roar
            for (int i = 0; i < 10; i++) s.Bell(s.R(0.2f, L - 1f), Hz(84 + s.I(0, 8)), 0.12f, 6f, 3.5f, 0.6f);
            s.Reverb(0.3f, 0.7f);
        });

        // ------------------------------------------------ Snapjaw
        D(S.PlantGrow, 1.2f, 2, 0.55f, 2, s =>
        {
            s.Tone(0, 0.8f, 70, 150, 0.8f, W.Saw, 0.02f, -0.8f, 33f, 0.3f);
            s.LowPass(1000);
            s.Noise(0, 0.8f, 0.6f, Sw(250, 1400), F.BP, 4f, 0f, -1f);                     // wet stretch
            s.Noise(0, 0.35f, 0.5f, Fixed(250), F.LP, 0.7f, 0.002f, 8f, 0, 0, true);
            s.Clicks(0, 0.3f, 600, 0.4f, 1800);
            s.Reverb(0.25f, 0.6f);
        });
        D(S.Lunge, 0.35f, 3, 0.35f, 3, s =>
        {
            s.Noise(0, 0.16f, 1f, Sw(500, 3500), F.BP, 1.5f, 0f, -1.4f);
            s.Tone(0, 0.15f, 120, 200, 0.3f, W.Tri, 0f, -1f);
        });
        D(S.Chomp, 0.6f, 4, 0.6f, 4, s =>
        {
            s.Noise(0, 0.015f, 1f, Fixed(3000), F.HP, 0.7f, 0.0003f, 200f);
            s.Noise(0.05f, 0.015f, 0.8f, Fixed(2600), F.HP, 0.7f, 0.0003f, 200f);
            s.Noise(0.02f, 0.16f, 0.7f, Sw(2200, 300), F.LP, 1f, 0.001f, 20f);
            s.Clicks(0.02f, 0.12f, 1200, 0.5f, 1800, 1.2f, 0.004f, 20f);
            s.Tone(0, 0.18f, 130, 60, 0.9f, W.Sine, 0.001f, 18f);
            s.Drive(2f);
            s.Reverb(0.2f, 0.5f);
        });
        D(S.Chew, 0.6f, 3, 0.4f, 3, s =>
        {
            for (int i = 0; i < 3; i++)
            {
                float t = i * 0.13f;
                s.Noise(t, 0.08f, 0.8f, Sw(1800, 400), F.BP, 2.5f, 0.002f, 30f);
                s.Clicks(t, 0.07f, 1000, 0.5f, 2200, 1.2f, 0.004f, 30f);
                s.Tone(t, 0.08f, 110, 70, 0.5f, W.Sine, 0.002f, 30f);
            }
            s.Reverb(0.15f, 0.4f);
        });

        // ------------------------------------------------ Seed Barrage
        D(S.SeedShoot, 0.35f, 4, 0.3f, 4, s =>
        {
            s.Tone(0, 0.07f, s.R(320, 380), 110, 1f, W.Sine, 0.001f, 40f);
            s.Noise(0, 0.05f, 0.6f, Sw(2500, 600), F.LP, 1f, 0.001f, 50f);
            s.Noise(0.01f, 0.12f, 0.3f, Sw(3000, 1200), F.BP, 2f, 0f, -1.4f);
            s.Reverb(0.15f, 0.4f);
        });
        D(S.SeedImpact, 0.6f, 4, 0.38f, 5, s =>
        {
            s.Noise(0, 0.22f, 0.9f, Sw(2400, 250), F.LP, 0.9f, 0.001f, 16f);
            s.Tone(0, 0.16f, s.R(85, 100), 42, 1f, W.Sine, 0.001f, 18f);
            s.Clicks(0, 0.09f, 1300, 0.5f, 2600, 1.2f, 0.004f, 30f);
            s.Bell(0.04f, Hz(86 + s.I(0, 4) * 2), 0.18f, 10f, 3f, 0.6f);
            s.Drive(1.8f);
            s.Reverb(0.2f, 0.55f);
        });

        // ------------------------------------------------ Pollen Haze
        D(S.Pollen, 2.4f, 2, 0.5f, 2, s =>
        {
            s.Noise(0, 1.6f, 0.6f, Sw3(2500, 6000, 3500), F.BP, 1.4f, 0f, -1.2f, 3f, 0.4f);
            s.Noise(0, 0.6f, 0.6f, Sw(400, 1500), F.BP, 1f, 0f, -1.5f);                   // soft poof
            int[] pent = { 81, 83, 86, 88, 90, 93, 95 };
            for (int i = 0; i < 9; i++) s.Bell(0.1f + i * 0.11f + s.R(0, 0.05f), Hz(pent[s.I(0, 7)]), 0.18f, 5f, 2.5f, 0.7f);
            s.Reverb(0.55f, 0.85f);
        });

        // ------------------------------------------------ Strangleroot
        D(S.Squeeze, 1.2f, 3, 0.32f, 4, s =>
        {
            s.Tone(0, 0.95f, s.R(260, 320), s.R(800, 1000), 0.6f, W.Tri, 0.05f, -0.6f, 9f, 0.04f, 2f);
            s.Noise(0, 0.95f, 0.5f, Sw(400, 1600), F.BP, 6f, 0.05f, -0.7f, 0, 0);
            s.Tone(0, 0.95f, 60, 75, 0.5f, W.Saw, 0.05f, -0.8f, 31f, 0.3f);
            s.LowPass(1800);
            s.Reverb(0.2f, 0.5f);
        });

        // ------------------------------------------------ Solar Lance
        D(S.Beam, 2.0f, 1, 0.6f, 2, s =>
        {
            float L = 1.75f;
            s.Tone(0, L, 110, 104, 0.55f, W.Saw, 0.04f, -0.25f, 7f, 0.01f);
            s.LowPass(1600);
            s.Tone(0, L, 220, 216, 0.5f, W.Sine, 0.04f, -0.25f);
            s.Tone(0, L, 660, 700, 0.25f, W.Sine, 0.04f, -0.25f, 11f, 0.02f);
            s.Tone(0, L, 1320, 1400, 0.1f, W.Sine, 0.04f, -0.25f, 13f, 0.03f);
            s.Noise(0, L, 0.35f, Fixed(5000), F.HP, 0.7f, 0.04f, -0.3f, 47f, 0.6f);     // sizzle
            s.Noise(0, 0.15f, 0.7f, Sw(6000, 800), F.LP, 0.8f, 0.001f, 18f);              // ignition crack
            s.Drive(1.5f);
            s.Reverb(0.3f, 0.75f);
        });
        D(S.Sizzle, 0.4f, 3, 0.2f, 3, s =>
        {
            s.Noise(0, 0.3f, 1f, Fixed(s.R(4500, 7000)), F.HP, 0.8f, 0.005f, 10f, 37f, 0.6f);
            s.Clicks(0, 0.25f, 900, 0.5f, 5000, 1f, 0.002f, 8f);
        });

        // ------------------------------------------------ Sporecaps
        D(S.ShroomPlant, 0.8f, 3, 0.45f, 2, s =>
        {
            s.Tone(0, 0.4f, s.R(180, 220), s.R(380, 440), 1f, W.Sine, 0.002f, 6f, 13f, 0.12f, 0.4f);   // boing
            s.Noise(0, 0.08f, 0.4f, Sw(1500, 300), F.LP, 0.8f, 0.001f, 30f);
            s.Bell(0.05f, Hz(79), 0.25f, 7f, 2f, 0.8f);
            s.Reverb(0.3f, 0.6f);
        });
        D(S.Inflate, 0.4f, 3, 0.3f, 4, s =>
        {
            s.Tone(0, 0.24f, s.R(350, 420), s.R(1100, 1300), 0.8f, W.Tri, 0.01f, -0.6f, 20f, 0.03f, 1.5f);
            s.Noise(0, 0.24f, 0.4f, Sw(500, 2500), F.BP, 5f, 0.01f, -0.6f);
        });
        D(S.SporeBoom, 1.2f, 4, 0.6f, 4, s =>
        {
            s.Noise(0, 0.55f, 1f, Sw(3500, 180), F.LP, 0.9f, 0.002f, 6f);
            s.Noise(0, 0.3f, 0.7f, Fixed(800), F.BP, 1f, 0.002f, 9f);                     // poof
            s.Tone(0, 0.45f, s.R(80, 95), 38, 1f, W.Sine, 0.002f, 8f);
            s.Drive(2.2f);
            s.Bell(0.05f, Hz(76 + s.I(0, 5)), 0.15f, 5f, 4.5f, 2f);                        // spore shimmer
            s.Reverb(0.32f, 0.75f);
        });

        // ------------------------------------------------ Elder Sapling
        D(S.TreeRise, 2.2f, 1, 0.8f, 1, s =>
        {
            s.Noise(0, 1.4f, 1f, Fixed(220), F.LP, 0.8f, 0.05f, 2.2f, 0, 0, true);
            s.Tone(0, 1.4f, 45, 60, 0.9f, W.Saw, 0.08f, -0.7f, 23f, 0.4f);
            s.LowPass(700);
            s.Clicks(0, 1.2f, 260, 0.6f, 1400, 1.4f, 0.01f, 1.5f);
            s.Noise(0.6f, 0.8f, 0.4f, Sw(1500, 6000), F.HP, 0.7f, 0f, -1.5f);             // leaves
            s.Bell(0.9f, Hz(74), 0.2f, 3f, 2f, 1f);
            s.Bell(1.0f, Hz(81), 0.2f, 3f, 2f, 1f);
            s.Drive(1.6f);
            s.Reverb(0.35f, 0.85f);
        });
        D(S.Stomp, 1.3f, 3, 0.85f, 2, s =>
        {
            s.Tone(0, 0.7f, s.R(58, 66), 26, 1f, W.Sine, 0.002f, 5.5f);
            s.Noise(0, 0.35f, 0.9f, Sw(1200, 120), F.LP, 0.8f, 0.001f, 9f);
            s.Clicks(0, 0.25f, 900, 0.5f, 1600, 1.1f, 0.006f, 10f);
            s.Noise(0.05f, 0.6f, 0.3f, Sw(2000, 6000), F.HP, 0.7f, 0f, -1.5f);           // leaves shaking
            s.Drive(3f);
            s.Reverb(0.25f, 0.8f);
        });
        D(S.Footstep, 0.4f, 4, 0.3f, 2, s =>
        {
            s.Tone(0, 0.15f, s.R(90, 110), 55, 1f, W.Sine, 0.002f, 20f);
            s.Noise(0, 0.1f, 0.5f, Sw(1200, 250), F.LP, 0.8f, 0.001f, 25f);
            s.Clicks(0, 0.08f, 600, 0.3f, 1500);
            s.Drive(1.5f);
        });

        // ------------------------------------------------ Grand Bloom
        D(S.GrandChord, 4.5f, 1, 0.8f, 1, s =>
        {
            float[] chord = { 38, 50, 57, 62, 66, 69, 74, 76 };
            foreach (float n in chord)
            {
                s.Tone(0, 3.2f, Hz(n) * 0.996f, Hz(n), 0.22f, W.Saw, 0.6f, -0.5f, 5f, 0.003f);
                s.Tone(0, 3.2f, Hz(n) * 1.004f, Hz(n), 0.22f, W.Saw, 0.6f, -0.5f, 4.3f, 0.003f);
            }
            s.LowPassSweep(Sw3(300, 5000, 1200), 3.2f);
            for (int i = 0; i < 6; i++) s.Tone(0.3f, 2.8f, Hz(74 + (i % 3) * 7), Hz(74 + (i % 3) * 7), 0.12f, W.Sine, 0.5f, -0.6f, 5.5f, 0.008f); // "choir"
            int[] arp = { 74, 78, 81, 86, 90, 93, 98, 102 };
            for (int i = 0; i < arp.Length; i++) s.Bell(0.55f + i * 0.06f, Hz(arp[i]), 0.25f, 3.5f, 3f, 1.2f);
            s.Reverb(0.6f, 0.92f);
        });
        D(S.Shockwave, 2.4f, 1, 0.85f, 1, s =>
        {
            s.Tone(0, 1.4f, 60, 26, 1f, W.Sine, 0.003f, 2.4f);
            s.Noise(0, 1.6f, 1f, Sw(6000, 180), F.LP, 0.9f, 0.003f, 2.2f);
            s.Noise(0, 1.5f, 0.6f, Sw3(300, 2500, 400), F.BP, 1.2f, 0f, -0.8f);          // whoosh across the field
            s.Drive(2.4f);
            s.Reverb(0.4f, 0.9f);
        });
    }

    // ------------------------------------------------------------------ ambience & music (background thread)

    static float[] Ambient()
    {
        const float L = 30f, CF = 1.5f;
        var s = new Syn(L + CF, 99);
        // Wind: brown noise through a breathing low-pass. Periods divide the loop length so it repeats cleanly.
        s.Noise(0, L + CF, 1f, k => { float t = k * (L + CF); return 260f + 160f * Mathf.Sin(t * Mathf.PI * 2f / 10f) + 90f * Mathf.Sin(t * Mathf.PI * 2f / 6f); },
                F.LP, 0.9f, 0f, 0f, 0f, 0f, true);
        s.Tremolo(0.15f, 0.35f);
        // Leaves rustling.
        for (int i = 0; i < 9; i++)
        {
            float t = s.R(0, L);
            s.Noise(t, s.R(1.2f, 2.8f), s.R(0.08f, 0.16f), Fixed(s.R(2500, 5000)), F.BP, 1.2f, 0f, -1.4f, s.R(6, 14), 0.6f);
        }
        // Birds.
        for (int i = 0; i < 16; i++)
        {
            float t = s.R(0.3f, L - 1.5f);
            float a = s.R(0.06f, 0.14f);
            int kind = s.I(0, 4);
            if (kind == 0)
            {
                float f = s.R(2600, 3400);
                s.Tone(t, 0.18f, f, f * 1.3f, a, W.Sine, 0.02f, -1f);
                s.Tone(t + 0.22f, 0.25f, f * 1.25f, f * 0.9f, a, W.Sine, 0.02f, -1f, 30f, 0.02f);
            }
            else if (kind == 1)
            {
                int n = s.I(5, 11); float f = s.R(3800, 5000);
                for (int k = 0; k < n; k++) s.Tone(t + k * 0.065f, 0.045f, f, f * 1.2f, a * 0.8f, W.Sine, 0.005f, -1f);
            }
            else if (kind == 2)
            {
                float f = s.R(2000, 2600);
                s.Tone(t, 0.14f, f * 1.2f, f, a, W.Sine, 0.01f, -1f);
                s.Tone(t + 0.2f, 0.14f, f * 1.2f, f, a, W.Sine, 0.01f, -1f);
                s.Tone(t + 0.4f, 0.3f, f * 0.95f, f * 0.8f, a, W.Sine, 0.01f, -1f, 25f, 0.03f);
            }
            else
            {
                float f = s.R(1100, 1400); // distant soft dove-like hoot
                s.Tone(t, 0.35f, f, f * 0.92f, a * 0.6f, W.Sine, 0.04f, -1f);
                s.Tone(t + 0.5f, 0.5f, f * 0.95f, f * 0.85f, a * 0.6f, W.Sine, 0.04f, -1f);
            }
        }
        s.Reverb(0.35f, 0.8f);
        return s.LoopCrossfade(L, CF, 0.6f);
    }

    // Calm lo-fi menu beat: 80 BPM, 8 bars (24 s). Electric piano, round bass, swung drums, vinyl crackle.
    static float[] Lobby()
    {
        const float bpm = 80f, beat = 60f / bpm, bar = beat * 4f;
        const int bars = 8;
        float L = bar * bars;
        float[][] chords =
        {
            new float[] { 41, 57, 60, 64, 67 },   // F maj7 (C E G over F)
            new float[] { 40, 55, 59, 62, 66 },   // E m7 add11-ish
            new float[] { 38, 57, 60, 64, 65 },   // D m9
            new float[] { 36, 55, 59, 62, 64 },   // C maj7 add9
        };
        var keys = new Syn(L + 5f, 701);
        var bass = new Syn(L + 3f, 702);
        var dr = new Syn(L + 3f, 703);
        var mel = new Syn(L + 5f, 704);
        float swing = beat * 0.09f;

        for (int b = 0; b < bars; b++)
        {
            var ch = chords[b % 4];
            float t0 = b * bar;
            // Lazy EP stabs: beat 1, the "and" of 2, a soft ghost on 4.
            float[] hits = { 0f, beat * 1.5f + swing, beat * 3f };
            float[] vel = { 0.32f, 0.22f, 0.14f };
            for (int h = 0; h < hits.Length; h++)
                for (int n = 1; n < ch.Length; n++)
                    keys.EP(t0 + hits[h] + n * 0.012f, h == 0 ? bar * 0.9f : beat * 1.2f, Hz(ch[n]), vel[h]);
            // Bass: root on 1, a passing note before the next bar.
            bass.Tone(t0, beat * 1.8f, Hz(ch[0]), Hz(ch[0]), 0.9f, W.Sine, 0.01f, 1.4f);
            bass.Tone(t0, beat * 1.8f, Hz(ch[0] + 12), Hz(ch[0] + 12), 0.15f, W.Tri, 0.01f, 2.5f);
            bass.Tone(t0 + beat * 2.5f + swing, beat * 1.2f, Hz(ch[0] + 7), Hz(ch[0] + 7), 0.6f, W.Sine, 0.01f, 2f);
            // Drums: boom ... bap, with a little extra kick for bounce.
            float[] kicks = { 0f, beat * 1.75f + swing, beat * 2.5f + swing };
            foreach (float k in kicks)
            {
                dr.Tone(t0 + k, 0.35f, 95, 42, k == 0f ? 1f : 0.7f, W.Sine, 0.002f, 10f);
                dr.Noise(t0 + k, 0.02f, 0.15f, Fixed(2500), F.LP, 0.7f, 0.001f, 100f);
            }
            for (int s = 1; s < 4; s += 2)
            {
                float st = t0 + s * beat;
                dr.Noise(st, 0.25f, 0.5f, Fixed(1600), F.BP, 0.7f, 0.002f, 14f);
                dr.Noise(st, 0.12f, 0.25f, Fixed(5000), F.HP, 0.7f, 0.001f, 30f);
                dr.Tone(st, 0.1f, 190, 150, 0.25f, W.Tri, 0.001f, 30f);
            }
            for (int h = 0; h < 8; h++)
            {
                float ht = t0 + h * beat * 0.5f + (h % 2 == 1 ? swing : 0f);
                dr.Noise(ht, 0.05f, h % 2 == 0 ? 0.13f : 0.07f, Fixed(9000), F.HP, 0.8f, 0.001f, 70f);
            }
            // Sparse kalimba melody from the F major pentatonic.
            int[] pent = { 72, 74, 77, 79, 81, 84 };
            for (int e = 0; e < 8; e++)
            {
                if (mel.R(0, 1) < 0.62f) continue;
                mel.Bell(t0 + e * beat * 0.5f + (e % 2 == 1 ? swing : 0f), Hz(pent[mel.I(0, pent.Length)]), mel.R(0.2f, 0.32f), 4.5f, 3.01f, 1.2f, 1.6f);
            }
        }
        keys.LowPass(3200);
        keys.Tremolo(4.2f, 0.12f);
        keys.Reverb(0.35f, 0.8f);
        mel.Reverb(0.5f, 0.85f);
        dr.LowPass(7000);
        dr.Reverb(0.18f, 0.55f);

        var mix = new float[keys.b.Length];
        for (int i = 0; i < mix.Length; i++)
        {
            float x = keys.b[i] * 0.75f + mel.b[i] * 0.45f;
            if (i < bass.b.Length) x += bass.b[i] * 0.55f;
            if (i < dr.b.Length) x += dr.b[i] * 0.5f;
            mix[i] = x;
        }
        // Vinyl: soft hiss and sparse crackle over the top.
        var vinyl = new Syn(L, 705);
        vinyl.Noise(0, L, 0.05f, Fixed(5000), F.LP, 0.7f, 0f, 0f);
        vinyl.Clicks(0, L, 7f, 0.25f, 3000, 0.8f, 0.0015f);
        for (int i = 0; i < vinyl.b.Length; i++) mix[i] += vinyl.b[i];
        return Syn.Fold(mix, L, 0.75f);
    }

    // An original, gentle 12-bar loop at 90 BPM (exactly 32 s) plus a separate rhythm layer.
    static float[] Music(out float[] drumsOut)
    {
        const float bpm = 90f, beat = 60f / bpm, bar = beat * 4f;
        const int bars = 12;
        float L = bar * bars;
        float[][] chords =
        {
            new float[] { 50, 57, 61, 64, 66 },   // D maj9
            new float[] { 47, 54, 57, 61, 62 },   // B m9
            new float[] { 43, 50, 54, 59, 64 },   // G maj7(13)
            new float[] { 45, 52, 57, 59, 62 },   // A sus
            new float[] { 43, 50, 54, 59, 66 },   // G maj7
            new float[] { 45, 52, 57, 61, 64 },   // A
        };
        var pad = new Syn(L + 6f, 501);
        var pluck = new Syn(L + 6f, 502);
        for (int c = 0; c < 6; c++)
        {
            float t0 = c * bar * 2f;
            var ch = chords[c];
            for (int n = 1; n < ch.Length; n++) pad.Pad(t0, bar * 2f + 1.2f, Hz(ch[n]), 0.16f);
            // Soft sub bass on beats 1 and 3.
            for (int b = 0; b < 8; b += 2) pad.Tone(t0 + b * beat, beat * 2f, Hz(ch[0] - 12), Hz(ch[0] - 12), 0.5f, W.Sine, 0.03f, 1.6f);
            // Kalimba-like arpeggio in eighth notes.
            for (int e = 0; e < 16; e++)
            {
                if (pluck.R(0, 1) < 0.28f) continue;
                float note = ch[1 + pluck.I(0, ch.Length - 1)] + 12f + (pluck.R(0, 1) < 0.25f ? 12f : 0f);
                pluck.Bell(t0 + e * beat * 0.5f, Hz(note), pluck.R(0.25f, 0.4f), 5f, 3.01f, 1.4f, 1.2f);
            }
        }
        pad.Reverb(0.45f, 0.88f);
        pluck.Reverb(0.4f, 0.8f);
        var mix = new float[pad.b.Length];
        for (int i = 0; i < mix.Length; i++) mix[i] = pad.b[i] * 0.7f + pluck.b[i] * 0.55f;
        var music = Syn.Fold(mix, L, 0.8f);

        var dr = new Syn(L + 2f, 503);
        int beats = bars * 4;
        for (int b = 0; b < beats; b++)
        {
            float t = b * beat;
            if (b % 2 == 0)
            {
                dr.Tone(t, 0.35f, 120, 45, 1f, W.Sine, 0.001f, 12f);
                dr.Noise(t, 0.02f, 0.3f, Fixed(3000), F.HP, 0.7f, 0.0005f, 120f);
            }
            else
            {
                dr.Noise(t, 0.18f, 0.45f, Fixed(1800), F.BP, 0.8f, 0.001f, 18f);
                dr.Tone(t, 0.08f, 220, 180, 0.25f, W.Tri, 0.001f, 30f);
            }
            for (int h = 0; h < 4; h++)
                dr.Noise(t + h * beat * 0.25f, 0.05f, h % 2 == 0 ? 0.16f : 0.09f, Fixed(8000), F.HP, 0.8f, 0.001f, 60f);
            if (b % 8 == 7) dr.Tone(t + beat * 0.5f, 0.2f, 160, 90, 0.4f, W.Sine, 0.001f, 16f);
        }
        dr.Reverb(0.15f, 0.5f);
        drumsOut = Syn.Fold(dr.b, L, 0.75f);
        return music;
    }

    // ------------------------------------------------------------------ synthesis toolkit

    public enum W { Sine, Tri, Saw, Square }
    public enum F { LP, BP, HP }

    struct SVF
    {
        float ic1, ic2, a1, a2, a3, k;
        public void Set(float fc, float q)
        {
            fc = Mathf.Clamp(fc, 20f, SR * 0.45f);
            float g = Mathf.Tan(Mathf.PI * fc / SR);
            k = 1f / Mathf.Max(0.05f, q);
            a1 = 1f / (1f + g * (g + k)); a2 = g * a1; a3 = g * a2;
        }
        public float Run(float v0, F mode)
        {
            float v3 = v0 - ic2;
            float v1 = a1 * ic1 + a2 * v3;
            float v2 = ic2 + a2 * ic1 + a3 * v3;
            ic1 = 2f * v1 - ic1; ic2 = 2f * v2 - ic2;
            return mode == F.LP ? v2 : mode == F.BP ? v1 * k : v0 - k * v1 - v2;
        }
    }

    public class Syn
    {
        public float[] b;
        readonly System.Random r;

        public Syn(float seconds, int seed)
        {
            b = new float[Mathf.CeilToInt(seconds * SR)];
            r = new System.Random(seed);
        }

        public float R(float a, float c) => a + (float)r.NextDouble() * (c - a);
        public int I(int a, int c) => r.Next(a, c);
        float N() => (float)r.NextDouble() * 2f - 1f;
        int Idx(float t) => Mathf.Clamp((int)(t * SR), 0, b.Length);

        // dec >= 0: attack then exponential decay. dec < 0: smooth swell, |dec| = sharpness.
        static float Env(float t, float dur, float att, float dec)
        {
            float a;
            if (dec >= 0f) a = t < att ? t / Mathf.Max(att, 1e-5f) : Mathf.Exp(-dec * (t - att));
            else a = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur)), -dec);
            return a * Mathf.Clamp01((dur - t) / 0.008f);
        }

        static float Blep(float t, float dt)
        {
            if (t < dt) { t /= dt; return t + t - t * t - 1f; }
            if (t > 1f - dt) { t = (t - 1f) / dt; return t * t + t + t + 1f; }
            return 0f;
        }

        static float Osc(W w, float p, float dt)
        {
            switch (w)
            {
                case W.Sine: return Mathf.Sin(p * Mathf.PI * 2f);
                case W.Tri: return 4f * Mathf.Abs(p - 0.5f) - 1f;
                case W.Saw: return 2f * p - 1f - Blep(p, dt);
                default:
                    float q = p < 0.5f ? 1f : -1f;
                    return q + Blep(p, dt) - Blep((p + 0.5f) % 1f, dt);
            }
        }

        public void Tone(float t0, float dur, float f0, float f1, float amp, W w = W.Sine, float att = 0.003f, float dec = 8f,
                         float vibHz = 0f, float vibAmt = 0f, float curve = 1f)
        {
            int s = Idx(t0), e = Idx(t0 + dur);
            double ph = r.NextDouble();
            float ratio = f1 / f0;
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)SR;
                float k = t / dur;
                float f = f0 * Mathf.Pow(ratio, Mathf.Pow(k, curve));
                if (vibHz > 0f) f *= 1f + vibAmt * Mathf.Sin(t * vibHz * Mathf.PI * 2f);
                double dt = f / SR;
                ph += dt; if (ph >= 1.0) ph -= 1.0;
                b[i] += amp * Env(t, dur, att, dec) * Osc(w, (float)ph, (float)dt);
            }
        }

        public void Noise(float t0, float dur, float amp, Func<float, float> cut, F mode, float q = 0.7f, float att = 0.003f, float dec = 8f,
                          float tremHz = 0f, float tremAmt = 0f, bool brown = false)
        {
            int s = Idx(t0), e = Idx(t0 + dur);
            var f = new SVF();
            float br = 0f;
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)SR;
                if (((i - s) & 15) == 0) f.Set(cut(t / dur), q);
                float n = N();
                if (brown) { br = (br + 0.02f * n) / 1.02f; n = br * 3.5f; }
                float y = f.Run(n, mode);
                float tr = tremHz > 0f ? 1f - tremAmt * (0.5f + 0.5f * Mathf.Sin(t * tremHz * Mathf.PI * 2f)) : 1f;
                b[i] += amp * y * tr * Env(t, dur, att, dec);
            }
        }

        // Random impulse train through a band-pass: crunches, crackles, creaks, chitters.
        public void Clicks(float t0, float dur, float rate, float amp, float fc, float q = 1.2f, float clickLen = 0.004f, float dec = 0f)
        {
            int s = Idx(t0), e = Idx(t0 + dur);
            var f = new SVF(); f.Set(fc, q);
            float env = 0f, fall = Mathf.Exp(-1f / (clickLen * SR));
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)SR;
                if (r.NextDouble() < rate / SR) env = R(0.4f, 1f);
                float y = f.Run(N() * env, F.BP);
                env *= fall;
                float overall = (dec > 0f ? Mathf.Exp(-dec * t) : 1f) * Mathf.Clamp01((dur - t) / 0.008f);
                b[i] += amp * y * overall * 2f;
            }
        }

        // Two-operator FM bell / pluck.
        public void Bell(float t0, float f, float amp, float dec = 6f, float ratio = 3.5f, float index = 2f, float dur = -1f)
        {
            if (dur < 0f) dur = Mathf.Min(6f / dec, 3f);
            int s = Idx(t0), e = Idx(t0 + dur);
            float w = f * Mathf.PI * 2f;
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)SR;
                float env = Mathf.Min(1f, t / 0.002f) * Mathf.Exp(-dec * t) * Mathf.Clamp01((dur - t) / 0.01f);
                float idx = index * Mathf.Exp(-dec * 1.5f * t);
                b[i] += amp * env * Mathf.Sin(w * t + idx * Mathf.Sin(w * ratio * t));
            }
        }

        // Electric-piano tine: soft FM with a bell-like attack that mellows out.
        public void EP(float t0, float dur, float f, float amp)
        {
            int s = Idx(t0), e = Idx(t0 + dur + 0.3f);
            float w = f * Mathf.PI * 2f;
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)SR;
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-1.6f * t) * Mathf.Clamp01((dur + 0.3f - t) / 0.3f);
                float idx = 0.35f + 1.4f * Mathf.Exp(-9f * t);
                float y = Mathf.Sin(w * t + idx * Mathf.Sin(w * t)) + 0.25f * Mathf.Sin(2f * w * t) * Mathf.Exp(-4f * t);
                b[i] += amp * env * y;
            }
        }

        // Warm detuned-saw pad voice with a slowly breathing filter.
        public void Pad(float t0, float dur, float f, float amp)
        {
            int s = Idx(t0), e = Idx(t0 + dur);
            var flt = new SVF();
            double p1 = r.NextDouble(), p2 = r.NextDouble();
            float lfo = R(0, 6.28f);
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)SR;
                if (((i - s) & 31) == 0) flt.Set(700f + 450f * Mathf.Sin(t * 0.7f + lfo), 0.9f);
                double d1 = f * 0.997 / SR, d2 = f * 1.003 / SR;
                p1 += d1; if (p1 >= 1) p1 -= 1;
                p2 += d2; if (p2 >= 1) p2 -= 1;
                float x = Osc(W.Saw, (float)p1, (float)d1) + Osc(W.Saw, (float)p2, (float)d2);
                float env = Mathf.Clamp01(t / 0.9f) * Mathf.Clamp01((dur - t) / 1.2f);
                b[i] += amp * env * flt.Run(x, F.LP);
            }
        }

        public void LowPass(float fc) => LowPassSweep(Fixed(fc));

        public void LowPassSweep(Func<float, float> cut, float span = -1f)
        {
            var f = new SVF();
            float len = span > 0f ? span : b.Length / (float)SR;
            for (int i = 0; i < b.Length; i++)
            {
                if ((i & 15) == 0) f.Set(cut(Mathf.Clamp01(i / (float)SR / len)), 0.8f);
                b[i] = f.Run(b[i], F.LP);
            }
        }

        public void Tremolo(float hz, float depth)
        {
            for (int i = 0; i < b.Length; i++) b[i] *= 1f - depth * (0.5f + 0.5f * Mathf.Sin(i / (float)SR * hz * Mathf.PI * 2f));
        }

        public void Drive(float k)
        {
            float norm = (float)Math.Tanh(k);
            for (int i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * k) / norm;
        }

        // Compact Freeverb-style reverb (mono).
        public void Reverb(float mix, float room = 0.8f, float damp = 0.3f)
        {
            int[] combs = { 1116, 1188, 1277, 1356, 1422, 1491 };
            int[] alls = { 556, 441, 341 };
            float fb = 0.7f + 0.28f * room;
            var wet = new float[b.Length];
            foreach (int len in combs)
            {
                var buf = new float[len]; int idx = 0; float store = 0f;
                for (int i = 0; i < b.Length; i++)
                {
                    float o = buf[idx];
                    store = o * (1f - damp) + store * damp;
                    buf[idx] = b[i] * 0.015f + store * fb;
                    if (++idx >= len) idx = 0;
                    wet[i] += o;
                }
            }
            foreach (int len in alls)
            {
                var buf = new float[len]; int idx = 0;
                for (int i = 0; i < wet.Length; i++)
                {
                    float bo = buf[idx];
                    float o = -wet[i] + bo;
                    buf[idx] = wet[i] + bo * 0.5f;
                    if (++idx >= len) idx = 0;
                    wet[i] = o;
                }
            }
            float g = 2.2f;
            for (int i = 0; i < b.Length; i++) b[i] = b[i] * (1f - mix * 0.4f) + wet[i] * mix * g;
        }

        public void Norm(float peak)
        {
            float m = 0f;
            for (int i = 0; i < b.Length; i++) m = Mathf.Max(m, Mathf.Abs(b[i]));
            if (m < 1e-6f) return;
            float g = peak / m;
            for (int i = 0; i < b.Length; i++) b[i] *= g;
        }

        // Cut trailing silence and fade the very end.
        public float[] Trimmed()
        {
            int end = b.Length - 1;
            while (end > 0 && Mathf.Abs(b[end]) < 0.0015f) end--;
            int n = Mathf.Min(b.Length, end + SR / 50);
            var o = new float[Mathf.Max(1, n)];
            Array.Copy(b, o, o.Length);
            int fade = Mathf.Min(SR / 50, o.Length);
            for (int i = 0; i < fade; i++) o[o.Length - 1 - i] *= i / (float)fade;
            return o;
        }

        // Seamless loop: blend the overhang past L back into the start.
        public float[] LoopCrossfade(float L, float cf, float peak)
        {
            int n = (int)(L * SR), c = (int)(cf * SR);
            var o = new float[n];
            Array.Copy(b, o, n);
            for (int i = 0; i < c && n + i < b.Length; i++)
            {
                float k = i / (float)c;
                o[i] = o[i] * k + b[n + i] * (1f - k);
            }
            var s = new Syn(0, 0) { b = o };
            s.Norm(peak);
            return o;
        }

        // Seamless loop for rhythmic material: wrap the tail (reverb, held notes) onto the start.
        public static float[] Fold(float[] src, float L, float peak)
        {
            int n = (int)(L * SR);
            var o = new float[n];
            for (int i = 0; i < src.Length; i++) o[i % n] += src[i];
            var s = new Syn(0, 0) { b = o };
            s.Norm(peak);
            return o;
        }
    }
}
