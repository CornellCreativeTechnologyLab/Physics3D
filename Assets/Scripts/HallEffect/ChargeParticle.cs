using UnityEngine;

public class ChargeParticle : MonoBehaviour
{
    public enum CarrierType { Electron, Hole }

    public CarrierType Carrier { get; set; }
    public Vector3 Velocity { get; set; }
    //public float MaxLifetime { get; set; } 
    public float Age { get; set; }

    public void Initialize(CarrierType carrierType, Vector3 initialVelocity, float lifetime)
    {
        Carrier = carrierType;
        Velocity = initialVelocity;
        //MaxLifetime = Mathf.Max(0.01f, lifetime);
        Age = 0f;
    }
}