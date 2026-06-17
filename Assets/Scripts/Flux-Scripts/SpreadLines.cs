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

//    [Header("Animation")]
//    [SerializeField] private float animationSpeed = 8f;
//    [SerializeField] private float retireEpsilon = 0.05f;
//    [SerializeField, Tooltip("New interior lines emerge from the center instead of snapping in.")]
//    private bool spawnFromCenter = true;

//    [Header("Debug")]
//    [SerializeField] private bool debugLogs = true;
//    [SerializeField] private bool debugLineCreation = false;

//    private readonly Queue<LineRenderer> pool = new();
//    private readonly List<FieldLine> active = new();
//    private readonly List<Vector3> targetBuffer = new();
//    private Vector3 currentOrigin;

//    public bool HasActiveField { get; private set; }
//    public float CurrentMinX { get; private set; }
//    public float CurrentMaxX { get; private set; }
//    public float CurrentMinY { get; private set; }
//    public float CurrentMaxY { get; private set; }
//    public float CurrentZ { get; private set; }

//    public Vector3 FieldNormal => Vector3.forward;

//    private class FieldLine
//    {
//        public LineRenderer renderer;
//        public Vector3 target;
//        public bool retiring; // true => animate back to center, then pool
//    }

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

//        if (isDebug) Log($"Awake finished. Initial HasActiveField={HasActiveField}");
//    }

//    private void Update()
//    {
//        if (active.Count == 0)
//            return;

//        // Framerate-independent exponential lerp toward each line's target.
//        float t = 1f - Mathf.Exp(-animationSpeed * Time.deltaTime);

//        for (int i = active.Count - 1; i >= 0; i--)
//        {
//            FieldLine fl = active[i];
//            if (fl.renderer == null)
//            {
//                active.RemoveAt(i);
//                continue;
//            }

//            Vector3 cur = fl.renderer.transform.position;
//            Vector3 dst = fl.retiring ? currentOrigin : fl.target;
//            Vector3 next = Vector3.Lerp(cur, dst, t);
//            fl.renderer.transform.position = next;

//            // A retiring line that has reached the center gets pooled.
//            if (fl.retiring && (next - dst).sqrMagnitude <= retireEpsilon * retireEpsilon)
//            {
//                ReturnToPool(fl.renderer);
//                active.RemoveAt(i);
//            }
//        }
//    }

//    private void ValidateRefs()
//    {
//        if (isDebug)
//        {
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
//        if (isDebug) Log($"InitializePool started. count={count}");

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

//    private void SpreadFieldLines(float stepSize)
//    {
//        if (isDebug)
//            Log($"SpreadFieldLines started. stepSize={stepSize}");

//        if (linePrefab == null)
//        {
//            if (isDebug)
//                Debug.LogError("[SpreadLines] Cannot spread field lines because linePrefab is missing.", this);
//            return;
//        }

//        Vector3 origin = linePrefab.transform.position;
//        currentOrigin = origin;

//        // Even divisions of halfExtent => a line always lands exactly on +/-halfExtent.
//        int divisions = Mathf.Max(1, Mathf.RoundToInt(halfExtent / stepSize));
//        float effectiveStep = halfExtent / divisions;

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

//        // Build target positions OUTERMOST RING FIRST. Index order is what keeps the
//        // boundary frame assigned to long-lived lines (so the edges never empty),
//        // while the center ring lands at the tail (where lines spawn / retire).
//        targetBuffer.Clear();
//        for (int r = divisions; r >= 1; r--)
//        {
//            for (int ix = -r; ix <= r; ix++)
//            {
//                for (int iy = -r; iy <= r; iy++)
//                {
//                    // Only the points on ring r (Chebyshev distance == r).
//                    if (Mathf.Max(Mathf.Abs(ix), Mathf.Abs(iy)) != r)
//                        continue;

//                    targetBuffer.Add(new Vector3(
//                        origin.x + ix * effectiveStep,
//                        origin.y + iy * effectiveStep,
//                        origin.z));
//                }
//            }
//        }
//        targetBuffer.Add(origin); // center point last

//        AssignTargets(origin);

//        Log($"SpreadFieldLines finished. active.Count={active.Count}, pool.Count={pool.Count}, targets={targetBuffer.Count}");
//    }

//    private void AssignTargets(Vector3 spawnCenter)
//    {
//        int targetCount = targetBuffer.Count;

//        // Reuse / spawn for the first targetCount slots.
//        for (int i = 0; i < targetCount; i++)
//        {
//            if (i < active.Count)
//            {
//                // Reused line: revive if it was retiring, give it a new target.
//                active[i].target = targetBuffer[i];
//                active[i].retiring = false;
//            }
//            else
//            {
//                LineRenderer lr = GetFromPool();
//                lr.transform.position = spawnFromCenter ? spawnCenter : targetBuffer[i];
//                active.Add(new FieldLine
//                {
//                    renderer = lr,
//                    target = targetBuffer[i],
//                    retiring = false
//                });

//                if (debugLineCreation)
//                    Log($"Line spawned -> target={targetBuffer[i]}");
//            }
//        }

//        // Surplus lines (the innermost rings) collapse back into the center, then pool.
//        for (int i = targetCount; i < active.Count; i++)
//            active[i].retiring = true;
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
//            ReturnToPool(active[i].renderer);

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


using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpreadLines : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool isDebug = true;

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
    [SerializeField] private int initialPoolSize = 200;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private bool debugLineCreation = false;

    private readonly Queue<LineRenderer> pool = new();
    private readonly List<LineRenderer> active = new();

    public bool HasActiveField { get; private set; }
    public float CurrentMinX { get; private set; }
    public float CurrentMaxX { get; private set; }
    public float CurrentMinY { get; private set; }
    public float CurrentMaxY { get; private set; }
    public float CurrentZ { get; private set; }

    public Vector3 FieldNormal => Vector3.forward;

    private void Awake()
    {
        Log("Awake started.");

        ValidateRefs();
        if (isDebug)
        {

            if (linePrefab == null || poolContainer == null)
            {
                Debug.LogError("[SpreadLines] Cannot initialize pool because linePrefab or poolContainer is missing.", this);
                return;
            }
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

        if (isDebug) Log($"Awake finished. Initial HasActiveField={HasActiveField}");
    }

    private void ValidateRefs()
    {
        if (isDebug)
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

    }

    private void InitializePool(int count)
    {
        if (isDebug) Log($"InitializePool started. count={count}");

        for (int i = 0; i < count; i++)
        {
            LineRenderer lr = Instantiate(linePrefab, poolContainer);
            lr.gameObject.SetActive(false);
            pool.Enqueue(lr);
        }
        if (isDebug)
            Log($"InitializePool finished. pool.Count={pool.Count}");
    }

    private void OnElectricFieldChanged(float value)
    {
        bool enabled = value > 0f;
        HasActiveField = enabled;
        if (isDebug)
            Log($"OnElectricFieldChanged: sliderValue={value}, HasActiveField={HasActiveField}");

        if (visualRoot != null)
            visualRoot.SetActive(enabled);

        for (int i = 0; i < toggleWithField.Count; i++)
        {
            if (toggleWithField[i] != null)
                toggleWithField[i].SetActive(enabled);
        }

        if (alsoTogglePrefabLineRenderer && linePrefab != null)
            linePrefab.enabled = enabled;

        if (!enabled)
        {
            if (isDebug)
                Log("Electric field value is 0, so clearing all active field lines.");
            ResetAndPoolAllLines();

            CurrentMinX = 0f;
            CurrentMaxX = 0f;
            CurrentMinY = 0f;
            CurrentMaxY = 0f;
            CurrentZ = 0f;

            return;
        }

        float step = StepSizeByEField(value);
        if (isDebug)
            Log($"Calculated step size from field. value={value}, step={step}");

        SpreadFieldLines(step);
    }

    private float StepSizeByEField(float eField)
    {
        if (eField <= 0f)
        {
            if (isDebug)
                Log($"StepSizeByEField received eField <= 0. eField={eField}");
            return stepClamp.y;
        }

        float rawStep = stepNumerator / Mathf.Sqrt(eField);
        float clampedStep = Mathf.Clamp(rawStep, stepClamp.x, stepClamp.y);
        if (isDebug)
            Log($"StepSizeByEField: eField={eField}, rawStep={rawStep}, clampedStep={clampedStep}");

        return clampedStep;
    }

    //private void SpreadFieldLines(float stepSize)
    //{
    //    if (isDebug)
    //        Log($"SpreadFieldLines started. stepSize={stepSize}");

    //    ResetAndPoolAllLines();

    //    if (linePrefab == null)
    //    {
    //        if (isDebug)
    //            Debug.LogError("[SpreadLines] Cannot spread field lines because linePrefab is missing.", this);
    //        return;
    //    }

    //    Vector3 origin = linePrefab.transform.position;

    //    float snapped = stepSize * Mathf.Floor(halfExtent / stepSize);

    //    float minX = origin.x - snapped;
    //    float maxX = origin.x + snapped;
    //    float minY = origin.y - snapped;
    //    float maxY = origin.y + snapped;

    //    CurrentMinX = minX;
    //    CurrentMaxX = maxX;
    //    CurrentMinY = minY;
    //    CurrentMaxY = maxY;
    //    CurrentZ = origin.z;
    //    if (isDebug)
    //    {
    //        Log($"Field bounds set: minX={CurrentMinX}, maxX={CurrentMaxX}, minY={CurrentMinY}, maxY={CurrentMaxY}, z={CurrentZ}");
    //        Log($"origin={origin}, halfExtent={halfExtent}, snapped={snapped}");
    //    }

    //    int createdCount = 0;

    //    for (float x = minX; x <= maxX + 0.0001f; x += stepSize)
    //    {
    //        for (float y = minY; y <= maxY + 0.0001f; y += stepSize)
    //        {
    //            LineRenderer lr = GetFromPool();
    //            lr.transform.position = new Vector3(x, y, origin.z);
    //            createdCount++;

    //            if (debugLineCreation)
    //                Log($"Line created at x={x}, y={y}, z={origin.z}");
    //        }
    //    }

    //    Log($"SpreadFieldLines finished. active.Count={active.Count}, pool.Count={pool.Count}, createdCount={createdCount}");
    //}


    private void SpreadFieldLines(float stepSize)
    {
        if (isDebug)
            Log($"SpreadFieldLines started. stepSize={stepSize}");

        ResetAndPoolAllLines();

        if (linePrefab == null)
        {
            if (isDebug)
                Debug.LogError("[SpreadLines] Cannot spread field lines because linePrefab is missing.", this);
            return;
        }

        Vector3 origin = linePrefab.transform.position;

        // How many even divisions fit in halfExtent. Round so density tracks the
        // requested step, Max(1) guarantees we always get the -10 / 0 / +10 lines.
        int divisions = Mathf.Max(1, Mathf.RoundToInt(halfExtent / stepSize));
        float effectiveStep = halfExtent / divisions;

        // Bounds are now ALWAYS the true extent, so a line sits exactly on each edge.
        float minX = origin.x - halfExtent;
        float maxX = origin.x + halfExtent;
        float minY = origin.y - halfExtent;
        float maxY = origin.y + halfExtent;

        CurrentMinX = minX;
        CurrentMaxX = maxX;
        CurrentMinY = minY;
        CurrentMaxY = maxY;
        CurrentZ = origin.z;

        if (isDebug)
        {
            Log($"Field bounds set: minX={CurrentMinX}, maxX={CurrentMaxX}, minY={CurrentMinY}, maxY={CurrentMaxY}, z={CurrentZ}");
            Log($"origin={origin}, halfExtent={halfExtent}, divisions={divisions}, effectiveStep={effectiveStep}");
        }

        int createdCount = 0;

        // Integer indexing from -divisions..+divisions: no float drift, both edges guaranteed.
        for (int ix = -divisions; ix <= divisions; ix++)
        {
            float x = origin.x + ix * effectiveStep;
            for (int iy = -divisions; iy <= divisions; iy++)
            {
                float y = origin.y + iy * effectiveStep;

                LineRenderer lr = GetFromPool();
                lr.transform.position = new Vector3(x, y, origin.z);
                createdCount++;

                if (debugLineCreation)
                    Log($"Line created at x={x}, y={y}, z={origin.z}");
            }
        }

        Log($"SpreadFieldLines finished. active.Count={active.Count}, pool.Count={pool.Count}, createdCount={createdCount}");
    }

    private LineRenderer GetFromPool()
    {
        LineRenderer lr;

        if (pool.Count > 0)
        {
            lr = pool.Dequeue();
        }
        else
        {
            lr = Instantiate(linePrefab, poolContainer);
            if (isDebug)
                Log("Pool was empty, instantiated extra LineRenderer.");
        }

        lr.gameObject.SetActive(true);
        active.Add(lr);

        return lr;
    }

    private void ReturnToPool(LineRenderer lr)
    {
        if (lr == null)
            return;

        lr.gameObject.SetActive(false);
        pool.Enqueue(lr);
    }

    private void ResetAndPoolAllLines()
    {
        int countBefore = active.Count;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            ReturnToPool(active[i]);
        }

        active.Clear();
        if (isDebug)
            Log($"ResetAndPoolAllLines: returned={countBefore}, active.Count={active.Count}, pool.Count={pool.Count}");
    }

    private void OnDestroy()
    {
        if (electricFieldSlider != null)
            electricFieldSlider.onValueChanged.RemoveListener(OnElectricFieldChanged);
        if (isDebug)
            Log("OnDestroy: listener removed.");
    }

    private void Log(string message)
    {
        if (!debugLogs)
            return;
        if (isDebug)
            Debug.Log($"[SpreadLines] {message}", this);
    }
}