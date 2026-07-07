using UnityEngine;

public class WireFieldLines : MonoBehaviour
{
    [Header("Wire")]
    public float length = 2.5f;

    [Header("Field loops")]
    public float[] radii = { 0.3f, 0.6f, 1.0f };  // spacing widens => field weakens (1/r)
    public int heights = 3;                        // stacked sets along the wire

    [Header("Line look")]
    public int segments = 64;
    public float thickness = 0.02f;
    public float arrowSize = 0.14f;
    public bool clockwise = false;   // flip to match your current direction
    public Color color = new Color(0.3f, 0.7f, 1f);

    void Start() => Build();

    void Update() => Build();

    public void Build()
    {
        var old = transform.Find("FieldLines");
        if (old != null) { if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject); }
        var holder = new GameObject("FieldLines").transform;
        holder.SetParent(transform, false);

        var wire = NewLine(holder, "Wire");          // draw the wire itself
        wire.positionCount = 2;
        wire.SetPosition(0, new Vector3(0, -length * 0.5f, 0));
        wire.SetPosition(1, new Vector3(0, length * 0.5f, 0));
        wire.widthMultiplier = thickness * 1.6f;
        wire.startColor = wire.endColor = new Color(0.7f, 0.7f, 0.7f);

        int nH = Mathf.Max(1, heights);
        for (int h = 0; h < nH; h++)
        {
            float y = nH == 1 ? 0f : Mathf.Lerp(-length * 0.4f, length * 0.4f, h / (float)(nH - 1));
            foreach (float r in radii)
                if (r > 1e-4f) AddLoop(holder, r, y);
        }
    }

    void AddLoop(Transform parent, float r, float y)
    {
        var lr = NewLine(parent, "Loop");
        lr.loop = true;
        lr.positionCount = segments;
        lr.widthMultiplier = thickness;
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a)));
        }

        // arrowhead tangent to the loop, showing circulation direction
        float sign = clockwise ? -1f : 1f;
        Vector3 p = new Vector3(r, y, 0f);                  // point at angle 0
        Vector3 tangent = sign * new Vector3(0f, 0f, 1f);   // tangent there
        var head = NewLine(parent, "Head");
        head.positionCount = 2;
        head.SetPosition(0, p - tangent * arrowSize * 0.5f);
        head.SetPosition(1, p + tangent * arrowSize * 0.5f);
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