//using System;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.UI;
//using static ChargeEmitter;
//using static HallEffect;

//public class ChargeEmitter : MonoBehaviour
//{
//    public enum CarrierType { Electron, Hole }
//    private enum DiskPlane { XY, XZ, YZ }

//    [Header("Endpoints")]
//    [SerializeField] private Transform cathode;
//    [SerializeField] private Transform anode;

//    [Header("UI")]
//    [SerializeField] private Slider voltageSlider;

//    [Header("Carrier Toggles")]
//    [SerializeField] private Toggle electronToggle;
//    [SerializeField] private Toggle holeToggle;
//    [SerializeField] private CarrierType carrier = CarrierType.Electron;

//    [Header("Prefabs (separate)")]
//    [SerializeField] private ChargeParticle electronPrefab;
//    [SerializeField] private ChargeParticle holePrefab;

//    [Header("Pool")]
//    [SerializeField] private int prewarmEach = 64;
//    [SerializeField] private Transform poolParent;

//    [Header("Voltage Mapping")]
//    [SerializeField] private float minAbsVoltageToEmit = 0.05f;
//    [SerializeField] private float maxAbsVoltage = 10f; // sliderValue * maxAbsVoltage

//    [Header("Emission vs Voltage")]
//    [SerializeField] private float baseRatePerSecond = 5f;
//    [SerializeField] private float ratePerVolt = 3f;

//    [Header("Speed vs Voltage")]
//    [SerializeField] private float baseSpeed = 0.5f;
//    [SerializeField] private float speedPerVolt = 1.5f;

//    [Header("Disk Spawn/Target")]
//    [SerializeField] private float diskRadiusWorld = 0.05f;
//    [SerializeField] private DiskPlane diskPlane = DiskPlane.XZ;
//    [SerializeField] private float surfaceOffset = 0.001f;

//    [Header("Lifetime")]
//    [SerializeField] private float maxLifetime = 3f;

//    private readonly Queue<ChargeParticle> electronPool = new();
//    private readonly Queue<ChargeParticle> holePool = new();

//    private float emitAccumulator;

//    private void Awake()
//    {
//        if (poolParent == null) poolParent = transform;

//        Prewarm(electronPrefab, electronPool, prewarmEach);
//        Prewarm(holePrefab, holePool, prewarmEach);

//        // Toggle wiring (use a ToggleGroup in the UI if possible)
//        if (electronToggle != null)
//            electronToggle.onValueChanged.AddListener(isOn => { if (isOn) SetCarrier(CarrierType.Electron); });

//        if (holeToggle != null)
//            holeToggle.onValueChanged.AddListener(isOn => { if (isOn) SetCarrier(CarrierType.Hole); });

//        // Sync UI -> carrier initial state
//        SyncTogglesToCarrier();
//    }

//    private void SetCarrier(CarrierType newType)
//    {
//        carrier = newType;
//        SyncTogglesToCarrier();

//        // Optional: if you want no "old type" particles lingering, call:
//        // ClearAllActiveParticles();
//    }

//    private void SyncTogglesToCarrier()
//    {
//        if (electronToggle != null) electronToggle.SetIsOnWithoutNotify(carrier == CarrierType.Electron);
//        if (holeToggle != null) holeToggle.SetIsOnWithoutNotify(carrier == CarrierType.Hole);
//    }

//    private void Prewarm(ChargeParticle prefab, Queue<ChargeParticle> pool, int count)
//    {
//        if (prefab == null) return;

//        for (int i = 0; i < count; i++)
//        {
//            var p = Instantiate(prefab, poolParent);
//            p.gameObject.SetActive(false);
//            p.SetReturnToPool(ReturnToPool);
//            pool.Enqueue(p);
//        }
//    }

//    private void Update()
//    {
//        if (cathode == null || anode == null || voltageSlider == null)
//            return;

//        if (cathode == anode)
//        {
//            Debug.LogError("[ChargeEmitter] Cathode and Anode are the SAME object.");
//            return;
//        }

//        float voltage = ReadVoltage(voltageSlider.value);
//        float absV = Mathf.Abs(voltage);

//        if (absV < minAbsVoltageToEmit)
//            return;

//        float rate = baseRatePerSecond + ratePerVolt * absV;
//        float speed = baseSpeed + speedPerVolt * absV;

//        emitAccumulator += rate * Time.deltaTime;

//        while (emitAccumulator >= 1f)
//        {
//            emitAccumulator -= 1f;
//            SpawnOne(speed);
//        }
//    }

//    private float ReadVoltage(float sliderValue)
//    {
//        // Assumes slider is 0..1 -> 0..maxAbsVoltage.
//        // If slider is -1..1, set slider min=-1 max=1 and keep same line.
//        return sliderValue * maxAbsVoltage;
//    }

//    private void SpawnOne(float speed)
//    {
//        bool isElectron = (carrier == CarrierType.Electron);

//        Transform spawnElectrode = isElectron ? cathode : anode;
//        Transform targetElectrode = isElectron ? anode : cathode;

//        // Key: matching point across disks (prevents convergence)
//        Vector3 localOffset = RandomLocalDiskOffset(diskRadiusWorld);

//        Vector3 spawnPos =
//            spawnElectrode.TransformPoint(localOffset) +
//            DiskNormalWorld(spawnElectrode) * surfaceOffset;

//        Vector3 targetPos =
//            targetElectrode.TransformPoint(localOffset) +
//            DiskNormalWorld(targetElectrode) * surfaceOffset;

//        var particle = GetFromPool(carrier);
//        particle.transform.position = spawnPos;
//        particle.gameObject.SetActive(true);

//        // 1. Calculate the initial velocity vector based on the target and speed
//        Vector3 initialDir = (targetPos - spawnPos).normalized;
//        Vector3 initialVelocity = initialDir * speed;

//        // 2. Use the new signature: Initialize(CarrierType, Vector3, float)
//        particle.Initialize(carrier, initialVelocity, maxLifetime);

//        // 3. Register with the central physics manager
//        // Find the HallEffectManager in the scene and add this particle to its active list
//        // Note: You may need a reference to HallEffectManager or make activeParticles public/accessible via a method.
//        var manager = FindFirstObjectByType<HallEffectManager>();
//        if (manager != null)
//        {
//            manager.RegisterActiveParticle(particle);
//        }
//    }

//    private Vector3 RandomLocalDiskOffset(float radiusWorld)
//    {
//        // Uniform over disk area
//        float u = Random.value;
//        float r = radiusWorld * Mathf.Sqrt(u);
//        float theta = Random.value * Mathf.PI * 2f;

//        float a = r * Mathf.Cos(theta);
//        float b = r * Mathf.Sin(theta);

//        return diskPlane switch
//        {
//            DiskPlane.XY => new Vector3(a, b, 0f),
//            DiskPlane.XZ => new Vector3(a, 0f, b),
//            DiskPlane.YZ => new Vector3(0f, a, b),
//            _ => new Vector3(a, 0f, b),
//        };
//    }

//    private Vector3 DiskNormalWorld(Transform reference)
//    {
//        return diskPlane switch
//        {
//            DiskPlane.XY => reference.forward, // local +Z
//            DiskPlane.XZ => reference.up,      // local +Y
//            DiskPlane.YZ => reference.right,   // local +X
//            _ => reference.up,
//        };
//    }

//    private ChargeParticle GetFromPool(CarrierType type)
//    {
//        Queue<ChargeParticle> pool = (type == CarrierType.Electron) ? electronPool : holePool;
//        ChargeParticle prefab = (type == CarrierType.Electron) ? electronPrefab : holePrefab;

//        if (pool.Count > 0)
//            return pool.Dequeue();

//        // Grow pool if needed
//        var p = Instantiate(prefab, poolParent);
//        p.gameObject.SetActive(false);
//        p.SetReturnToPool(ReturnToPool);
//        return p;
//    }

//    private void ReturnToPool(ChargeParticle p)
//    {
//        p.gameObject.SetActive(false);

//        if (p.Carrier == CarrierType.Electron) electronPool.Enqueue(p);
//        else holePool.Enqueue(p);
//    }

//    // Optional helper if you want to purge on toggle-switch
//    private void ClearAllActiveParticles()
//    {
//        for (int i = poolParent.childCount - 1; i >= 0; i--)
//        {
//            var child = poolParent.GetChild(i);
//            var p = child.GetComponent<ChargeParticle>();
//            if (p != null && child.gameObject.activeSelf)
//                ReturnToPool(p);
//        }
//    }
//}