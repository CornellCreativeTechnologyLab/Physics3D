using UnityEngine;

public class LoopChargeWalker : MonoBehaviour
{
    [SerializeField] private GameObject chargePrefab;
    [SerializeField, Range(1, 10)] private int chargeCount = 6;
    [SerializeField] private float currentSensitivity = 1f;

    private float _currentAmps;
    private Vector3[] _waypoints;
    private float _totalPathLength;
    private Transform[] _charges;
    private float[] _chargeProgress;

    private void Awake()
    {
        SpawnCharges();
    }

    public void SetWaypoints(Vector3[] waypoints)
    {
        _waypoints = waypoints;
        RecalculatePathLength();
    }

    public void SetCurrent(float amps)
    {
        _currentAmps = amps;
    }

    private void Update()
    {
        if (_charges == null || _waypoints == null || _waypoints.Length < 2)
            return;

        if (Mathf.Approximately(_currentAmps, 0f))
            return;

        MoveCharges();
    }

    private void SpawnCharges()
    {
        if (_charges != null)
        {
            foreach (Transform c in _charges)
                if (c != null) Destroy(c.gameObject);
        }

        _charges = new Transform[chargeCount];
        _chargeProgress = new float[chargeCount];

        if (chargePrefab == null)
            return;

        for (int i = 0; i < chargeCount; i++)
        {
            GameObject obj = Instantiate(chargePrefab, transform);
            obj.SetActive(false);
            _charges[i] = obj.transform;
            _chargeProgress[i] = (float)i / chargeCount;
        }
    }

    private void RecalculatePathLength()
    {
        _totalPathLength = 0f;

        if (_waypoints == null || _waypoints.Length < 2)
            return;

        for (int i = 0; i < _waypoints.Length; i++)
        {
            Vector3 a = _waypoints[i];
            Vector3 b = _waypoints[(i + 1) % _waypoints.Length];
            _totalPathLength += Vector3.Distance(a, b);
        }

        if (_charges != null)
        {
            foreach (Transform c in _charges)
                if (c != null) c.gameObject.SetActive(true);

            UpdateAllPositions();
        }
    }

    private void MoveCharges()
    {
        float moveDelta = (_currentAmps * currentSensitivity * Time.deltaTime) / _totalPathLength;

        for (int i = 0; i < _charges.Length; i++)
        {
            _chargeProgress[i] += moveDelta;

            if (_chargeProgress[i] > 1f) _chargeProgress[i] -= 1f;
            else if (_chargeProgress[i] < 0f) _chargeProgress[i] += 1f;
        }

        UpdateAllPositions();
    }

    private void UpdateAllPositions()
    {
        for (int i = 0; i < _charges.Length; i++)
        {
            if (_charges[i] == null) continue;
            _charges[i].localPosition = GetPointAtProgress(_chargeProgress[i]);
        }
    }

    private Vector3 GetPointAtProgress(float t)
    {
        float targetDist = t * _totalPathLength;
        float currentDist = 0f;

        for (int i = 0; i < _waypoints.Length; i++)
        {
            Vector3 a = _waypoints[i];
            Vector3 b = _waypoints[(i + 1) % _waypoints.Length];
            float segLen = Vector3.Distance(a, b);

            if (currentDist + segLen >= targetDist)
            {
                float remaining = targetDist - currentDist;
                return Vector3.Lerp(a, b, remaining / segLen);
            }

            currentDist += segLen;
        }

        return _waypoints[0];
    }
}
