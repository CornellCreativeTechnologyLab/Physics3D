using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpreadLinesPlanes : MonoBehaviour
{
    public enum PlaneType { XY, YZ, ZX }

    [Header("Orientation")]
    [SerializeField] private PlaneType spreadPlane = PlaneType.XY;

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

    private readonly Queue<LineRenderer> pool = new();
    private readonly List<LineRenderer> active = new();

    private void Awake()
    {
        ValidateRefs();
        InitializePool(initialPoolSize);

        electricFieldSlider.onValueChanged.AddListener(OnElectricFieldChanged);
        OnElectricFieldChanged(electricFieldSlider.value);
    }

    private void ValidateRefs()
    {
        if (!linePrefab) Debug.LogError($"{nameof(SpreadLines)}: Missing linePrefab.", this);
        if (!poolContainer) Debug.LogError($"{nameof(SpreadLines)}: Missing poolContainer.", this);
        if (!electricFieldSlider) Debug.LogError($"{nameof(SpreadLines)}: Missing electricFieldSlider.", this);
    }

    private void InitializePool(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var lr = Instantiate(linePrefab, poolContainer);
            lr.gameObject.SetActive(false);
            pool.Enqueue(lr);
        }
    }

    private void OnElectricFieldChanged(float value)
    {
        bool isEnabled = value > 0f;

        if (visualRoot) visualRoot.SetActive(isEnabled);
        foreach (var obj in toggleWithField) if (obj) obj.SetActive(isEnabled);

        if (alsoTogglePrefabLineRenderer && linePrefab)
            linePrefab.enabled = isEnabled;

        if (!isEnabled)
        {
            ResetAndPoolAllLines();
            return;
        }

        float step = StepSizeByEField(value);
        SpreadFieldLines(step);
    }

    private float StepSizeByEField(float eField)
    {
        float step = stepNumerator / Mathf.Sqrt(eField);
        return Mathf.Clamp(step, stepClamp.x, stepClamp.y);
    }

    private void SpreadFieldLines(float stepSize)
    {
        ResetAndPoolAllLines();

        Vector3 origin = linePrefab.transform.position;
        float snapped = stepSize * Mathf.Floor(halfExtent / stepSize);

        // Loop through the two dimensions of the grid
        for (float i = -snapped; i <= snapped; i += stepSize)
        {
            for (float j = -snapped; j <= snapped; j += stepSize)
            {
                LineRenderer lr = GetFromPool();

                // Map the loop variables (i, j) to the chosen 3D plane
                Vector3 targetPos = Vector3.zero;

                switch (spreadPlane)
                {
                    case PlaneType.XY:
                        targetPos = new Vector3(origin.x + i, origin.y + j, origin.z);
                        break;
                    case PlaneType.YZ:
                        targetPos = new Vector3(origin.x, origin.y + i, origin.z + j);
                        break;
                    case PlaneType.ZX:
                        targetPos = new Vector3(origin.x + j, origin.y, origin.z + i);
                        break;
                }

                lr.transform.position = targetPos;
            }
        }
    }

    private LineRenderer GetFromPool()
    {
        LineRenderer lr = pool.Count > 0 ? pool.Dequeue() : Instantiate(linePrefab, poolContainer);
        lr.gameObject.SetActive(true);
        active.Add(lr);
        return lr;
    }

    private void ReturnToPool(LineRenderer lr)
    {
        lr.gameObject.SetActive(false);
        pool.Enqueue(lr);
    }

    private void ResetAndPoolAllLines()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            ReturnToPool(active[i]);
        }
        active.Clear();
    }

    private void OnDestroy()
    {
        if (electricFieldSlider)
            electricFieldSlider.onValueChanged.RemoveListener(OnElectricFieldChanged);
    }
}