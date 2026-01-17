using UnityEngine;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using Unity.VisualScripting;

public class GestureDetector : MonoBehaviour
{
    // Landmark indices for right arm
    private const int RIGHT_SHOULDER = 12;
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
    public event SwingDetected OnSwingDetected;

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