using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Unity.Sample;

public class PoseController : MonoBehaviour
{
    [Header("Pose Detection Settings")]
    [SerializeField] private float confidenceThreshold = 0.5f;
    [SerializeField] private float minPoseDetectionConfidence = 0.5f;
    [SerializeField] private float minTrackingConfidence = 0.5f;

    [Header("Movement Settings")]
    [SerializeField] private float movementSensitivity = 3.5f;
    [SerializeField] private float forwardBackDeadzone = 3f; // degrees - bigger = more neutral zone
    [SerializeField] private float leftRightDeadzone = 3f; // degrees - bigger = more neutral zone
    [SerializeField] private float maxLeanAngle = 20f; // angle for max speed
    [SerializeField] private float pitchSensitivity = 300f; // Multiplier for nose-to-shoulder distance

    [Header("Swing Detection")]
    [SerializeField] private float swingSpeedThreshold = 0.5f;
    [SerializeField] private float minArmExtension = 0.3f;

    [Header("Bootstrap (Required)")]
    [SerializeField] private GameObject bootstrapPrefab;

    // Landmark indices
    private const int NOSE = 0;
    private const int LEFT_SHOULDER = 11;
    private const int RIGHT_SHOULDER = 12;
    private const int RIGHT_ELBOW = 14;
    private const int RIGHT_WRIST = 16;
    private const int MOUTH_LEFT = 9;
    private const int MOUTH_RIGHT = 10;

    // Events
    public event Action<Vector2> OnMovementDetected;
    public event Action<float> OnSwingDetected;

    // Internal state
    private WebCamTexture _webCamTexture;
    private PoseLandmarker _poseLandmarker;
    private Mediapipe.Unity.Experimental.TextureFramePool _textureFramePool;
    private bool _isRunning = false;
    private bool _useExternalWebCam = false;

    // Swing tracking
    private Vector3 _previousWristPosition;
    private float _previousTime;

    // Calibration
    private float _calibratedNoseToShoulderY = -1f;
    private float _calibratedTiltAngle = 0f;
    private bool _isCalibrated = false;

    // Smoothing
    private Vector2 _smoothedInput = Vector2.zero;
    [SerializeField] private float inputSmoothing = 0.5f; // Higher = more responsive (0-1)

    // Public accessors
    public bool IsRunning => _isRunning;
    public WebCamTexture WebCamTexture => _webCamTexture;

    // Public methods
    public void Recalibrate()
    {
        _isCalibrated = false;
        _smoothedInput = Vector2.zero;
        Debug.Log("PoseController: Recalibration requested. Move to neutral position.");
    }

    private IEnumerator Start()
    {
        // Initialize Bootstrap if needed
        var bootstrap = FindOrCreateBootstrap();
        if (bootstrap == null)
        {
            Debug.LogError("PoseController: Cannot start without Bootstrap. Please assign the Bootstrap prefab in the Inspector.");
            yield break;
        }
        yield return new WaitUntil(() => bootstrap.isFinished);

        // Load model
        yield return AssetLoader.PrepareAssetAsync("pose_landmarker_full.bytes");

        // Start webcam
        yield return InitializeWebCam();

        // Create pose landmarker
        InitializePoseLandmarker();

        _previousTime = Time.time;
        _isRunning = true;

        // Start processing loop
        StartCoroutine(ProcessFrames());
    }

    private Bootstrap FindOrCreateBootstrap()
    {
        var existing = GameObject.Find("Bootstrap");
        if (existing != null)
        {
            return existing.GetComponent<Bootstrap>();
        }

        if (bootstrapPrefab == null)
        {
            Debug.LogError("PoseController: Bootstrap prefab not assigned! Please assign it in the Inspector.");
            return null;
        }

        var bootstrapObj = Instantiate(bootstrapPrefab);
        bootstrapObj.name = "Bootstrap";
        DontDestroyOnLoad(bootstrapObj);
        return bootstrapObj.GetComponent<Bootstrap>();
    }

    private IEnumerator InitializeWebCam()
    {
        // Try to use existing ImageSource from MediaPipe if available
        var imageSource = ImageSourceProvider.ImageSource;
        if (imageSource != null && imageSource.isPrepared)
        {
            var existingTexture = imageSource.GetCurrentTexture() as WebCamTexture;
            if (existingTexture != null && existingTexture.isPlaying)
            {
                Debug.Log("PoseController: Using existing webcam from ImageSourceProvider");
                _webCamTexture = existingTexture;
                _textureFramePool = new Mediapipe.Unity.Experimental.TextureFramePool(_webCamTexture.width, _webCamTexture.height, TextureFormat.RGBA32, 10);
                _useExternalWebCam = true;
                yield break;
            }
        }

        // Request camera permission on Android
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
            yield return new WaitForSeconds(0.5f);
        }
#endif

        var devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("PoseController: No webcam found!");
            yield break;
        }

        Debug.Log($"PoseController: Starting webcam {devices[0].name}...");

        // Use first available camera
        _webCamTexture = new WebCamTexture(devices[0].name, 1280, 720, 30);
        _webCamTexture.Play();

        // Wait for webcam to initialize (longer timeout)
        int timeout = 500;
        while (_webCamTexture.width <= 16 && timeout > 0)
        {
            yield return null;
            timeout--;
        }

        if (_webCamTexture.width <= 16)
        {
            Debug.LogError($"PoseController: Failed to start webcam! Width={_webCamTexture.width}. Is another app using the camera?");
            yield break;
        }

        Debug.Log($"PoseController: Webcam started - {_webCamTexture.width}x{_webCamTexture.height}");

        // Create texture frame pool for MediaPipe
        _textureFramePool = new Mediapipe.Unity.Experimental.TextureFramePool(_webCamTexture.width, _webCamTexture.height, TextureFormat.RGBA32, 10);
    }

    private void InitializePoseLandmarker()
    {
        var options = new PoseLandmarkerOptions(
            new Mediapipe.Tasks.Core.BaseOptions(
                Mediapipe.Tasks.Core.BaseOptions.Delegate.CPU,
                modelAssetPath: "pose_landmarker_full.bytes"
            ),
            runningMode: RunningMode.IMAGE,
            numPoses: 1,
            minPoseDetectionConfidence: minPoseDetectionConfidence,
            minPosePresenceConfidence: confidenceThreshold,
            minTrackingConfidence: minTrackingConfidence,
            outputSegmentationMasks: false
        );

        _poseLandmarker = PoseLandmarker.CreateFromOptions(options);
        Debug.Log("PoseController: PoseLandmarker initialized");
    }

    private IEnumerator ProcessFrames()
    {
        var result = PoseLandmarkerResult.Alloc(1, false);
        var waitForEndOfFrame = new WaitForEndOfFrame();
        AsyncGPUReadbackRequest req = default;
        var waitUntilReqDone = new WaitUntil(() => req.done);

        while (_isRunning)
        {
            if (_webCamTexture == null || !_webCamTexture.isPlaying)
            {
                yield return null;
                continue;
            }

            // Get texture frame from pool
            if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
            {
                yield return waitForEndOfFrame;
                continue;
            }

            // Read texture asynchronously
            req = textureFrame.ReadTextureAsync(_webCamTexture, flipHorizontally: false, flipVertically: false);
            yield return waitUntilReqDone;

            if (req.hasError)
            {
                Debug.LogWarning("PoseController: Failed to read texture from webcam");
                continue;
            }

            // Build CPU image for MediaPipe
            var image = textureFrame.BuildCPUImage();
            textureFrame.Release();

            // Run detection
            var imageProcessingOptions = new ImageProcessingOptions(rotationDegrees: 0);
            if (_poseLandmarker.TryDetect(image, imageProcessingOptions, ref result))
            {
                ProcessPoseResult(result);
            }
        }
    }

    private void ProcessPoseResult(PoseLandmarkerResult result)
    {
        if (result.poseLandmarks == null || result.poseLandmarks.Count == 0)
            return;

        var landmarks = result.poseLandmarks[0];

        // Process movement
        ProcessMovement(landmarks);

        // Process swing
        ProcessSwing(landmarks);
    }

    private void ProcessMovement(Mediapipe.Tasks.Components.Containers.NormalizedLandmarks landmarks)
    {
        if (landmarks.landmarks[NOSE].presence < confidenceThreshold ||
            landmarks.landmarks[LEFT_SHOULDER].presence < confidenceThreshold ||
            landmarks.landmarks[RIGHT_SHOULDER].presence < confidenceThreshold ||
            landmarks.landmarks[MOUTH_LEFT].presence < confidenceThreshold ||
            landmarks.landmarks[MOUTH_RIGHT].presence < confidenceThreshold)
            return;

        Vector3 nose = LandmarkToVector(landmarks.landmarks[NOSE]);
        Vector3 leftShoulder = LandmarkToVector(landmarks.landmarks[LEFT_SHOULDER]);
        Vector3 rightShoulder = LandmarkToVector(landmarks.landmarks[RIGHT_SHOULDER]);
        Vector3 mouthLeft = LandmarkToVector(landmarks.landmarks[MOUTH_LEFT]);
        Vector3 mouthRight = LandmarkToVector(landmarks.landmarks[MOUTH_RIGHT]);


        Vector2 mouthLine = new Vector2(mouthRight.x - mouthLeft.x, mouthRight.y - mouthLeft.y);
        Vector2 horizontalRef = new Vector2(1, 0); // Perfectly horizontal reference


        Vector3 shoulderCenter = (leftShoulder + rightShoulder) * 0.5f;

        // Left/Right angle (tilt)
        float tiltAngle = Vector2.SignedAngle(horizontalRef, mouthLine);

        // Forward/Back - nose to shoulder Y distance
        float noseToShoulderY = shoulderCenter.y - nose.y;

        // Calibrate on first valid frame
        if (!_isCalibrated)
        {
            _calibratedNoseToShoulderY = noseToShoulderY;
            _calibratedTiltAngle = tiltAngle;
            _isCalibrated = true;
            Debug.Log($"PoseController: Calibrated. NoseToShoulderY={_calibratedNoseToShoulderY:F3}, TiltAngle={_calibratedTiltAngle:F1}");
            return;
        }

        // Calculate deltas from calibrated neutral position
        float tiltDelta = tiltAngle - _calibratedTiltAngle;
        while (tiltDelta > 180f) tiltDelta -= 360f;
        while (tiltDelta < -180f) tiltDelta += 360f;
        float pitchDelta = (noseToShoulderY - _calibratedNoseToShoulderY) * pitchSensitivity;

        // Apply deadzone and calculate left/right speed
        float moveX = 0f;
        if (Mathf.Abs(tiltDelta) > leftRightDeadzone)
        {
            float adjustedAngle = Mathf.Abs(tiltDelta) - leftRightDeadzone;
            float normalizedAngle = Mathf.Clamp01(adjustedAngle / maxLeanAngle);
            // Use power curve for better control at small angles
            float speed = Mathf.Pow(normalizedAngle, 1.2f) * movementSensitivity;
            moveX = Mathf.Sign(tiltDelta) * speed;
        }

        // Apply deadzone and calculate forward/back speed
        float moveY = 0f;
        if (Mathf.Abs(pitchDelta) > forwardBackDeadzone)
        {
            float adjustedDelta = Mathf.Abs(pitchDelta) - forwardBackDeadzone;
            float normalizedDelta = Mathf.Clamp01(adjustedDelta / maxLeanAngle);
            // Use power curve for better control
            float speed = Mathf.Pow(normalizedDelta, 1.2f) * movementSensitivity;
            moveY = Mathf.Sign(pitchDelta) * speed;
        }

        // Smooth the input to reduce jitter
        Vector2 targetInput = new Vector2(moveX, moveY);
        _smoothedInput = Vector2.Lerp(_smoothedInput, targetInput, inputSmoothing);

        // Only send movement if above a small threshold (anti-jitter)
        if (_smoothedInput.magnitude > 0.01f)
        {
            OnMovementDetected?.Invoke(_smoothedInput);
        }
        else
        {
            OnMovementDetected?.Invoke(Vector2.zero);
        }
    }

    private void ProcessSwing(Mediapipe.Tasks.Components.Containers.NormalizedLandmarks landmarks)
    {
        if (landmarks.landmarks[RIGHT_WRIST].presence < confidenceThreshold ||
            landmarks.landmarks[RIGHT_ELBOW].presence < confidenceThreshold ||
            landmarks.landmarks[RIGHT_SHOULDER].presence < confidenceThreshold)
            return;

        Vector3 shoulder = LandmarkToVector(landmarks.landmarks[RIGHT_SHOULDER]);
        Vector3 elbow = LandmarkToVector(landmarks.landmarks[RIGHT_ELBOW]);
        Vector3 wrist = LandmarkToVector(landmarks.landmarks[RIGHT_WRIST]);

        float deltaTime = Time.time - _previousTime;
        if (deltaTime > 0)
        {
            Vector3 velocity = (wrist - _previousWristPosition) / deltaTime;
            float speed = velocity.magnitude;

            float armExtension = CalculateArmExtension(shoulder, elbow, wrist);

            if (speed > swingSpeedThreshold && armExtension > minArmExtension)
            {
                float power = Mathf.Clamp01(speed / (swingSpeedThreshold * 3f));
                OnSwingDetected?.Invoke(power);
            }
        }

        _previousWristPosition = wrist;
        _previousTime = Time.time;
    }

    private Vector3 LandmarkToVector(Mediapipe.Tasks.Components.Containers.NormalizedLandmark landmark)
    {
        return new Vector3(landmark.x, landmark.y, landmark.z);
    }

    private float CalculateArmExtension(Vector3 shoulder, Vector3 elbow, Vector3 wrist)
    {
        Vector3 upperArm = elbow - shoulder;
        Vector3 forearm = wrist - elbow;
        float angle = Vector3.Angle(upperArm, forearm);
        return Mathf.Clamp01((angle - 90f) / 90f);
    }

    private void OnDestroy()
    {
        _isRunning = false;

        // Only stop webcam if we created it ourselves
        if (_webCamTexture != null && !_useExternalWebCam)
        {
            _webCamTexture.Stop();
        }
        _webCamTexture = null;

        _poseLandmarker?.Close();
        _poseLandmarker = null;

        _textureFramePool?.Dispose();
        _textureFramePool = null;
    }
}
