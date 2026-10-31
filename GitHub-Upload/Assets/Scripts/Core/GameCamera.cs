using UnityEngine;

// Two poses: in the menu it sits low in the meadow looking up at the towering lotus;
// in play it is a tilted top-down view from behind the lotus, which fills the bottom of the frame.
public class GameCamera : MonoBehaviour
{
    public static GameCamera I;
    public Camera Cam { get; private set; }

    public Vector3 basePos = new Vector3(0f, 27f, -19f);
    public Vector3 lookAt = new Vector3(0f, 0f, 23f);
    public Vector3 menuPos = new Vector3(5f, 3.2f, 25f);
    public Vector3 menuLook = new Vector3(-3.5f, 11.5f, 0f);

    float trauma;
    Vector3 drift;
    float play, playTarget, playVel;

    void Awake()
    {
        I = this;
        Cam = gameObject.AddComponent<Camera>();
        gameObject.tag = "MainCamera";
        gameObject.AddComponent<AudioListener>();
        Cam.fieldOfView = 52f;
        Cam.nearClipPlane = 0.3f;
        Cam.farClipPlane = 400f;
        Cam.clearFlags = CameraClearFlags.SolidColor;
        Cam.backgroundColor = WorldBuilder.FogColor;
        Cam.allowMSAA = true;
        QualitySettings.antiAliasing = 4;
        transform.position = menuPos;
        transform.LookAt(menuLook);
    }

    public void SetMenu(bool menu) => playTarget = menu ? 0f : 1f;

    public static void Shake(float amount)
    {
        if (I) I.trauma = Mathf.Min(1.2f, I.trauma + amount);
    }

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        play = Mathf.SmoothDamp(play, playTarget, ref playVel, 0.9f, Mathf.Infinity, dt);
        float k = LP.Smooth(play);

        // Slight parallax toward the mouse so the scene feels alive.
        Vector2 m = new Vector2(Input.mousePosition.x / Mathf.Max(1, Screen.width) - 0.5f, Input.mousePosition.y / Mathf.Max(1, Screen.height) - 0.5f);
        Vector3 targetDrift = new Vector3(m.x * 2.2f, 0f, m.y * 1.2f) * k;
        drift = Vector3.Lerp(drift, targetDrift, dt * 2f);

        trauma = Mathf.MoveTowards(trauma, 0f, dt * 1.4f);
        float s = trauma * trauma;
        float t = Time.unscaledTime * 22f;
        Vector3 shake = new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, Mathf.PerlinNoise(0.3f, t) - 0.5f, Mathf.PerlinNoise(t, t * 0.5f) - 0.5f) * 2.2f * s;

        // Menu pose drifts gently, like a slow handheld shot.
        float mt = Time.unscaledTime;
        Vector3 menuSway = new Vector3(Mathf.Sin(mt * 0.13f) * 2.2f, Mathf.Sin(mt * 0.21f) * 0.4f, Mathf.Sin(mt * 0.1f) * 1.2f);

        // Swing through an arc above the pit rather than straight through the flower.
        Vector3 pos = Vector3.Lerp(menuPos + menuSway, basePos + drift, k);
        pos.y += Mathf.Sin(k * Mathf.PI) * 14f;
        pos.x += Mathf.Sin(k * Mathf.PI) * 16f;
        Vector3 look = Vector3.Lerp(menuLook + menuSway * 0.3f, lookAt + drift * 0.6f, k);

        transform.position = pos + shake;
        transform.LookAt(look + shake * 0.3f);
        Cam.fieldOfView = Mathf.Lerp(58f, 52f, k);
    }

    // Mouse ray onto the ground plane (y = 0).
    public bool MouseGround(out Vector3 point)
    {
        Ray r = Cam.ScreenPointToRay(Input.mousePosition);
        if (r.direction.y < -0.001f)
        {
            float d = -r.origin.y / r.direction.y;
            point = r.origin + r.direction * d;
            return true;
        }
        point = r.origin + r.direction * 100f;
        point.y = 0f;
        return false;
    }
}
