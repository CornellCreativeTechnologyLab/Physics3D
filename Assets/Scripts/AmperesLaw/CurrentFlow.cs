// CurrentFlow.cs
// Simulates current by moving charge prefabs along a parametric path.
// Attach one to each conductor (line wire, solenoid, toroid), pick the shape,
// and tune the shape fields to match the mesh. Paths are generated as local-space
// waypoints, so the charges follow the conductor's transform automatically.
//
// Open paths (StraightWire, Helix): a charge that reaches the end restarts at
// the beginning. Closed paths (ToroidalHelix): charges loop seamlessly.

using UnityEngine;

public class CurrentFlow : MonoBehaviour
{
    public enum PathShape { StraightWire, Helix, ToroidalHelix }

    [Header("Path Shape")]
    [SerializeField] private PathShape shape = PathShape.StraightWire;

    [Header("Straight Wire (along local Y)")]
    [SerializeField] private float wireLength = 4f;

    [Header("Helix (axis = local Y)")]
    [SerializeField] private float helixRadius = 0.8f;
    [SerializeField] private float helixHeight = 2.5f;
    [SerializeField, Min(1)] private int helixTurns = 10;

    [Header("Toroidal Helix (major circle in local XZ)")]
    [SerializeField] private float majorRadius = 3f;
    [SerializeField] private float minorRadius = 0.7f;
    [SerializeField, Min(1)] private int toroidTurns = 40;

    [Header("Sampling")]
    [SerializeField, Range(4, 64)] private int pointsPerTurn = 16;

    [Header("Path Orientation (local-space euler, applied to the generated path)")]
    [SerializeField] private Vector3 pathRotation = Vector3.zero;

    [Header("Charges")]
    [SerializeField] private GameObject chargePrefab;
    [SerializeField, Range(1, 200)] private int chargeCount = 20;
    [SerializeField] private float currentSensitivity = 1f;
    [SerializeField] private float current = 1f; // amps; sign flips direction

    private Vector3[] _waypoints;
    private float[] _cumulativeLength; // cumulativeLength[i] = path length up to waypoint i
    private float _totalLength;
    private bool _isClosed;

    private Transform[] _charges;
    private float[] _progress; // 0..1 along the path

    public void SetCurrent(float amps) => current = amps;

    // Convenience hook for a UI Slider's OnValueChanged.
    public void OnCurrentSliderChanged(float value) => current = value;

    private void Awake()
    {
        RebuildPath();
        SpawnCharges();
    }

    private void OnValidate()
    {
        if (Application.isPlaying && _charges != null)
        {
            RebuildPath();
            UpdateAllPositions();
        }
    }

    private void Update()
    {
        if (_charges == null || _waypoints == null || _waypoints.Length < 2)
            return;

        if (!Mathf.Approximately(current, 0f))
        {
            float delta = (current * currentSensitivity * Time.deltaTime) / _totalLength;
            for (int i = 0; i < _progress.Length; i++)
                _progress[i] = Mathf.Repeat(_progress[i] + delta, 1f);
        }

        UpdateAllPositions();
    }

    // ---------------- Path generation ----------------

    public void RebuildPath()
    {
        switch (shape)
        {
            case PathShape.StraightWire: BuildStraightWire(); break;
            case PathShape.Helix: BuildHelix(); break;
            case PathShape.ToroidalHelix: BuildToroidalHelix(); break;
        }

        if (pathRotation != Vector3.zero)
        {
            Quaternion rot = Quaternion.Euler(pathRotation);
            for (int i = 0; i < _waypoints.Length; i++)
                _waypoints[i] = rot * _waypoints[i];
        }

        RecomputeLengths();
    }

    private void BuildStraightWire()
    {
        _isClosed = false;
        _waypoints = new[]
        {
            new Vector3(0f, -wireLength * 0.5f, 0f),
            new Vector3(0f,  wireLength * 0.5f, 0f),
        };
    }

    private void BuildHelix()
    {
        _isClosed = false;
        int n = helixTurns * pointsPerTurn + 1;
        _waypoints = new Vector3[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);
            float ang = t * helixTurns * Mathf.PI * 2f;
            _waypoints[i] = new Vector3(
                helixRadius * Mathf.Cos(ang),
                -helixHeight * 0.5f + t * helixHeight,
                helixRadius * Mathf.Sin(ang));
        }
    }

    private void BuildToroidalHelix()
    {
        _isClosed = true;
        int n = toroidTurns * pointsPerTurn;
        _waypoints = new Vector3[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float theta = t * Mathf.PI * 2f;               // around the torus axis
            float phi = t * toroidTurns * Mathf.PI * 2f;   // around the tube
            float axisDist = majorRadius + minorRadius * Mathf.Cos(phi);
            _waypoints[i] = new Vector3(
                axisDist * Mathf.Cos(theta),
                minorRadius * Mathf.Sin(phi),
                axisDist * Mathf.Sin(theta));
        }
    }

    private void RecomputeLengths()
    {
        int segCount = _isClosed ? _waypoints.Length : _waypoints.Length - 1;
        _cumulativeLength = new float[_waypoints.Length + 1];
        _totalLength = 0f;

        for (int i = 0; i < segCount; i++)
        {
            Vector3 a = _waypoints[i];
            Vector3 b = _waypoints[(i + 1) % _waypoints.Length];
            _totalLength += Vector3.Distance(a, b);
            _cumulativeLength[i + 1] = _totalLength;
        }
    }

    // ---------------- Charges ----------------

    private void SpawnCharges()
    {
        if (chargePrefab == null) return;

        _charges = new Transform[chargeCount];
        _progress = new float[chargeCount];

        for (int i = 0; i < chargeCount; i++)
        {
            GameObject obj = Instantiate(chargePrefab, transform);
            _charges[i] = obj.transform;
            _progress[i] = i / (float)chargeCount;
        }

        UpdateAllPositions();
    }

    private void UpdateAllPositions()
    {
        for (int i = 0; i < _charges.Length; i++)
        {
            if (_charges[i] == null) continue;
            _charges[i].localPosition = GetPointAtProgress(_progress[i]);
        }
    }

    private Vector3 GetPointAtProgress(float t)
    {
        float targetDist = Mathf.Repeat(t, 1f) * _totalLength;
        int segCount = _isClosed ? _waypoints.Length : _waypoints.Length - 1;

        // Binary search over cumulative lengths for the containing segment.
        int lo = 0, hi = segCount - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (_cumulativeLength[mid + 1] < targetDist) lo = mid + 1;
            else hi = mid;
        }

        Vector3 a = _waypoints[lo];
        Vector3 b = _waypoints[(lo + 1) % _waypoints.Length];
        float segLen = _cumulativeLength[lo + 1] - _cumulativeLength[lo];
        float u = segLen > 1e-6f ? (targetDist - _cumulativeLength[lo]) / segLen : 0f;
        return Vector3.Lerp(a, b, u);
    }

    // Editor aid: visualize the generated path so it can be matched to the mesh.
    private void OnDrawGizmosSelected()
    {
        RebuildPath();
        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        int segCount = _isClosed ? _waypoints.Length : _waypoints.Length - 1;
        for (int i = 0; i < segCount; i++)
            Gizmos.DrawLine(_waypoints[i], _waypoints[(i + 1) % _waypoints.Length]);
    }
}
