using UnityEngine;

namespace DJMaster
{
    // Turns a tracked hand's index fingertip into a Pointer. Works with both OVR and OpenXR hand skeletons.
    [DefaultExecutionOrder(-100)]
    public class HandPointerSource : MonoBehaviour
    {
        public OVRHand hand;
        public OVRSkeleton skeleton;
        [Tooltip("Small glowing sphere drawn on the fingertip so you can see exactly where you're touching.")]
        public Renderer tipMarker;
        public Color idleColor = new Color(0.6f, 0.9f, 1f, 0.6f);
        public Color touchColor = new Color(1f, 0.85f, 0.2f, 1f);

        Pointer pointer;
        Transform tip;
        MaterialPropertyBlock block;

        void OnEnable() => pointer = Pointers.Create();
        void OnDisable() { Pointers.Remove(pointer); pointer = null; }

        void LateUpdate()
        {
            if (tip == null) tip = FindTip();
            bool valid = tip != null && hand != null && hand.IsTracked && hand.IsDataHighConfidence && skeleton.IsDataValid;
            pointer.Active = valid;
            if (valid) pointer.Position = tip.position;

            if (tipMarker != null)
            {
                tipMarker.enabled = valid;
                if (valid)
                {
                    tipMarker.transform.position = tip.position;
                    block ??= new MaterialPropertyBlock();
                    block.SetColor("_Color", pointer.Owner != null ? touchColor : idleColor);
                    tipMarker.SetPropertyBlock(block);
                }
            }
        }

        Transform FindTip()
        {
            if (skeleton == null || !skeleton.IsInitialized || skeleton.Bones == null) return null;
            // OVR and OpenXR bone ids share numeric values, so pick the id for this skeleton's type.
            var type = skeleton.GetSkeletonType();
            bool openXR = type == OVRSkeleton.SkeletonType.XRHandLeft || type == OVRSkeleton.SkeletonType.XRHandRight;
            var tipId = openXR ? OVRSkeleton.BoneId.XRHand_IndexTip : OVRSkeleton.BoneId.Hand_IndexTip;
            foreach (var bone in skeleton.Bones)
                if (bone.Id == tipId)
                    return bone.Transform;
            return null;
        }
    }
}
