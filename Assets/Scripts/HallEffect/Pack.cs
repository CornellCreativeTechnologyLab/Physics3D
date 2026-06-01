using System;
using System.Collections.Generic;
using UnityEngine;

public class Pack : MonoBehaviour
{
    [Header("Pack Data")]
    [SerializeField] private List<ChargeParticle> charges = new();

    [Header("Physics")]
    [SerializeField] private ChargeParticle.CarrierType carrierType;
    [SerializeField] private Vector3 velocity;
    [SerializeField] private Vector3 forwardDirection;
    [SerializeField] private Vector3 electricField;
    [SerializeField] private Vector3 magneticField;

    [Header("Tuning")]
    [SerializeField] private float chargeMagnitude = 1f;
    [SerializeField] private float mass = 1f;
    [SerializeField] private float deflectionMultiplier = 2f;

    [SerializeField] private float deformationScale = 0.2f;
    [SerializeField] private float stiffness = 12f;
    [Range(0f, 1f)]
    [SerializeField] private float deformationDamping = 0.85f;

    // runtime-only
    private float initialForwardSpeed;
    private Vector3 deformation;
    private Vector3 deformationVelocity;

    public Vector3 Center => transform.position;
    public Vector3 Velocity => velocity;
    public ChargeParticle.CarrierType Carrier => carrierType;
    public float CarrierChargeSign => carrierType == ChargeParticle.CarrierType.Hole ? 1f : -1f;

    public void Initialize(
        Vector3 centerOfPack,
        List<ChargeParticle> packCharges,
        ChargeParticle.CarrierType type,
        Vector3 initialVelocity,
        Vector3 eField,
        Vector3 bField)
    {
        transform.position = centerOfPack;

        charges = packCharges ?? new List<ChargeParticle>();
        carrierType = type;
        velocity = initialVelocity;
        forwardDirection = initialVelocity.sqrMagnitude > 1e-10f ? initialVelocity.normalized : Vector3.right;
        initialForwardSpeed = initialVelocity.magnitude;
        electricField = eField;
        magneticField = bField;

        SetupChargeOffsets();
    }

    private void UpdateChargePositions()
    {
        if (charges == null) return;

        for (int i = 0; i < charges.Count; i++)
        {
            ChargeParticle charge = charges[i];
            if (charge == null) continue;

            Vector3 baseOffset = charge.LocalOffsetFromPackCenter;

            float side = i % 2 == 0 ? 1f : -1f;

            Vector3 stretchOffset = Vector3.zero;

            if (deformation.sqrMagnitude > 0.0001f)
            {
                Vector3 deformationDir = deformation.normalized;

                float alignment = Vector3.Dot(baseOffset.normalized, deformationDir);

                stretchOffset =
                    deformationDir *
                    deformation.magnitude *
                    alignment *
                    0.35f;

                // Small sideways spreading so it feels cloud-like, not just a line.
                Vector3 sideways = Vector3.Cross(deformationDir, Vector3.up);

                if (sideways.sqrMagnitude < 0.001f)
                    sideways = Vector3.Cross(deformationDir, Vector3.right);

                sideways.Normalize();

                stretchOffset += sideways * side * deformation.magnitude * 0.08f;
            }

            charge.SetPositionFromPackCenter(transform.position, stretchOffset);
        }
    }

    private void SetupChargeOffsets()
    {
        if (charges == null || charges.Count == 0) return;

        for (int i = 0; i < charges.Count; i++)
        {
            ChargeParticle charge = charges[i];
            if (charge == null) continue;

            Vector3 localOffset = charge.transform.position - transform.position;
            charge.Initialize(carrierType, localOffset);
        }
    }

    public void SetFields(Vector3 eField, Vector3 bField)
    {
        electricField = eField;
        magneticField = bField;
    }

    public void SetVelocity(Vector3 newVelocity)
    {
        velocity = newVelocity;
    }

    public void KillOutwardLateral(Vector3 axis)
    {
        float latVel = Vector3.Dot(velocity, axis);
        velocity -= axis * latVel;
    }

    public void AddCharge(ChargeParticle charge)
    {
        if (charge == null) return;

        if (charges == null) charges = new List<ChargeParticle>();
        charges.Add(charge);

        Vector3 localOffset = charge.transform.position - transform.position;
        charge.Initialize(carrierType, localOffset);
    }

    public void Despawn()
    {
        if (charges != null)
        {
            for (int i = 0; i < charges.Count; i++)
            {
                ChargeParticle charge = charges[i];
                if (charge == null) continue;
                charge.gameObject.SetActive(false);
            }

            charges.Clear();
        }

        velocity = Vector3.zero;
        electricField = Vector3.zero;
        magneticField = Vector3.zero;

        deformation = Vector3.zero;
        deformationVelocity = Vector3.zero;

        gameObject.SetActive(false);
    }

    private Vector3 previousForce;

    private void UpdateCloudDeformation(Vector3 force, float dt)
    {
        if (dt <= 0f) return;

        Vector3 forceChange = force - previousForce;
        previousForce = force;

        Vector3 targetDeformation = Vector3.zero;

        if (force.sqrMagnitude > 0.0001f)
        {
            Vector3 forceDir = force.normalized;

            float forceAmount = Mathf.Min(force.magnitude * deformationScale, 1.5f);

            // Basic stretch in force direction.
            targetDeformation = forceDir * forceAmount;

            // Extra visual kick when direction changes suddenly.
            if (forceChange.sqrMagnitude > 0.0001f)
            {
                targetDeformation += forceChange.normalized * Mathf.Min(forceChange.magnitude * deformationScale, 1.0f);
            }
        }

        deformationVelocity += (targetDeformation - deformation) * stiffness * dt;
        deformationVelocity *= deformationDamping;

        deformation += deformationVelocity * dt;
    }

    private Vector3 ComputeLorentzForce()
    {
        float chargeSign = carrierType == ChargeParticle.CarrierType.Hole ? 1f : -1f;
        float q = chargeSign * chargeMagnitude;

        // deflectionMultiplier scales only the magnetic term for visual tuning
        Vector3 force = q * electricField + q * Vector3.Cross(velocity, magneticField) * deflectionMultiplier;
        return force;
    }

    public void Step(float dt)
    {
        if (dt <= 0f) return;

        Vector3 force = ComputeLorentzForce();
        velocity += (force / Mathf.Max(mass, 1e-6f)) * dt;

        // Drude model: pin forward drift speed; only lateral velocity accumulates
        float fwdComp = Vector3.Dot(velocity, forwardDirection);
        velocity += forwardDirection * (initialForwardSpeed - fwdComp);

        transform.position += velocity * dt;

        UpdateCloudDeformation(force, dt);
        UpdateChargePositions();
    }
}