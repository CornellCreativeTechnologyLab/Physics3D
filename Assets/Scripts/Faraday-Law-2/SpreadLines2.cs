using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpreadLines2 : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool isDebug = false;

    [Header("References")]
    [SerializeField] private LineRenderer linePrefab;
    [SerializeField] private Transform poolContainer;
    [SerializeField] private GameObject visualRoot;

    [Header("UI")]
    [SerializeField] private Slider electricFieldSlider;

    [Header("Toggles")]
    [SerializeField] private List<GameObject> toggleWithField = new();
    [SerializeField] private bool alsoTogglePrefabLineRenderer = true;

    [Header("Grid Settings")]
    [SerializeField] private float halfExtent = 10f;
    [SerializeField] private float stepNumerator = 5f;
    [SerializeField] private Vector2 stepClamp = new Vector2(1f, 51f);

    [Header("Pooling")]
    [SerializeField] private int initialPoolSize = 500;
    [Tooltip("Hard ceiling on total LineRenderers. Prevents runaway Instantiate.")]
    [SerializeField] private int maxLines = 5000;

    [Header("Animation")]
    [SerializeField] private float animationSpeed = 5f;
    [SerializeField] private float retireEpsilon = 0.05f;
    [SerializeField, Tooltip("New interior lines emerge from the center instead of snapping in.")]
    private bool spawnFromCenter = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private bool debugLineCreation = false;

    private readonly Queue<LineRenderer> pool = new();
    private readonly List<FieldLine> active = new();
    private readonly List<Vector3> targetBuffer = new();
    private Vector3 currentOrigin;
    private int totalInstantiated;   // every LineRenderer this script has ever created

    public bool HasActiveField { get; private set; }
    public float CurrentMinX { get; private set; }
    public float CurrentMaxX { get; private set; }
    public float CurrentMinY { get; private set; }
    public float CurrentMaxY { get; private set; }
    public float CurrentZ { get; private set; }

    public Vector3 FieldNormal => Vector3.forward;

    private class FieldLine
    {
        public LineRenderer renderer;
        public Vector3 target;
        public bool retiring;
    }

    private void Awake()
    {
        Log("Awake started.");

        ValidateRefs();
        if (linePrefab == null || poolContainer == null)
        {
            Debug.LogError("[SpreadLines] Cannot initialize pool because linePrefab or poolContainer is missing.", this);
            return;
        }

        InitializePool(initialPoolSize);

        if (electricFieldSlider != null)
        {
            electricFieldSlider.onValueChanged.AddListener(OnElectricFieldChanged);
            OnElectricFieldChanged(electricFieldSlider.value);
        }
        else
        {
            Debug.LogError("[SpreadLines] electricFieldSlider is missing. HasActiveField will stay false.", this);
        }

        Log($"Awake finished. Initial HasActiveField={HasActiveField}");
    }

    private void Update()
    {
        if (active.Count == 0)
            return;

        float t = 1f - Mathf.Exp(-animationSpeed * Time.deltaTime);

        for (int i = active.Count - 1; i >= 0; i--)
        {
            FieldLine fl = active[i];
            if (fl.renderer == null)
            {
                active.RemoveAt(i);
                continue;
            }

            Vector3 cur = fl.renderer.transform.position;
            Vector3 dst = fl.retiring ? currentOrigin : fl.target;
            Vector3 next = Vector3.Lerp(cur, dst, t);
            fl.renderer.transform.position = next;

            if (fl.retiring && (next - dst).sqrMagnitude <= retireEpsilon * retireEpsilon)
            {
                ReturnToPool(fl.renderer);
                active.RemoveAt(i);
            }
        }
    }

    private void ValidateRefs()
    {
        if (linePrefab == null)
            Debug.LogError("[SpreadLines] Missing linePrefab.", this);
        if (poolContainer == null)
            Debug.LogError("[SpreadLines] Missing poolContainer.", this);
        if (electricFieldSlider == null)
            Debug.LogError("[SpreadLines] Missing electricFieldSlider.", this);
        if (visualRoot == null)
            Log("visualRoot is not assigned. This is okay if you do not need it.");
    }

    private void InitializePool(int count)
    {
        Log($"InitializePool started. count={count}");

        for (int i = 0; i < count; i++)
        {
            LineRenderer lr = Instantiate(linePrefab, poolContainer);
            totalInstantiated++;
            lr.gameObject.SetActive(false);
            pool.Enqueue(lr);
        }
        Log($"InitializePool finished. pool.Count={pool.Count}");
    }

    private void OnElectricFieldChanged(float value)
    {
        bool enabled = value > 0f;
        HasActiveField = enabled;
        Log($"OnElectricFieldChanged: sliderValue={value}, HasActiveField={HasActiveField}");

        if (visualRoot != null)
            visualRoot.SetActive(enabled);

        for (int i = 0; i < toggleWithField.Count; i++)
            if (toggleWithField[i] != null)
                toggleWithField[i].SetActive(enabled);

        if (alsoTogglePrefabLineRenderer && linePrefab != null)
            linePrefab.enabled = enabled;

        if (!enabled)
        {
            Log("Electric field value is 0, clearing all active field lines.");
            ResetAndPoolAllLines();
            CurrentMinX = CurrentMaxX = CurrentMinY = CurrentMaxY = CurrentZ = 0f;
            return;
        }

        float step = StepSizeByEField(value);
        Log($"Calculated step size from field. value={value}, step={step}");
        SpreadFieldLines(step);
    }

    private float StepSizeByEField(float eField)
    {
        if (eField <= 0f)
            return stepClamp.y;

        float rawStep = stepNumerator / Mathf.Sqrt(eField);
        return Mathf.Clamp(rawStep, stepClamp.x, stepClamp.y);
    }

    private void SpreadFieldLines(float stepSize)
    {
        Log($"SpreadFieldLines started. stepSize={stepSize}");

        if (linePrefab == null)
        {
            Debug.LogError("[SpreadLines] Cannot spread field lines because linePrefab is missing.", this);
            return;
        }

        Vector3 origin = linePrefab.transform.position;
        currentOrigin = origin;

        int divisions = Mathf.Max(1, Mathf.RoundToInt(halfExtent / stepSize));
        float effectiveStep = halfExtent / divisions;

        CurrentMinX = origin.x - halfExtent;
        CurrentMaxX = origin.x + halfExtent;
        CurrentMinY = origin.y - halfExtent;
        CurrentMaxY = origin.y + halfExtent;
        CurrentZ = origin.z;

        // Build targets outermost ring first; center last.
        targetBuffer.Clear();
        for (int r = divisions; r >= 1; r--)
        {
            for (int ix = -r; ix <= r; ix++)
            {
                for (int iy = -r; iy <= r; iy++)
                {
                    if (Mathf.Max(Mathf.Abs(ix), Mathf.Abs(iy)) != r)
                        continue;
                    targetBuffer.Add(new Vector3(
                        origin.x + ix * effectiveStep,
                        origin.y + iy * effectiveStep,
                        origin.z));
                }
            }
        }
        targetBuffer.Add(origin);

        AssignTargets(origin);

        Log($"SpreadFieldLines finished. active.Count={active.Count}, pool.Count={pool.Count}, targets={targetBuffer.Count}");
    }

    private void AssignTargets(Vector3 spawnCenter)
    {
        int targetCount = targetBuffer.Count;

        // KEY FIX: build the new active set from scratch using a reuse pool of
        // currently-active lines. This compacts the list so retiring lines from a
        // previous shrink are reused instead of stranded, and nothing is double-counted.
        // First, collect all currently-live renderers (non-null) as reusable.
        var reusable = new Queue<FieldLine>();
        for (int i = 0; i < active.Count; i++)
            if (active[i].renderer != null)
                reusable.Enqueue(active[i]);
        active.Clear();

        for (int i = 0; i < targetCount; i++)
        {
            FieldLine fl;
            if (reusable.Count > 0)
            {
                fl = reusable.Dequeue();
            }
            else
            {
                LineRenderer lr = GetFromPool();
                if (lr == null) // hit the hard cap
                    break;
                lr.transform.position = spawnFromCenter ? spawnCenter : targetBuffer[i];
                fl = new FieldLine { renderer = lr };
            }

            fl.target = targetBuffer[i];
            fl.retiring = false;
            active.Add(fl);
        }

        // Anything left in reusable beyond what targets needed: retire it (collapse + pool).
        while (reusable.Count > 0)
        {
            FieldLine fl = reusable.Dequeue();
            fl.retiring = true;
            active.Add(fl);
        }
    }

    private LineRenderer GetFromPool()
    {
        if (pool.Count > 0)
        {
            LineRenderer lr = pool.Dequeue();
            lr.gameObject.SetActive(true);
            return lr;
        }

        // Hard cap: refuse to instantiate past maxLines. This is the brake that
        // prevents the runaway 15 GB allocation.
        if (totalInstantiated >= maxLines)
        {
            Debug.LogWarning($"[SpreadLines] Hit maxLines={maxLines}; not instantiating more. " +
                             "Raise maxLines or lower density (stepClamp.x) if you need more.", this);
            return null;
        }

        LineRenderer fresh = Instantiate(linePrefab, poolContainer);
        totalInstantiated++;
        fresh.gameObject.SetActive(true);
        return fresh;
    }

    private void ReturnToPool(LineRenderer lr)
    {
        if (lr == null) return;
        lr.gameObject.SetActive(false);
        pool.Enqueue(lr);
    }

    private void ResetAndPoolAllLines()
    {
        for (int i = active.Count - 1; i >= 0; i--)
            ReturnToPool(active[i].renderer);
        active.Clear();
    }

    private void OnDestroy()
    {
        if (electricFieldSlider != null)
            electricFieldSlider.onValueChanged.RemoveListener(OnElectricFieldChanged);
    }

    private void Log(string message)
    {
        if (!debugLogs || !isDebug) return;
        Debug.Log($"[SpreadLines] {message}", this);
    }
}

//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.UI;

//public class SpreadLines : MonoBehaviour
//{
//    [Header("Debug")]
//    [SerializeField] private bool isDebug = true;

//    [Header("References")]
//    [SerializeField] private LineRenderer linePrefab;
//    [SerializeField] private Transform poolContainer;
//    [SerializeField] private GameObject visualRoot;

//    [Header("UI")]
//    [SerializeField] private Slider electricFieldSlider;

//    [Header("Toggles")]
//    [SerializeField] private List<GameObject> toggleWithField = new();
//    [SerializeField] private bool alsoTogglePrefabLineRenderer = true;

//    [Header("Grid Settings")]
//    [SerializeField] private float halfExtent = 10f;
//    [SerializeField] private float stepNumerator = 5f;
//    [SerializeField] private Vector2 stepClamp = new Vector2(1f, 51f);

//    [Header("Pooling")]
//    [SerializeField] private int initialPoolSize = 200;

//    [Header("Debug")]
//    [SerializeField] private bool debugLogs = true;
//    [SerializeField] private bool debugLineCreation = false;

//    private readonly Queue<LineRenderer> pool = new();
//    private readonly List<LineRenderer> active = new();

//    public bool HasActiveField { get; private set; }
//    public float CurrentMinX { get; private set; }
//    public float CurrentMaxX { get; private set; }
//    public float CurrentMinY { get; private set; }
//    public float CurrentMaxY { get; private set; }
//    public float CurrentZ { get; private set; }

//    public Vector3 FieldNormal => Vector3.forward;

//    private void Awake()
//    {
//        Log("Awake started.");

//        ValidateRefs();
//        if (isDebug)
//        {

//            if (linePrefab == null || poolContainer == null)
//            {
//                Debug.LogError("[SpreadLines] Cannot initialize pool because linePrefab or poolContainer is missing.", this);
//                return;
//            }
//        }


//        InitializePool(initialPoolSize);

//        if (electricFieldSlider != null)
//        {
//            electricFieldSlider.onValueChanged.AddListener(OnElectricFieldChanged);
//            OnElectricFieldChanged(electricFieldSlider.value);
//        }
//        else
//        {
//            Debug.LogError("[SpreadLines] electricFieldSlider is missing. HasActiveField will stay false.", this);
//        }

//        if(isDebug) Log($"Awake finished. Initial HasActiveField={HasActiveField}");
//    }

//    private void ValidateRefs()
//    {
//        if (isDebug) {
//            if (linePrefab == null)
//                Debug.LogError("[SpreadLines] Missing linePrefab.", this);

//            if (poolContainer == null)
//                Debug.LogError("[SpreadLines] Missing poolContainer.", this);

//            if (electricFieldSlider == null)
//                Debug.LogError("[SpreadLines] Missing electricFieldSlider.", this);

//            if (visualRoot == null)
//                Log("visualRoot is not assigned. This is okay if you do not need it.");
//        }

//    }

//    private void InitializePool(int count)
//    {
//     if (isDebug)   Log($"InitializePool started. count={count}");

//        for (int i = 0; i < count; i++)
//        {
//            LineRenderer lr = Instantiate(linePrefab, poolContainer);
//            lr.gameObject.SetActive(false);
//            pool.Enqueue(lr);
//        }
//        if (isDebug)
//            Log($"InitializePool finished. pool.Count={pool.Count}");
//    }

//    private void OnElectricFieldChanged(float value)
//    {
//        bool enabled = value > 0f;
//        HasActiveField = enabled;
//        if (isDebug)
//            Log($"OnElectricFieldChanged: sliderValue={value}, HasActiveField={HasActiveField}");

//        if (visualRoot != null)
//            visualRoot.SetActive(enabled);

//        for (int i = 0; i < toggleWithField.Count; i++)
//        {
//            if (toggleWithField[i] != null)
//                toggleWithField[i].SetActive(enabled);
//        }

//        if (alsoTogglePrefabLineRenderer && linePrefab != null)
//            linePrefab.enabled = enabled;

//        if (!enabled)
//        {
//            if (isDebug)
//                Log("Electric field value is 0, so clearing all active field lines.");
//            ResetAndPoolAllLines();

//            CurrentMinX = 0f;
//            CurrentMaxX = 0f;
//            CurrentMinY = 0f;
//            CurrentMaxY = 0f;
//            CurrentZ = 0f;

//            return;
//        }

//        float step = StepSizeByEField(value);
//        if (isDebug)
//            Log($"Calculated step size from field. value={value}, step={step}");

//        SpreadFieldLines(step);
//    }

//    private float StepSizeByEField(float eField)
//    {
//        if (eField <= 0f)
//        {
//            if (isDebug)
//                Log($"StepSizeByEField received eField <= 0. eField={eField}");
//            return stepClamp.y;
//        }

//        float rawStep = stepNumerator / Mathf.Sqrt(eField);
//        float clampedStep = Mathf.Clamp(rawStep, stepClamp.x, stepClamp.y);
//        if (isDebug)
//            Log($"StepSizeByEField: eField={eField}, rawStep={rawStep}, clampedStep={clampedStep}");

//        return clampedStep;
//    }

//    //private void SpreadFieldLines(float stepSize)
//    //{
//    //    if (isDebug)
//    //        Log($"SpreadFieldLines started. stepSize={stepSize}");

//    //    ResetAndPoolAllLines();

//    //    if (linePrefab == null)
//    //    {
//    //        if (isDebug)
//    //            Debug.LogError("[SpreadLines] Cannot spread field lines because linePrefab is missing.", this);
//    //        return;
//    //    }

//    //    Vector3 origin = linePrefab.transform.position;

//    //    float snapped = stepSize * Mathf.Floor(halfExtent / stepSize);

//    //    float minX = origin.x - snapped;
//    //    float maxX = origin.x + snapped;
//    //    float minY = origin.y - snapped;
//    //    float maxY = origin.y + snapped;

//    //    CurrentMinX = minX;
//    //    CurrentMaxX = maxX;
//    //    CurrentMinY = minY;
//    //    CurrentMaxY = maxY;
//    //    CurrentZ = origin.z;
//    //    if (isDebug)
//    //    {
//    //        Log($"Field bounds set: minX={CurrentMinX}, maxX={CurrentMaxX}, minY={CurrentMinY}, maxY={CurrentMaxY}, z={CurrentZ}");
//    //        Log($"origin={origin}, halfExtent={halfExtent}, snapped={snapped}");
//    //    }

//    //    int createdCount = 0;

//    //    for (float x = minX; x <= maxX + 0.0001f; x += stepSize)
//    //    {
//    //        for (float y = minY; y <= maxY + 0.0001f; y += stepSize)
//    //        {
//    //            LineRenderer lr = GetFromPool();
//    //            lr.transform.position = new Vector3(x, y, origin.z);
//    //            createdCount++;

//    //            if (debugLineCreation)
//    //                Log($"Line created at x={x}, y={y}, z={origin.z}");
//    //        }
//    //    }

//    //    Log($"SpreadFieldLines finished. active.Count={active.Count}, pool.Count={pool.Count}, createdCount={createdCount}");
//    //}


//    private void SpreadFieldLines(float stepSize)
//    {
//        if (isDebug)
//            Log($"SpreadFieldLines started. stepSize={stepSize}");

//        ResetAndPoolAllLines();

//        if (linePrefab == null)
//        {
//            if (isDebug)
//                Debug.LogError("[SpreadLines] Cannot spread field lines because linePrefab is missing.", this);
//            return;
//        }

//        Vector3 origin = linePrefab.transform.position;

//        // How many even divisions fit in halfExtent. Round so density tracks the
//        // requested step, Max(1) guarantees we always get the -10 / 0 / +10 lines.
//        int divisions = Mathf.Max(1, Mathf.RoundToInt(halfExtent / stepSize));
//        float effectiveStep = halfExtent / divisions;

//        // Bounds are now ALWAYS the true extent, so a line sits exactly on each edge.
//        float minX = origin.x - halfExtent;
//        float maxX = origin.x + halfExtent;
//        float minY = origin.y - halfExtent;
//        float maxY = origin.y + halfExtent;

//        CurrentMinX = minX;
//        CurrentMaxX = maxX;
//        CurrentMinY = minY;
//        CurrentMaxY = maxY;
//        CurrentZ = origin.z;

//        if (isDebug)
//        {
//            Log($"Field bounds set: minX={CurrentMinX}, maxX={CurrentMaxX}, minY={CurrentMinY}, maxY={CurrentMaxY}, z={CurrentZ}");
//            Log($"origin={origin}, halfExtent={halfExtent}, divisions={divisions}, effectiveStep={effectiveStep}");
//        }

//        int createdCount = 0;

//        // Integer indexing from -divisions..+divisions: no float drift, both edges guaranteed.
//        for (int ix = -divisions; ix <= divisions; ix++)
//        {
//            float x = origin.x + ix * effectiveStep;
//            for (int iy = -divisions; iy <= divisions; iy++)
//            {
//                float y = origin.y + iy * effectiveStep;

//                LineRenderer lr = GetFromPool();
//                lr.transform.position = new Vector3(x, y, origin.z);
//                createdCount++;

//                if (debugLineCreation)
//                    Log($"Line created at x={x}, y={y}, z={origin.z}");
//            }
//        }

//        Log($"SpreadFieldLines finished. active.Count={active.Count}, pool.Count={pool.Count}, createdCount={createdCount}");
//    }

//    private LineRenderer GetFromPool()
//    {
//        LineRenderer lr;

//        if (pool.Count > 0)
//        {
//            lr = pool.Dequeue();
//        }
//        else
//        {
//            lr = Instantiate(linePrefab, poolContainer);
//            if (isDebug)
//                Log("Pool was empty, instantiated extra LineRenderer.");
//        }

//        lr.gameObject.SetActive(true);
//        active.Add(lr);

//        return lr;
//    }

//    private void ReturnToPool(LineRenderer lr)
//    {
//        if (lr == null)
//            return;

//        lr.gameObject.SetActive(false);
//        pool.Enqueue(lr);
//    }

//    private void ResetAndPoolAllLines()
//    {
//        int countBefore = active.Count;

//        for (int i = active.Count - 1; i >= 0; i--)
//        {
//            ReturnToPool(active[i]);
//        }

//        active.Clear();
//        if (isDebug)
//            Log($"ResetAndPoolAllLines: returned={countBefore}, active.Count={active.Count}, pool.Count={pool.Count}");
//    }

//    private void OnDestroy()
//    {
//        if (electricFieldSlider != null)
//            electricFieldSlider.onValueChanged.RemoveListener(OnElectricFieldChanged);
//        if (isDebug)
//            Log("OnDestroy: listener removed.");
//    }

//    private void Log(string message)
//    {
//        if (!debugLogs)
//            return;
//        if (isDebug)
//            Debug.Log($"[SpreadLines] {message}", this);
//    }
//}