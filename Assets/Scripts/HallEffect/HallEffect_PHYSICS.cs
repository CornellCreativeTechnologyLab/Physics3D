using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives two ChargeBelts (electrons + holes) to visualize the Hall effect.
///
/// Carrier mode picks which belt is the MOBILE carrier:
///   Negative -> electron belt moves & polarizes; hole belt stays put (neutral background).
///   Positive -> hole belt moves & polarizes; electron belt stays put.
///
/// Voltage drives drift speed; (v x B) drives lateral polarization. Because the
/// stationary carrier stays centred, the moving carrier accumulating on one edge
/// produces a visible NET polarization.
///
/// Geometry follows ChargeBelt's local axes:
///   +X = flow,  +Y = Hall/lateral (polarization),  +Z = B field axis.
/// Conventional current is +X for positive voltage.
/// </summary>
[DisallowMultipleComponent]
public class HallEffect : MonoBehaviour
{
    public enum CarrierMode { Negative, Positive } // Negative = electrons, Positive = holes

    [Header("Belts")]
    [SerializeField] private ChargeBelt electronBelt;
    [SerializeField] private ChargeBelt holeBelt;

    [Header("UI (optional)")]
    [SerializeField] private Slider voltageSlider;
    [SerializeField] private Slider magneticFieldSlider;
    [SerializeField] private Toggle negativeToggle; // electrons
    [SerializeField] private Toggle positiveToggle; // holes

    [Header("Numeric Inputs (optional)")]
    [SerializeField] private TMP_InputField voltageInput;
    [SerializeField] private TMP_InputField magneticFieldInput;
    [Tooltip("Number format for the input fields, e.g. \"0.##\" or \"0.00\".")]
    [SerializeField] private string valueFormat = "0.##";

    [Header("Input Ranges")]
    [SerializeField] private float maxVoltage = 10f;
    [SerializeField] private float maxField = 1f;
    [Tooltip("Flip to -1 to reverse the magnetic field direction without changing the slider.")]
    [SerializeField] private float fieldDirection = 1f;

    [Header("Response")]
    [SerializeField] private float maxDriftSpeed = 3f;
    [Range(0f, 1f)][SerializeField] private float maxPolarization = 1f;
    [Tooltip("Higher = snappier response; lower = smoother glide between states.")]
    [SerializeField] private float smoothing = 8f;

    [Header("State")]
    [SerializeField] private CarrierMode mode = CarrierMode.Negative;

    [Header("Readout (read-only)")]
    [SerializeField] private float activeDriftSpeed;
    [SerializeField] private float activePolarization;

    // smoothed per-belt values
    private float eSpeed, ePol, hSpeed, hPol;

    public CarrierMode Mode => mode;
    public ChargeBelt ActiveBelt => mode == CarrierMode.Negative ? electronBelt : holeBelt;

    private void Start()
    {
        if (voltageSlider) voltageSlider.value = 1f;
        if (magneticFieldSlider) magneticFieldSlider.value = 0f;

        // Keep each slider and its text box in sync (both directions).
        BindSliderInput(voltageSlider, voltageInput);
        BindSliderInput(magneticFieldSlider, magneticFieldInput);

        if (negativeToggle)
            negativeToggle.onValueChanged.AddListener(on => { if (on) SetCarrierMode(CarrierMode.Negative); });
        if (positiveToggle)
            positiveToggle.onValueChanged.AddListener(on => { if (on) SetCarrierMode(CarrierMode.Positive); });

        SetCarrierMode(mode);
    }

    /// <summary>Switch which carrier is mobile. Hook this to a dropdown/button too if you like.</summary>
    public void SetCarrierMode(CarrierMode m)
    {
        mode = m;
        if (m == CarrierMode.Negative)
        {
            if (negativeToggle) negativeToggle.SetIsOnWithoutNotify(true);
            if (positiveToggle) positiveToggle.SetIsOnWithoutNotify(false);
        }
        else
        {
            if (positiveToggle) positiveToggle.SetIsOnWithoutNotify(true);
            if (negativeToggle) negativeToggle.SetIsOnWithoutNotify(false);
        }
    }

    // Convenience hooks for UnityEvents (e.g. a single toggle's onValueChanged(bool)).
    public void SetPositiveCarriers(bool on) { if (on) SetCarrierMode(CarrierMode.Positive); }
    public void SetNegativeCarriers(bool on) { if (on) SetCarrierMode(CarrierMode.Negative); }

    /// <summary>
    /// Two-way bind a slider and a TMP input field. Moving the slider updates the text;
    /// committing the text (Enter / focus loss) updates the slider, clamped to its range.
    /// SetXWithoutNotify is used to break the feedback loop between the two.
    /// </summary>
    private void BindSliderInput(Slider slider, TMP_InputField input)
    {
        if (slider == null || input == null) return;

        // Seed the field with the slider's current value.
        input.SetTextWithoutNotify(slider.value.ToString(valueFormat));

        // Slider -> text.
        slider.onValueChanged.AddListener(val =>
            input.SetTextWithoutNotify(val.ToString(valueFormat)));

        // Text -> slider (on commit).
        input.onEndEdit.AddListener(text =>
        {
            if (float.TryParse(text, out float parsed))
            {
                float clamped = Mathf.Clamp(parsed, slider.minValue, slider.maxValue);
                slider.value = clamped;                                 // updates the belt next frame
                input.SetTextWithoutNotify(clamped.ToString(valueFormat)); // tidy the displayed text
            }
            else
            {
                // Unparseable entry -> restore the last good value.
                input.SetTextWithoutNotify(slider.value.ToString(valueFormat));
            }
        });
    }

    private void Update()
    {
        float v = voltageSlider ? voltageSlider.value : 1f;
        float b = magneticFieldSlider ? magneticFieldSlider.value : 0f;

        float vNorm = maxVoltage > 1e-5f ? Mathf.Clamp(v / maxVoltage, -1f, 1f) : 0f;
        float bNorm = maxField > 1e-5f ? Mathf.Clamp(b / maxField, -1f, 1f) * fieldDirection : 0f;

        // Lateral deflection magnitude scales with |V|*|B|; its base sign flips with V and B.
        float baseLateral = Mathf.Clamp(vNorm * bNorm, -1f, 1f) * maxPolarization;

        // Carrier charge sign sends the two types to OPPOSITE ends.
        const float electronSign = -1f; // negative carriers
        const float holeSign = +1f;     // positive carriers

        // Only the selected carrier moves & polarizes; the other stays centred & still.
        float eSpeedTarget = 0f, ePolTarget = 0f, hSpeedTarget = 0f, hPolTarget = 0f;

        if (mode == CarrierMode.Negative)
        {
            // Electrons move: drift against voltage (negative direction)
            eSpeedTarget = -vNorm * maxDriftSpeed;
            ePolTarget = electronSign * baseLateral;

            // Holes stay still (stationary)
            hSpeedTarget = 0f;
            hPolTarget = 0f;
        }
        else
        {
            // Holes move: drift with voltage (positive direction) - OPPOSITE to electrons
            hSpeedTarget = vNorm * maxDriftSpeed;
            hPolTarget = holeSign * baseLateral;

            // Electrons stay still (stationary)
            eSpeedTarget = 0f;
            ePolTarget = 0f;
        }

        // Frame-rate independent smoothing so charges glide rather than snap.
        float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        eSpeed = Mathf.Lerp(eSpeed, eSpeedTarget, k);
        ePol = Mathf.Lerp(ePol, ePolTarget, k);
        hSpeed = Mathf.Lerp(hSpeed, hSpeedTarget, k);
        hPol = Mathf.Lerp(hPol, hPolTarget, k);

        if (electronBelt)
        {
            electronBelt.DriftSpeed = eSpeed;
            electronBelt.LateralPolarization = ePol;
        }
        if (holeBelt)
        {
            holeBelt.DriftSpeed = hSpeed;
            holeBelt.LateralPolarization = hPol;
        }

        activeDriftSpeed = mode == CarrierMode.Negative ? eSpeed : hSpeed;
        activePolarization = mode == CarrierMode.Negative ? ePol : hPol;
    }
}