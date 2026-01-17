# Pose Detection for Hockey Gesture Control Tutorial

This tutorial will guide you through implementing pose detection to control hockey puck swinging using gestures in Unity with MediaPipe.

## Table of Contents
1. [Overview](#overview)
2. [Understanding the MediaPipe Integration](#understanding-the-mediapipe-integration)
3. [How Pose Detection Works](#how-pose-detection-works)
4. [Implementing Gesture Recognition](#implementing-gesture-recognition)
5. [Connecting Gestures to Game Actions](#connecting-gestures-to-game-actions)
6. [Complete Implementation Example](#complete-implementation-example)
7. [Testing and Debugging](#testing-and-debugging)

---

## Overview

Your hockey game uses **MediaPipe Unity Plugin** to detect 33 body landmarks in real-time from webcam input. This tutorial shows you how to convert these landmarks into hockey swing gestures.

**What you'll learn:**
- How MediaPipe pose detection works in your project
- How to access pose landmark data
- How to detect arm swing gestures
- How to trigger game actions from gestures

---

## Understanding the MediaPipe Integration

### Architecture

```
Webcam Input
    ↓
ImageSource (captures frames)
    ↓
PoseLandmarkerRunner (processes frames)
    ↓
PoseLandmarkerResult (33 landmarks with x,y,z coordinates)
    ↓
Your Game Logic (detect gestures)
    ↓
Player Actions (swing puck)
```

### Key Components

#### 1. PoseLandmarkerRunner
**Location:** `Assets/MediaPipeUnity/Samples/Scenes/Pose Landmark Detection/PoseLandmarkerRunner.cs`

This is the core component that:
- Captures webcam frames
- Sends frames to MediaPipe for processing
- Returns pose detection results
- Runs in three modes: IMAGE, VIDEO, or LIVE_STREAM

#### 2. PoseLandmarkerResult
Contains detected pose data:
```csharp
public class PoseLandmarkerResult
{
    public NormalizedLandmark[][] landmarks;        // x,y coordinates (0-1)
    public NormalizedLandmark[][] worldLandmarks;   // 3D world coordinates
    public float[][] landmarkPresence;              // confidence scores (0-1)
}
```

#### 3. Configuration
**Location:** `Assets/MediaPipeUnity/Samples/Scenes/Pose Landmark Detection/PoseLandmarkDetectionConfig.cs`

Key settings:
- `MinPoseDetectionConfidence`: 0.5f (how confident detection must be)
- `MinTrackingConfidence`: 0.5f (tracking threshold)
- `NumPoses`: 1 (detect one person)
- Model: BlazePoseLite/Full/Heavy (speed vs accuracy tradeoff)

---

## How Pose Detection Works

### The 33 Body Landmarks

MediaPipe detects these key points on your body:

| Index | Landmark | Description |
|-------|----------|-------------|
| 0 | NOSE | Nose tip |
| 11 | LEFT_SHOULDER | Left shoulder |
| 12 | RIGHT_SHOULDER | Right shoulder |
| 13 | LEFT_ELBOW | Left elbow |
| 14 | RIGHT_ELBOW | Right elbow |
| 15 | LEFT_WRIST | Left wrist |
| 16 | RIGHT_WRIST | Right wrist |
| 23 | LEFT_HIP | Left hip |
| 24 | RIGHT_HIP | Right hip |

**For hockey swinging, focus on:**
- Right Shoulder (12)
- Right Elbow (14)
- Right Wrist (16)

### Accessing Landmark Data

Each landmark provides:
```csharp
NormalizedLandmark landmark = result.landmarks[0][14]; // Right elbow

float x = landmark.x;        // Horizontal position (0-1, left to right)
float y = landmark.y;        // Vertical position (0-1, top to bottom)
float z = landmark.z;        // Depth (relative to hip midpoint)
float visibility = result.landmarkPresence[0][14]; // Confidence (0-1)
```

---

## Implementing Gesture Recognition

### Step 1: Create a Gesture Detection Script

Create a new file: `Assets/scripts/GestureDetector.cs`

```csharp
using UnityEngine;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using System.Collections.Generic;

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
        if (result == null || result.landmarks == null || result.landmarks.Length == 0)
            return;

        // Get landmarks for the first detected person
        var landmarks = result.landmarks[0];
        var presences = result.landmarkPresence[0];

        // Check if arm landmarks are visible with good confidence
        if (presences[RIGHT_WRIST] < confidenceThreshold ||
            presences[RIGHT_ELBOW] < confidenceThreshold ||
            presences[RIGHT_SHOULDER] < confidenceThreshold)
            return;

        // Get landmark positions
        Vector3 shoulder = LandmarkToVector(landmarks[RIGHT_SHOULDER]);
        Vector3 elbow = LandmarkToVector(landmarks[RIGHT_ELBOW]);
        Vector3 wrist = LandmarkToVector(landmarks[RIGHT_WRIST]);

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
```

### Step 2: Modify PoseLandmarkerRunner to Send Results

Edit: `Assets/MediaPipeUnity/Samples/Scenes/Pose Landmark Detection/PoseLandmarkerRunner.cs`

Add this field at the top of the class:
```csharp
[SerializeField] private GestureDetector gestureDetector;
```

In the `Run()` method, after line 136 where results are drawn, add:
```csharp
case Tasks.Vision.Core.RunningMode.IMAGE:
    if (taskApi.TryDetect(image, imageProcessingOptions, ref result))
    {
        _poseLandmarkerResultAnnotationController.DrawNow(result);

        // Send result to gesture detector
        if (gestureDetector != null)
        {
            gestureDetector.ProcessPoseResult(result);
        }
    }
```

Do the same for VIDEO mode (around line 145) and for LIVE_STREAM mode in `OnPoseLandmarkDetectionOutput()` (line 162).

---

## Connecting Gestures to Game Actions

### Step 3: Implement Player Controller

Edit: `Assets/scripts/mainPlayer.cs`

```csharp
using UnityEngine;

public class mainPlayer : MonoBehaviour
{
    [SerializeField] private GameObject puck;
    [SerializeField] private Transform stick;
    [SerializeField] private float swingForceMultiplier = 10f;
    [SerializeField] private GestureDetector gestureDetector;

    private Rigidbody puckRigidbody;
    private bool canSwing = true;
    private float swingCooldown = 1f;

    void Start()
    {
        if (puck != null)
        {
            puckRigidbody = puck.GetComponent<Rigidbody>();
        }

        // Subscribe to gesture events
        if (gestureDetector != null)
        {
            gestureDetector.OnSwingDetected += HandleSwing;
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (gestureDetector != null)
        {
            gestureDetector.OnSwingDetected -= HandleSwing;
        }
    }

    private void HandleSwing(float power)
    {
        if (!canSwing || puckRigidbody == null)
            return;

        Debug.Log($"Swing detected! Power: {power:F2}");

        // Calculate swing direction (from stick position to forward)
        Vector3 swingDirection = stick != null ? stick.forward : transform.forward;

        // Apply force to puck
        float force = power * swingForceMultiplier;
        puckRigidbody.AddForce(swingDirection * force, ForceMode.Impulse);

        // Start cooldown
        StartCoroutine(SwingCooldown());
    }

    private System.Collections.IEnumerator SwingCooldown()
    {
        canSwing = false;
        yield return new WaitForSeconds(swingCooldown);
        canSwing = true;
    }

    void Update()
    {
        // Additional game logic here
    }
}
```

---

## Complete Implementation Example

### Unity Scene Setup

1. **Create the scene hierarchy:**
   ```
   MainWorld
   ├── Player (with mainPlayer.cs)
   │   └── Stick (3D model)
   ├── Puck (with Rigidbody)
   ├── PoseLandmarkerRunner (from MediaPipe samples)
   │   └── WebcamDisplay
   └── Main Camera
   ```

2. **Configure PoseLandmarkerRunner:**
   - Add `PoseLandmarkerRunner` script to a GameObject
   - Assign the WebcamDisplay for visualization
   - Set Running Mode to `LIVE_STREAM` for real-time detection

3. **Add GestureDetector:**
   - Create empty GameObject named "GestureManager"
   - Add `GestureDetector.cs` script
   - Adjust thresholds:
     - Swing Speed Threshold: 0.5
     - Min Arm Extension: 0.3
     - Confidence Threshold: 0.7

4. **Connect Components:**
   - In `PoseLandmarkerRunner`, assign `GestureDetector` reference
   - In `mainPlayer`, assign:
     - Puck GameObject
     - Stick Transform
     - GestureDetector reference
     - Swing Force Multiplier: 10

---

## Testing and Debugging

### Debug Visualization

Add debug visualization to `GestureDetector.cs`:

```csharp
void OnDrawGizmos()
{
    // Draw wrist position
    Gizmos.color = Color.green;
    Gizmos.DrawSphere(previousWristPosition, 0.05f);

    // Draw velocity vector
    if (previousWristPosition != Vector3.zero)
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(previousWristPosition,
                       previousWristPosition + (previousWristPosition - transform.position) * 0.1f);
    }
}
```

### Console Logging

Add detailed logging to track gesture detection:

```csharp
public void ProcessPoseResult(PoseLandmarkerResult result)
{
    // ... existing code ...

    Debug.Log($"Wrist Speed: {speed:F2} | Extension: {armExtension:F2} | Threshold: {swingSpeedThreshold}");

    if (speed > swingSpeedThreshold && armExtension > minArmExtension)
    {
        Debug.Log($"SWING DETECTED! Power: {power:F2}");
        OnSwingDetected?.Invoke(power);
    }
}
```

### Testing Checklist

- [ ] Webcam displays video feed
- [ ] Skeleton overlay appears on body
- [ ] Right arm landmarks are detected (green dots)
- [ ] Console shows wrist speed values when moving arm
- [ ] Swing gesture triggers when arm moves quickly
- [ ] Puck moves when swing is detected
- [ ] Swing power varies with arm speed

### Common Issues

| Problem | Solution |
|---------|----------|
| No webcam feed | Check Webcam.cs is attached and camera permissions granted |
| Skeleton not appearing | Verify PoseLandmarkerRunner is set to LIVE_STREAM mode |
| Gestures not detected | Lower `swingSpeedThreshold` to 0.3 for testing |
| False positives | Increase `confidenceThreshold` to 0.8 |
| Puck doesn't move | Check Rigidbody is attached to puck, not kinematic |

---

## Advanced Topics

### Detecting Different Swing Types

Add swing direction detection:

```csharp
public enum SwingDirection { Left, Right, Overhead }

private SwingDirection DetectSwingDirection(Vector3 velocity)
{
    if (Mathf.Abs(velocity.x) > Mathf.Abs(velocity.y))
    {
        return velocity.x > 0 ? SwingDirection.Right : SwingDirection.Left;
    }
    else
    {
        return SwingDirection.Overhead;
    }
}
```

### Two-Handed Detection

Track both arms for more complex gestures:

```csharp
private const int LEFT_WRIST = 15;
private const int RIGHT_WRIST = 16;

private bool DetectTwoHandedSwing(NormalizedLandmark[] landmarks)
{
    Vector3 leftWrist = LandmarkToVector(landmarks[LEFT_WRIST]);
    Vector3 rightWrist = LandmarkToVector(landmarks[RIGHT_WRIST]);

    float handDistance = Vector3.Distance(leftWrist, rightWrist);
    return handDistance < 0.3f; // Hands close together
}
```

### Smoothing and Filtering

Reduce jitter with exponential smoothing:

```csharp
private Vector3 smoothedWristPosition;
private float smoothingFactor = 0.3f;

void Start()
{
    smoothedWristPosition = Vector3.zero;
}

public void ProcessPoseResult(PoseLandmarkerResult result)
{
    Vector3 wrist = LandmarkToVector(landmarks[RIGHT_WRIST]);

    // Exponential smoothing
    smoothedWristPosition = Vector3.Lerp(smoothedWristPosition, wrist, smoothingFactor);

    // Use smoothedWristPosition for calculations
    Vector3 velocity = (smoothedWristPosition - previousWristPosition) / deltaTime;
}
```

---

## Summary

You now have a complete pose detection system that:
1. Captures webcam input
2. Detects 33 body landmarks using MediaPipe
3. Recognizes arm swing gestures
4. Triggers hockey puck actions based on gesture power

**Key Files Modified:**
- `Assets/scripts/GestureDetector.cs` (new)
- `Assets/scripts/mainPlayer.cs` (updated)
- `Assets/MediaPipeUnity/Samples/Scenes/Pose Landmark Detection/PoseLandmarkerRunner.cs` (updated)

**Next Steps:**
- Fine-tune threshold parameters for your setup
- Add more gesture types (slap shot, wrist shot)
- Implement two-player detection
- Add visual feedback for gesture recognition

---

## Resources

- [MediaPipe Pose Documentation](https://developers.google.com/mediapipe/solutions/vision/pose_landmarker)
- [MediaPipe Unity Plugin GitHub](https://github.com/homuler/MediaPipeUnityPlugin)
- Unity Input System documentation
- C# Events and Delegates tutorial (see CSHARP_UNITY_BASICS.md)
