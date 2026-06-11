//using UnityEngine;

//public class ChargeParticle : MonoBehaviour
//{
//    public enum CarrierType
//    {
//        Electron,
//        Hole
//    }

//    public CarrierType Carrier { get; private set; }

//    public Pack ParentPack { get; private set; }

//    public Vector3 LocalOffsetFromPackCenter { get; private set; }

//    public void Initialize(
//        CarrierType carrierType,
//        Vector3 localOffset,
//        Pack parentPack)
//    {
//        Carrier = carrierType;
//        LocalOffsetFromPackCenter = localOffset;
//        ParentPack = parentPack;
//    }

//    public void SetParentPack(Pack parentPack)
//    {
//        ParentPack = parentPack;

//        if (ParentPack != null)
//        {
//            LocalOffsetFromPackCenter = transform.position - ParentPack.Center;
//        }
//    }

//    public void ClearParentPack()
//    {
//        ParentPack = null;
//        LocalOffsetFromPackCenter = Vector3.zero;
//    }

//    public void ChangePack(Pack newPack)
//    {
//        if (ParentPack == newPack) return;

//        ParentPack?.RemoveCharge(this);

//        newPack?.AddCharge(this);
//    }

//    public void SetLocalOffset(Vector3 localOffset)
//    {
//        LocalOffsetFromPackCenter = localOffset;
//    }

//    public void SetPositionFromPackCenter(Vector3 packCenter, Vector3 dynamicOffset)
//    {
//        transform.position = packCenter + LocalOffsetFromPackCenter + dynamicOffset;
//    }
//}