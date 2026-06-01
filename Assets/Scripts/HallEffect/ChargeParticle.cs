using UnityEngine;

public class ChargeParticle : MonoBehaviour
{
    public enum CarrierType
    {
        Electron,
        Hole
    }

    public CarrierType Carrier { get; private set; }
    public Vector3 LocalOffsetFromPackCenter { get; private set; }

    public void Initialize(CarrierType carrierType, Vector3 localOffset)
    {
        Carrier = carrierType;
        LocalOffsetFromPackCenter = localOffset;
    }

    public void SetPositionFromPackCenter(Vector3 packCenter, Vector3 dynamicOffset)
    {
        transform.position = packCenter + LocalOffsetFromPackCenter + dynamicOffset;
    }
}