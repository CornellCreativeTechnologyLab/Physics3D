using UnityEngine;

public class SolenoidFieldLines : MonoBehaviour
{
    [Header("Cylinder")]
    public float radius = 0.6f;
    public float height = 2.0f;

    [Header("How many lines")]
    public int rings = 2;   // radial shells (plus one center line)
    public int perRing = 8;   // lines around each shell

    [Header("Line look")]
    public float thickness = 0.02f;
    public float arrowSize = 0.14f;
    public bool pointUp = true;   // flip to match your coil's current direction
    public Color color = new Color(1f, 0.55f, 0.2f);

    void Start() => Build();

    void Update() => Build();
    public void Build()
    {
        var old = transform.Find("FieldLines");
        if (old != null) { if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject); }
        var holder = new GameObject("FieldLines").transform;
        holder.SetParent(transform, false);

        AddLine(holder, Vector3.zero);                        // center line
        int nRing = Mathf.Max(0, rings);
        for (int s = 1; s <= nRing; s++)
        {
            float r = radius * s / nRing;
            int n = Mathf.Max(1, perRing);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                AddLine(holder, new Vector3(r * Mathf.Cos(a), 0f, r * Mathf.Sin(a)));
            }
        }
    }

    void AddLine(Transform parent, Vector3 xz)
    {
        float dir = pointUp ? 1f : -1f;
        Vector3 start = new Vector3(xz.x, -dir * height * 0.5f, xz.z);
        Vector3 tip = new Vector3(xz.x, dir * height * 0.5f, xz.z);

        var lr = NewLine(parent, "Line");
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, tip);
        lr.widthMultiplier = thickness;

        // arrowhead: a camera-facing triangle made by tapering width to zero
        var head = NewLine(parent, "Head");
        head.positionCount = 2;
        head.SetPosition(0, tip - new Vector3(0, dir * arrowSize, 0));
        head.SetPosition(1, tip);
        head.numCapVertices = 0;
        head.widthCurve = new AnimationCurve(new Keyframe(0, 1f), new Keyframe(1, 0f));
        head.widthMultiplier = arrowSize * 0.7f;
    }

    LineRenderer NewLine(Transform parent, string n)
    {
        var go = new GameObject(n);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.numCapVertices = 2;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = color;
        return lr;
    }
}