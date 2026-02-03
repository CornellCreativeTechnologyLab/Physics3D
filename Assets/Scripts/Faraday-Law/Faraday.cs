using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.UI;

public class Faraday : MonoBehaviour
{
    [Header("Faraday Configuration")]
    [SerializeField] public float length = 1.0f;
    [SerializeField] public float width = 1.0f;
    [SerializeField] public float rotation = 1.0f;
    [SerializeField] public float relativeScale = 1.0f;

    private GameObject frame;

    [Header("UI Controls")]
    [SerializeField] public TMP_InputField electricFieldInput; // Displays B-Field
    [SerializeField] public Slider MagneticFieldSlider;       // Controls B-Field

    [SerializeField] public TMP_Text fluxText;
    [SerializeField] public TMP_Text emfText;
    [SerializeField] public TMP_Text currentText;
    [SerializeField] public TMP_Text thetaText;
    [SerializeField] public TMP_Text areaText;

    [SerializeField] public Slider lengthSlider;
    [SerializeField] public TMP_InputField lengthInput;
    [SerializeField] public Slider widthSlider;
    [SerializeField] public TMP_InputField widthInput;
    [SerializeField] public Slider rotationSlider;
    [SerializeField] public TMP_InputField rotationInput;

    [SerializeField] public Slider frequencySlider;
    [SerializeField] public TMP_InputField frequencyInput;

    [Header("Physics Values")]
    [SerializeField] public float Resistance;
    [SerializeField] public static float frequency;
    [SerializeField] private float magneticFieldMagnitude = 1f;

    [Header("Simulation Objects")]
    [SerializeField] public GameObject cube; // The rotating coil

    // Arrows
    private float area;
    [SerializeField] private GameObject areaArrowAttachedTo;
    [SerializeField] private Vector3 arrowheadDirection = new Vector3(0, 1, 0);
    [SerializeField] private float initialLengthOfTail = 1f;
    [SerializeField] private GameObject arrowGameObject;

    private Arrow areaArrow;
    private Arrow fieldArrow;
    [SerializeField] private GameObject fieldArrowParent;

    [Header("Visualization & Materials")]
    [SerializeField] private CurrentVisualizer currentVisualizer;
    [SerializeField] private Material areaMaterial;
    [SerializeField] private Material fieldMaterial;

    [Header("Prefabs (Labels)")]
    [SerializeField] private GameObject APrefab; // Area Label
    [SerializeField] private GameObject BPrefab; // Magnetic Field Label

    private float previousFlux = 0f;

    private void Awake()
    {
        frequency = 1f;
        Resistance = 10f;

        MagneticFieldSlider.minValue = 0f;
        MagneticFieldSlider.maxValue = 10f;

        lengthSlider.minValue = 1.0f;
        lengthSlider.maxValue = 2.0f;

        widthSlider.minValue = 1.0f;
        widthSlider.maxValue = 2.0f;

        rotationSlider.minValue = 0f;
        rotationSlider.maxValue = 360f;

        MagneticFieldSlider.value = 5f;
        lengthSlider.value = 1f;
        widthSlider.value = 1f;
        rotationSlider.value = 90f;
        rotationInput.text = "90.00";

        area = lengthSlider.value * widthSlider.value;
        magneticFieldMagnitude = MagneticFieldSlider.value;
    }

    void Start()
    {
        // 1. Safety check for attachment point
        if (areaArrowAttachedTo == null)
        {
            if (cube != null) areaArrowAttachedTo = cube;
            else areaArrowAttachedTo = this.gameObject;
        }

        // 2. Instantiate Area Arrow
        areaArrow = new Arrow(
            areaArrowAttachedTo,
            arrowheadDirection,
            initialLengthOfTail,
            relativeScale,
            headMaterial: areaMaterial,
            tailMaterial: areaMaterial,
            label3D: APrefab,
            labelScale: 0.05f,
            labelRotation: Quaternion.Euler(90, 0, -90),
            labelOffst: new Vector3(1f, 0f, 0f)
        );

        // 3. Instantiate Field Arrow
        frame = this.gameObject;
        float arrowLength = 1f;

        fieldArrowParent.AddComponent<MeshFilter>();

        fieldArrow = new Arrow(
            fieldArrowParent,
            new Vector3(0, 0, 1), // B-Field direction
            arrowLength,
            relativeScale,
            headMaterial: fieldMaterial,
            tailMaterial: fieldMaterial,
            label3D: BPrefab,
            labelScale: 0.05f,
            labelRotation: Quaternion.Euler(-90, 180, 0),
            labelOffst: new Vector3(1f, 0f, 0f)
        );

        // --- LISTENERS SETUP ---
        MagneticFieldSlider.onValueChanged.AddListener(OnMagneticFieldSliderChanged);
        lengthSlider.onValueChanged.AddListener(OnLengthSliderChanged);
        widthSlider.onValueChanged.AddListener(OnWidthSliderChanged);
        rotationSlider.onValueChanged.AddListener(OnRotationSliderChanged);
        rotationSlider.onValueChanged.AddListener(SnapToClosestPoint);
        frequencySlider.onValueChanged.AddListener(OnFrequencySliderChanged);

        // Input Fields
        lengthInput.text = "1";
        widthInput.text = "1";
        electricFieldInput.onEndEdit.AddListener(OnMagneticFieldInputChanged);
        lengthInput.onEndEdit.AddListener(OnLengthInputChanged);
        widthInput.onEndEdit.AddListener(OnWidthInputChanged);
        rotationInput.onEndEdit.AddListener(OnRotationInputChanged);
        frequencyInput.onEndEdit.AddListener(OnFrequencyInputChanged);

        // Mobile Inputs
        electricFieldInput.onSelect.AddListener(ShowMobileKeyboard);
        lengthInput.onSelect.AddListener(ShowMobileKeyboard);
        widthInput.onSelect.AddListener(ShowMobileKeyboard);
        rotationInput.onSelect.AddListener(ShowMobileKeyboard);

        // Calculation Listeners
        lengthSlider.onValueChanged.AddListener(delegate { CalculateFlux(); });
        widthSlider.onValueChanged.AddListener(delegate { CalculateFlux(); });
        rotationSlider.onValueChanged.AddListener(delegate { CalculateFlux(); });
        MagneticFieldSlider.onValueChanged.AddListener(delegate { CalculateFlux(); });

        // Initial Calculations
        areaText.text = "Area: " + (lengthSlider.value * widthSlider.value).ToString("F2") + " m^2";
        frequencyInput.text = System.Math.Round(frequencySlider.value, 3).ToString("F2");

        CalculateFlux();
        previousFlux = FaradayLawVariables.Flux;

        CalculateEMF();
        CalculateCurrent();
        RotateObject();

        // Scene Setup
        areaArrow.SetScene();
        areaArrow.SetTailLength(CalculateTailLengthByArea(CalculateArea(), 10f));
        fieldArrow.SetScene();

        Debug.Log("Faraday Start completed.");
    }

    void Update()
    {
        // --- UPDATED LENGTH LOGIC (Matches Flux.cs) ---
        // Area Arrow updates with max length 5f
        areaArrow.SetTailLength(CalculateTailLengthByArea(CalculateArea(), 5f));

        // Field Arrow updates with max length 10f
        fieldArrow.SetTailLength(CalculateLengthByField(MagneticFieldSlider.value, 10f));

        // 2. Rotate the Coil (Cube)
        RotateObject();

        // 3. Update Arrow Logic (Parent tracking, labels)
        if (areaArrow != null)
        {
            if (areaArrow.IsParentTransformChanged()) areaArrow.UpdateParentTransform();
            areaArrow.Update();
        }

        if (fieldArrow != null)
        {
            if (fieldArrow.IsParentTransformChanged()) fieldArrow.UpdateParentTransform();
            fieldArrow.Update();
        }

        // 4. Physics Calculations
        CalculateFlux();
        CalculateEMF();
        CalculateCurrent();
    }

    // --- HELPERS (Updated to match Flux.cs logic exactly) ---

    public float CalculateTailLengthByArea(float area, float maxLength)
    {
        if (area > 0 && maxLength > 0)
        {
            // Matching Flux.cs thresholds
            float maxArea = 4f;
            float minArea = 0.1f;

            // Ensure area is within the bounds
            float clampedArea = Mathf.Clamp(area, minArea, maxArea);

            // Calculate proportionate length
            float length = (maxLength / maxArea) * clampedArea;

            // Further clamp the length
            float minLength = maxLength * 0.1f;
            return Mathf.Clamp(length, minLength, maxLength);
        }
        return 0;
    }

    public float CalculateLengthByField(float field, float maxLength)
    {
        if (field >= 0 && maxLength > 0)
        {
            // Matching Flux.cs thresholds
            float maxField = 10f;
            float minField = 0f;

            // Ensure field is within the bounds
            float clampedField = Mathf.Clamp(field, minField, maxField);

            // Calculate proportionate length
            float length = (maxLength / maxField) * clampedField;

            // Further clamp the length
            float minLength = maxLength * 0.1f;
            return Mathf.Clamp(length, minLength, maxLength);
        }
        return 0;
    }

    // --- UI & PHYSICS HANDLERS (Unchanged) ---

    void OnMagneticFieldSliderChanged(float value)
    {
        magneticFieldMagnitude = value;
        electricFieldInput.text = value.ToString();
        CalculateFlux();
    }

    void OnLengthSliderChanged(float value)
    {
        float newLength = value * 0.05f;
        float currentWidth = cube.transform.localScale.z;
        Vector3 newScale = new Vector3(newLength, cube.transform.localScale.y, currentWidth);
        cube.transform.localScale = newScale;
        lengthInput.text = value.ToString();
    }

    void OnWidthSliderChanged(float value)
    {
        float newWidth = value * 0.05f;
        float currentLength = cube.transform.localScale.x;
        Vector3 newScale = new Vector3(currentLength, cube.transform.localScale.y, newWidth);
        cube.transform.localScale = newScale;
        widthInput.text = value.ToString();
    }

    void OnRotationSliderChanged(float value)
    {
        rotationInput.text = value.ToString();
    }

    void RotateObject()
    {
        cube.transform.rotation = Quaternion.Euler(0, rotationSlider.value + 180, 90);
    }

    void OnMagneticFieldInputChanged(string value)
    {
        if (float.TryParse(value, out float result))
        {
            magneticFieldMagnitude = result;
            MagneticFieldSlider.value = result;
            CalculateFlux();
        }
    }

    void OnLengthInputChanged(string value)
    {
        if (float.TryParse(value, out float result))
        {
            lengthSlider.value = result;
            CalculateFlux();
        }
    }

    void OnWidthInputChanged(string value)
    {
        if (float.TryParse(value, out float result))
        {
            widthSlider.value = result;
            CalculateFlux();
        }
    }

    void OnRotationInputChanged(string value)
    {
        if (float.TryParse(value, out float newRotation))
        {
            newRotation = Mathf.Clamp(newRotation % 360, 0, 360);
            rotationSlider.value = newRotation;
            rotationInput.text = newRotation.ToString();
            cube.transform.rotation = Quaternion.Euler(0, newRotation, 0);
        }
        else
        {
            rotationInput.text = rotationSlider.value.ToString();
        }
        CalculateFlux();
    }

    private void OnFrequencySliderChanged(float arg0)
    {
        frequency = arg0;
        frequencyInput.text = arg0.ToString("F2");
    }

    private void OnFrequencyInputChanged(string arg0)
    {
        if (float.TryParse(arg0, out float result))
        {
            frequencySlider.value = result;
            frequency = result;
        }
    }

    public float CalculateArea()
    {
        return lengthSlider.value * widthSlider.value;
    }

    public float FluxValue(float B, float A, float theta)
    {
<<<<<<< HEAD
        return B * A * Mathf.Sin(theta * (Mathf.PI / 180));
=======
        return B * A * Mathf.Cos(theta * (Mathf.PI / 180));
>>>>>>> c5de63b56bf9714503326fda9cc4eb4efcb49210
    }

    void CalculateFlux()
    {
        float area = CalculateArea();
        float theta = rotationSlider.value - 90;
        float flux = FluxValue(magneticFieldMagnitude, area, theta);
        float thetaTextValue = Mathf.Abs(180 - Mathf.Abs(90 - theta));

        FaradayLawVariables.Flux = flux;

        fluxText.text = "Flux: " + flux.ToString("F2");
        thetaText.text = "Angle with +X axis: " + thetaTextValue.ToString("F2") + " \u00B0";
        electricFieldInput.text = magneticFieldMagnitude.ToString("F2");
        areaText.text = "Area: " + area.ToString("F2") + " m\u00B2";
    }

    void CalculateEMF()
    {
        float currentFlux = FaradayLawVariables.Flux;
        float deltaFlux = currentFlux - previousFlux;
        float emf = -deltaFlux / Time.deltaTime;

        previousFlux = currentFlux;
        FaradayLawVariables.EMF = emf;

        if (emfText) emfText.text = $"EMF: " + emf.ToString("F3");
    }

    public float CurrentValue(float Resistance)
    {
        return FaradayLawVariables.EMF / Resistance;
    }

    void CalculateCurrent()
    {
        float current = CurrentValue(Resistance);
        FaradayLawVariables.Current = current;
        currentText.text = $"Current: " + current.ToString();

        if (currentVisualizer != null)
        {
            currentVisualizer.currentAmps = current;
        }
    }

    public float[] snapPoints;
    public float threshold = 5f;

    private void SnapToClosestPoint(float value)
    {
        if (snapPoints == null || snapPoints.Length == 0) return;
    }

    private void ShowMobileKeyboard(string text)
    {
        TouchScreenKeyboard.Open("", TouchScreenKeyboardType.Default);
    }
}