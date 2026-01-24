using UnityEngine;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using Unity.VisualScripting;
using Unity.Mathematics;

public class GestureDetector : MonoBehaviour
{
    // Landmark indices for right arm
    // Landmark indices
    private const int NOSE = 0;
    private const int LEFT_SHOULDER = 11;
    private const int RIGHT_SHOULDER = 12;
    private const int MOUTH_LEFT = 9;
    private const int MOUTH_RIGHT = 10;
    private const int RIGHT_ELBOW = 14;
    private const int RIGHT_WRIST = 16;

    // Swing detection parameters
    [SerializeField] private float swingSpeedThreshold = 0.5f;
    [SerializeField] private float minArmExtension = 0.3f;
    [SerializeField] private float confidenceThreshold = 0.7f;

    // Track previous positions for velocity calculation
    private Vector3 previousWristPosition;
    private float previousTime;

    // Events
    public delegate void SwingDetected(float power);
    // Events

    public event SwingDetected OnSwingDetected;
    public event System.Action<Vector2> OnMovementDetected;

    [Header("Movement Settings")]
    [SerializeField] private float leaningThreshold = 0.1f;
    [SerializeField] private float movementSensitivity = 2.0f;
    [SerializeField] private float forwardLeanRatioResting = 0.5f; // Calibrate this value

    void Start()
    {
        previousTime = Time.time;
    }

    public void ProcessPoseResult(PoseLandmarkerResult result)
    {
        if (result.poseLandmarks == null || result.poseLandmarks.Count == 0)
            return;

        // Get landmarks for the first detected person
        var landmarks = result.poseLandmarks[0];
        // Check if arm landmarks are visible with good confidence
        if (landmarks.landmarks[RIGHT_WRIST].presence < confidenceThreshold ||
            landmarks.landmarks[RIGHT_ELBOW].presence < confidenceThreshold ||
            landmarks.landmarks[RIGHT_SHOULDER].presence < confidenceThreshold)
            return;

        // Get landmark positions
        Vector3 shoulder = LandmarkToVector(landmarks.landmarks[RIGHT_SHOULDER]);
        Vector3 elbow = LandmarkToVector(landmarks.landmarks[RIGHT_ELBOW]);
        Vector3 wrist = LandmarkToVector(landmarks.landmarks[RIGHT_WRIST]);

        // Calculate wrist velocity
        float deltaTime = Time.time - previousTime;
        if (deltaTime > 0)
        {
            Vector3 velocity = (wrist - previousWristPosition) / deltaTime;
            float speed = velocity.magnitude;

            // Check if arm is extended (not bent too much)
            float armExtension = CalculateArmExtension(shoulder, elbow, wrist);

            // Detect swing gesture
            if (speed > swingSpeedThreshold && armExtension > minArmExtension)
            {
                // Calculate swing power based on speed
                float power = Mathf.Clamp01(speed / (swingSpeedThreshold * 3f));
                OnSwingDetected?.Invoke(power);
            }
        }

        // Update previous state
        previousWristPosition = wrist;
        previousTime = Time.time;

        // --- Movement Logic ---
        ProcessMovement(landmarks);
    }

    private void ProcessMovement(NormalizedLandmarks landmarks)
    {
        // Ensure required landmarks are present
        if (landmarks.landmarks[NOSE].presence < confidenceThreshold ||
            landmarks.landmarks[LEFT_SHOULDER].presence < confidenceThreshold ||
            landmarks.landmarks[RIGHT_SHOULDER].presence < confidenceThreshold)
            return;

        Vector3 nose = LandmarkToVector(landmarks.landmarks[NOSE]);
        Vector3 leftShoulder = LandmarkToVector(landmarks.landmarks[LEFT_SHOULDER]);
        Vector3 rightShoulder = LandmarkToVector(landmarks.landmarks[RIGHT_SHOULDER]);

        Vector3 mouthLeft = LandmarkToVector(landmarks.landmarks[MOUTH_LEFT]);
        Vector3 mouthRight = LandmarkToVector(landmarks.landmarks[MOUTH_RIGHT]);

        Vector3 shoulderCenter = (leftShoulder + rightShoulder) * 0.5f;
        float shoulderWidth = Vector3.Distance(leftShoulder, rightShoulder);

        if (shoulderWidth <= 0.001f) return;

        float noseToCenterDist = Vector3.Distance(nose, shoulderCenter);
        float currentRatio = noseToCenterDist / shoulderWidth;
        float moveY = (forwardLeanRatioResting - currentRatio) * movementSensitivity;

        Vector2 noseDir = new Vector2(nose.x - shoulderCenter.x, nose.y - shoulderCenter.y);
        Vector2 upVectorMP = nose - shoulderCenter;
        upVectorMP.Normalize();
        float angle = Vector2.SignedAngle(upVectorMP, noseDir);

        // Map angle to X movement. 
        // If angle is positive (Nose to the Right of Up), Move Right?
        float moveX = Mathf.Clamp(angle / 45f, -1f, 1f); // 45 degrees max lean

        // Debug.Log($"Ratio: {currentRatio:F3}, Angle: {angle:F1}");

        OnMovementDetected?.Invoke(new Vector2(moveX, moveY));
    }

    private Vector3 LandmarkToVector(NormalizedLandmark landmark)
    {
        return new Vector3(landmark.x, landmark.y, landmark.z);
    }

    private float CalculateArmExtension(Vector3 shoulder, Vector3 elbow, Vector3 wrist)
    {
        // Calculate the angle at the elbow
        Vector3 upperArm = elbow - shoulder;
        Vector3 forearm = wrist - elbow;

        float angle = Vector3.Angle(upperArm, forearm);

        // Extension: 180° = fully extended (1.0), 90° = bent (0.0)
        return Mathf.Clamp01((angle - 90f) / 90f);
    }
}