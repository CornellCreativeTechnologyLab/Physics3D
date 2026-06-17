using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A 3D volumetric "conveyor belt" of charge prefabs for the Hall-effect sim.
///
/// Charges are laid out once inside a local box (length x width x thickness) and
/// recycled forever by advancing a single shared phase and wrapping with
/// Mathf.Repeat -- no runtime instantiation / destruction, no respawning.
///
/// The box is a BOUNDING region. An optional conductor shape (a Collider, or a
/// custom predicate) carves the real occupied volume out of it, so charges sit
/// naturally inside the material. For a prismatic conductor (constant cross-section
/// along the flow axis -- the usual Hall bar), containment is tested ONCE on the
/// cross-section at build time and stays valid as charges scroll: still zero
/// per-frame shape cost.
///
/// Local axes:
///   +X = flow direction (length)
///   +Y = lateral / Hall-deflection axis (width)
///   +Z = depth (thickness)
/// The box is centered on the transform, so transform.position is the belt center.
///
/// Charges store NORMALIZED coordinates, so length / width / thickness are live
/// knobs. Changing ChargeCount, the shape, or the box size requires a Rebuild()
/// for containment to be re-evaluated.
/// </summary>
[DisallowMultipleComponent]
public class ChargeBelt : MonoBehaviour
{
    public enum Arrangement { Scatter, Lattice }

    [Header("Charge Prefab")]
    [Tooltip("The 3D charge visual to instance. Set its material/color on the prefab.")]
    [SerializeField] private Transform chargePrefab;

    [Header("Belt Volume (local space) -- bounding box")]
    [Tooltip("Length along local +X (flow direction).")]
    [SerializeField] private float length = 5f;
    [Tooltip("Width along local +Y (Hall deflection axis).")]
    [SerializeField] private float width = 1f;
    [Tooltip("Thickness along local +Z (depth).")]
    [SerializeField] private float thickness = 0.5f;

    [Header("Containment Shape (optional)")]
    [Tooltip("Charges are kept only where they fall inside this collider. " +
             "Use BoxCollider for a slab, or a convex MeshCollider/Capsule for " +
             "rounded bars. Leave empty to fill the whole box.")]
    [SerializeField] private Collider conductorShape;
    [Tooltip("ON: cross-section is constant along flow (a prism) -> test once, " +
             "free at runtime. OFF: shape varies along flow -> per-frame test " +
             "(more expensive, charges pop in/out at the silhouette).")]
    [SerializeField] private bool prismaticAlongFlow = true;

    [Header("Arrangement")]
    [SerializeField] private Arrangement arrangement = Arrangement.Lattice;
    [Tooltip("Lattice resolution: X = planes along flow, Y = width, Z = thickness.")]
    [SerializeField] private Vector3Int latticeCounts = new Vector3Int(40, 8, 8);
    [Tooltip("Target count for Scatter mode (Lattice derives its own count).")]
    [SerializeField] private int chargeCount = 240;
    [Tooltip("Seed for Scatter layout. Same seed + same count makes two belts align.")]
    [SerializeField] private int seed = 12345;

    [Header("Motion")]
    [Tooltip("Scroll speed in world units/sec. Drive from voltage. Negative reverses.")]
    [SerializeField] private float driftSpeed = 1.5f;
    [Range(0f, 1f)]
    [Tooltip("Phase offset as a fraction of length [0,1) -- interleave belts.")]
    [SerializeField] private float startOffset = 0f;

    [Header("Hall Polarization (visual)")]
    [Range(-1f, 1f)]
    [Tooltip("-1..1 lateral skew. Packs charges toward one width edge (accumulation).")]
    [SerializeField] private float lateralPolarization = 0f;
    [Range(0f, 1f)]
    [SerializeField] private float polarizationStrength = 0.9f;

    // ---- runtime state ----
    private struct Charge
    {
        public Transform t;
        public float slot; // [0,1) longitudinal slot
        public float u;    // [-0.5,0.5] lateral (width)
        public float v;    // [-0.5,0.5] depth (thickness)
    }

    private Charge[] charges;
    private readonly List<Transform> pool = new();
    private float phase;        // [0,1), advances each frame
    private bool liveCulling;   // true when a per-frame containment test is needed

    /// <summary>
    /// Optional custom containment test (world point -> inside?). If set, it is used
    /// instead of conductorShape. Plug in an SDF, voxel mask, concave test, etc.
    /// Set this before Rebuild() (or call Rebuild() after assigning).
    /// </summary>
    public System.Func<Vector3, bool> ContainmentOverride;

    // ============================================================ PUBLIC API ==

    public float Length { get => length; set => length = Mathf.Max(0.0001f, value); }
    public float Width { get => width; set => width = Mathf.Max(0f, value); }
    public float Thickness { get => thickness; set => thickness = Mathf.Max(0f, value); }

    /// <summary>Scatter-mode target count. (Lattice mode derives its own count.)</summary>
    public int ChargeCount
    {
        get => chargeCount;
        set { value = Mathf.Max(0, value); if (value != chargeCount) { chargeCount = value; Rebuild(); } }
    }

    public float DriftSpeed { get => driftSpeed; set => driftSpeed = value; }
    public float StartOffset { get => startOffset; set => startOffset = Mathf.Repeat(value, 1f); }
    public float LateralPolarization
    {
        get => lateralPolarization;
        set => lateralPolarization = Mathf.Clamp(value, -1f, 1f);
    }

    /// <summary>The conductor shape charges are confined to. Reassigning rebuilds.</summary>
    public Collider ConductorShape
    {
        get => conductorShape;
        set { conductorShape = value; Rebuild(); }
    }

    public Vector3 BeltCenter { get => transform.position; set => transform.position = value; }
    public float VerticalOffset
    {
        get => transform.position.y;
        set { var p = transform.position; p.y = value; transform.position = p; }
    }

    public float Phase01 { get => phase; set => phase = Mathf.Repeat(value, 1f); }

    /// <summary>Bounding-box volume (not the carved volume).</summary>
    public float Volume => length * width * thickness;
    public float Density => Volume > 1e-6f ? chargeCount / Volume : 0f;

    public void SetCrossSection(float newWidth, float newThickness)
    {
        width = Mathf.Max(0f, newWidth);
        thickness = Mathf.Max(0f, newThickness);
        Rebuild(); // re-evaluate containment against the new cross-section
    }

    public void SetDensity(float targetDensity)
    {
        ChargeCount = Mathf.Max(0, Mathf.RoundToInt(targetDensity * Volume));
    }

    public void SetPhaseSlots(float slots)
    {
        int n = Mathf.Max(1, charges != null ? charges.Length : chargeCount);
        startOffset = Mathf.Repeat(slots / n, 1f);
    }

    /// <summary>Rebuild the charge layout (arrangement + containment). Auto-called on Awake.</summary>
    public void Rebuild()
    {
        bool hasShape = conductorShape != null || ContainmentOverride != null;
        liveCulling = hasShape && !prismaticAlongFlow;

        List<Charge> built = arrangement == Arrangement.Lattice ? BuildLattice() : BuildScatter();
        int n = built.Count;

        EnsurePool(n);
        charges = new Charge[n];
        for (int i = 0; i < n; i++)
        {
            Charge c = built[i];
            c.t = pool[i];
            charges[i] = c;
            pool[i].gameObject.SetActive(true);
        }
        for (int i = n; i < pool.Count; i++)
            pool[i].gameObject.SetActive(false);
    }

    // ============================================================ BUILDERS ====

    private List<Charge> BuildLattice()
    {
        int nx = Mathf.Max(1, latticeCounts.x);
        int ny = Mathf.Max(1, latticeCounts.y);
        int nz = Mathf.Max(1, latticeCounts.z);

        // Valid cross-section columns. For a prism, cull here once and reuse for
        // every longitudinal plane. Otherwise keep all and cull live each frame.
        var cols = new List<Vector2>(ny * nz);
        for (int iy = 0; iy < ny; iy++)
            for (int iz = 0; iz < nz; iz++)
            {
                float u = (iy + 0.5f) / ny - 0.5f;
                float v = (iz + 0.5f) / nz - 0.5f;
                if (prismaticAlongFlow && !IsInside(SamplePoint(0.5f, u, v))) continue;
                cols.Add(new Vector2(u, v));
            }

        var result = new List<Charge>(nx * cols.Count);
        for (int ix = 0; ix < nx; ix++)
        {
            float slot = (ix + 0.5f) / nx;
            for (int j = 0; j < cols.Count; j++)
                result.Add(new Charge { slot = slot, u = cols[j].x, v = cols[j].y });
        }
        return result;
    }

    private List<Charge> BuildScatter()
    {
        var rng = new System.Random(seed);
        int target = Mathf.Max(0, chargeCount);
        var result = new List<Charge>(target);

        int attempts = 0, maxAttempts = target * 40 + 1000;
        while (result.Count < target && attempts++ < maxAttempts)
        {
            float slot = (float)rng.NextDouble();
            float u = (float)rng.NextDouble() - 0.5f;
            float v = (float)rng.NextDouble() - 0.5f;
            if (prismaticAlongFlow && !IsInside(SamplePoint(0.5f, u, v))) continue;
            result.Add(new Charge { slot = slot, u = u, v = v });
        }

        if (result.Count < target)
            Debug.LogWarning($"{name}: placed {result.Count}/{target} charges inside the shape " +
                             $"(shape may be small relative to the box).", this);
        return result;
    }

    // ============================================================ INTERNALS ===

    private void Awake() => Rebuild();

    private void LateUpdate()
    {
        if (charges == null || charges.Length == 0) return;

        if (length > 1e-4f)
        {
            phase += (driftSpeed / length) * Time.deltaTime;
            phase = Mathf.Repeat(phase, 1f);
        }

        float p = lateralPolarization * polarizationStrength;
        float dir = p >= 0f ? 1f : -1f;
        float k = 1f + Mathf.Abs(p) * 4f;

        for (int i = 0; i < charges.Length; i++)
        {
            var c = charges[i];
            if (c.t == null) continue;

            float s = Mathf.Repeat(c.slot + phase + startOffset, 1f);
            float x = (s - 0.5f) * length;

            float lat = Mathf.Clamp01((c.u * dir) + 0.5f);
            lat = Mathf.Pow(lat, 1f / k);
            float y = ((lat - 0.5f) * dir) * width;

            float z = c.v * thickness;

            c.t.localPosition = new Vector3(x, y, z);

            if (liveCulling)
            {
                bool inside = IsInside(c.t.position);
                if (c.t.gameObject.activeSelf != inside)
                    c.t.gameObject.SetActive(inside);
            }
        }
    }

    /// <summary>World point for a normalized (slot, u, v) -- containment is tested here.</summary>
    private Vector3 SamplePoint(float slot, float u, float v)
    {
        float x = (slot - 0.5f) * length;
        return transform.TransformPoint(new Vector3(x, u * width, v * thickness));
    }

    private bool IsInside(Vector3 worldPoint)
    {
        if (ContainmentOverride != null) return ContainmentOverride(worldPoint);
        if (conductorShape == null) return true;
        // Inside iff the closest surface point is (essentially) the point itself.
        // Note: Collider.ClosestPoint needs a convex/primitive collider.
        return (conductorShape.ClosestPoint(worldPoint) - worldPoint).sqrMagnitude < 1e-8f;
    }

    private void EnsurePool(int n)
    {
        while (pool.Count < n)
        {
            Transform t;
            if (chargePrefab != null)
            {
                t = Instantiate(chargePrefab, transform);
            }
            else
            {
                t = new GameObject("Charge").transform;
                t.SetParent(transform, false);
                if (pool.Count == 0)
                    Debug.LogWarning($"{name}: ChargeBelt has no chargePrefab; spawning empty markers.", this);
            }
            pool.Add(t);
        }
    }

    private void OnValidate()
    {
        length = Mathf.Max(0.0001f, length);
        width = Mathf.Max(0f, width);
        thickness = Mathf.Max(0f, thickness);
        chargeCount = Mathf.Max(0, chargeCount);
        latticeCounts.x = Mathf.Max(1, latticeCounts.x);
        latticeCounts.y = Mathf.Max(1, latticeCounts.y);
        latticeCounts.z = Mathf.Max(1, latticeCounts.z);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(length, width, thickness));
    }
}