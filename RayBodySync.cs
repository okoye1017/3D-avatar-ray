            using UnityEngine;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Components.Containers;

public class RayBodySync : MonoBehaviour
{
    [Header("ray をセットしてね")]
    public Animator rayAnimator;

    [Header("スムージング")]
    [Range(0.01f, 1f)] public float smoothSpeed = 0.15f;

    [Header("ノイズ除去")]
    [Range(0f, 0.05f)] public float noiseThreshold = 0.01f;

    [Header("肩の傾き強さ")]
    [Range(0f, 2f)] public float shoulderTiltStrength = 1.0f;

    [Header("体の左右移動強さ")]
    [Range(0f, 2f)] public float bodyShiftStrength = 1.0f;

    [Header("信頼度フィルタ（低いほど許容）")]
    [Range(0f, 1f)] public float visibilityThreshold = 0.5f;

    private Transform leftUpperArm;
    private Transform leftLowerArm;
    private Transform rightUpperArm;
    private Transform rightLowerArm;
    private Transform spine;
    private Transform chest;

    private Quaternion leftUpperArmTarget;
    private Quaternion leftLowerArmTarget;
    private Quaternion rightUpperArmTarget;
    private Quaternion rightLowerArmTarget;
    private Quaternion spineTarget;
    private Quaternion chestTarget;

    private Vector3 bodyShiftTarget;
    private Vector3 initialPosition;

    private bool hasData = false;

    void Start()
    {
        if (rayAnimator != null)
        {
            leftUpperArm = rayAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArm = rayAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            rightUpperArm = rayAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm = rayAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            spine = rayAnimator.GetBoneTransform(HumanBodyBones.Spine);
            chest = rayAnimator.GetBoneTransform(HumanBodyBones.Chest);

            if (leftUpperArm) leftUpperArmTarget = leftUpperArm.rotation;
            if (leftLowerArm) leftLowerArmTarget = leftLowerArm.rotation;
            if (rightUpperArm) rightUpperArmTarget = rightUpperArm.rotation;
            if (rightLowerArm) rightLowerArmTarget = rightLowerArm.rotation;
            if (spine) spineTarget = spine.rotation;
            if (chest) chestTarget = chest.rotation;

            initialPosition = rayAnimator.transform.position;
            bodyShiftTarget = initialPosition;
        }
    }

    public void OnPoseDataReceived(PoseLandmarkerResult result)
    {
        if (result.poseLandmarks == null || result.poseLandmarks.Count == 0) return;

        var landmarks = result.poseLandmarks[0].landmarks;
        if (landmarks.Count < 33) return;

        // 腕（信頼度が低い関節は前回の姿勢を維持）
        rightUpperArmTarget = CalcRotation(landmarks[12], landmarks[14], Vector3.right, rightUpperArmTarget);
        rightLowerArmTarget = CalcRotation(landmarks[14], landmarks[16], Vector3.right, rightLowerArmTarget);
        leftUpperArmTarget  = CalcRotation(landmarks[11], landmarks[13], Vector3.left,  leftUpperArmTarget);
        leftLowerArmTarget  = CalcRotation(landmarks[13], landmarks[15], Vector3.left,  leftLowerArmTarget);

        // 肩が両方見えているときだけ、傾きと左右移動を更新する
        if (landmarks[11].visibility >= visibilityThreshold &&
            landmarks[12].visibility >= visibilityThreshold)
        {
            float leftShoulderY = landmarks[11].y;
            float rightShoulderY = landmarks[12].y;
            float tiltAngle = (rightShoulderY - leftShoulderY) * 90f * shoulderTiltStrength;

            if (spine) spineTarget = Quaternion.Euler(0f, 0f, tiltAngle);
            if (chest) chestTarget = Quaternion.Euler(0f, 0f, tiltAngle * 0.5f);

            float shoulderCenterX = (landmarks[11].x + landmarks[12].x) / 2f;
            float shift = (shoulderCenterX - 0.5f) * 2f * bodyShiftStrength;
            bodyShiftTarget = initialPosition + new Vector3(shift, 0f, 0f);
        }

        hasData = true;
    }

    Quaternion CalcRotation(NormalizedLandmark start, NormalizedLandmark end, Vector3 baseDir, Quaternion current)
    {
        // どちらかの関節の信頼度が低ければ、前回の姿勢を維持する
        if (start.visibility < visibilityThreshold || end.visibility < visibilityThreshold)
        {
            return current;
        }

        Vector3 direction = new Vector3(
            end.x - start.x,
            -(end.y - start.y),
            end.z - start.z
        ).normalized;

        if (direction.magnitude < noiseThreshold) return current;

        return Quaternion.FromToRotation(baseDir, direction);
    }

    void LateUpdate()
    {
        if (!hasData) return;

        // フレームレートに依存しない補間量
        float t = 1f - Mathf.Exp(-smoothSpeed * 60f * Time.deltaTime);

        if (rightUpperArm) rightUpperArm.localRotation = Quaternion.Slerp(
            rightUpperArm.localRotation,
            Quaternion.Inverse(rightUpperArm.parent.rotation) * rightUpperArmTarget,
            t);
        if (rightLowerArm) rightLowerArm.localRotation = Quaternion.Slerp(
            rightLowerArm.localRotation,
            Quaternion.Inverse(rightLowerArm.parent.rotation) * rightLowerArmTarget,
            t);
        if (leftUpperArm) leftUpperArm.localRotation = Quaternion.Slerp(
            leftUpperArm.localRotation,
            Quaternion.Inverse(leftUpperArm.parent.rotation) * leftUpperArmTarget,
            t);
        if (leftLowerArm) leftLowerArm.localRotation = Quaternion.Slerp(
            leftLowerArm.localRotation,
            Quaternion.Inverse(leftLowerArm.parent.rotation) * leftLowerArmTarget,
            t);

        if (spine) spine.localRotation = Quaternion.Slerp(
            spine.localRotation, spineTarget, t);
        if (chest) chest.localRotation = Quaternion.Slerp(
            chest.localRotation, chestTarget, t);

        rayAnimator.transform.position = Vector3.Lerp(
            rayAnimator.transform.position, bodyShiftTarget, t);
    }
}