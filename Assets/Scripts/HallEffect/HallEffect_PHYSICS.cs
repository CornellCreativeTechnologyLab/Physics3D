//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.UI;

////[System.Serializable]
////public struct PlateSurface
////{
////    public Transform plate;
////    public Renderer rendererOverride;

////    [HideInInspector] public Vector3 center;
////    [HideInInspector] public Vector2 extents;
////    [HideInInspector] public bool isValid;
////}

//public class HallEffect_PHYSICS : MonoBehaviour
//{
//    [Header("Plates")]
//    [SerializeField] private PlateSurface cathode;
//    [SerializeField] private PlateSurface anode;

//    [Header("UI Controls")]
//    [SerializeField] private Slider voltageSlider;
//    [SerializeField] private Slider magneticFieldSlider;
//    [SerializeField] private Toggle electronsToggle;
//    [SerializeField] private Toggle holesToggle;

//    [Header("Coordinate Frame")]
//    [SerializeField] private Vector3 magneticFieldAxis = new Vector3(100f, 0f, 0f);

//    [Header("Carrier Prefabs")]
//    [SerializeField] private ChargeParticle electronPrefab;
//    [SerializeField] private ChargeParticle holePrefab;

//    [Header("Pack Mechanics")]
//    [SerializeField] private Pack packPrefab;
//    [SerializeField] private int chargesPerPack = 12;
//    [SerializeField] private int packPoolSize = 100;
//    [SerializeField] private Transform packParent;

//    [Header("Carrier Properties")]
//    [SerializeField] private float baseSpeed = 3.5f;

//    [Header("Emission")]
//    [SerializeField] private float minAbsVoltage = 0.1f;
//    [SerializeField] private float maxAbsVoltage = 10f;
//    [SerializeField] private float maxEmissionRate = 20f;
//    [SerializeField] private int surfaceGridSide = 16;

//    [Range(0.1f, 1.0f)]
//    [SerializeField] private float emissionScale = 0.9f;

//    [Header("Pooling")]
//    [SerializeField] private int poolSizeElectrons = 500;
//    [SerializeField] private int poolSizeHoles = 500;
//    [SerializeField] private Transform poolParentElectrons;
//    [SerializeField] private Transform poolParentHoles;

//    [Header("Physical Hall Model")]
//    [SerializeField] private float carrierDensity = 1e21f;
//    [SerializeField] private float carrierCharge = 1.6e-19f;
//    [SerializeField] private float conductorWidth = 0.02f;
//    [SerializeField] private float conductorThickness = 0.001f;
//    [SerializeField] private float maxCurrent = 0.02f;

//    [Header("Computed Hall Values")]
//    [SerializeField] private float current;
//    [SerializeField] private float currentDensity;
//    [SerializeField] private float driftVelocity;
//    [SerializeField] private float hallCoefficient;
//    [SerializeField] private float hallElectricField;
//    [SerializeField] private float hallVoltage;

//    [Header("Hall Equilibrium (Visual)")]
//    [SerializeField] private float visualHalfWidth = 0.5f;
//    [SerializeField] private float hallFieldGain = 5f;
//    [SerializeField] private float maxHallEField = 50f;

//    public float Current => current;
//    public float CurrentDensity => currentDensity;
//    public float DriftVelocity => driftVelocity;
//    public float HallElectricField => hallElectricField;
//    public float HallVoltage => hallVoltage;

//    private readonly Queue<ChargeParticle> poolElectrons = new();
//    private readonly Queue<ChargeParticle> poolHoles = new();
//    private readonly Queue<Pack> packPool = new();

//    private readonly List<Pack> activePacks = new();

//    private readonly List<Vector3> cathodeSpawnPoints = new();
//    private readonly List<Vector3> anodeSpawnPoints = new();

//    private ChargeParticle.CarrierType carrierType = ChargeParticle.CarrierType.Electron;

//    private float emitAccumulator;
//    private int spawnIndex;

//    private Vector3 flowDirection = Vector3.right;
//    private float totalMaterialLength;
//    private Vector3 hallAxis;
//    private float conductorLateralCenter;
//    private float netLateralCharge;

//    private void Awake()
//    {
//        if (poolParentElectrons == null) poolParentElectrons = transform;
//        if (poolParentHoles == null) poolParentHoles = transform;
//        if (packParent == null) packParent = transform;

//        PrewarmPool(electronPrefab, poolSizeElectrons, poolParentElectrons, poolElectrons);
//        PrewarmPool(holePrefab, poolSizeHoles, poolParentHoles, poolHoles);
//        PrewarmPackPool();

//        CalculatePlateGeometry();
//    }

//    private void Start()
//    {
//        if (voltageSlider != null) voltageSlider.value = 0f;
//        if (magneticFieldSlider != null) magneticFieldSlider.value = 0f;

//        if (electronsToggle != null)
//        {
//            electronsToggle.onValueChanged.AddListener(isOn =>
//            {
//                if (isOn) SetCarrier(ChargeParticle.CarrierType.Electron);
//            });
//        }

//        if (holesToggle != null)
//        {
//            holesToggle.onValueChanged.AddListener(isOn =>
//            {
//                if (isOn) SetCarrier(ChargeParticle.CarrierType.Hole);
//            });
//        }

//        SetCarrier(ChargeParticle.CarrierType.Electron);
//    }

//    private void Update()
//    {
//        if (!cathode.isValid || !anode.isValid) return;

//        float voltage = voltageSlider != null ? voltageSlider.value : 0f;
//        float magneticField = magneticFieldSlider != null ? magneticFieldSlider.value : 0f;
//        float dt = Time.deltaTime;

//        UpdateHallPhysics(voltage, magneticField);

//        if (Mathf.Abs(voltage) >= minAbsVoltage)
//        {
//            HandlePackEmission(voltage, magneticField, dt);
//        }

//        UpdatePacks(voltage, magneticField, dt);
//    }

//    private void UpdateHallPhysics(float voltage, float magneticField)
//    {
//        float voltageFraction = Mathf.Clamp(voltage / maxAbsVoltage, -1f, 1f);

//        current = maxCurrent * voltageFraction;

//        float crossSectionArea = conductorWidth * conductorThickness;

//        if (carrierDensity <= 0f || carrierCharge <= 0f || crossSectionArea <= 0f)
//        {
//            currentDensity = 0f;
//            driftVelocity = 0f;
//            hallCoefficient = 0f;
//            hallElectricField = 0f;
//            hallVoltage = 0f;
//            return;
//        }

//        float chargeSign = carrierType == ChargeParticle.CarrierType.Hole ? 1f : -1f;

//        currentDensity = current / crossSectionArea;
//        hallCoefficient = chargeSign / (carrierDensity * carrierCharge);
//        driftVelocity = currentDensity / (carrierDensity * carrierCharge);

//        hallElectricField = hallCoefficient * currentDensity * magneticField;
//        hallVoltage = hallElectricField * conductorWidth;
//    }

//    private void HandlePackEmission(float voltage, float magneticField, float dt)
//    {
//        float intensity = Mathf.InverseLerp(minAbsVoltage, maxAbsVoltage, Mathf.Abs(voltage));
//        if (intensity <= 0f) return;

//        emitAccumulator += maxEmissionRate * intensity * dt;
//        int spawnCount = Mathf.FloorToInt(emitAccumulator);
//        emitAccumulator -= spawnCount;

//        Vector3 travelDir = GetFlowDirection(voltage);
//        float speed = baseSpeed * Mathf.Max(0.1f, intensity);

//        bool spawnAtCathode = Vector3.Dot(travelDir, flowDirection) > 0f;
//        List<Vector3> spawnPoints = spawnAtCathode ? cathodeSpawnPoints : anodeSpawnPoints;

//        if (spawnPoints.Count == 0) return;

//        for (int i = 0; i < spawnCount; i++)
//        {
//            SpawnPack(spawnPoints, travelDir, speed, voltage, magneticField);
//        }
//    }

//    private void SpawnPack(
//        List<Vector3> spawnPoints,
//        Vector3 travelDir,
//        float speed,
//        float voltage,
//        float magneticField)
//    {
//        Pack pack = GetPackFromPool();
//        if (pack == null) return;

//        Vector3 center = spawnPoints[spawnIndex++ % spawnPoints.Count];

//        List<ChargeParticle> charges = new();

//        for (int i = 0; i < chargesPerPack; i++)
//        {
//            ChargeParticle charge = GetParticleFromPool(carrierType);
//            if (charge == null) continue;

//            Vector3 randomOffset = Random.insideUnitSphere * 0.12f;

//            charge.transform.position = center + randomOffset;
//            charge.gameObject.SetActive(true);

//            charges.Add(charge);
//        }

//        Vector3 eField = GetElectricField(voltage);
//        Vector3 bField = GetMagneticField(magneticField);

//        pack.gameObject.SetActive(true);

//        pack.Initialize(
//            center,
//            charges,
//            carrierType,
//            travelDir * speed,
//            eField,
//            bField
//        );

//        activePacks.Add(pack);
//    }

//    private void UpdatePacks(float voltage, float magneticField, float dt)
//    {
//        Vector3 eField = GetElectricField(voltage);
//        Vector3 bField = GetMagneticField(magneticField);

//        for (int i = activePacks.Count - 1; i >= 0; i--)
//        {
//            Pack pack = activePacks[i];

//            if (pack == null)
//            {
//                activePacks.RemoveAt(i);
//                continue;
//            }

//            pack.SetFields(eField, bField);
//            pack.Step(dt);

//            float packLateralPos = Vector3.Dot(pack.transform.position, hallAxis);
//            float lateralDist = packLateralPos - conductorLateralCenter;

//            if (Mathf.Abs(lateralDist) >= visualHalfWidth)
//            {
//                float side = Mathf.Sign(lateralDist);
//                netLateralCharge += side * pack.CarrierChargeSign * carrierCharge * dt;

//                Vector3 pos = pack.transform.position;
//                pos -= hallAxis * (lateralDist - side * visualHalfWidth);
//                pack.transform.position = pos;
//                pack.KillOutwardLateral(hallAxis);
//            }

//            if (ShouldDespawnPack(pack, voltage))
//            {
//                DespawnPack(pack, i);
//            }
//        }
//    }

//    private Vector3 GetElectricField(float voltage)
//    {
//        Vector3 flowDir = GetFlowDirection(voltage);
//        float fieldStrength = Mathf.Abs(voltage) / Mathf.Max(totalMaterialLength, 0.001f);
//        return flowDir * fieldStrength + GetHallTransverseField();
//    }

//    private Vector3 GetHallTransverseField()
//    {
//        float eFieldMag = Mathf.Clamp(-netLateralCharge * hallFieldGain, -maxHallEField, maxHallEField);
//        return hallAxis * eFieldMag;
//    }

//    private Vector3 GetMagneticField(float magneticField)
//    {
//        return magneticFieldAxis.normalized * magneticField;
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

//    private bool ShouldDespawnPack(Pack pack, float voltage)
//    {
//        Vector3 travelDir = GetFlowDirection(voltage);

//        bool movingFromCathode = Vector3.Dot(travelDir, flowDirection) > 0f;
//        Vector3 origin = movingFromCathode ? cathode.center : anode.center;

//        Vector3 rel = pack.transform.position - origin;
//        float forwardDist = Vector3.Dot(rel, travelDir);

//        return forwardDist > totalMaterialLength || forwardDist < -0.5f;
//    }

//    private void DespawnPack(Pack pack, int index)
//    {
//        activePacks.RemoveAt(index);

//        pack.Despawn();
//        packPool.Enqueue(pack);
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

//        ClearActivePacks();
//    }

//    private void ClearActivePacks()
//    {
//        for (int i = activePacks.Count - 1; i >= 0; i--)
//        {
//            DespawnPack(activePacks[i], i);
//        }

//        emitAccumulator = 0f;
//        netLateralCharge = 0f;
//    }

//    private void CalculatePlateGeometry()
//    {
//        if (cathode.plate == null || anode.plate == null) return;

//        cathode.isValid = ExtractBounds(ref cathode);
//        anode.isValid = ExtractBounds(ref anode);

//        if (!cathode.isValid || !anode.isValid) return;

//        flowDirection = (anode.center - cathode.center).normalized;
//        totalMaterialLength = Vector3.Distance(cathode.center, anode.center);

//        hallAxis = Vector3.Cross(flowDirection, magneticFieldAxis.normalized);
//        if (hallAxis.sqrMagnitude < 1e-6f)
//        {
//            Debug.LogWarning("[HallEffect_PHYSICS] magneticFieldAxis is parallel to the conductor flow direction — v × B = 0, no deflection will occur. Set magneticFieldAxis perpendicular to the conductor axis.");
//            hallAxis = Vector3.up;
//        }
//        else
//        {
//            hallAxis.Normalize();
//        }

//        conductorLateralCenter = Vector3.Dot(cathode.center, hallAxis);

//        GenerateEmissionPoints();
//    }

//    private bool ExtractBounds(ref PlateSurface surface)
//    {
//        Renderer r = surface.rendererOverride != null
//            ? surface.rendererOverride
//            : surface.plate.GetComponentInChildren<Renderer>();

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
//        Renderer r = surface.rendererOverride != null
//            ? surface.rendererOverride
//            : surface.plate.GetComponentInChildren<Renderer>();

//        if (r == null) return;

//        Bounds b = r.bounds;

//        int n = Mathf.Max(2, surfaceGridSide);

//        float halfY = b.extents.y * emissionScale;
//        float halfZ = b.extents.z * emissionScale;

//        for (int y = 0; y < n; y++)
//        {
//            for (int z = 0; z < n; z++)
//            {
//                float normY = (y / (float)(n - 1)) * 2f - 1f;
//                float normZ = (z / (float)(n - 1)) * 2f - 1f;

//                pointsList.Add(b.center + new Vector3(0f, normY * halfY, normZ * halfZ));
//            }
//        }
//    }

//    private void PrewarmPool(
//        ChargeParticle prefab,
//        int count,
//        Transform parent,
//        Queue<ChargeParticle> pool)
//    {
//        if (prefab == null) return;
//        if (parent == null) parent = transform;

//        for (int i = 0; i < count; i++)
//        {
//            ChargeParticle p = Instantiate(prefab, parent);
//            p.gameObject.SetActive(false);
//            pool.Enqueue(p);
//        }
//    }

//    private void PrewarmPackPool()
//    {
//        if (packPrefab == null) return;
//        if (packParent == null) packParent = transform;

//        for (int i = 0; i < packPoolSize; i++)
//        {
//            Pack p = Instantiate(packPrefab, packParent);
//            p.gameObject.SetActive(false);
//            packPool.Enqueue(p);
//        }
//    }

//    private Pack GetPackFromPool()
//    {
//        if (packPool.Count > 0)
//            return packPool.Dequeue();

//        if (packPrefab == null) return null;

//        return Instantiate(packPrefab, packParent != null ? packParent : transform);
//    }

//    private ChargeParticle GetParticleFromPool(ChargeParticle.CarrierType type)
//    {
//        Queue<ChargeParticle> pool = type == ChargeParticle.CarrierType.Electron
//            ? poolElectrons
//            : poolHoles;

//        ChargeParticle prefab = type == ChargeParticle.CarrierType.Electron
//            ? electronPrefab
//            : holePrefab;

//        Transform parent = type == ChargeParticle.CarrierType.Electron
//            ? poolParentElectrons
//            : poolParentHoles;

//        if (pool.Count > 0)
//            return pool.Dequeue();

//        if (prefab == null) return null;

//        return Instantiate(prefab, parent != null ? parent : transform);
//    }

//    private void OnDrawGizmos()
//    {
//        if (cathode.plate == null || anode.plate == null) return;

//        Vector3 flow = (anode.plate.position - cathode.plate.position).normalized;
//        Vector3 bDir = magneticFieldAxis.normalized;
//        Vector3 hallAxis = Vector3.Cross(flow, bDir).normalized;

//        Vector3 origin = cathode.plate.position;

//        Gizmos.color = Color.yellow;
//        Gizmos.DrawRay(origin, flow * 2f);

//        Gizmos.color = Color.green;
//        Gizmos.DrawRay(origin, bDir * 2f);

//        Gizmos.color = Color.red;
//        Gizmos.DrawRay(origin, hallAxis * 2f);
//    }
//}