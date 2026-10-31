using UnityEngine;
using UnityEngine.UI;

// Procedurally drawn radial menu: ring segments, cooldown shading and a centre "next page" button.
public class WheelGraphic : MaskableGraphic
{
    public int count = 6;
    public float inner = 120f, outer = 300f, centerR = 85f;
    public int hover = -1;
    public bool hoverCenter;
    public int selected = -1;
    public Color[] colors = new Color[6];
    public float[] cooldown = new float[6];   // 0..1 remaining
    public float open = 1f;                   // 0..1 open animation
    public float hoverAnim;                   // eases in on the hovered segment

    public const float Gap = 2.2f;

    public void Refresh() => SetVerticesDirty();

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float o = Mathf.Max(0.001f, open);
        float step = 360f / count;
        for (int i = 0; i < count; i++)
        {
            bool h = i == hover;
            float a0 = 90f - i * step + step * 0.5f - Gap;   // segment 0 centred at top, clockwise
            float a1 = 90f - i * step - step * 0.5f + Gap;
            float rin = inner * o, rout = (outer + (h ? 28f * hoverAnim : 0f)) * o;
            Color baseCol = colors[i];
            Color fill = Color.Lerp(new Color(0.09f, 0.07f, 0.1f, 0.88f), baseCol * 0.55f, h ? 0.85f : 0.35f);
            fill.a = 0.9f;
            Color rim = baseCol; rim.a = h ? 1f : 0.85f;
            if (i == selected) fill = Color.Lerp(fill, baseCol * 0.7f, 0.45f);

            Arc(vh, rin, rout - 10f * o, a0, a1, fill, fill);
            Arc(vh, rout - 10f * o, rout, a0, a1, rim, rim);
            // Cooldown shading sweeps outward from the inner edge.
            if (cooldown[i] > 0f)
            {
                float cr = Mathf.Lerp(rin, rout, cooldown[i]);
                Arc(vh, rin, cr, a0, a1, new Color(0f, 0f, 0f, 0.55f), new Color(0f, 0f, 0f, 0.55f));
            }
        }
        // Centre button.
        Color cc = hoverCenter ? new Color(1f, 0.55f, 0.8f, 0.95f) : new Color(0.12f, 0.09f, 0.12f, 0.92f);
        Arc(vh, 0f, centerR * o, 0f, 360f, cc, cc, 40);
        Color cr2 = new Color(1f, 0.6f, 0.82f, 0.9f);
        Arc(vh, centerR * o - 5f, centerR * o, 0f, 360f, cr2, cr2, 40);
    }

    static void Arc(VertexHelper vh, float r0, float r1, float a0, float a1, Color c0, Color c1, int steps = 14)
    {
        for (int s = 0; s < steps; s++)
        {
            float t0 = Mathf.Lerp(a0, a1, s / (float)steps) * Mathf.Deg2Rad;
            float t1 = Mathf.Lerp(a0, a1, (s + 1) / (float)steps) * Mathf.Deg2Rad;
            Vector2 d0 = new Vector2(Mathf.Cos(t0), Mathf.Sin(t0)), d1 = new Vector2(Mathf.Cos(t1), Mathf.Sin(t1));
            int i = vh.currentVertCount;
            vh.AddVert(d0 * r0, c0, Vector4.zero);
            vh.AddVert(d0 * r1, c1, Vector4.zero);
            vh.AddVert(d1 * r1, c1, Vector4.zero);
            vh.AddVert(d1 * r0, c0, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }

    // Which segment a canvas-space offset from the wheel centre points at (-1 none, -2 centre).
    public int HitTest(Vector2 local)
    {
        float d = local.magnitude;
        if (d < centerR) return -2;
        if (d < inner * 0.55f) return -1;
        float ang = Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg; // 0 = up, clockwise positive
        if (ang < 0f) ang += 360f;
        float step = 360f / count;
        return Mathf.FloorToInt((ang + step * 0.5f) / step) % count;
    }
}
