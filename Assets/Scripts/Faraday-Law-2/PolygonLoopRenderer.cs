using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PolygonLoopRenderer : MonoBehaviour
{
    [Header("Polygon")]
    [SerializeField] private int sides = 4;
    [SerializeField] private float radius = 1f;

    [Header("Transform Controls")]
    [SerializeField, Range(0.1f, 10f)] private float loopScale = 1f;
    [SerializeField] private Vector3 loopRotationEuler = Vector3.zero;

    [Header("Frame")]
    [SerializeField] private float frameRadius = 0.35f;
    [SerializeField] private Material frameMaterial;

    [Header("Shaded Area")]
    [SerializeField] private Material areaMaterial;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Transform frameParent;
    private Vector3[] vertices;

    public int Sides => sides;
    public float Radius => radius;
    public float LoopScale => loopScale;
    public Vector3 LoopRotationEuler => loopRotationEuler;
    public Vector3[] Vertices => vertices;

    private void Awake()
    {
        Init();
        RenderLoop();
    }



    private void Init()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        if (areaMaterial != null)
            meshRenderer.material = areaMaterial;

        CreateFrameParent();
    }

    public void SetPolygon(int newSides, float newRadius)
    {
        sides = Mathf.Clamp(newSides, 3, 64);
        radius = Mathf.Max(0.01f, newRadius);
        RenderLoop();
    }

    public void SetLoopScale(float newScale)
    {
        loopScale = Mathf.Max(0.1f, newScale);
        RenderLoop();
    }

    public void SetLoopRotation(Vector3 newEuler)
    {
        loopRotationEuler = newEuler;
        ApplyLocalRotation();
    }

    private void CreateFrameParent()
    {
        Transform existing = transform.Find("CylinderFrame");

        if (existing != null)
        {
            frameParent = existing;
            return;
        }

        GameObject parent = new GameObject("CylinderFrame");
        parent.transform.SetParent(transform, false);
        frameParent = parent.transform;
    }

    private void RenderLoop()
    {
        if (meshFilter == null || meshRenderer == null)
            Init();

        BuildVertices();
        BuildAreaMesh();
        BuildCylinderFrame();
        ApplyLocalRotation();
    }

    private void BuildVertices()
    {
        vertices = new Vector3[sides];

        float scaledRadius = radius * loopScale;

        for (int i = 0; i < sides; i++)
        {
            float angle = Mathf.PI * 2f * i / sides;
            float x = Mathf.Cos(angle) * scaledRadius;
            float z = Mathf.Sin(angle) * scaledRadius;

            vertices[i] = new Vector3(x, 0f, z);
        }
    }

    private void BuildAreaMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Polygon Shaded Area";

        Vector3[] meshVertices = new Vector3[sides + 1];

        // Double-sided: front + back triangles
        int[] triangles = new int[sides * 6];

        meshVertices[0] = Vector3.zero;

        for (int i = 0; i < sides; i++)
        {
            meshVertices[i + 1] = vertices[i];
        }

        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            int index = i * 6;

            // Front face
            triangles[index] = 0;
            triangles[index + 1] = i + 1;
            triangles[index + 2] = next + 1;

            // Back face
            triangles[index + 3] = 0;
            triangles[index + 4] = next + 1;
            triangles[index + 5] = i + 1;
        }

        mesh.vertices = meshVertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;
    }

    //private void BuildAreaMesh()
    //{
    //    Mesh mesh = new Mesh();
    //    mesh.name = "Polygon Shaded Area";

    //    Vector3[] meshVertices = new Vector3[sides + 1];
    //    int[] triangles = new int[sides * 3];

    //    meshVertices[0] = Vector3.zero;

    //    for (int i = 0; i < sides; i++)
    //    {
    //        meshVertices[i + 1] = vertices[i];
    //    }

    //    for (int i = 0; i < sides; i++)
    //    {
    //        int next = (i + 1) % sides;

    //        triangles[i * 3] = 0;
    //        triangles[i * 3 + 1] = i + 1;
    //        triangles[i * 3 + 2] = next + 1;
    //    }

    //    mesh.vertices = meshVertices;
    //    mesh.triangles = triangles;
    //    mesh.RecalculateNormals();
    //    mesh.RecalculateBounds();

    //    meshFilter.mesh = mesh;
    //}

    private void BuildCylinderFrame()
    {
        ClearFrame();

        for (int i = 0; i < sides; i++)
        {
            Vector3 a = vertices[i];
            Vector3 b = vertices[(i + 1) % sides];

            CreateCylinderBetween(a, b, "Edge_" + i);
        }
    }

    private void CreateCylinderBetween(Vector3 start, Vector3 end, string name)
    {
        Vector3 midpoint = (start + end) * 0.5f;
        Vector3 direction = end - start;
        float length = direction.magnitude;

        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(frameParent, false);

        cylinder.transform.localPosition = midpoint;
        cylinder.transform.localScale = new Vector3(frameRadius, length * 0.5f, frameRadius);
        cylinder.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);

        if (frameMaterial != null)
        {
            Renderer renderer = cylinder.GetComponent<Renderer>();
            renderer.material = frameMaterial;
        }

        Collider collider = cylinder.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
    }

    private void ClearFrame()
    {
        if (frameParent == null)
            CreateFrameParent();

        for (int i = frameParent.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
                Destroy(frameParent.GetChild(i).gameObject);
            else
                DestroyImmediate(frameParent.GetChild(i).gameObject);
        }
    }

    public void AlignAreaVectorTo(Vector3 targetNormal)
    {
        if (targetNormal.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.FromToRotation(transform.up, targetNormal.normalized) * transform.rotation;
    }

    private void ApplyLocalRotation()
    {
        transform.localRotation = Quaternion.Euler(loopRotationEuler);
        //Quaternion rotX = Quaternion.AngleAxis(loopRotationEuler.x, Vector3.right);
        //Quaternion rotY = Quaternion.AngleAxis(loopRotationEuler.y, Vector3.up);
        //Quaternion rotZ = Quaternion.AngleAxis(loopRotationEuler.z, Vector3.forward);

        //transform.localRotation = rotY * rotX * rotZ;
    }

    private void OnValidate()
    {
        sides = Mathf.Clamp(sides, 3, 64);
        radius = Mathf.Max(0.01f, radius);
        loopScale = Mathf.Max(0.1f, loopScale);
        frameRadius = Mathf.Max(0.001f, frameRadius);

        if (!Application.isPlaying) return;

        RenderLoop();
    }
}