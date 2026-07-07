using System.Collections.Generic;
using UnityEngine;

public class UniformFieldModel : FieldModel
{
    [SerializeField] private Vector3 size = Vector3.one;
    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private Vector3 direction = Vector3.up;
    [SerializeField] private float strength = 1f;
    [SerializeField] private Vector3Int counts = new Vector3Int(3, 3, 3);

    public override Vector3 SampleLocal(Vector3 p) => direction.normalized * strength;

    public override bool ContainsLocal(Vector3 p)
    {
        Vector3 d = p - center;
        return Mathf.Abs(d.x) <= size.x * 0.5f
            && Mathf.Abs(d.y) <= size.y * 0.5f
            && Mathf.Abs(d.z) <= size.z * 0.5f;
    }

    public override IEnumerable<Vector3> SamplePoints()
    {
        int nx = Mathf.Max(1, counts.x), ny = Mathf.Max(1, counts.y), nz = Mathf.Max(1, counts.z);
        for (int x = 0; x < nx; x++)
            for (int y = 0; y < ny; y++)
                for (int z = 0; z < nz; z++)
                    yield return center + Vector3.Scale(
                        new Vector3(Frac(x, nx), Frac(y, ny), Frac(z, nz)), size);
    }

    private static float Frac(int i, int n) => n <= 1 ? 0f : -0.5f + (float)i / (n - 1);
}