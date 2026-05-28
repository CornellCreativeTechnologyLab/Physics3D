using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct PlateSurface
{
    public Transform plate;
    public Renderer rendererOverride;

    [HideInInspector] public Vector3 center;
    [HideInInspector] public Vector2 extents;
    [HideInInspector] public bool isValid;
}

public class HallEffect_PHYSICS : MonoBehaviour
{
    [Header("Plates")]
    [SerializeField] private PlateSurface cathode;
    [SerializeField] private PlateSurface anode;

    [Header("UI Controls")]
    [SerializeField] private Slider voltageSlider;
    [SerializeField] private Slider magneticFieldSlider;
    [SerializeField] private Toggle electronsToggle;
    [SerializeField] private Toggle holesToggle;

    [Header("Coordinate Frame")]
    [SerializeField] private Vector3 magneticFieldAxis = Vector3.up;
    [SerializeField] private float materialWidth = 5.0f;
    [SerializeField] private float materialDepth = 1.0f;

    [Header("Carrier Properties")]
    [SerializeField] private float baseSpeed = 3.5f;
    [SerializeField] private float carrierMobility = 1e-3f; // m²/(V·s) - how easily carriers respond to E-field
    [SerializeField] private float relaxationTime = 1e-13f; // seconds - collision damping timescale

    [Header("Material Properties")]
    [SerializeField] private float permittivityRelative = 11.7f; // Silicon
    [SerializeField] private float recombinationRate = 0.0f; // per second - charge leakage (0 = no leakage)

    [Header("Carrier Prefabs")]
    [SerializeField] private ChargeParticle electronPrefab;
    [SerializeField] private ChargeParticle holePrefab;

    [Header("Emission")]
    [SerializeField] private float minAbsVoltage = 0.1f;
    [SerializeField] private float maxAbsVoltage = 10f;
    [SerializeField] private float maxEmissionRate = 50f;
    [SerializeField] private int surfacePointCount = 256;
    [Range(0.1f, 1.0f)] [SerializeField] private float emissionScale = 0.9f;

    [Header("Pooling")]
    [SerializeField] private int poolSize = 500;
    [SerializeField] private Transform poolParent;

    // Physical constants
    private const float eps0 = 8.854187817e-12f;  // F/m (permittivity of free space)
    private const float elementaryCharge = 1.6e-19f;  // Coulombs

    // Internal state
    private readonly Queue<ChargeParticle> poolElectrons = new();
    private readonly Queue<ChargeParticle> poolHoles = new();
    private readonly List<ChargeParticle> activeParticles = new();

    private float emitAccumulator;
    private float netLateralCharge = 0f;  // Total accumulated charge on sides (Coulombs)
    public float CurrentNetLateralCharge => netLateralCharge;

    private ChargeParticle.CarrierType carrierType = ChargeParticle.CarrierType.Electron;

    private readonly List<Vector3> cathodeSpawnPoints = new();
    private readonly List<Vector3> anodeSpawnPoints = new();
    private int spawnIndex = 0;

    // Coordinate frame
    private Vector3 flowDirection = Vector3.right;        // X-axis: direction of current
    private Vector3 lateralDirection = Vector3.up;        // Y-axis: perpendicular to flow (deflection direction)
    private Vector3 depthDirection = Vector3.forward;     // Z-axis: perpendicular to flow (other lateral direction)

    // Calculated geometry
    private float hallFieldArea;  // materialWidth * materialDepth (where charge accumulates)
    private float totalMaterialLength;

    private void Awake()
    {
        if (poolParent == null) poolParent = transform;

        PrewarmPool(electronPrefab, poolSize, poolElectrons);
        PrewarmPool(holePrefab, poolSize, poolHoles);

        if (electronsToggle != null)
            electronsToggle.onValueChanged.AddListener(isOn => { if (isOn) SetCarrier(ChargeParticle.CarrierType.Electron); });

        if (holesToggle != null)
            holesToggle.onValueChanged.AddListener(isOn => { if (isOn) SetCarrier(ChargeParticle.CarrierType.Hole); });

        if (electronsToggle != null && electronsToggle.isOn) SetCarrier(ChargeParticle.CarrierType.Electron);
        else if (holesToggle != null && holesToggle.isOn) SetCarrier(ChargeParticle.CarrierType.Hole);
        else SetCarrier(ChargeParticle.CarrierType.Electron);

        CalculatePlateGeometry();
    }

    private void SetCarrier(ChargeParticle.CarrierType type)
    {
        carrierType = type;

        if (type == ChargeParticle.CarrierType.Electron)
        {
            if (holesToggle != null) holesToggle.SetIsOnWithoutNotify(false);
            if (electronsToggle != null) electronsToggle.SetIsOnWithoutNotify(true);
        }
        else
        {
            if (electronsToggle != null) electronsToggle.SetIsOnWithoutNotify(false);
            if (holesToggle != null) holesToggle.SetIsOnWithoutNotify(true);
        }

        netLateralCharge = 0f;
    }

    private void Update()
    {
        if (!cathode.isValid || !anode.isValid) return;

        float voltage = (voltageSlider != null) ? voltageSlider.value : 0f;
        if (Mathf.Abs(voltage) < minAbsVoltage) return;

        float dt = Time.deltaTime;
        if (dt <= 1e-7f) dt = Time.unscaledDeltaTime;

        HandleEmission(voltage, dt);
        UpdateParticles(voltage, dt);
    }

    private void UpdateParticles(float voltage, float dt)
    {
        if (activeParticles.Count == 0) return;

        // Determine flow direction based on voltage and carrier type
        Vector3 currentFlowDir = GetFlowDirection(voltage);

        // Get magnetic field
        float bFieldStrength = (magneticFieldSlider != null) ? magneticFieldSlider.value : 0f;
        Vector3 bField = magneticFieldAxis.normalized * bFieldStrength;

        // Calculate Hall E-field from accumulated charge using physics equation
        // E_Hall = σ / (ε₀ * ε_r) where σ = charge / area
        float hallEFieldStrength = CalculateHallEField();
        Vector3 hallEField = lateralDirection * hallEFieldStrength;

        // Origin for distance calculations
        bool movingFromCathode = Vector3.Dot(currentFlowDir, flowDirection) > 0;
        Vector3 origin = movingFromCathode ? cathode.center : anode.center;

        // Apply charge recombination (optional leakage)
        if (recombinationRate > 0f)
        {
            netLateralCharge *= Mathf.Exp(-recombinationRate * dt);
        }

        // Update each particle
        for (int i = activeParticles.Count - 1; i >= 0; i--)
        {
            ChargeParticle p = activeParticles[i];
            if (p == null) continue;

            // Charge sign: electron = -1, hole = +1
            float chargeSign = (p.Carrier == ChargeParticle.CarrierType.Hole) ? 1f : -1f;

            // ==================== PHYSICS: Force Calculation ====================
            // F = q(E_Hall + v × B)
            // This is the complete Lorentz force with Hall field opposing deflection

            Vector3 vCrossB = Vector3.Cross(p.Velocity, bField);
            Vector3 totalEField = hallEField + vCrossB;
            Vector3 totalForce = chargeSign * totalEField;

            // ==================== Physics: Velocity Update ====================
            // Apply force with carrier mobility (reduced mass effect)
            // Mobility links force to velocity change: a = μ * F / q
            p.Velocity += carrierMobility * totalForce * dt;

            // Apply relaxation time damping (collision with lattice)
            // v(t) = v(0) * exp(-t / tau)
            float dampingFactor = Mathf.Exp(-dt / relaxationTime);
            p.Velocity *= dampingFactor;

            // ==================== Physics: Drift Velocity Maintenance ====================
            // Maintain forward drift velocity (set by applied voltage)
            float driftVelocity = baseSpeed * Mathf.Max(0.1f, Mathf.Abs(voltage) / maxAbsVoltage);
            float currentForwardSpeed = Vector3.Dot(p.Velocity, currentFlowDir);
            float speedError = driftVelocity - currentForwardSpeed;
            p.Velocity += currentFlowDir * speedError;  // Directly set forward component

            // ==================== Update Position ====================
            p.transform.position += p.Velocity * dt;

            // ==================== Boundary Enforcement ====================
            Vector3 relPos = p.transform.position - origin;
            float forwardDist = Vector3.Dot(relPos, currentFlowDir);
            float lateralDist = Vector3.Dot(relPos, lateralDirection);
            float depthDist = Vector3.Dot(relPos, depthDirection);

            // Forward boundary: despawn when exiting material
            if (forwardDist >= totalMaterialLength || forwardDist < -0.2f)
            {
                DespawnParticle(p, i);
                continue;
            }

            // Lateral boundary: Y-axis (width) - where Hall effect occurs
            float halfWidth = materialWidth * 0.5f;
            if (Mathf.Abs(lateralDist) > halfWidth)
            {
                float wallSide = Mathf.Sign(lateralDist);

                // Clamp position to wall
                Vector3 correctedPos = p.transform.position;
                correctedPos -= lateralDirection * (lateralDist - (wallSide * halfWidth));
                p.transform.position = correctedPos;

                // Kill perpendicular velocity component (particle sticks/bounces)
                float lateralVel = Vector3.Dot(p.Velocity, lateralDirection);
                p.Velocity -= lateralDirection * lateralVel;

                // ==================== Charge Accumulation ====================
                // When particle hits wall, accumulate charge
                // Charge = chargeSign * elementaryCharge per particle
                netLateralCharge += wallSide * chargeSign * elementaryCharge;
            }

            // Depth boundary: Z-axis (depth)
            float halfDepth = materialDepth * 0.5f;
            if (Mathf.Abs(depthDist) > halfDepth)
            {
                float depthSide = Mathf.Sign(depthDist);

                Vector3 correctedPos = p.transform.position;
                correctedPos -= depthDirection * (depthDist - (depthSide * halfDepth));
                p.transform.position = correctedPos;

                float depthVel = Vector3.Dot(p.Velocity, depthDirection);
                p.Velocity -= depthDirection * depthVel;

                // Accumulate charge on depth boundaries too
                netLateralCharge += depthSide * chargeSign * elementaryCharge;
            }
        }
    }

    private float CalculateHallEField()
    {
        // Physics: E_Hall = σ / (ε₀ * ε_r)
        // where σ = Q / A (surface charge density)
        
        if (hallFieldArea <= 0) return 0f;

        float surfaceChargeDensity = netLateralCharge / hallFieldArea;
        float permittivity = eps0 * permittivityRelative;
        float hallEFieldStrength = surfaceChargeDensity / permittivity;

        return hallEFieldStrength;
    }

    private Vector3 GetFlowDirection(float voltage)
    {
        Vector3 dir = flowDirection;

        // Holes flow opposite to electrons
        if (carrierType == ChargeParticle.CarrierType.Hole)
            dir = -dir;

        // Negative voltage reverses flow
        if (voltage < 0f)
            dir = -dir;

        return dir.normalized;
    }

    private void HandleEmission(float voltage, float dt)
    {
        float intensity = Mathf.InverseLerp(minAbsVoltage, maxAbsVoltage, Mathf.Abs(voltage));
        if (intensity <= 0f) return;

        float emissionRate = maxEmissionRate * intensity;
        emitAccumulator += emissionRate * dt;
        int spawnCount = Mathf.FloorToInt(emitAccumulator);
        emitAccumulator -= spawnCount;

        float speed = baseSpeed * Mathf.Max(0.1f, intensity);
        Vector3 travelDir = GetFlowDirection(voltage);

        bool spawningAtCathode = Vector3.Dot(travelDir, flowDirection) > 0;
        List<Vector3> currentSpawnPoints = spawningAtCathode ? cathodeSpawnPoints : anodeSpawnPoints;

        if (currentSpawnPoints.Count == 0) return;

        for (int i = 0; i < spawnCount; i++)
        {
            ChargeParticle p = GetParticleFromPool(carrierType);
            if (p == null) continue;

            Vector3 spawnPos = currentSpawnPoints[spawnIndex++ % currentSpawnPoints.Count];
            p.transform.position = spawnPos;
            p.gameObject.SetActive(true);
            p.Initialize(carrierType, travelDir * speed, 10f);

            activeParticles.Add(p);
        }
    }

    private void CalculatePlateGeometry()
    {
        if (cathode.plate == null || anode.plate == null) return;

        cathode.isValid = ExtractBounds(ref cathode);
        anode.isValid = ExtractBounds(ref anode);

        if (cathode.isValid && anode.isValid)
        {
            Renderer catRenderer = cathode.rendererOverride ?? cathode.plate.GetComponentInChildren<Renderer>();
            if (catRenderer != null)
            {
                Bounds b = catRenderer.bounds;
                materialWidth = b.extents.y * 2f;
                materialDepth = b.extents.z * 2f;
            }

            totalMaterialLength = Mathf.Abs(anode.center.x - cathode.center.x);
            hallFieldArea = materialWidth * materialDepth;

            GenerateEmissionPoints();
        }
    }

    private bool ExtractBounds(ref PlateSurface surface)
    {
        Renderer r = surface.rendererOverride != null ? surface.rendererOverride : surface.plate.GetComponentInChildren<Renderer>();
        if (r == null) return false;

        Bounds b = r.bounds;
        surface.center = b.center;
        surface.extents = new Vector2(b.extents.x, b.extents.y);

        return true;
    }

    private void GenerateEmissionPoints()
    {
        cathodeSpawnPoints.Clear();
        anodeSpawnPoints.Clear();

        BuildPointsForPlate(cathode, cathodeSpawnPoints);
        BuildPointsForPlate(anode, anodeSpawnPoints);
    }

    private void BuildPointsForPlate(PlateSurface surface, List<Vector3> pointsList)
    {
        Renderer r = surface.rendererOverride != null ? surface.rendererOverride : surface.plate.GetComponentInChildren<Renderer>();
        if (r == null) return;

        Bounds b = r.bounds;

        float halfY = b.extents.y * emissionScale;
        float halfZ = b.extents.z * emissionScale;

        int gridSide = Mathf.CeilToInt(Mathf.Sqrt(surfacePointCount));

        for (int y = 0; y < gridSide; y++)
        {
            for (int z = 0; z < gridSide; z++)
            {
                float normY = gridSide > 1 ? (y / (float)(gridSide - 1)) * 2f - 1f : 0f;
                float normZ = gridSide > 1 ? (z / (float)(gridSide - 1)) * 2f - 1f : 0f;

                Vector3 offset = new Vector3(0f, normY * halfY, normZ * halfZ);
                pointsList.Add(surface.center + offset);

                if (pointsList.Count >= surfacePointCount) break;
            }
            if (pointsList.Count >= surfacePointCount) break;
        }

        for (int i = 0; i < pointsList.Count; i++)
        {
            Vector3 temp = pointsList[i];
            int randomIndex = Random.Range(i, pointsList.Count);
            pointsList[i] = pointsList[randomIndex];
            pointsList[randomIndex] = temp;
        }
    }

    private void PrewarmPool(ChargeParticle prefab, int count, Queue<ChargeParticle> pool)
    {
        if (prefab == null) return;
        for (int i = 0; i < count; i++)
        {
            ChargeParticle p = Instantiate(prefab, poolParent);
            p.gameObject.SetActive(false);
            pool.Enqueue(p);
        }
    }

    private ChargeParticle GetParticleFromPool(ChargeParticle.CarrierType type)
    {
        var pool = (type == ChargeParticle.CarrierType.Electron) ? poolElectrons : poolHoles;
        if (pool.Count > 0) return pool.Dequeue();

        var prefab = (type == ChargeParticle.CarrierType.Electron) ? electronPrefab : holePrefab;
        return prefab != null ? Instantiate(prefab, poolParent) : null;
    }

    private void DespawnParticle(ChargeParticle p, int index)
    {
        activeParticles.RemoveAt(index);
        p.gameObject.SetActive(false);
        if (p.Carrier == ChargeParticle.CarrierType.Electron) poolElectrons.Enqueue(p);
        else poolHoles.Enqueue(p);
    }

    private void OnDrawGizmos()
    {
        if (cathode.plate == null || anode.plate == null) return;

        Vector3 flow = (anode.plate.position - cathode.plate.position).normalized;
        Vector3 bField = magneticFieldAxis.normalized;
        Vector3 lateral = Vector3.Cross(flow, bField).normalized;

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(cathode.plate.position, flow * 2f);

        Gizmos.color = Color.red;
        Gizmos.DrawRay(cathode.plate.position, lateral * 2f);

        Gizmos.color = Color.green;
        Gizmos.DrawRay(cathode.plate.position, bField * 2f);

        if (cathodeSpawnPoints.Count > 0)
        {
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            foreach (var pt in cathodeSpawnPoints) Gizmos.DrawSphere(pt, 0.05f);
        }

        if (anodeSpawnPoints.Count > 0)
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            foreach (var pt in anodeSpawnPoints) Gizmos.DrawSphere(pt, 0.05f);
        }
    }
}
