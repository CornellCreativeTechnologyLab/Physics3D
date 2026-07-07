using UnityEngine;

public class ToroidFieldLines : MonoBehaviour
{
    [Header("Donut shape")]
    public float majorRadius = 1.0f;   // axis to tube center
    public float minorRadius = 0.35f;  // tube radius

    [Header("How many loops")]
    public int shells = 3;   // nested loops from core to wall
    public int spokes = 6;   // loops around the tube cross-section

    [Header("Line look")]
    public int segments = 64;    // smoothness of each loop
    public float thickness = 0.02f;
    public Color color = new Color(0.3f, 0.7f, 1f);

    void Start() => Build();

    void Update() => Build();

    public void Build()
    {
        // clear any previous loops, then make a fresh holder (no duplicate buildup)
        var old = transform.Find("FieldLines");
        if (old != null)
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }
        var holder = new GameObject("FieldLines").transform;
        holder.SetParent(transform, false);

        int nShell = Mathf.Max(1, shells);
        for (int s = 0; s < nShell; s++)
        {
            float rho = nShell == 1 ? 0f : Mathf.Lerp(0f, minorRadius, s / (float)(nShell - 1));

            if (rho < 1e-4f)            // core: one loop down the tube's spine
            {
                AddLoop(holder, majorRadius, 0f);
                continue;
            }

            int nSpoke = Mathf.Max(1, spokes);
            for (int p = 0; p < nSpoke; p++)
            {
                float phi = p / (float)nSpoke * Mathf.PI * 2f;
                float r = majorRadius + rho * Mathf.Cos(phi);  // axis-distance of this loop
                float y = rho * Mathf.Sin(phi);                // height of this loop
                AddLoop(holder, r, y);
            }
        }
    }

    // One field line = a horizontal circle of radius r at height y, in local space.
    void AddLoop(Transform parent, float r, float y)
    {
        var go = new GameObject("Loop");
        go.transform.SetParent(parent, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;     // follow this object's transform
        lr.loop = true;               // close the circle
        lr.positionCount = segments;
        lr.widthMultiplier = thickness;
        lr.numCapVertices = 2;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = color;

        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a)));
        }
    }


}