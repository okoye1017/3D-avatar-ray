using System.Collections;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Events;

namespace Mediapipe.Unity.Sample.PoseLandmarkDetection
{
    [System.Serializable]
    public class PoseResultEvent : UnityEvent<PoseLandmarkerResult> { }

    public class PoseLandmarkerRunner : VisionTaskApiRunner<PoseLandmarker>
    {
        [Header("Settings")]
        [SerializeField] private PoseLandmarkerResultAnnotationController _poseLandmarkerResultAnnotationController;
        public PoseResultEvent OnPoseResultAction;

        private Experimental.TextureFramePool _textureFramePool;
        [HideInInspector] public PoseLandmarkDetectionConfig config = new PoseLandmarkDetectionConfig();

        private PoseLandmarkerResult _safeResult;
        private bool _isResultReady = false;

        private void Update()
        {
            if (_isResultReady)
            {
                OnPoseResultAction?.Invoke(_safeResult);
                _isResultReady = false;
            }
        }

        public override void Stop()
        {
            base.Stop();
            _textureFramePool?.Dispose();
            _textureFramePool = null;
        }

        protected override IEnumerator Run()
        {
            string modelPath = "pose_landmarker_full.bytes";
            yield return AssetLoader.PrepareAssetAsync(modelPath);

            var options = new PoseLandmarkerOptions(
                new Mediapipe.Tasks.Core.BaseOptions(
                    Mediapipe.Tasks.Core.BaseOptions.Delegate.CPU,
                    modelAssetPath: modelPath),
                runningMode: Tasks.Vision.Core.RunningMode.LIVE_STREAM,
                numPoses: 1,
                minPoseDetectionConfidence: 0.5f,
                minPosePresenceConfidence: 0.5f,
                minTrackingConfidence: 0.5f,
                outputSegmentationMasks: false,
                resultCallback: OnPoseLandmarkDetectionOutput
            );

            taskApi = PoseLandmarker.CreateFromOptions(options, GpuManager.GpuResources);
            var imageSource = ImageSourceProvider.ImageSource;

            var elapsed = 0f;
while (!imageSource.isPrepared && elapsed < 15f)
{
    elapsed += Time.deltaTime;
    yield return null;
}

if (!imageSource.isPrepared)
{
    Debug.LogError("Pose: ImageSource not prepared, exiting...");
    yield break;
}
            _textureFramePool = new Experimental.TextureFramePool(
                imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);
            screen.Initialize(imageSource);

            if (_poseLandmarkerResultAnnotationController != null)
            {
                SetupAnnotationController(_poseLandmarkerResultAnnotationController, imageSource);
            }

            var waitForEndOfFrame = new WaitForEndOfFrame();

            while (true)
            {
                if (isPaused) { yield return new WaitWhile(() => isPaused); }
                if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
                {
                    yield return waitForEndOfFrame;
                    continue;
                }

                var req = textureFrame.ReadTextureAsync(
                    imageSource.GetCurrentTexture(),
                    imageSource.GetTransformationOptions().flipHorizontally,
                    imageSource.GetTransformationOptions().flipVertically);
                yield return new WaitUntil(() => req.done);

                var image = textureFrame.BuildCPUImage();
                textureFrame.Release();

                taskApi.DetectAsync(image, GetCurrentTimestampMillisec(), null);
            }
        }

        private void OnPoseLandmarkDetectionOutput(PoseLandmarkerResult result, Image image, long timestamp)
        {
            Debug.Log($"pose received: {System.DateTime.Now:HH:mm:ss.fff}");
            if (_poseLandmarkerResultAnnotationController != null)
                _poseLandmarkerResultAnnotationController.DrawLater(result);
            _safeResult = result;
            _isResultReady = true;
        }
    }
}        
