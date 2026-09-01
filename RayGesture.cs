using UnityEngine;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using UnityEngine.Events;

public class RayGesture : MonoBehaviour
{
    [Header("検出感度（大きいほど高く上げないと反応しない）")]
    [Range(0f, 0.3f)] public float raiseThreshold = 0.1f;

    [Header("信頼度フィルタ")]
    [Range(0f, 1f)] public float visibilityThreshold = 0.5f;

    [Header("エフェクト（後で繋ぎます）")]
    public UnityEvent onRightHandRaised;
    public UnityEvent onLeftHandRaised;

    // 状態フラグ（押しっぱなしで連発しないようにする）
    private bool rightHandRaised = false;
    private bool leftHandRaised = false;

    public void OnPoseDataReceived(PoseLandmarkerResult result)
    {
        if (result.poseLandmarks == null || result.poseLandmarks.Count == 0) return;

        var landmarks = result.poseLandmarks[0].landmarks;
        if (landmarks.Count < 33) return;

        // 0:鼻, 15:左手首, 16:右手首
        var nose = landmarks[0];
        var rightWrist = landmarks[16];
        var leftWrist = landmarks[15];

        // 鼻が見えていなければ基準が取れないので何もしない
        if (nose.visibility < visibilityThreshold) return;

        // MediaPipeのY座標は上が0、下が1。手首が鼻より上＝Y値が小さい

        // --- 右手 ---
        if (rightWrist.visibility >= visibilityThreshold)
        {
            bool isUp = rightWrist.y < nose.y - raiseThreshold;

            if (isUp && !rightHandRaised)
            {
                rightHandRaised = true;
                OnRightHandRaised();
            }
            else if (!isUp)
            {
                rightHandRaised = false;
            }
        }

        // --- 左手 ---
        if (leftWrist.visibility >= visibilityThreshold)
        {
            bool isUp = leftWrist.y < nose.y - raiseThreshold;

            if (isUp && !leftHandRaised)
            {
                leftHandRaised = true;
                OnLeftHandRaised();
            }
            else if (!isUp)
            {
                leftHandRaised = false;
            }
        }
    }

    void OnRightHandRaised()
    {
        Debug.Log("★右手を上げた！");
        onRightHandRaised?.Invoke();
    }

    void OnLeftHandRaised()
    {
        Debug.Log("★左手を上げた！");
        onLeftHandRaised?.Invoke();
    }
}