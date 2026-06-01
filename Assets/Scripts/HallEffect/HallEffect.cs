// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UI;

// [System.Serializable]
// public struct PlateSurface
// {
//     public Transform plate;
//     public Renderer rendererOverride;

//     [HideInInspector] public Vector3 center;
//     [HideInInspector] public Vector2 extents;
//     [HideInInspector] public bool isValid;
// }

// public class HallEffect : MonoBehaviour
// {
//     [Header("Plates")]
//     [SerializeField] private PlateSurface cathode;
//     [SerializeField] private PlateSurface anode;

//     [Header("UI Controls")]
//     [SerializeField] private Slider voltageSlider;
//     [SerializeField] private Slider magneticFieldSlider;
//     [SerializeField] private Toggle electronsToggle;
//     [SerializeField] private Toggle holesToggle;

//     [Header("Physics Dynamics")]
//     [SerializeField] private Vector3 magneticFieldAxis = Vector3.up;
//     [SerializeField] private float deflectionMultiplier = 2.0f;
//     [SerializeField] private float macroChargeValue = 1.6e-19f;
//     [SerializeField] private float eFieldTuningConstant = 1e18f;
//     [SerializeField] private float materialWidth = 5.0f;
//     [Tooltip("Shrinks the spawn area slightly so particles don't spawn inside walls.")]
//     [Range(0.1f, 1.0f)][SerializeField] private float emissionScale = 0.9f;

//     [Header("Carrier Prefabs")]
//     [SerializeField] private ChargeParticle electronPrefab;
//     [SerializeField] private ChargeParticle holePrefab;

//     [Header("Emission Mapping")]
//     [SerializeField] private float minAbsVoltage = 0.1f;
//     [SerializeField] private float maxAbsVoltage = 10f;
//     [SerializeField] private float maxEmissionRate = 50f;
//     [SerializeField] private float baseSpeed = 3.5f;
//     [SerializeField] private int surfacePointCount = 256;

//     [Header("Hall Field Parameters")]
//     [SerializeField] private float relativePermittivity = 11.7f;
//     [SerializeField] private float hallSideArea = 0.01f;
//     [SerializeField] private float maxHallEField = 1e6f;

//     [Header("Pooling & Memory")]
//     [SerializeField] private int poolSize = 500;
//     [SerializeField] private Transform poolParent;

//     private readonly Queue<ChargeParticle> poolElectrons = new();
//     private readonly Queue<ChargeParticle> poolHoles = new();
//     private readonly List<ChargeParticle> activeParticles = new();

//     private readonly List<Vector3> cachedSpawnPoints = new();
  

//     private float emitAccumulator;
//     private float netLateralCharge = 0f;
//     public float CurrentNetLateralCharge => netLateralCharge;

//     private ChargeParticle.CarrierType carrierType = ChargeParticle.CarrierType.Electron;

//     private readonly List<Vector3> cathodeSpawnPoints = new();
//     private readonly List<Vector3> anodeSpawnPoints = new();
//     private int spawnIndex = 0;

//     private void Awake()
//     {
//         if (poolParent == null) poolParent = transform;

//         PrewarmPool(electronPrefab, poolSize, poolElectrons);
//         PrewarmPool(holePrefab, poolSize, poolHoles);

//         // Listen for clicks on the toggles
//         if (electronsToggle != null)
//             electronsToggle.onValueChanged.AddListener(isOn => { if (isOn) SetCarrier(ChargeParticle.CarrierType.Electron); });

//         if (holesToggle != null)
//             holesToggle.onValueChanged.AddListener(isOn => { if (isOn) SetCarrier(ChargeParticle.CarrierType.Hole); });

//         // Initialize the starting state
//         if (electronsToggle != null && electronsToggle.isOn) SetCarrier(ChargeParticle.CarrierType.Electron);
//         else if (holesToggle != null && holesToggle.isOn) SetCarrier(ChargeParticle.CarrierType.Hole);
//         else SetCarrier(ChargeParticle.CarrierType.Electron);

//         CalculatePlateGeometry();
//     }

//     private void SetCarrier(ChargeParticle.CarrierType type)
//     {
//         carrierType = type;

//         // Update the UI toggles silently so they act like Radio Buttons
//         if (type == ChargeParticle.CarrierType.Electron)
//         {
//             if (holesToggle != null) holesToggle.SetIsOnWithoutNotify(false);
//             if (electronsToggle != null) electronsToggle.SetIsOnWithoutNotify(true);
//         }
//         else
//         {
//             if (electronsToggle != null) electronsToggle.SetIsOnWithoutNotify(false);
//             if (holesToggle != null) holesToggle.SetIsOnWithoutNotify(true);
//         }

//         // Reset the Hall Voltage back to 0 when switching materials
//         netLateralCharge = 0f;
//     }

//     private void Update()
//     {
//         if (!cathode.isValid || !anode.isValid) return;

//         float voltage = (voltageSlider != null) ? voltageSlider.value : 0f;
//         if (Mathf.Abs(voltage) < minAbsVoltage) return;

//         float dt = Time.deltaTime <= 1e-7f ? Time.unscaledDeltaTime : Time.deltaTime;

//         HandleEmission(voltage, dt);
//         ProcessPhysics(voltage, dt);
//     }

//     private void ProcessPhysics(float voltage, float dt)
//     {
//         if (activeParticles.Count == 0) return;

//         Vector3 travelDir = GetParticleTravelDirection(voltage);
//         Vector3 bDir = magneticFieldAxis.normalized;
//         Vector3 lateralAxis = Vector3.Cross(travelDir, bDir).normalized;

//         float bFieldStrength = (magneticFieldSlider != null) ? magneticFieldSlider.value : 0f;
//         Vector3 magneticField = bDir * bFieldStrength;

//         // Hardcoded distance based on the exact World X coordinates of your plates
//         float totalDist = Mathf.Abs(anode.center.x - cathode.center.x);

//         bool startingAtCathode = travelDir.x > 0;
//         Vector3 sourceCenter = startingAtCathode ? cathode.center : anode.center;

//         // Calculate Macroscopic Hall E-Field
//         const float eps0 = 8.854187817e-12f;
//         float eps = eps0 * relativePermittivity;
//         float sigma = netLateralCharge / Mathf.Max(1e-8f, hallSideArea);

//         // Apply the tuning constant BEFORE the safety clamp!
//         float rawEFieldMag = (sigma / eps) * eFieldTuningConstant;
//         float eFieldMag = Mathf.Clamp(rawEFieldMag, -maxHallEField, maxHallEField);
//         Vector3 hallElectricField = (-lateralAxis) * eFieldMag;

//         for (int i = activeParticles.Count - 1; i >= 0; i--)
//         {
//             ChargeParticle p = activeParticles[i];
//             float qSign = (p.Carrier == ChargeParticle.CarrierType.Hole) ? 1f : -1f;

//             // 1. Lorentz Force Calculation
//             Vector3 vCrossB = Vector3.Cross(p.Velocity, magneticField) * deflectionMultiplier;
//             Vector3 acceleration = qSign * (hallElectricField + vCrossB);

//             // Integrate Lateral Velocity
//             p.Velocity += acceleration * dt;

//             // 2. Strict Forward Drift
//             // Force the forward velocity to remain constant, only allowing the magnetic field to alter sideways momentum.
//             float targetForwardSpeed = baseSpeed * Mathf.Max(0.2f, Mathf.Abs(voltage) / maxAbsVoltage);
//             Vector3 lateralVel = p.Velocity - travelDir * Vector3.Dot(p.Velocity, travelDir);
//             p.Velocity = (travelDir * targetForwardSpeed) + lateralVel;

//             p.transform.position += p.Velocity * dt;

//             // Boundary Checks
//             Vector3 relPos = p.transform.position - sourceCenter;
//             float forwardDist = Vector3.Dot(relPos, travelDir);
//             float lateralDist = Vector3.Dot(relPos, lateralAxis);

//             // 3. THE VISUAL HALL EQUILIBRIUM MECHANIC (Slide and Charge)
//             if (Mathf.Abs(lateralDist) >= materialWidth * 0.5f)
//             {
//                 float side = Mathf.Sign(lateralDist);

//                 // Gradually build up the Hall Voltage charge while it is pressed against the wall
//                 netLateralCharge += side * (qSign * macroChargeValue) * dt;

//                 // Visually clamp the particle to the wall so it doesn't disappear
//                 Vector3 correctedPos = p.transform.position;
//                 correctedPos -= lateralAxis * (lateralDist - (side * materialWidth * 0.5f));
//                 p.transform.position = correctedPos;

//                 // Kill only the sideways velocity so it slides forward instead of pushing out
//                 float latVel = Vector3.Dot(p.Velocity, lateralAxis);
//                 if (Mathf.Sign(latVel) == side)
//                 {
//                     p.Velocity -= lateralAxis * latVel;
//                 }
//             }

//             // 4. FORWARD BOUNDARY: This is the ONLY place particles despawn
//             if (forwardDist >= totalDist || forwardDist < -0.2f)
//             {
//                 DespawnParticle(p, i);
//                 continue;
//             }
//         }
//     }

//     //private void ProcessPhysics(float voltage, float dt)
//     //{
//     //    if (activeParticles.Count == 0) return;

//     //    Vector3 bDir = magneticFieldAxis.normalized;

//     //    float bFieldStrength = (magneticFieldSlider != null) ? magneticFieldSlider.value : 0f;
//     //    Vector3 magneticField = bDir * bFieldStrength;

//     //    Vector3 travelDir = GetParticleTravelDirection(voltage);
//     //    Vector3 lateralAxis = Vector3.Cross(travelDir, bDir).normalized;

//     //    // Hardcode the total distance to the ~1 unit X-gap
//     //    float totalDist = Mathf.Abs(anode.center.x - cathode.center.x);

//     //    // Determine origin for the distance check
//     //    bool startingAtCathode = travelDir.x > 0;
//     //    Vector3 sourceCenter = startingAtCathode ? cathode.center : anode.center;


//     //    //float totalDist = Vector3.Distance(cathode.center, anode.center);

//     //    //bool startingAtCathode = Vector3.Dot(travelDir, anode.center - cathode.center) > 0;
//     //    //Vector3 sourceCenter = startingAtCathode ? cathode.center : anode.center;

//     //    //// Calculate Macroscopic Hall E-Field
//     //    //const float eps0 = 8.854187817e-12f;
//     //    //float eps = eps0 * relativePermittivity;
//     //    //float sigma = netLateralCharge / Mathf.Max(1e-8f, hallSideArea);

//     //    //// Apply the tuning constant BEFORE the safety clamp!
//     //    //float rawEFieldMag = (sigma / eps) * eFieldTuningConstant;
//     //    //float eFieldMag = Mathf.Clamp(rawEFieldMag, -maxHallEField, maxHallEField);

//     //    //Vector3 hallElectricField = (-lateralAxis) * eFieldMag;

//     //    //for (int i = activeParticles.Count - 1; i >= 0; i--)
//     //    //{
//     //    //    ChargeParticle p = activeParticles[i];
//     //    //    float qSign = (p.Carrier == ChargeParticle.CarrierType.Hole) ? 1f : -1f;

//     //    //    // 1. Lorentz Force Calculation
//     //    //    Vector3 vCrossB = Vector3.Cross(p.Velocity, magneticField) * deflectionMultiplier;
//     //    //    Vector3 acceleration = qSign * (hallElectricField + vCrossB);

//     //    //    p.Velocity += acceleration * dt;

//     //    //    // 2. --- THE FIX: Solid-State Friction (Drude Model) ---
//     //    //    // Aggressively dampen the sideways momentum so the E-field pushes them gently
//     //    //    // instead of railgunning them across the gap.
//     //    //    float currentLatVel = Vector3.Dot(p.Velocity, lateralAxis);
//     //    //    p.Velocity -= lateralAxis * (currentLatVel * 0.85f); // 85% friction per frame

//     //    //    // 3. --- THE FIX: The Speed Limit ---
//     //    //    // Physically prevent the particle from moving faster than the width of the tube in one second
//     //    //    float maxLatSpeed = materialWidth * 2f;
//     //    //    float clampedLatVel = Mathf.Clamp(Vector3.Dot(p.Velocity, lateralAxis), -maxLatSpeed, maxLatSpeed);

//     //    //    // Reassemble the velocity vector safely
//     //    //    float currentForwardSpeed = Vector3.Dot(p.Velocity, travelDir);
//     //    //    p.Velocity = (travelDir * currentForwardSpeed) + (lateralAxis * clampedLatVel);

//     //    //    // 4. Solid-State Forward Drift Velocity Override
//     //    //    float targetForwardSpeed = baseSpeed * Mathf.Max(0.2f, Mathf.Abs(voltage) / maxAbsVoltage);
//     //    //    p.Velocity += travelDir * (targetForwardSpeed - currentForwardSpeed);

//     //    //    p.transform.position += p.Velocity * dt;
//     //    //    // Boundary Checks (measured from whichever plate they spawned at)
//     //    //    Vector3 relPos = p.transform.position - sourceCenter;
//     //    //    float forwardDist = Vector3.Dot(relPos, travelDir);
//     //    //    float lateralDist = Vector3.Dot(relPos, lateralAxis);

//     //    //    // 1. LATERAL BOUNDARY: Clamp to the wall and slide!
//     //    //    if (Mathf.Abs(lateralDist) >= materialWidth * 0.5f)
//     //    //    {
//     //    //        float side = Mathf.Sign(lateralDist);

//     //    //        // Clamp the particle's position so it doesn't leave the material
//     //    //        Vector3 correctedPos = p.transform.position;
//     //    //        correctedPos -= lateralAxis * (lateralDist - (side * materialWidth * 0.5f));
//     //    //        p.transform.position = correctedPos;

//     //    //        // Strip away the sideways velocity so it doesn't keep pushing through the wall,
//     //    //        // but keep its forward velocity so it slides toward the target!
//     //    //        float latVel = Vector3.Dot(p.Velocity, lateralAxis);
//     //    //        if (Mathf.Sign(latVel) == side)
//     //    //        {
//     //    //            p.Velocity -= lateralAxis * latVel;
//     //    //        }

//     //    //        // Gradually build up the Hall Voltage charge while it slides against the wall
//     //    //        netLateralCharge += side * (qSign * macroChargeValue) * dt;
//     //    //    }

//     //    //    // 2. FORWARD BOUNDARY: This is now the ONLY place particles despawn
//     //    //    if (forwardDist >= totalDist || forwardDist < -0.2f)
//     //    //    {
//     //    //        DespawnParticle(p, i);
//     //    //        continue;
//     //    //    }
//     //    //}
//     //    // Calculate Macroscopic Hall E-Field
//     //    const float eps0 = 8.854187817e-12f;
//     //    float eps = eps0 * relativePermittivity;
//     //    float sigma = netLateralCharge / Mathf.Max(1e-8f, hallSideArea);

//     //    float rawEFieldMag = (sigma / eps) * eFieldTuningConstant;
//     //    float eFieldMag = Mathf.Clamp(rawEFieldMag, -maxHallEField, maxHallEField);
//     //    Vector3 hallElectricField = (-lateralAxis) * eFieldMag;

//     //    for (int i = activeParticles.Count - 1; i >= 0; i--)
//     //    {
//     //        ChargeParticle p = activeParticles[i];
//     //        float qSign = (p.Carrier == ChargeParticle.CarrierType.Hole) ? 1f : -1f;

//     //        // 1. Lorentz Force Calculation
//     //        Vector3 vCrossB = Vector3.Cross(p.Velocity, magneticField) * deflectionMultiplier;
//     //        Vector3 acceleration = qSign * (hallElectricField + vCrossB);

//     //        // Integrate Lateral Velocity
//     //        p.Velocity += acceleration * dt;

//     //        // 2. Strict Forward Drift
//     //        // Force the forward velocity to remain constant, only allowing the magnetic field to alter sideways momentum.
//     //        float targetForwardSpeed = baseSpeed * Mathf.Max(0.2f, Mathf.Abs(voltage) / maxAbsVoltage);
//     //        Vector3 lateralVel = p.Velocity - travelDir * Vector3.Dot(p.Velocity, travelDir);
//     //        p.Velocity = (travelDir * targetForwardSpeed) + lateralVel;

//     //        p.transform.position += p.Velocity * dt;

//     //        // Boundary Checks
//     //        Vector3 relPos = p.transform.position - sourceCenter;
//     //        float forwardDist = Vector3.Dot(relPos, travelDir);
//     //        float lateralDist = Vector3.Dot(relPos, lateralAxis);

//     //        // 3. THE HALL EQUILIBRIUM MECHANIC
//     //        if (Mathf.Abs(lateralDist) >= materialWidth * 0.5f)
//     //        {
//     //            // Particle hits the wall. Deposit the charge!
//     //            float side = Mathf.Sign(lateralDist);
//     //            netLateralCharge += side * (qSign * macroChargeValue);

//     //            // Despawn this particle. It has become part of the surface charge.
//     //            // Once enough particles do this, the E-Field perfectly cancels the B-Field,
//     //            // and subsequent particles will fly perfectly straight!
//     //            DespawnParticle(p, i);
//     //            continue;
//     //        }

//     //        if (forwardDist >= totalDist || forwardDist < -0.2f)
//     //        {
//     //            DespawnParticle(p, i);
//     //            continue;
//     //        }
//     //    }
//     //}

//     private Vector3 GetParticleTravelDirection(float voltage)
//     {
//         // 1. Force the base direction to be exactly +X (Left to Right)
//         Vector3 travelDir = Vector3.right;

//         // 2. Holes flow the opposite way (Anode to Cathode, Right to Left)
//         if (carrierType == ChargeParticle.CarrierType.Hole)
//             travelDir = Vector3.left;

//         // 3. Reversing the voltage reverses the E-field and the flow
//         if (voltage < 0f)
//             travelDir = -travelDir;

//         return travelDir;
//     }

//     private void HandleEmission(float voltage, float dt)
//     {
//         float intensity = Mathf.InverseLerp(minAbsVoltage, maxAbsVoltage, Mathf.Abs(voltage));
//         if (intensity <= 0f) return;

//         float emissionRate = maxEmissionRate * intensity;
//         emitAccumulator += emissionRate * dt;
//         int spawnCount = Mathf.FloorToInt(emitAccumulator);
//         emitAccumulator -= spawnCount;

//         float speed = baseSpeed * Mathf.Max(0.2f, intensity);
//         Vector3 travelDir = GetParticleTravelDirection(voltage);

//         // If travelDir is +X, start at the Cathode (Left). 
//         // If travelDir is -X, start at the Anode (Right).
//         bool spawningAtCathode = travelDir.x > 0;
//         List<Vector3> currentSpawnPoints = spawningAtCathode ? cathodeSpawnPoints : anodeSpawnPoints;

//         if (currentSpawnPoints.Count == 0) return;

//         for (int i = 0; i < spawnCount; i++)
//         {
//             ChargeParticle p = GetParticleFromPool(carrierType);
//             if (p == null) continue;

//             Vector3 spawnPos = currentSpawnPoints[spawnIndex++ % currentSpawnPoints.Count];

//             p.transform.position = spawnPos;
//             p.gameObject.SetActive(true);
//             p.Initialize(carrierType, travelDir * speed, 10f);

//             activeParticles.Add(p);
//         }
//     }

//     private Vector3 GetFlowDirection(float voltage)
//     {
//         Vector3 dir = (anode.center - cathode.center).normalized;
//         return voltage < 0f ? -dir : dir;
//     }

//     [ContextMenu("Recalculate Geometry")]
//     private void CalculatePlateGeometry()
//     {
//         if (cathode.plate == null || anode.plate == null) return;

//         cathode.isValid = ExtractBounds(ref cathode);
//         anode.isValid = ExtractBounds(ref anode);

//         if (cathode.isValid && anode.isValid)
//         {
//             GenerateEmissionPoints();
//         }
//     }

//     private bool ExtractBounds(ref PlateSurface surface)
//     {
//         Renderer r = surface.rendererOverride != null ? surface.rendererOverride : surface.plate.GetComponentInChildren<Renderer>();
//         if (r == null) return false;

//         Bounds b = r.bounds;
//         surface.center = b.center;

//         // Convert to plate's local scale to find flat dimensions
//         surface.extents = new Vector2(b.extents.x, b.extents.y);

//         if (ReferenceEquals(surface.plate, cathode.plate))
//         {
//             materialWidth = Mathf.Max(b.size.x, b.size.y, b.size.z);
//         }
//         return true;
//     }

//     private void GenerateEmissionPoints()
//     {
//         cathodeSpawnPoints.Clear();
//         anodeSpawnPoints.Clear();

//         BuildPointsForPlate(cathode, cathodeSpawnPoints);
//         BuildPointsForPlate(anode, anodeSpawnPoints);
//     } 

//     //private void BuildPointsForPlate(PlateSurface surface, List<Vector3> pointsList)
//     //{
//     //    Vector3 right = surface.plate.right * surface.extents.x * emissionScale;
//     //    Vector3 up = surface.plate.up * surface.extents.y * emissionScale;

//     //    for (int i = 0; i < surfacePointCount; i++)
//     //    {
//     //        // Pick a completely random spot between the left/right and top/bottom edges
//     //        float randomX = Random.Range(-1f, 1f);
//     //        float randomY = Random.Range(-1f, 1f);

//     //        Vector3 offset = (randomX * right) + (randomY * up);
//     //        pointsList.Add(surface.center + offset);
//     //    }

//     //    // Shuffle the list so the emission order is completely randomized, 
//     //    // preventing them from spawning in clumps or patterns.
//     //    for (int i = 0; i < pointsList.Count; i++)
//     //    {
//     //        Vector3 temp = pointsList[i];
//     //        int randomIndex = Random.Range(i, pointsList.Count);
//     //        pointsList[i] = pointsList[randomIndex];
//     //        pointsList[randomIndex] = temp;
//     //    }
//     //}

//     //private void BuildPointsForPlate(PlateSurface surface, List<Vector3> pointsList)
//     //{
//     //    Vector3 right = surface.plate.right * surface.extents.x * emissionScale;
//     //    Vector3 up = surface.plate.up * surface.extents.y * emissionScale;

//     //    // Calculate how many points we need per row/column to hit your target count
//     //    int gridSide = Mathf.CeilToInt(Mathf.Sqrt(surfacePointCount));

//     //    for (int x = 0; x < gridSide; x++)
//     //    {
//     //        for (int y = 0; y < gridSide; y++)
//     //        {
//     //            // Map the x and y indices to a range between -1.0 and 1.0
//     //            float normX = gridSide > 1 ? (x / (float)(gridSide - 1)) * 2f - 1f : 0f;
//     //            float normY = gridSide > 1 ? (y / (float)(gridSide - 1)) * 2f - 1f : 0f;

//     //            Vector3 offset = (normX * right) + (normY * up);
//     //            pointsList.Add(surface.center + offset);

//     //            // Stop early if we've hit the exact surfacePointCount 
//     //            // (in case it's not a perfect square like 256)
//     //            if (pointsList.Count >= surfacePointCount) break;
//     //        }
//     //        if (pointsList.Count >= surfacePointCount) break;
//     //    }

//     //    // Shuffle the grid!
//     //    // This stops the "typewriter" effect so they emit randomly across the plate,
//     //    // but because the points themselves are a perfect grid, the cloud remains perfectly uniform.
//     //    for (int i = 0; i < pointsList.Count; i++)
//     //    {
//     //        Vector3 temp = pointsList[i];
//     //        int randomIndex = Random.Range(i, pointsList.Count);
//     //        pointsList[i] = pointsList[randomIndex];
//     //        pointsList[randomIndex] = temp;
//     //    }
//     //}

//     private void BuildPointsForPlate(PlateSurface surface, List<Vector3> pointsList)
//     {
//         Renderer r = surface.rendererOverride != null ? surface.rendererOverride : surface.plate.GetComponentInChildren<Renderer>();
//         if (r == null) return;

//         Bounds b = r.bounds;

//         // Since the flow is along the X-axis, the face of the plate spans the Y and Z axes.
//         // We use the renderer's world bounds extents for Y (Height) and Z (Depth).
//         float halfY = b.extents.y * emissionScale;
//         float halfZ = b.extents.z * emissionScale;

//         int gridSide = Mathf.CeilToInt(Mathf.Sqrt(surfacePointCount));

//         for (int y = 0; y < gridSide; y++)
//         {
//             for (int z = 0; z < gridSide; z++)
//             {
//                 // Map indices to a -1.0 to 1.0 range
//                 float normY = gridSide > 1 ? (y / (float)(gridSide - 1)) * 2f - 1f : 0f;
//                 float normZ = gridSide > 1 ? (z / (float)(gridSide - 1)) * 2f - 1f : 0f;

//                 // Offset purely on the Y and Z axes (X stays 0 relative to the center)
//                 Vector3 offset = new Vector3(0f, normY * halfY, normZ * halfZ);
//                 pointsList.Add(surface.center + offset);

//                 if (pointsList.Count >= surfacePointCount) break;
//             }
//             if (pointsList.Count >= surfacePointCount) break;
//         }

//         // Shuffle the list so they emit randomly from the grid instead of row-by-row
//         for (int i = 0; i < pointsList.Count; i++)
//         {
//             Vector3 temp = pointsList[i];
//             int randomIndex = Random.Range(i, pointsList.Count);
//             pointsList[i] = pointsList[randomIndex];
//             pointsList[randomIndex] = temp;
//         }
//     }

//     private void SyncToggles()
//     {
//         bool eOn = electronsToggle != null && electronsToggle.isOn;
//         bool hOn = holesToggle != null && holesToggle.isOn;

//         if (eOn && hOn) { if (holesToggle) holesToggle.isOn = false; hOn = false; }
//         if (!eOn && !hOn) { if (electronsToggle) electronsToggle.isOn = true; eOn = true; }

//         carrierType = eOn ? ChargeParticle.CarrierType.Electron : ChargeParticle.CarrierType.Hole;
//     }

//     private void PrewarmPool(ChargeParticle prefab, int count, Queue<ChargeParticle> pool)
//     {
//         if (prefab == null) return;
//         for (int i = 0; i < count; i++)
//         {
//             ChargeParticle p = Instantiate(prefab, poolParent);
//             p.gameObject.SetActive(false);
//             pool.Enqueue(p);
//         }
//     }

//     private ChargeParticle GetParticleFromPool(ChargeParticle.CarrierType type)
//     {
//         var pool = (type == ChargeParticle.CarrierType.Electron) ? poolElectrons : poolHoles;
//         if (pool.Count > 0) return pool.Dequeue();

//         var prefab = (type == ChargeParticle.CarrierType.Electron) ? electronPrefab : holePrefab;
//         return prefab != null ? Instantiate(prefab, poolParent) : null;
//     }

//     private void DespawnParticle(ChargeParticle p, int index)
//     {
//         activeParticles.RemoveAt(index);
//         p.gameObject.SetActive(false);
//         if (p.Carrier == ChargeParticle.CarrierType.Electron) poolElectrons.Enqueue(p);
//         else poolHoles.Enqueue(p);
//     }

//     // --- VISUAL DEBUGGING FOR THE SCENE VIEW ---
//     private void OnDrawGizmos()
//     {
//         if (cathode.plate == null || anode.plate == null) return;

//         Vector3 flow = (anode.plate.position - cathode.plate.position).normalized;
//         Vector3 bField = magneticFieldAxis.normalized;
//         Vector3 lateral = Vector3.Cross(flow, bField).normalized;

//         Gizmos.color = Color.blue;
//         Gizmos.DrawRay(cathode.plate.position, flow * 2f);

//         Gizmos.color = Color.red;
//         Gizmos.DrawRay(cathode.plate.position, lateral * 2f);

//         Gizmos.color = Color.green;
//         Gizmos.DrawRay(cathode.plate.position, bField * 2f);

//         if (cachedSpawnPoints.Count > 0)
//         {
//             Gizmos.color = new Color(1, 1, 0, 0.3f);
//             foreach (var pt in cachedSpawnPoints) Gizmos.DrawSphere(pt, 0.05f);
//         }
//     }
// }