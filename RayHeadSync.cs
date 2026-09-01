using UnityEngine;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using VRM;

public class RayHeadSync : MonoBehaviour
{
    [Header("ray をセットしてね")]
    public Animator rayAnimator;
    
    private VRMBlendShapeProxy blendShapeProxy;
    private Transform headBone;
    private Quaternion targetRot;
    private bool hasData = false;

    [Header("表情の強さ調整")]
    [Range(0, 2f)] public float blinkSensitivity = 1.2f;
    [Range(0, 2f)] public float mouthSensitivity = 1.5f;

    void Start()
    {
        if (rayAnimator != null)
        {
            headBone = rayAnimator.GetBoneTransform(HumanBodyBones.Head);
            blendShapeProxy = rayAnimator.GetComponent<VRMBlendShapeProxy>();
        }
    }

    public void OnFaceDataReceived(FaceLandmarkerResult result)
    {
        if (result.faceLandmarks != null && result.faceLandmarks.Count > 0)
        {
            var landmarks = result.faceLandmarks[0].landmarks;
            Vector3 nose = new Vector3(landmarks[4].x, landmarks[4].y, landmarks[4].z);
            Vector3 left = new Vector3(landmarks[234].x, landmarks[234].y, landmarks[234].z);
            Vector3 right = new Vector3(landmarks[454].x, landmarks[454].y, landmarks[454].z);
            Vector3 forward = (nose - (left + right) / 2f).normalized;
            Vector3 rightVec = (right - left).normalized;
            Vector3 up = Vector3.Cross(rightVec, forward).normalized;
            
            if (forward != Vector3.zero && up != Vector3.zero)
            {
                targetRot = Quaternion.LookRotation(forward, up) * Quaternion.Euler(0, 180f, 0);
                hasData = true;
            }

            if (result.faceBlendshapes != null && result.faceBlendshapes.Count > 0 && blendShapeProxy != null)
            {
                var shapes = result.faceBlendshapes[0].categories;
                float scoreBlinkL = 0, scoreBlinkR = 0, scoreMouth = 0;
                
                foreach (var category in shapes)
                {
                    if (category.categoryName == "eyeBlinkLeft") scoreBlinkL = category.score;
                    if (category.categoryName == "eyeBlinkRight") scoreBlinkR = category.score;
                    if (category.categoryName == "jawOpen") scoreMouth = category.score;
                }

                blendShapeProxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink_L), scoreBlinkL * blinkSensitivity);
                blendShapeProxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink_R), scoreBlinkR * blinkSensitivity);
                blendShapeProxy.ImmediatelySetValue(BlendShapeKey.CreateFromPreset(BlendShapePreset.A), scoreMouth * mouthSensitivity);
            }
        }
    }

    void LateUpdate()
    {
        if (headBone == null || !hasData) return;
        headBone.localRotation = Quaternion.Slerp(headBone.localRotation, targetRot, 0.2f);
    }
}