using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Faraday2 : MonoBehaviour
{
    [Header("UI - Field")]
    [SerializeField] private TMP_InputField magneticFieldInput;
    [SerializeField] private Slider magneticFieldSlider;

    [Header("UI - Shape")]
    [SerializeField] private TMP_InputField polygonSidesInput;
    [SerializeField] private Slider polygonSidesSlider;

    [Header("UI - Motion")]
    [SerializeField] private Button playButton;

    [Header("UI - Output")]
    [SerializeField] private TMP_Text fluxText;
    [SerializeField] private TMP_Text emfText;
    [SerializeField] private TMP_Text currentText;

    [Header("Physics")]
    [SerializeField] private float magneticField = 1f;
    [SerializeField] private float area = 1f;
    [SerializeField] private float resistance = 10f;
    [SerializeField] private int polygonSides = 4;
    [SerializeField] private float emfNoiseThreshold = 0.001f;

    [Header("Visual")]
    [SerializeField] private PolygonLoopRenderer loopRenderer;
    [SerializeField] private float visualScale = 1f;
    [SerializeField] private LoopChargeWalker loopChargeWalker;

    [Header("Magnetic Field Area")]
    [SerializeField] private SpreadLines fieldLines;

    [Header("Loop Motion")]
    [SerializeField] private Transform leftPoint;
    [SerializeField] private Transform rightPoint;
    [SerializeField] private float moveSpeed = 0.1f;
    [SerializeField] private bool pingPong = true;
    [SerializeField] private Slider oscSlider;

    [Header("Loop Orientation")]
    [SerializeField] private Vector3 loopRotationOffsetEuler = new Vector3(90f, 0f, 0f);

    [Header("Overlap Visual")]
    [SerializeField] private MeshFilter overlapMeshFilter;
    [SerializeField] private MeshRenderer overlapMeshRenderer;
    //[SerializeField] private Material overlapMaterial;
    [SerializeField] private float overlapVisualZOffset = -0.01f;

    [Header("Debug")]
    [SerializeField] private bool isDebug = false;
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private bool debugEveryFrame = false;
    [SerializeField] private int debugEveryNFrames = 30;
    [SerializeField] private bool debugPolygonPoints = false;

    [Header("Cached Area Visuals")]
    [SerializeField] private Transform areaVisualCacheRoot;
    [SerializeField] private Material nonOverlapAreaMaterial;
    [SerializeField] private Material overlapAreaMaterial;
    [SerializeField] private float areaVisualLocalZOffset = -0.01f;
    [SerializeField] private bool useCachedAreaVisuals = true;

    private readonly Dictionary<int, AreaVisualPair> areaVisualPairs = new();

    private class AreaVisualPair
    {
        public GameObject root;
        public GameObject nonOverlapObject;
        public GameObject overlapObject;

        public MeshFilter nonOverlapFilter;
        public MeshFilter overlapFilter;

        public MeshRenderer nonOverlapRenderer;
        public MeshRenderer overlapRenderer;

        public Mesh nonOverlapMesh;
        public Mesh overlapMesh;
    }

    private bool isPlaying;
    private float previousFlux;
    private float motionTime;

    private void Start()
    {
        if (isDebug) Log("Start called.");

        ValidateRefs();

        SetupSliders();
        SetupListeners();
        SyncUI();

        UpdateVisual();
        InitializeCachedAreaVisuals();
        UpdateCachedAreaVisuals();

        previousFlux = CalculateFlux("Start initial previousFlux");
        FaradayLawVariables.Flux = previousFlux;
        FaradayLawVariables.EMF = 0f;
        FaradayLawVariables.Current = 0f;

        UpdateOutput(previousFlux, 0f);

        if (isDebug) Log($"Start finished. previousFlux={previousFlux}");
        
    }

    private void Update()
    {
        if (isPlaying)
            motionTime += Time.deltaTime * moveSpeed;

        UpdateVisual();
        UpdateCachedAreaVisuals();

        float flux = CalculateFlux("Update");

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        float deltaFlux = flux - previousFlux;
        float emf = -deltaFlux / deltaTime;

        if (Mathf.Abs(emf) < emfNoiseThreshold)
            emf = 0f;

        previousFlux = flux;

        float current = resistance == 0f ? 0f : emf / resistance;

        FaradayLawVariables.Flux = flux;
        FaradayLawVariables.EMF = emf;
        FaradayLawVariables.Current = current;

        loopChargeWalker?.SetCurrent(current);

        UpdateOutput(flux, emf);

        if (ShouldLogThisFrame())
        {
            if (isDebug)
            {
                Log(
                    $"Update values: " +
                    $"isPlaying={isPlaying}, " +
                    $"motionTime={motionTime}, " +
                    $"Time.deltaTime={Time.deltaTime}, " +
                    $"deltaTimeUsed={deltaTime}, " +
                    $"flux={flux}, " +
                    $"previousFluxNow={previousFlux}, " +
                    $"deltaFlux={deltaFlux}, " +
                    $"emf={emf}, " +
                    $"resistance={resistance}, " +
                    $"current={current}"
                );
            }
        }
    }

    private void ValidateRefs()
    {
        if (magneticFieldInput == null)
            if (isDebug) Log("magneticFieldInput is missing.");

        if (magneticFieldSlider == null)
            if (isDebug) Log("magneticFieldSlider is missing.");

        if (polygonSidesInput == null)
            if (isDebug) Log("polygonSidesInput is missing.");

        if (polygonSidesSlider == null)
            if (isDebug) Log("polygonSidesSlider is missing.");

        if (playButton == null)
            if (isDebug) Log("playButton is missing.");

        if (fluxText == null)
            if (isDebug) Log("fluxText is missing.");

        if (emfText == null)
            if (isDebug) Log("emfText is missing.");

        if (currentText == null)
            if (isDebug) Log("currentText is missing.");

        if (loopRenderer == null)
            if (isDebug) Debug.LogError("[Faraday2] loopRenderer is missing. Flux will be 0.", this);

        if (fieldLines == null)
            if (isDebug) Debug.LogError("[Faraday2] fieldLines is missing. Flux will be 0.", this);

        if (overlapMeshFilter == null)
            if (isDebug) Log("overlapMeshFilter is missing. Overlap visual will not show.");

        if (overlapMeshRenderer == null)
            if (isDebug) Log("overlapMeshRenderer is missing. Overlap material will not be applied.");

        //if (overlapMaterial == null)
        //    if (isDebug) Log("overlapMaterial is missing. Overlap visual may still exist but material will not be assigned.");
    }

    private void InitializeCachedAreaVisuals()
    {
        if (!useCachedAreaVisuals)
            return;

        if (loopRenderer == null)
        {
            Debug.LogError("[Faraday2] Cannot initialize cached area visuals because loopRenderer is missing.", this);
            return;
        }

        if (areaVisualCacheRoot == null)
        {
            GameObject root = new GameObject("AreaVisualCache");
            areaVisualCacheRoot = root.transform;
        }

        areaVisualCacheRoot.SetParent(loopRenderer.transform, false);
        areaVisualCacheRoot.localPosition = Vector3.zero;
        areaVisualCacheRoot.localRotation = Quaternion.identity;
        areaVisualCacheRoot.localScale = Vector3.one;

        int minSides = 3;
        int maxSides = 12;

        if (polygonSidesSlider != null)
        {
            minSides = Mathf.RoundToInt(polygonSidesSlider.minValue);
            maxSides = Mathf.RoundToInt(polygonSidesSlider.maxValue);
        }

        foreach (Transform child in areaVisualCacheRoot)
        {
            Destroy(child.gameObject);
        }

        areaVisualPairs.Clear();

        int originalSides = polygonSides;

        for (int sides = minSides; sides <= maxSides; sides++)
        {
            AreaVisualPair pair = CreateAreaVisualPair(sides);
            areaVisualPairs.Add(sides, pair);
        }

        polygonSides = originalSides;

        SetActiveAreaVisualPair(polygonSides);

        Debug.Log(
            $"[Faraday2] Cached area visuals initialized. " +
            $"Count={areaVisualPairs.Count}, " +
            $"cacheRootParent={(areaVisualCacheRoot.parent ? areaVisualCacheRoot.parent.name : "NULL")}, " +
            $"loopRenderer={loopRenderer.name}",
            this
        );
    }

    private AreaVisualPair CreateAreaVisualPair(int sides)
    {
        GameObject pairRoot = new GameObject($"Polygon_{sides}_AreaVisuals");
        pairRoot.transform.SetParent(areaVisualCacheRoot, false);

        //GameObject nonOverlapObject = new GameObject("NonOverlapArea");
        //nonOverlapObject.transform.SetParent(pairRoot.transform, false);

        GameObject overlapObject = new GameObject("OverlapArea");
        overlapObject.transform.SetParent(pairRoot.transform, false);

        //nonOverlapObject.transform.localPosition = Vector3.zero;
        //nonOverlapObject.transform.localRotation = Quaternion.identity;
        //nonOverlapObject.transform.localScale = Vector3.one;

        overlapObject.transform.localPosition = Vector3.zero;
        overlapObject.transform.localRotation = Quaternion.identity;
        overlapObject.transform.localScale = Vector3.one;

        //MeshFilter nonOverlapFilter = nonOverlapObject.AddComponent<MeshFilter>();
        //MeshRenderer nonOverlapRenderer = nonOverlapObject.AddComponent<MeshRenderer>();

        MeshFilter overlapFilter = overlapObject.AddComponent<MeshFilter>();
        MeshRenderer overlapRenderer = overlapObject.AddComponent<MeshRenderer>();

        //Mesh nonOverlapMesh = new Mesh();
        //nonOverlapMesh.name = $"NonOverlapArea_{sides}";

        Mesh overlapMesh = new Mesh();
        overlapMesh.name = $"OverlapArea_{sides}";

        //nonOverlapFilter.sharedMesh = nonOverlapMesh;
        overlapFilter.sharedMesh = overlapMesh;

        //if (nonOverlapAreaMaterial != null)
        //    nonOverlapRenderer.sharedMaterial = nonOverlapAreaMaterial;

        if (overlapAreaMaterial != null)
            overlapRenderer.sharedMaterial = overlapAreaMaterial;

        //if (nonOverlapRenderer.sharedMaterial != null)
        //    nonOverlapRenderer.sharedMaterial.renderQueue = 3000;

        //if (overlapRenderer.sharedMaterial != null)
        //    overlapRenderer.sharedMaterial.renderQueue = 3100;

        //float radius = Mathf.Sqrt(area / Mathf.PI) * visualScale;
        //Vector3[] polygonVertices = GeneratePolygonVerticesLocal(sides, radius);
        //BuildFanMesh(nonOverlapMesh, polygonVertices);

        overlapMesh.Clear();

        pairRoot.SetActive(false);

        return new AreaVisualPair
        {
            root = pairRoot,
            //nonOverlapObject = nonOverlapObject,
            overlapObject = overlapObject,

            //nonOverlapFilter = nonOverlapFilter,
            overlapFilter = overlapFilter,

            //nonOverlapRenderer = nonOverlapRenderer,
            overlapRenderer = overlapRenderer,

            //nonOverlapMesh = nonOverlapMesh,
            overlapMesh = overlapMesh
        };

        //nonOverlapRenderer.sortingOrder = 0;
        overlapRenderer.sortingOrder = 100;

        //if (nonOverlapRenderer.sharedMaterial != null)
        //{
        //    nonOverlapRenderer.sharedMaterial.renderQueue = 3000;
        //}

        if (overlapRenderer.sharedMaterial != null)
        {
            overlapRenderer.sharedMaterial.renderQueue = 3100;
        }
    }

    private Vector3[] GeneratePolygonVerticesLocal(int sides, float radius)
    {
        Vector3[] generated = new Vector3[sides];

        for (int i = 0; i < sides; i++)
        {
            float angle = Mathf.PI * 2f * i / sides;

            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;

            // Match PolygonLoopRenderer's plane: XZ, with Y as the small offset layer.
            generated[i] = new Vector3(x, areaVisualLocalZOffset, z);
        }

        return generated;
    }

    private void BuildFanMesh(Mesh mesh, Vector3[] outerVertices)
    {
        if (mesh == null || outerVertices == null || outerVertices.Length < 3)
            return;

        int vertexCount = outerVertices.Length + 1;

        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[outerVertices.Length * 6];

        Vector3 center = Vector3.zero;

        for (int i = 0; i < outerVertices.Length; i++)
        {
            center += outerVertices[i];
        }

        center /= outerVertices.Length;

        vertices[0] = center;

        for (int i = 0; i < outerVertices.Length; i++)
        {
            vertices[i + 1] = outerVertices[i];
        }

        for (int i = 0; i < outerVertices.Length; i++)
        {
            int next = (i + 1) % outerVertices.Length;
            int index = i * 6;

            // Front face
            triangles[index]     = 0;
            triangles[index + 1] = i + 1;
            triangles[index + 2] = next + 1;

            // Back face
            triangles[index + 3] = 0;
            triangles[index + 4] = next + 1;
            triangles[index + 5] = i + 1;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void UpdateCachedAreaVisuals()
    {
        if (!useCachedAreaVisuals)
            return;

        if (loopRenderer == null || fieldLines == null)
            return;

        if (!areaVisualPairs.TryGetValue(polygonSides, out AreaVisualPair pair))
            return;

        if (!fieldLines.HasActiveField)
        {
            pair.overlapMesh.Clear();
            return;
        }

        Vector2[] loopPoints = GetLoopPointsXY();
        Vector2[] fieldPoints = GetFieldPointsFromSpreadLines();

        List<Vector2> overlapPoints = PolygonOverlap2D.GetIntersectionPolygon(
            loopPoints,
            fieldPoints
        );

        if (overlapPoints == null || overlapPoints.Count < 3)
        {
            pair.overlapMesh.Clear();
            return;
        }

        Vector3[] localOverlapVertices = new Vector3[overlapPoints.Count];

        for (int i = 0; i < overlapPoints.Count; i++)
        {
            Vector3 worldPoint = new Vector3(
                overlapPoints[i].x,
                overlapPoints[i].y,
                fieldLines.CurrentZ
            );

            Vector3 localPoint = loopRenderer.transform.InverseTransformPoint(worldPoint);

            // PolygonLoopRenderer uses local XZ as the polygon plane.
            // So Y is the layer offset, not Z.
            localPoint.y = areaVisualLocalZOffset + 0.05f;

            localOverlapVertices[i] = localPoint;
        }

        //pair.nonOverlapObject.SetActive(false);
        pair.overlapObject.SetActive(true);

        BuildFanMesh(pair.overlapMesh, localOverlapVertices);

        Debug.Log(
    $"[AREA VISUAL CHECK] " +
    $"polygonSides={polygonSides}, " +
    $"overlapPoints={overlapPoints.Count}, " +
    $"overlapArea={PolygonOverlap2D.GetOverlapArea(loopPoints, fieldPoints)}, " +
    $"overlapMeshVerts={pair.overlapMesh.vertexCount}, " +
    $"overlapMeshTris={pair.overlapMesh.triangles.Length / 3}, " +
    $"pairRootActive={pair.root.activeSelf}, " +
    $"overlapActive={pair.overlapObject.activeSelf}, " +
    $"overlapMat={(pair.overlapRenderer.sharedMaterial ? pair.overlapRenderer.sharedMaterial.name : "NULL")}, " +
    $"loopPos={loopRenderer.transform.position}, " +
    $"cacheRootParent={(areaVisualCacheRoot.parent ? areaVisualCacheRoot.parent.name : "NULL")}, " +
    $"loopRendererName={loopRenderer.name}, " +
    $"fieldMin=({fieldLines.CurrentMinX}, {fieldLines.CurrentMinY}), " +
    $"fieldMax=({fieldLines.CurrentMaxX}, {fieldLines.CurrentMaxY})"
);
    }
    private void SetupSliders()
    {
        if (magneticFieldSlider != null)
        {
            magneticFieldSlider.minValue = 0f;
            magneticFieldSlider.maxValue = 5f;
            magneticFieldSlider.value = magneticField;

            if (isDebug) Log($"Magnetic field slider setup: min={magneticFieldSlider.minValue}, max={magneticFieldSlider.maxValue}, value={magneticFieldSlider.value}");
        }

        if (polygonSidesSlider != null)
        {
            polygonSidesSlider.minValue = 3f;
            polygonSidesSlider.maxValue = 12f;
            polygonSidesSlider.wholeNumbers = true;
            polygonSidesSlider.value = polygonSides;

            if (isDebug) Log($"Polygon sides slider setup: min={polygonSidesSlider.minValue}, max={polygonSidesSlider.maxValue}, value={polygonSidesSlider.value}");
        }
        
        if(oscSlider != null)
        {
            oscSlider.minValue = 0f;
            oscSlider.maxValue = 1f;
            oscSlider.value = moveSpeed;

            if (isDebug) Log($"Osc Slider setup done!");
        }
    }

    private void SetupListeners()
    {
        if (playButton != null)
            playButton.onClick.AddListener(TogglePlay);

        if (oscSlider != null)
        {
            oscSlider.onValueChanged.AddListener(v =>

            moveSpeed = v

            );
        }

        if (magneticFieldSlider != null)
        {
            magneticFieldSlider.onValueChanged.AddListener(v =>
            {
                magneticField = v;

                if (isDebug) Log($"Magnetic field slider changed: magneticField={magneticField}");

                if (magneticFieldInput != null)
                    magneticFieldInput.SetTextWithoutNotify(v.ToString("F2"));

                ResetFlux("magnetic field slider changed");
            });
        }

        if (polygonSidesSlider != null)
        {
            polygonSidesSlider.onValueChanged.AddListener(v =>
            {
                polygonSides = Mathf.RoundToInt(v);

                SetActiveAreaVisualPair(polygonSides);

                if (isDebug) Log($"Polygon sides slider changed: polygonSides={polygonSides}");

                if (polygonSidesInput != null)
                    polygonSidesInput.SetTextWithoutNotify(polygonSides.ToString());

                ResetFlux("polygon sides slider changed");
            });
        }

        BindInput(magneticFieldInput, v =>
        {
            magneticField = Mathf.Clamp(v, 0f, 5f);

            if (isDebug) Log($"Magnetic field input changed: raw={v}, clamped={magneticField}");

            if (magneticFieldSlider != null)
                magneticFieldSlider.SetValueWithoutNotify(magneticField);

            ResetFlux("magnetic field input changed");
        });

        BindInput(polygonSidesInput, v =>
        {
            polygonSides = Mathf.Clamp(Mathf.RoundToInt(v), 3, 12);
            SetActiveAreaVisualPair(polygonSides);

            if (isDebug) Log($"Polygon sides input changed: raw={v}, clamped={polygonSides}");

            if (polygonSidesSlider != null)
                polygonSidesSlider.SetValueWithoutNotify(polygonSides);

            ResetFlux("polygon sides input changed");
        });
    }

    private void BindInput(TMP_InputField input, System.Action<float> setter)
    {
        if (input == null)
            return;

        input.onEndEdit.AddListener(text =>
        {
            if (isDebug) Log($"Input edited: object={input.name}, text={text}");

            if (!float.TryParse(text, out float value))
            {
                if (isDebug) Log($"Input parse failed: object={input.name}, text={text}");
                return;
            }

            setter(value);
            SyncUI();
        });
    }

    private void TogglePlay()
    {
        isPlaying = !isPlaying;

        if (isDebug) Log($"TogglePlay called. isPlaying={isPlaying}");

        TMP_Text label = playButton != null ? playButton.GetComponentInChildren<TMP_Text>() : null;

        if (label != null)
            label.text = isPlaying ? "Pause" : "Play";

        ResetFlux("play toggled");
    }

    private float CalculateFlux(string source)
    {
        if (loopRenderer == null)
        {
            LogFluxZero(source, "loopRenderer is null");
            return 0f;
        }

        if (loopRenderer.Vertices == null)
        {
            LogFluxZero(source, "loopRenderer.Vertices is null");
            return 0f;
        }

        if (loopRenderer.Vertices.Length < 3)
        {
            LogFluxZero(source, $"loopRenderer.Vertices has fewer than 3 points. Length={loopRenderer.Vertices.Length}");
            return 0f;
        }

        if (fieldLines == null)
        {
            LogFluxZero(source, "fieldLines is null");
            return 0f;
        }

        if (!fieldLines.HasActiveField)
        {
            LogFluxZero(source, "fieldLines.HasActiveField is false. This usually means the SpreadLines electricFieldSlider is 0.");
            return 0f;
        }

        Vector2[] loopPoints = GetLoopPointsXY();
        Vector2[] fieldPoints = GetFieldPointsFromSpreadLines();

        float overlapArea = PolygonOverlap2D.GetOverlapArea(loopPoints, fieldPoints);

        Vector3 loopAreaVector = loopRenderer.transform.up.normalized;
        Vector3 fieldDirection = fieldLines.FieldNormal.normalized;

        float rawDot = Vector3.Dot(loopAreaVector, fieldDirection);
        float cosTheta = Mathf.Abs(rawDot);

        float flux = magneticField * overlapArea * cosTheta;

        if (ShouldLogThisFrame())
        {
            if (isDebug)
            {
                Log(
                    $"CalculateFlux from {source}: " +
                    $"magneticField={magneticField}, " +
                    $"overlapArea={overlapArea}, " +
                    $"loopAreaVector={loopAreaVector}, " +
                    $"fieldDirection={fieldDirection}, " +
                    $"rawDot={rawDot}, " +
                    $"cosThetaAbs={cosTheta}, " +
                    $"flux={flux}"
                );

                Log(
                    $"Field bounds: " +
                    $"minX={fieldLines.CurrentMinX}, " +
                    $"maxX={fieldLines.CurrentMaxX}, " +
                    $"minY={fieldLines.CurrentMinY}, " +
                    $"maxY={fieldLines.CurrentMaxY}, " +
                    $"z={fieldLines.CurrentZ}"
                );

                Log(
                    $"Loop transform: " +
                    $"position={loopRenderer.transform.position}, " +
                    $"rotationEuler={loopRenderer.transform.rotation.eulerAngles}, " +
                    $"up={loopRenderer.transform.up}"
                );

                if (debugPolygonPoints)
                {
                    LogPoints("Loop points", loopPoints);
                    LogPoints("Field points", fieldPoints);
                }
            }
        }

        return flux;
    }

    private void UpdateVisual()
    {
        if (loopRenderer == null)
            return;

        float radius = Mathf.Sqrt(area / Mathf.PI) * visualScale;

        loopRenderer.SetPolygon(polygonSides, radius);

        if (leftPoint != null && rightPoint != null)
        {
            float t = pingPong
                ? Mathf.PingPong(motionTime, 1f)
                : Mathf.Clamp01(motionTime);

            Vector3 newPosition = Vector3.Lerp(
                leftPoint.position,
                rightPoint.position,
                t
            );

            loopRenderer.transform.position = newPosition;

            if (ShouldLogThisFrame())
            {
                if (isDebug)
                {
                    Log(
                        $"UpdateVisual motion: " +
                        $"left={leftPoint.position}, " +
                        $"right={rightPoint.position}, " +
                        $"t={t}, " +
                        $"newPosition={newPosition}"
                    );
                }
            }
        }
        else
        {
            if (ShouldLogThisFrame())
                if (isDebug) Log("UpdateVisual: leftPoint or rightPoint missing, loop position is not being moved.");
        }

        loopRenderer.transform.rotation = Quaternion.Euler(loopRotationOffsetEuler);

        if (loopChargeWalker != null && loopRenderer.Vertices != null)
            loopChargeWalker.SetWaypoints(loopRenderer.Vertices);

        if (ShouldLogThisFrame())
        {
            if (isDebug)
            {
                Log(
                    $"UpdateVisual shape: " +
                    $"area={area}, " +
                    $"visualScale={visualScale}, " +
                    $"radius={radius}, " +
                    $"polygonSides={polygonSides}, " +
                    $"loopRotationOffsetEuler={loopRotationOffsetEuler}"
                );
            }
        }
    }

    private void UpdateOverlapVisual()
    {
        if (overlapMeshFilter == null || loopRenderer == null || fieldLines == null)
            return;

        if (!fieldLines.HasActiveField)
        {
            overlapMeshFilter.mesh = null;

            if (ShouldLogThisFrame())
                if (isDebug) Log("UpdateOverlapVisual: field inactive, clearing overlap mesh.");

            return;
        }

        Vector2[] loopPoints = GetLoopPointsXY();
        Vector2[] fieldPoints = GetFieldPointsFromSpreadLines();

        List<Vector2> overlapPoints = PolygonOverlap2D.GetIntersectionPolygon(
            loopPoints,
            fieldPoints
        );

        if (overlapPoints == null || overlapPoints.Count < 3)
        {
            overlapMeshFilter.mesh = null;

            if (ShouldLogThisFrame())
                if (isDebug) Log($"UpdateOverlapVisual: no valid overlap polygon. overlapPointsCount={(overlapPoints == null ? -1 : overlapPoints.Count)}");

            return;
        }

        Vector3[] vertices = new Vector3[overlapPoints.Count + 1];
        int[] triangles = new int[overlapPoints.Count * 3];

        Vector3 center = Vector3.zero;

        for (int i = 0; i < overlapPoints.Count; i++)
        {
            Vector3 worldPoint = new Vector3(
                overlapPoints[i].x,
                overlapPoints[i].y,
                fieldLines.CurrentZ + overlapVisualZOffset
            );

            vertices[i + 1] = worldPoint;
            center += worldPoint;
        }

        center /= overlapPoints.Count;
        vertices[0] = center;

        for (int i = 0; i < overlapPoints.Count; i++)
        {
            int next = (i + 1) % overlapPoints.Count;

            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = next + 1;
        }

        Mesh mesh = new Mesh();
        mesh.name = "Overlap Area Mesh";
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        overlapMeshFilter.mesh = mesh;

        //if (overlapMeshRenderer != null && overlapMaterial != null)
        //    overlapMeshRenderer.material = overlapMaterial;

        if (ShouldLogThisFrame())
        {
            if (isDebug)
            {
                Log(
                    $"UpdateOverlapVisual: overlap mesh created. " +
                    $"overlapPoints={overlapPoints.Count}, " +
                    $"vertices={vertices.Length}, " +
                    $"triangles={triangles.Length / 3}"
                );
            }
        }
    }

    private Vector2[] GetLoopPointsXY()
    {
        Vector3[] local = loopRenderer.Vertices;
        Vector2[] points = new Vector2[local.Length];

        for (int i = 0; i < local.Length; i++)
        {
            Vector3 worldPoint = loopRenderer.transform.TransformPoint(local[i]);
            points[i] = new Vector2(worldPoint.x, worldPoint.y);
        }

        return points;
    }

    private Vector2[] GetFieldPointsFromSpreadLines()
    {
        return new Vector2[]
        {
            new Vector2(fieldLines.CurrentMinX, fieldLines.CurrentMinY),
            new Vector2(fieldLines.CurrentMaxX, fieldLines.CurrentMinY),
            new Vector2(fieldLines.CurrentMaxX, fieldLines.CurrentMaxY),
            new Vector2(fieldLines.CurrentMinX, fieldLines.CurrentMaxY)
        };
    }

    private void UpdateOutput(float flux, float emf)
    {
        float current = resistance == 0f ? 0f : emf / resistance;

        if (fluxText != null)
            fluxText.text = $"Flux: {flux:F2} Wb";

        if (emfText != null)
            emfText.text = $"EMF: {emf:F2} V";

        if (currentText != null)
            currentText.text = $"Current: {current:F2} A";
    }

    private void SyncUI()
    {
        if (magneticFieldInput != null)
            magneticFieldInput.SetTextWithoutNotify(magneticField.ToString("F2"));

        if (polygonSidesInput != null)
            polygonSidesInput.SetTextWithoutNotify(polygonSides.ToString());

        if (isDebug) Log($"SyncUI: magneticField={magneticField}, polygonSides={polygonSides}");
    }

    private void ResetFlux(string reason)
    {
        UpdateVisual();
        UpdateCachedAreaVisuals();

        previousFlux = CalculateFlux($"ResetFlux: {reason}");

        FaradayLawVariables.Flux = previousFlux;
        FaradayLawVariables.EMF = 0f;
        FaradayLawVariables.Current = 0f;

        UpdateOutput(previousFlux, 0f);

        if (isDebug) Log($"ResetFlux called. reason={reason}, previousFlux={previousFlux}");
    }                                   

    private void SetActiveAreaVisualPair(int activeSides)
    {
        if (!useCachedAreaVisuals)
            return;

        foreach (KeyValuePair<int, AreaVisualPair> entry in areaVisualPairs)
        {
            bool shouldBeActive = entry.Key == activeSides;

            if (entry.Value.root != null)
                entry.Value.root.SetActive(shouldBeActive);
        }

        if (isDebug) Log($"Active cached area visual pair set to polygonSides={activeSides}");
    }

    private bool ShouldLogThisFrame()
    {
        if (!debugLogs)
            return false;

        if (debugEveryFrame)
            return true;

        if (debugEveryNFrames <= 0)
            debugEveryNFrames = 30;

        return Time.frameCount % debugEveryNFrames == 0;
    }

    private void LogFluxZero(string source, string reason)
    {
        if (!ShouldLogThisFrame())
            return;

        if (isDebug) Log($"CalculateFlux from {source} returned 0. Reason: {reason}");
    }

    private void LogPoints(string label, Vector2[] points)
    {
        if (points == null)
        {
            if (isDebug) Log($"{label}: null");
            return;
        }

        string output = $"{label}: count={points.Length} ";

        for (int i = 0; i < points.Length; i++)
        {
            output += $"[{i}]={points[i]} ";
        }

        if (isDebug) Log(output);
    }

    private void Log(string message)
    {
        if (!isDebug)
            return;

        Debug.Log($"[Faraday2] {message}", this);
    }
}