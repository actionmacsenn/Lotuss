using System.Collections;
using UnityEngine;

// Automated showcase used for testing builds: launch with "-lotusDemo -shotDir <folder>".
// Screenshots the menu screens, starts a run, casts every ability, shows the pink stragglers, then quits.
public class DemoDirector : MonoBehaviour
{
    string dir;

    IEnumerator Start()
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-shotDir");
        dir = i >= 0 && i + 1 < args.Length ? args[i + 1] : Application.persistentDataPath;
        System.IO.Directory.CreateDirectory(dir);

        // Snapshot the player's real progress so the demo leaves it untouched.
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        int realFlowers = Save.Flowers, realBest = Save.BestWave;
        var realLevels = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var ab in Abilities.All) foreach (var u in ab.upgrades) realLevels[u.key] = u.Level;

        // Give the demo some currency and a couple of upgrades so the shop shows variety.
        Save.Flowers = 640;
        Save.SetLevel("snap.count", 1);
        Save.SetLevel("root.size", 2);

        yield return new WaitForSeconds(3f);
        Shot("00_menu");
        yield return null; yield return null;   // screenshots are taken at the end of the frame
        Menu.I.OpenUpgrades();
        yield return new WaitForSecondsRealtime(0.5f);
        Shot("01_upgrades");
        yield return null; yield return null;
        Menu.I.OpenSettings();
        yield return new WaitForSecondsRealtime(0.4f);
        Shot("02_settings");
        yield return null; yield return null;
        Menu.I.Show(null);
        Game.I.StartGame();
        yield return new WaitForSeconds(1.1f);
        Shot("03_intro_swoop");
        yield return new WaitForSeconds(2.2f);

        var caster = AbilityCaster.I;
        caster.ForcedAim = new Vector3(0, 0, 30);
        yield return new WaitForSeconds(4f);
        Shot("04_wave1");
        Crowd(60);

        float[] shotDelay = { 1.5f, 0.35f, 0.3f, 1.6f, 2.2f, 0.7f, 2f, 0.9f, 1.1f, 1.0f, 2.2f, 1.2f };
        for (int a = 0; a < Abilities.All.Count; a++)
        {
            if (EnemyManager.All.Count < 50) Crowd(40);
            yield return new WaitForSeconds(1.2f);
            Vector3 target = Cluster();
            caster.ForcedAim = target;
            caster.Select(a);
            yield return null;
            caster.TryCast(a, target);
            yield return new WaitForSeconds(shotDelay[a]);
            Shot((a + 10).ToString("00") + "_" + Abilities.All[a].name.Replace(" ", ""));
            yield return new WaitForSeconds(1.5f);
        }

        // Down to the last few: they should glow pink with markers.
        // Wait out lingering effects (guardians, clouds) first so they don't eat the stragglers.
        yield return new WaitForSeconds(9f);
        while (EnemyManager.I.InBreak) yield return null;
        Crowd(20, 60f);
        yield return new WaitForSeconds(0.3f);
        EnemyManager.I.Thin(7);
        yield return new WaitForSeconds(1.5f);
        Shot("30_stragglers");
        yield return null; yield return null;

        if (SFX.I != null) SFX.I.Dump(System.IO.Path.Combine(dir, "sounds"));

        caster.Select(0);
        caster.OpenWheel(true);
        yield return new WaitForSecondsRealtime(0.6f);
        Shot("31_wheel");
        yield return null; yield return null;
        caster.OpenWheel(false);

        // Escape back to the menu.
        Game.ReturnToMenu();
        yield return new WaitForSecondsRealtime(2.5f);
        Shot("32_back_to_menu");
        yield return new WaitForSecondsRealtime(0.5f);

        Save.Flowers = realFlowers;
        PlayerPrefs.SetInt("bestWave", realBest);
        foreach (var kv in realLevels) Save.SetLevel(kv.Key, kv.Value);
        Save.Flush();
        Application.Quit();
    }

    void Crowd(int n, float zCenter = 40f)
    {
        for (int k = 0; k < n; k++)
        {
            var kind = Random.value < 0.08f ? EnemyKind.Brute : Random.value < 0.5f ? EnemyKind.Husk : EnemyKind.Mite;
            EnemyManager.I.Spawn(kind, new Vector3(Random.Range(-20f, 20f), 0, zCenter + Random.Range(-18f, 18f)));
        }
    }

    static Vector3 Cluster()
    {
        Vector3 sum = Vector3.zero; int c = 0;
        foreach (var e in EnemyManager.All)
        {
            if (e == null || !e.Alive) continue;
            Vector3 p = e.transform.position;
            if (p.z < 18f || p.z > 70f) continue;
            sum += p; c++;
        }
        return c > 0 ? LP.Flat(sum / c) : new Vector3(0, 0, 32);
    }

    void Shot(string name)
    {
        string path = System.IO.Path.Combine(dir, name + ".png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log("[Demo] screenshot " + path);
    }
}
