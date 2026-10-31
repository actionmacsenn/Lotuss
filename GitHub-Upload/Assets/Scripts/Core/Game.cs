using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Root object. Builds every system at startup so the game runs from any (even empty) scene.
// Flow: Menu (lotus towers over you) -> Intro (camera swoops into position) -> Playing (waves). Esc returns to Menu.
public class Game : MonoBehaviour
{
    public static Game I;

    public enum Mode { Menu, Intro, Playing }
    public Mode State { get; private set; } = Mode.Menu;

    public int Kills { get; private set; }
    public int Combo { get; private set; }
    public int BestCombo { get; private set; }
    public float ComboTimer { get; private set; }
    public float KillPulse { get; private set; }
    public int SessionFlowers { get; private set; }

    static bool demo;
    float introT;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        demo = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-lotusDemo") >= 0;
        Create();
    }

    static void Create()
    {
        if (FindAnyObjectByType<Game>() != null) return;
        new GameObject("Lotus Game").AddComponent<Game>();
    }

    void Awake()
    {
        I = this;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        Application.targetFrameRate = 144;
        QualitySettings.vSyncCount = 1;
        QualitySettings.shadowDistance = 170f;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.High;

        // Clear whatever the scene came with; the game builds its own camera and light.
        foreach (var c in FindObjectsByType<Camera>()) Destroy(c.gameObject);
        foreach (var l in FindObjectsByType<Light>()) Destroy(l.gameObject);

        // Audio lives across menu resets so music never restarts and sounds are synthesized only once.
        if (SFX.I == null)
        {
            var sfx = new GameObject("SFX");
            DontDestroyOnLoad(sfx);
            sfx.AddComponent<SFX>();
        }

        Add<FX>("FX");
        Add<WorldBuilder>("World");
        Add<BloomFlowers>("Bloom Flowers");
        Add<GameCamera>("Camera");
        Add<Lotus>("Lotus");
        Add<EnemyManager>("Enemies");
        Add<AbilityCaster>("Abilities").enabled = false;
        Add<HUD>("HUD");
        Add<Menu>("Menu");
        SFX.I.SetMenuMusic(true);
        if (demo && FindAnyObjectByType<DemoDirector>() == null) Add<DemoDirector>("Demo");
    }

    T Add<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        return go.AddComponent<T>();
    }

    public void StartGame()
    {
        if (State != Mode.Menu) return;
        State = Mode.Intro;
        introT = 0f;
        Lotus.I.SetMenu(false);
        GameCamera.I.SetMenu(false);
        SFX.I.SetMenuMusic(false);
        SFX.Play(SFX.S.GroundTear, 0.7f, 0f);
        SFX.Play(SFX.S.Charge, 0.6f, 0f);
    }

    // Tear everything down and rebuild a fresh world in menu mode.
    public static void ReturnToMenu()
    {
        Save.Flush();
        Time.timeScale = 1f;
        if (SFX.I) SFX.I.StartCoroutine(Rebuild());
    }

    static IEnumerator Rebuild()
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) Destroy(root);
        yield return null;
        EnemyManager.All.Clear();
        Create();
    }

    public void OnKill()
    {
        Kills++;
        Combo++;
        if (Combo > BestCombo) BestCombo = Combo;
        if (Combo == 15 || Combo == 30 || Combo == 50 || Combo == 80 || (Combo > 80 && Combo % 50 == 0)) SFX.Play(SFX.S.Combo, 1f, 0f);
        ComboTimer = 1.6f;
        KillPulse = 1f;
    }

    public void AddFlowers(int n, Vector3 from)
    {
        if (State != Mode.Playing) return;
        Save.Flowers += n;
        SessionFlowers += n;
        if (HUD.I) HUD.I.FlyFlowers(from, n);
    }

    void Update()
    {
        KillPulse = Mathf.MoveTowards(KillPulse, 0f, Time.unscaledDeltaTime * 4f);
        if (ComboTimer > 0f)
        {
            ComboTimer -= Time.deltaTime;
            if (ComboTimer <= 0f) Combo = 0;
        }

        if (State == Mode.Intro)
        {
            introT += Time.deltaTime;
            if (introT > 2.4f)
            {
                State = Mode.Playing;
                EnemyManager.I.Begin();
                AbilityCaster.I.enabled = true;
            }
        }

        if (State != Mode.Menu && Input.GetKeyDown(KeyCode.Escape))
        {
            if (AbilityCaster.I && AbilityCaster.I.WheelOpen) AbilityCaster.I.OpenWheel(false);
            ReturnToMenu();
        }
    }

    void OnApplicationQuit() => Save.Flush();
}
