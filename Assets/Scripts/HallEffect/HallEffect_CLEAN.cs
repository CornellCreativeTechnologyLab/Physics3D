//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.UI;

//[System.Serializable]
//public struct PlateSurface
//{
//    public Transform plate;
//    public Renderer rendererOverride;

//    [HideInInspector] public Vector3 center;
//    [HideInInspector] public Vector2 extents;
//    [HideInInspector] public bool isValid;
//}

//public class HallEffect : MonoBehaviour
//{
//    [Header("Plates")]
//    [SerializeField] private PlateSurface cathode;
//    [SerializeField] private PlateSurface anode;

//    [Header("UI Controls")]
//    [SerializeField] private Slider voltageSlider;
//    [SerializeField] private Slider magneticFieldSlider;
//    [SerializeField] private Toggle electronsToggle;
//    [SerializeField] private Toggle holesToggle;

//    [Header("Physics Parameters")]
//    [SerializeField] private Vector3 magneticFieldAxis = Vector3.up;
//    [SerializeField] private float baseSpeed = 3.5f;
//    [SerializeField] private float materialWidth = 5.0f;
//    [SerializeField] private float materialDepth = 1.0f;
    
//    [Header("Carrier Mobility & Damping")]
//    [SerializeField] private float carrierMobility = 0.5f;
//    [SerializeField] private float velocityDamping = 0.8f;

//    [Header("Hall Effect Parameters")]
//    [SerializeField] private float hallCoefficient = 0.001f;
//    [SerializeField] private float maxHallEField = 1e6f;

//    [Header("Carrier Prefabs")]
//    [SerializeField] private ChargeParticle electronPrefab;
//    [SerializeField] private ChargeParticle holePrefab;

//    [Header("Emission")]
//    [SerializeField] private float minAbsVoltage = 0.1f;
//    [SerializeField] private float maxAbsVoltage = 10f;
//    [SerializeField] private float maxEmissionRate = 50f;
//    [SerializeField] private int surfacePointCount = 256;
//    [Range(0.1f, 1.0f)] [SerializeField] private float emissionScale = 0.9f;

//    [Header("Pooling")]
//    [SerializeField] private int poolSize = 500;
//    [SerializeField] private Transform poolParent;

//    private readonly Queue<ChargeParticle> poolElectrons = new();
//    private readonly Queue<ChargeParticle> poolHoles = new();
//    private readonly List<ChargeParticle> activeParticles = new();

//    private float emitAccumulator;
//    private float netLateralCharge = 0f;
//    public float CurrentNetLateralCharge => netLateralCharge;

//    private ChargeParticle.CarrierType carrierType = ChargeParticle.CarrierType.Electron;

//    private readonly List<Vector3> cathodeSpawnPoints = new();
//    private readonly List<Vector3> anodeSpawnPoints = new();
//    private int spawnIndex = 0;

//    // Coordinate frame
//    private Vector3 flowDirection = Vector3.right;
//    private Vector3 lateralDirection = Vector3.up;
//    private Vector3 depthDirection = Vector3.forward;

//    private void Awake()
//    {
//        if (poolParent == null) poolParent = transform;

//        PrewarmPool(electronPrefab, poolSize, poolElectrons);
//        PrewarmPool(holePrefab, poolSize, poolHoles);

//        if (electronsToggle != null)
//            electronsToggle.onValueChanged.AddListener(isOn => { if (isOn) SetCarrier(ChargeParticle.CarrierType.Electron); });

//        if (holesToggle != null)
//            holesToggle.onValueChanged.AddListener(isOn => { if (isOn) SetCarrier(ChargeParticle.CarrierType.Hole); });

//        if (electronsToggle != null && electronsToggle.isOn) SetCarrier(ChargeParticle.CarrierType.Electron);
//        else if (holesToggle != null && holesToggle.isOn) SetCarrier(ChargeParticle.CarrierType.Hole);
//        else SetCarrier(ChargeParticle.CarrierType.Electron);

//        CalculatePlateGeometry();
//    }

//    private void SetCarrier(ChargeParticle.CarrierType type)
//    {
//        carrierType = type;

//        if (type == ChargeParticle.CarrierType.Electron)
//        {
//            if (holesToggle != null) holesToggle.SetIsOnWithoutNotify(false);
//            if (electronsToggle != null) electronsToggle.SetIsOnWithoutNotify(true);
//        }
//        else
//        {
//            if (electronsToggle != null) electronsToggle.SetIsOnWithoutNotify(false);
//            if (holesToggle != null) holesToggle.SetIsOnWithoutNotify(true);
//        }

//        netLateralCharge = 0f;
//    }

//    private void Update()
//    {
//        if (!cathode.isValid || !anode.isValid) return;

//        float voltage = (voltageSlider != null) ? voltageSlider.value : 0f;
//        if (Mathf.Abs(voltage) < minAbsVoltage) return;

//        float dt = Time.deltaTime;
//        if (dt <= 1e-7f) dt = Time.unscaledDeltaTime;

//        HandleEmission(voltage, dt);
//        UpdateParticles(voltage, dt);
//    }

//    private void UpdateParticles(float voltage, float dt)
//    {
//        if (activeParticles.Count == 0) return;

//        Vector3 currentFlowDir = GetFlowDirection(voltage);
//        float bFieldStrength = (magneticFieldSlider != null) ? magneticFieldSlider.value : 0f;
//        Vector3 bField = magneticFieldAxis.normalized * bFieldStrength;

//        float hallEFieldStrength = CalculateHallEField();
//        Vector3 hallEField = lateralDirection * hallEFieldStrength;

//        bool movingFromCathode = Vector3.Dot(currentFlowDir, flowDirection) > 0;
//        Vector3 origin = movingFromCathode ? cathode.center : anode.center;

//        float totalDistance = Mathf.Abs(anode.center.x - cathode.center.x);

//        for (int i = activeParticles.Count - 1; i >= 0; i--)
//        {
//            ChargeParticle p = activeParticles[i];
//            if (p == null) continue;

//            float chargeSign = (p.Carrier == ChargeParticle.CarrierType.Hole) ? 1f : -1f;

//            // Calculate Lorentz force
//            Vector3 vCrossB = Vector3.Cross(p.Velocity, bField);
//            Vector3 totalForce = chargeSign * (hallEField + vCrossB);

//            // Apply force with mobility and damping
//            p.Velocity += totalForce * carrierMobility * dt;
//            p.Velocity *= velocityDamping;

//            // Maintain forward speed
//            float targetSpeed = baseSpeed * Mathf.Max(0.1f, Mathf.Abs(voltage) / maxAbsVoltage);
//            float currentForwardSpeed = Vector3.Dot(p.Velocity, currentFlowDir);
//            float speedCorrection = (targetSpeed - currentForwardSpeed) * 0.5f;
//            p.Velocity += currentFlowDir * speedCorrection;

//            // Update position
//            p.transform.position += p.Velocity * dt;

//            // Enforce boundaries
//            Vector3 relPos = p.transform.position - origin;
//            float forwardDist = Vector3.Dot(relPos, currentFlowDir);
//            float lateralDist = Vector3.Dot(relPos, lateralDirection);
//            float depthDist = Vector3.Dot(relPos, depthDirection);

//            // Forward boundary
//            if (forwardDist >= totalDistance || forwardDist < -0.2f)
//            {
//                DespawnParticle(p, i);
//                continue;
//            }

//            // Lateral boundary
//            float halfWidth = materialWidth * 0.5f;
//            if (Mathf.Abs(lateralDist) > halfWidth)
//            {
//                float wallSide = Mathf.Sign(lateralDist);
//                Vector3 correctedPos = p.transform.position;
//                correctedPos -= lateralDirection * (lateralDist - (wallSide * halfWidth));
//                p.transform.position = correctedPos;

//                float lateralVel = Vector3.Dot(p.Velocity, lateralDirection);
//                p.Velocity -= lateralDirection * lateralVel;

//                netLateralCharge += wallSide * chargeSign * dt;
//            }

//            // Depth boundary
//            float halfDepth = materialDepth * 0.5f;
//            if (Mathf.Abs(depthDist) > halfDepth)
//            {
//                float depthSide = Mathf.Sign(depthDist);
//                Vector3 correctedPos = p.transform.position;
//                correctedPos -= depthDirection * (depthDist - (depthSide * halfDepth));
//                p.transform.position = correctedPos;

//                float depthVel = Vector3.Dot(p.Velocity, depthDirection);
//                p.Velocity -= depthDirection * depthVel;
//            }
//        }
//    }

//    private float CalculateHallEField()
//    {
//        float eFieldStrength = hallCoefficient * netLateralCharge;
//        return Mathf.Clamp(eFieldStrength, -maxHallEField, maxHallEField);
//    }

//    private Vector3 GetFlowDirection(float voltage)
//    {
//        Vector3 dir = flowDirection;

//        if (carrierType == ChargeParticle.CarrierType.Hole)
//            dir = -dir;

//        if (voltage < 0f)
//            dir = -dir;

//        return dir.normalized;
//    }

//    private void HandleEmission(float voltage, float dt)
//    {
//        float intensity = Mathf.InverseLerp(minAbsVoltage, maxAbsVoltage, Mathf.Abs(voltage));
//        if (intensity <= 0f) return;

//        float emissionRate = maxEmissionRate * intensity;
//        emitAccumulator += emissionRate * dt;
//        int spawnCount = Mathf.FloorToInt(emitAccumulator);
//        emitAccumulator -= spawnCount;

//        float speed = baseSpeed * Mathf.Max(0.1f, intensity);
//        Vector3 travelDir = GetFlowDirection(voltage);

//        bool spawningAtCathode = Vector3.Dot(travelDir, flowDirection) > 0;
//        List<Vector3> currentSpawnPoints = spawningAtCathode ? cathodeSpawnPoints : anodeSpawnPoints;

//        if (currentSpawnPoints.Count == 0) return;

//        for (int i = 0; i < spawnCount; i++)
//        {
//            ChargeParticle p = GetParticleFromPool(carrierType);
//            if (p == null) continue;

//            Vector3 spawnPos = currentSpawnPoints[spawnIndex++ % currentSpawnPoints.Count];
//            p.transform.position = spawnPos;
//            p.gameObject.SetActive(true);
//            p.Initialize(carrierType, travelDir * speed, 10f);

//            activeParticles.Add(p);
//        }
//    }

//    private void CalculatePlateGeometry()
//    {
//        if (cathode.plate == null || anode.plate == null) return;

//        cathode.isValid = ExtractBounds(ref cathode);
//        anode.isValid = ExtractBounds(ref anode);

//        if (cathode.isValid && anode.isValid)
//        {
//            Renderer catRenderer = cathode.rendererOverride ?? cathode.plate.GetComponentInChildren<Renderer>();
//            if (catRenderer != null)
//            {
//                Bounds b = catRenderer.bounds;
//                materialWidth = b.extents.y * 2f;
//                materialDepth = b.extents.z * 2f;
//            }

//            GenerateEmissionPoints();
//        }
//    }

//    private bool ExtractBounds(ref PlateSurface surface)
//    {
//        Renderer r = surface.rendererOverride != null ? surface.rendererOverride : surface.plate.GetComponentInChildren<Renderer>();
//        if (r == null) return false;

//        Bounds b = r.bounds;
//        surface.center = b.center;
//        surface.extents = new Vector2(b.extents.x, b.extents.y);

//        return true;
//    }

//    private void GenerateEmissionPoints()
//    {
//        cathodeSpawnPoints.Clear();
//        anodeSpawnPoints.Clear();

//        BuildPointsForPlate(cathode, cathodeSpawnPoints);
//        BuildPointsForPlate(anode, anodeSpawnPoints);
//    }

//    private void BuildPointsForPlate(PlateSurface surface, List<Vector3> pointsList)
//    {
//        Renderer r = surface.rendererOverride != null ? surface.rendererOverride : surface.plate.GetComponentInChildren<Renderer>();
//        if (r == null) return;

//        Bounds b = r.bounds;

//        float halfY = b.extents.y * emissionScale;
//        float halfZ = b.extents.z * emissionScale;

//        int gridSide = Mathf.CeilToInt(Mathf.Sqrt(surfacePointCount));

//        for (int y = 0; y < gridSide; y++)
//        {
//            for (int z = 0; z < gridSide; z++)
//            {
//                float normY = gridSide > 1 ? (y / (float)(gridSide - 1)) * 2f - 1f : 0f;
//                float normZ = gridSide > 1 ? (z / (float)(gridSide - 1)) * 2f - 1f : 0f;

//                Vector3 offset = new Vector3(0f, normY * halfY, normZ * halfZ);
//                pointsList.Add(surface.center + offset);

//                if (pointsList.Count >= surfacePointCount) break;
//            }
//            if (pointsList.Count >= surfacePointCount) break;
//        }

//        for (int i = 0; i < pointsList.Count; i++)
//        {
//            Vector3 temp = pointsList[i];
//            int randomIndex = Random.Range(i, pointsList.Count);
//            pointsList[i] = pointsList[randomIndex];
//            pointsList[randomIndex] = temp;
//        }
//    }

//    private void PrewarmPool(ChargeParticle prefab, int count, Queue<ChargeParticle> pool)
//    {
//        if (prefab == null) return;
//        for (int i = 0; i < count; i++)
//        {
//            ChargeParticle p = Instantiate(prefab, poolParent);
//            p.gameObject.SetActive(false);
//            pool.Enqueue(p);
//        }
//    }

//    private ChargeParticle GetParticleFromPool(ChargeParticle.CarrierType type)
//    {
//        var pool = (type == ChargeParticle.CarrierType.Electron) ? poolElectrons : poolHoles;
//        if (pool.Count > 0) return pool.Dequeue();

//        var prefab = (type == ChargeParticle.CarrierType.Electron) ? electronPrefab : holePrefab;
//        return prefab != null ? Instantiate(prefab, poolParent) : null;
//    }

//    private void DespawnParticle(ChargeParticle p, int index)
//    {
//        activeParticles.RemoveAt(index);
//        p.gameObject.SetActive(false);
//        if (p.Carrier == ChargeParticle.CarrierType.Electron) poolElectrons.Enqueue(p);
//        else poolHoles.Enqueue(p);
//    }

//    private void OnDrawGizmos()
//    {
//        if (cathode.plate == null || anode.plate == null) return;

//        Vector3 flow = (anode.plate.position - cathode.plate.position).normalized;
//        Vector3 bField = magneticFieldAxis.normalized;
//        Vector3 lateral = Vector3.Cross(flow, bField).normalized;

//        Gizmos.color = Color.blue;
//        Gizmos.DrawRay(cathode.plate.position, flow * 2f);

//        Gizmos.color = Color.red;
//        Gizmos.DrawRay(cathode.plate.position, lateral * 2f);

//        Gizmos.color = Color.green;
//        Gizmos.DrawRay(cathode.plate.position, bField * 2f);

//        if (cathodeSpawnPoints.Count > 0)
//        {
//            Gizmos.color = new Color(0, 1, 0, 0.3f);
//            foreach (var pt in cathodeSpawnPoints) Gizmos.DrawSphere(pt, 0.05f);
//        }

//        if (anodeSpawnPoints.Count > 0)
//        {
//            Gizmos.color = new Color(1, 0, 0, 0.3f);
//            foreach (var pt in anodeSpawnPoints) Gizmos.DrawSphere(pt, 0.05f);
//        }
//    }
//}
