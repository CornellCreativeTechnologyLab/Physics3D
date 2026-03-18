// Amperes.cs
// ONE script: conductors toggles + loop toggles + circle/rectangle sizing.
// Segment is ignored (no geometry generation / no size UI).
//
// Put this on your ControlPanel (UI root).
// Requirements:
// - Each conductor GameObject is assigned (line/sheet/solenoid/toroid). NoWire can be null.
// - Each loop parent (circleParent/rectangleParent) has a LineRenderer somewhere under it.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Amperes : MonoBehaviour
{
    // ---------------- Enums ----------------
    public enum ConductorType { NoWire, Line, Sheet, Solenoid, Toroid }
    public enum LoopType { None, Circle, Rectangle, Segment }
    public enum PlaneOrientation { XZ_Floor, XY_Front, YZ_Side }

    // ---------------- Conductors ----------------
    [Header("Conductors (Objects)")]
    [SerializeField] private GameObject noWireObj; // optional
    [SerializeField] private GameObject lineWireObj;
    [SerializeField] private GameObject sheetWireObj;
    [SerializeField] private GameObject solenoidObj;
    [SerializeField] private GameObject toroidObj;

    [Header("Conductors (Toggles)")]
    [SerializeField] private Toggle noWireToggle;
    [SerializeField] private Toggle lineWireToggle;
    [SerializeField] private Toggle sheetWireToggle;
    [SerializeField] private Toggle solenoidToggle;
    [SerializeField] private Toggle toroidToggle;

    [Header("State")]
    public ConductorType conductorType = ConductorType.NoWire;

    // ---------------- Loop Type ----------------
    [Header("Loop Type (Toggles)")]
    [SerializeField] private Toggle loopNoneToggle;
    [SerializeField] private Toggle loopCircularToggle;
    [SerializeField] private Toggle loopRectangularToggle;
    [SerializeField] private Toggle loopSegmentToggle; // ignored

    [Header("Loop Parents (Contain LineRenderers)")]
    [SerializeField] private GameObject circleParent;
    [SerializeField] private GameObject rectangleParent;

    [Header("Loop Dimension Panels")]
    [SerializeField] private GameObject circleDimensionsPanel;
    [SerializeField] private GameObject rectDimensionsPanel;

    [Header("Loop State")]
    public LoopType loopType = LoopType.None;

    // ---------------- Shared Loop Params ----------------
    [Header("Plane / Rotation")]
    public PlaneOrientation orientation = PlaneOrientation.XZ_Floor;
    [Range(0f, 360f)] public float shapeRotation = 0f;
    public bool isClosedLoop = true;

    // ---------------- Circle Controls ----------------
    [Header("Circle Controls")]
    [SerializeField] private Slider circleRadiusSlider;
    [SerializeField] private TMP_InputField circleRadiusInput;

    [Header("Circle Settings")]
    [Min(0.001f)] public float radius = 2f;
    [Range(3, 200)] public int circleResolution = 64;

    // ---------------- Rectangle Controls ----------------
    [Header("Rectangle Controls")]
    [SerializeField] private Slider rectWidthSlider;
    [SerializeField] private TMP_InputField rectWidthInput;
    [SerializeField] private Slider rectHeightSlider;
    [SerializeField] private TMP_InputField rectHeightInput;

    [Header("Rectangle Settings")]
    [Min(0.001f)] public float rectWidth = 4f;
    [Min(0.001f)] public float rectHeight = 2f;
    [Range(1, 50)] public int segmentsPerEdge = 1;

    [Header("UI Size Limits")]
    [SerializeField] private float minSize = 0.1f;
    [SerializeField] private float maxSize = 10f;

    [Header("Plane Dropdowns")]
    [SerializeField] private TMP_Dropdown circlePlaneDropdown;
    [SerializeField] private TMP_Dropdown rectPlaneDropdown;


    // Add under [Header("Circle Controls")]
    [SerializeField] private Slider circleOffsetSlider;
    public float circleOffset = 0f;

    // Add under [Header("Rectangle Controls")]
    [SerializeField] private Slider rectOffsetSlider;
    public float rectOffset = 0f;

    // ---------------- Internals ----------------
    private LineRenderer _lr;
    private readonly List<Vector3> _points = new List<Vector3>();

    // Replace the single floats with these
    private float[] circleOffsets = new float[3]; // [0]=XZ, [1]=XY, [2]=YZ
    private float[] rectOffsets = new float[3];

    private void Awake()
    {
        // init slider/input display
        InitPair(circleRadiusSlider, circleRadiusInput, radius);
        InitPair(rectWidthSlider, rectWidthInput, rectWidth);
        InitPair(rectHeightSlider, rectHeightInput, rectHeight);
    }

    private void Start()
    {
        // Default UI: Loop None checked (like your screenshot)
        if (loopNoneToggle != null) loopNoneToggle.isOn = true;

        // Hook conductor toggles
        if (noWireToggle != null) noWireToggle.onValueChanged.AddListener(v => { if (v) SetConductor(ConductorType.NoWire); });
        if (lineWireToggle != null) lineWireToggle.onValueChanged.AddListener(v => { if (v) SetConductor(ConductorType.Line); });
        if (sheetWireToggle != null) sheetWireToggle.onValueChanged.AddListener(v => { if (v) SetConductor(ConductorType.Sheet); });
        if (solenoidToggle != null) solenoidToggle.onValueChanged.AddListener(v => { if (v) SetConductor(ConductorType.Solenoid); });
        if (toroidToggle != null) toroidToggle.onValueChanged.AddListener(v => { if (v) SetConductor(ConductorType.Toroid); });

        // Hook loop toggles
        if (loopNoneToggle != null) loopNoneToggle.onValueChanged.AddListener(v => { if (v) SetLoop(LoopType.None); });
        if (loopCircularToggle != null) loopCircularToggle.onValueChanged.AddListener(v => { if (v) SetLoop(LoopType.Circle); });
        if (loopRectangularToggle != null) loopRectangularToggle.onValueChanged.AddListener(v => { if (v) SetLoop(LoopType.Rectangle); });
        if (loopSegmentToggle != null) loopSegmentToggle.onValueChanged.AddListener(v => { if (v) SetLoop(LoopType.Segment); }); // ignored

        // Hook size controls
        if (circleRadiusSlider != null) circleRadiusSlider.onValueChanged.AddListener(OnCircleRadiusSlider);
        if (circleRadiusInput != null) circleRadiusInput.onEndEdit.AddListener(OnCircleRadiusInput);

        if (rectWidthSlider != null) rectWidthSlider.onValueChanged.AddListener(OnRectWidthSlider);
        if (rectWidthInput != null) rectWidthInput.onEndEdit.AddListener(OnRectWidthInput);

        if (rectHeightSlider != null) rectHeightSlider.onValueChanged.AddListener(OnRectHeightSlider);
        if (rectHeightInput != null) rectHeightInput.onEndEdit.AddListener(OnRectHeightInput);

        if (circlePlaneDropdown != null)
        {
            circlePlaneDropdown.onValueChanged.AddListener(i => OnPlaneDropdownChanged(LoopType.Circle, i));
            OnPlaneDropdownChanged(LoopType.Circle, circlePlaneDropdown.value);
        }

        if (rectPlaneDropdown != null)
        {
            rectPlaneDropdown.onValueChanged.AddListener(i => OnPlaneDropdownChanged(LoopType.Rectangle, i));
            OnPlaneDropdownChanged(LoopType.Rectangle, rectPlaneDropdown.value);
        }

        // Hook up Offset Sliders (-5 to 5 limit)
        if (circleOffsetSlider != null)
        {
            circleOffsetSlider.minValue = -5f;
            circleOffsetSlider.maxValue = 5f;
            circleOffsetSlider.value = circleOffset;
            circleOffsetSlider.onValueChanged.AddListener(OnCircleOffsetSlider);
        }

        if (rectOffsetSlider != null)
        {
            rectOffsetSlider.minValue = -5f;
            rectOffsetSlider.maxValue = 5f;
            rectOffsetSlider.value = rectOffset;
            rectOffsetSlider.onValueChanged.AddListener(OnRectOffsetSlider);
        }


        UpdateActiveConductor();
        RefreshActiveLoop(); // sets parents, picks LR, updates panels
        Rebuild();
    }

    private void OnCircleOffsetSlider(float v)
    {
        // Save to the slot corresponding to the current plane
        circleOffsets[(int)_circlePlane] = v;
        Rebuild();
    }

    private void OnRectOffsetSlider(float v)
    {
        // Save to the slot corresponding to the current plane
        rectOffsets[(int)_rectPlane] = v;
        Rebuild();
    }

    private void OnValidate()
    {
        // Keep editor updates safe (no assumptions about LineRenderer on this object)
        UpdateActiveConductor();
        RefreshActiveLoop();
        Rebuild();
    }

    private PlaneOrientation _circlePlane = PlaneOrientation.XZ_Floor;
    private PlaneOrientation _rectPlane = PlaneOrientation.XZ_Floor;

    private void OnPlaneDropdownChanged(LoopType which, int index)
    {
        var p = (PlaneOrientation)index;

        if (which == LoopType.Circle)
        {
            _circlePlane = p;
            // Load the saved offset for this specific plane into the slider
            if (circleOffsetSlider != null)
                circleOffsetSlider.value = circleOffsets[index];
        }
        else if (which == LoopType.Rectangle)
        {
            _rectPlane = p;
            // Load the saved offset for this specific plane into the slider
            if (rectOffsetSlider != null)
                rectOffsetSlider.value = rectOffsets[index];
        }

        if (loopType == which)
        {
            orientation = p;
            Rebuild();
        }
    }



    // =========================================================
    // Conductors
    // =========================================================
    public void SetConductor(ConductorType t)
    {
        conductorType = t;
        UpdateActiveConductor();
    }

    private void UpdateActiveConductor()
    {
        if (noWireObj != null) noWireObj.SetActive(conductorType == ConductorType.NoWire);
        if (lineWireObj != null) lineWireObj.SetActive(conductorType == ConductorType.Line);
        if (sheetWireObj != null) sheetWireObj.SetActive(conductorType == ConductorType.Sheet);
        if (solenoidObj != null) solenoidObj.SetActive(conductorType == ConductorType.Solenoid);
        if (toroidObj != null) toroidObj.SetActive(conductorType == ConductorType.Toroid);
    }

    // =========================================================
    // Loops: toggles + parents + panels + LineRenderer selection
    // =========================================================
    public void SetLoop(LoopType t)
    {
        loopType = t;
        RefreshActiveLoop();
        Rebuild();
    }

    private void RefreshActiveLoop()
    {
        orientation = (loopType == LoopType.Circle) ? _circlePlane : _rectPlane;

        // Parents visible only when active
        if (circleParent != null) circleParent.SetActive(loopType == LoopType.Circle);
        if (rectangleParent != null) rectangleParent.SetActive(loopType == LoopType.Rectangle);

        // Panels visible only when active
        if (circleDimensionsPanel != null) circleDimensionsPanel.SetActive(loopType == LoopType.Circle);
        if (rectDimensionsPanel != null) rectDimensionsPanel.SetActive(loopType == LoopType.Rectangle);

        // Segment ignored: treat as no active loop renderer
        if (loopType == LoopType.None || loopType == LoopType.Segment)
        {
            _lr = null;
            return;
        }

        // Pick LineRenderer from active parent
        GameObject active = (loopType == LoopType.Circle) ? circleParent : rectangleParent;
        if (active == null)
        {
            _lr = null;
            return;
        }

        _lr = active.GetComponentInChildren<LineRenderer>(true);
        if (_lr != null) _lr.useWorldSpace = false;
    }

    // =========================================================
    // UI: size controls (circle / rectangle)
    // =========================================================
    private void InitPair(Slider s, TMP_InputField f, float value)
    {
        if (s == null || f == null) return;
        s.minValue = minSize;
        s.maxValue = maxSize;
        s.value = Mathf.Clamp(value, minSize, maxSize);
        f.text = s.value.ToString("0.###");
    }

    private void OnCircleRadiusSlider(float v)
    {
        radius = Mathf.Max(0.001f, v);
        if (circleRadiusInput != null) circleRadiusInput.text = v.ToString("0.###");
        Rebuild();
    }

    private void OnCircleRadiusInput(string s)
    {
        if (!float.TryParse(s, out float v)) return;
        v = Mathf.Clamp(v, minSize, maxSize);
        if (circleRadiusSlider != null) circleRadiusSlider.value = v; // triggers slider handler
    }

    private void OnRectWidthSlider(float v)
    {
        rectWidth = Mathf.Max(0.001f, v);
        if (rectWidthInput != null) rectWidthInput.text = v.ToString("0.###");
        Rebuild();
    }

    private void OnRectWidthInput(string s)
    {
        if (!float.TryParse(s, out float v)) return;
        v = Mathf.Clamp(v, minSize, maxSize);
        if (rectWidthSlider != null) rectWidthSlider.value = v;
    }

    private void OnRectHeightSlider(float v)
    {
        rectHeight = Mathf.Max(0.001f, v);
        if (rectHeightInput != null) rectHeightInput.text = v.ToString("0.###");
        Rebuild();
    }

    private void OnRectHeightInput(string s)
    {
        if (!float.TryParse(s, out float v)) return;
        v = Mathf.Clamp(v, minSize, maxSize);
        if (rectHeightSlider != null) rectHeightSlider.value = v;
    }

    // =========================================================
    // Loop Geometry (circle + rectangle)
    // =========================================================
    public void Rebuild()
    {
        if (_lr == null) return;                 // None/Segment or missing LR => safe no-op
        if (loopType == LoopType.Segment) return; // leave alone

        _points.Clear();

        if (loopType == LoopType.Circle) BuildCircle();
        else if (loopType == LoopType.Rectangle) BuildRectangle();
        else return;

        UpdateLineRenderer();
    }

    private void BuildCircle()
    {
        int n = Mathf.Max(3, circleResolution);

        for (int i = 0; i < n; i++)
        {
            float ang = (float)i / n * Mathf.PI * 2f;
            float u = Mathf.Cos(ang) * radius;
            float v = Mathf.Sin(ang) * radius;
            _points.Add(GetPointOnPlane(u, v));
        }
    }

    private void BuildRectangle()
    {
        int s = Mathf.Max(1, segmentsPerEdge);
        float hw = rectWidth * 0.5f;
        float hh = rectHeight * 0.5f;

        Vector2 a = new Vector2(-hw, -hh);
        Vector2 b = new Vector2(hw, -hh);
        Vector2 c = new Vector2(hw, hh);
        Vector2 d = new Vector2(-hw, hh);

        AddEdge(a, b, s, true);
        AddEdge(b, c, s, false);
        AddEdge(c, d, s, false);
        AddEdge(d, a, s, false);
    }

    private void AddEdge(Vector2 start, Vector2 end, int segments, bool includeStart)
    {
        int startIndex = includeStart ? 0 : 1;
        for (int i = startIndex; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector2 p = Vector2.Lerp(start, end, t);
            _points.Add(GetPointOnPlane(p.x, p.y));
        }
    }

    private Vector3 GetPointOnPlane(float u, float v)
    {
        // Pull the specific offset for the ACTIVE plane
        float currentOffset = (loopType == LoopType.Circle)
            ? circleOffsets[(int)orientation]
            : rectOffsets[(int)orientation];

        float rad = shapeRotation * Mathf.Deg2Rad;
        float uRot = u * Mathf.Cos(rad) - v * Mathf.Sin(rad);
        float vRot = u * Mathf.Sin(rad) + v * Mathf.Cos(rad);

        // Apply the planar offset
        uRot += currentOffset;

        return orientation switch
        {
            PlaneOrientation.XY_Front => new Vector3(uRot, vRot, 0f),
            PlaneOrientation.YZ_Side => new Vector3(0f, vRot, uRot),
            _ => new Vector3(uRot, 0f, vRot),
        };
    }

    private void UpdateLineRenderer()
    {
        if (_points.Count == 0) return;

        int count = isClosedLoop ? _points.Count + 1 : _points.Count;
        _lr.positionCount = count;

        for (int i = 0; i < _points.Count; i++)
            _lr.SetPosition(i, _points[i]);

        if (isClosedLoop)
            _lr.SetPosition(_points.Count, _points[0]);
    }
}
