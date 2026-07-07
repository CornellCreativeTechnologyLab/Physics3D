using System.Collections.Generic;
using UnityEngine;

//[RequireComponent(typeof(FieldModel))]
public class FieldVisualizer : MonoBehaviour
{
    [SerializeField] private Transform fieldPrefab;
    [SerializeField] private Transform container;                 // reference frame; defaults to self
    [SerializeField] private Vector3 prefabForward = Vector3.forward; // which prefab axis is the "tip"

    [Header("Length scaling")]
    [SerializeField] private bool scaleByMagnitude = true;
    [SerializeField] private float referenceMagnitude = 1f;       // magnitude that maps to scale 1
    [SerializeField] private Vector2 lengthClamp = new Vector2(0.25f, 4f);

    private FieldModel model;
    private readonly List<Transform> spawned = new List<Transform>();

    private void Awake()
    {
        model = GetComponent<FieldModel>();
        if (container == null) container = transform;
    }

    private void Start() => Rebuild();

    public void Rebuild()
    {
        Clear();                                  // never accumulate across rebuilds
        if (fieldPrefab == null || model == null) return;

        // Maps the prefab's tip axis onto +Z so LookRotation aims it correctly.
        Quaternion fix = Quaternion.FromToRotation(prefabForward.normalized, Vector3.forward);

        foreach (Vector3 localPos in model.SamplePoints())
        {
            Vector3 localField = model.SampleLocal(localPos);
            float mag = localField.magnitude;
            if (mag < 1e-6f) continue;

            Vector3 worldPos = container.TransformPoint(localPos);
            Vector3 worldDir = container.TransformDirection(localField / mag);

            Transform t = Instantiate(fieldPrefab, worldPos,
                                      Quaternion.LookRotation(worldDir) * fix, container);

            if (scaleByMagnitude)
            {
                float len = Mathf.Clamp(mag / Mathf.Max(referenceMagnitude, 1e-6f),
                                        lengthClamp.x, lengthClamp.y);
                Vector3 s = t.localScale; s.z *= len; t.localScale = s;
            }
            spawned.Add(t);
        }
    }

    public void Clear()
    {
        foreach (Transform t in spawned)
            if (t != null)
            {
                if (Application.isPlaying) Destroy(t.gameObject);
                else DestroyImmediate(t.gameObject);
            }
        spawned.Clear();
    }
}