using System;
using System.Collections;
using UnityEngine;

public class mainPlayer : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private bool DragStart = false;
    [SerializeField] private float swingForce = 200;
    [SerializeField] private GameObject net;
    [SerializeField] private float fov = 60f;
    [SerializeField] private float hitRange = 4f;
    [SerializeField] private GameObject puck;
    private Vector2 startSwingPos;
    private Vector2 endSwingPos;
    public LayerMask groundLayer;

    [Header("Hockey Stick Settings")]
    [SerializeField] private GameObject hockeyStick;
    [SerializeField] private float maxHitDistance = 5.5f; // Maximum distance to hit puck
    [SerializeField] private float minHitDistance = 0.5f; // Minimum distance for max force
    [SerializeField] private float swingAnimationDuration = 0.3f;
    [SerializeField] private float swingRotationAngle = 90f; // How far the stick rotates during swing
    private bool isSwinging = false;
    private Quaternion stickOriginalRotation;
    private Vector3 stickOriginalPosition;

    [Header("Pose Movement Settings")]
    [SerializeField] private PoseController poseController;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 360f; // Degrees per second for smooth rotation
    private Vector2 currentMoveInput;

    [Header("Head Bob")]
    [SerializeField] private float bobFrequency = 8f;
    [SerializeField] private float bobAmplitude = 0.05f;
    private float bobTimer = 0f;
    private float defaultCameraY;

    private GameObject mainCamera;
    void Start()
    {
        mainCamera = GetComponentInChildren<Camera>().gameObject;
        defaultCameraY = mainCamera.transform.localPosition.y;

        if (poseController != null)
        {
            poseController.OnMovementDetected += HandleMovement;
        }
        else
        {
            Debug.LogWarning("PoseController not assigned to mainPlayer. Head tracking disabled.");
        }

        // Store hockey stick original transform
        if (hockeyStick != null)
        {
            stickOriginalRotation = hockeyStick.transform.localRotation;
            stickOriginalPosition = hockeyStick.transform.localPosition;
        }
        else
        {
            Debug.LogWarning("Hockey stick not assigned! Please assign the hockey stick GameObject in the Inspector.");
        }
    }

    void OnDestroy()
    {
        if (poseController != null)
        {
            poseController.OnMovementDetected -= HandleMovement;
        }
    }

    private void HandleMovement(Vector2 movement)
    {
        currentMoveInput = movement;
    }

    // Update is called once per frame
    void Update()
    {
        // Recalibrate pose tracking with 'C' key
        if (Input.GetKeyDown(KeyCode.C) && poseController != null)
        {
            poseController.Recalibrate();
            Debug.Log("Recalibrating head tracking - hold neutral position!");
        }

        // Apply head tracking movement FIRST
        bool isMoving = currentMoveInput != Vector2.zero;
        if (isMoving)
        {
            Vector3 movement = new Vector3(currentMoveInput.x, 0, currentMoveInput.y) * moveSpeed * Time.deltaTime;
            transform.position += movement;

            // // Head bob while moving
            // bobTimer += Time.deltaTime * bobFrequency;
            // float bobOffset = Mathf.Sin(bobTimer) * bobAmplitude;
            // Vector3 camPos = mainCamera.transform.localPosition;
            // camPos.y = defaultCameraY + bobOffset;
            // mainCamera.transform.localPosition = camPos;
        }
        else
        {
            // Reset head bob when not moving
            bobTimer = 0f;
            Vector3 camPos = mainCamera.transform.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, defaultCameraY, Time.deltaTime * 10f);
            mainCamera.transform.localPosition = camPos;
        }

        // Make player look at the goal AFTER movement (smooth rotation)
        if (net != null)
        {
            Vector3 directionToGoal = net.transform.position - transform.position;
            directionToGoal.y = 0; // Keep rotation only on horizontal plane
            if (directionToGoal.sqrMagnitude > 0.001f) // Use sqrMagnitude for performance
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToGoal);
                // Smooth rotation instead of instant snap
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
        }

        Vector2 startSwingScreenPos = new Vector2();
        Vector2 endSwingScreenPos = new Vector2();
        if (Input.GetMouseButtonDown(0) && !DragStart)
        {
            Debug.Log("Started Drag");
            DragStart = true;
            // Get mouse position in screen space - use a fixed distance from camera
            Vector3 screenPos = Input.mousePosition;
            startSwingScreenPos = Input.mousePosition;
            screenPos.z = Vector3.Distance(Camera.main.transform.position, puck.transform.position);
            startSwingPos = Camera.main.ScreenToWorldPoint(screenPos);
        }

        if (Input.GetMouseButtonUp(0) && DragStart && !isSwinging)
        {
            DragStart = false;
            endSwingScreenPos = Input.mousePosition;
            Vector2 vec = endSwingScreenPos - startSwingScreenPos;

            // Check distance to puck first
            float distanceToPuck = Vector3.Distance(transform.position, puck.transform.position);

            if (distanceToPuck > maxHitDistance)
            {
                Debug.Log($"Puck too far! Distance: {distanceToPuck:F2}m (Max: {maxHitDistance}m)");
                return;
            }

            // Calculate force based on distance (closer = more force)
            // Normalize distance: 0 = at minHitDistance (max force), 1 = at maxHitDistance (min force)
            float distanceRatio = Mathf.Clamp01((distanceToPuck - minHitDistance) / (maxHitDistance - minHitDistance));
            float distanceMultiplier = 1f - distanceRatio; // Invert: closer = higher multiplier
            distanceMultiplier = Mathf.Max(distanceMultiplier, 0.3f); // Minimum 30% force

            // Get swing input strength
            float y_component = vec.y;
            float swingStrength = Mathf.Clamp01(y_component / Screen.height);

            if (swingStrength < 0.1f)
            {
                Debug.Log("Swing too weak!");
                return;
            }

            // Use camera's forward direction as the base force direction
            Vector3 cameraForward = Camera.main.transform.forward;
            Vector3 cameraRight = Camera.main.transform.right;

            // Map screen space movement to camera space
            float screenXRatio = vec.x / Screen.width;
            float screenYRatio = vec.y / Screen.height;

            // Create force based on camera orientation, swing strength, and distance
            Vector3 baseDirection = (cameraForward * screenYRatio + cameraRight * screenXRatio).normalized;
            float finalForce = swingForce * swingStrength * distanceMultiplier;
            Vector3 worldForce = baseDirection * finalForce;

            Debug.Log($"Swing! Distance: {distanceToPuck:F2}m, Force Multiplier: {distanceMultiplier:F2}, Final Force: {finalForce:F1}");

            // Start swing animation and apply force
            StartCoroutine(SwingHockeyStick(worldForce, swingStrength));
        }
    }

    private IEnumerator SwingHockeyStick(Vector3 forceToApply, float swingStrength)
    {
        if (hockeyStick == null || puck == null)
        {
            Debug.LogWarning("Cannot swing - hockey stick or puck not assigned!");
            yield break;
        }

        isSwinging = true;

        // Calculate swing direction (perpendicular to camera forward, since stick is sideways)
        // The stick swings from right to left (or vice versa) in front of the camera
        Vector3 swingAxis = Camera.main.transform.up; // Rotate around camera's up axis
        Quaternion backswingRotation = stickOriginalRotation * Quaternion.AngleAxis(-swingRotationAngle * 0.5f, swingAxis);
        Quaternion forwardSwingRotation = stickOriginalRotation * Quaternion.AngleAxis(swingRotationAngle, swingAxis);

        // Phase 1: Backswing (20% of duration)
        float backswingDuration = swingAnimationDuration * 0.2f;
        float elapsed = 0f;

        while (elapsed < backswingDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / backswingDuration;
            hockeyStick.transform.localRotation = Quaternion.Slerp(stickOriginalRotation, backswingRotation, t);
            yield return null;
        }

        // Phase 2: Forward swing (50% of duration)
        float forwardSwingDuration = swingAnimationDuration * 0.5f;
        elapsed = 0f;
        bool forceApplied = false;

        while (elapsed < forwardSwingDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / forwardSwingDuration;

            // Use ease-out curve for realistic swing
            float curvedT = 1f - Mathf.Pow(1f - t, 3f);
            hockeyStick.transform.localRotation = Quaternion.Slerp(backswingRotation, forwardSwingRotation, curvedT);

            // Apply force at the midpoint of the swing (when stick would contact puck)
            if (!forceApplied && t >= 0.5f)
            {
                ApplyPuckForce(forceToApply, swingStrength);
                forceApplied = true;
            }

            yield return null;
        }

        // Ensure force is applied even if we missed the timing
        if (!forceApplied)
        {
            ApplyPuckForce(forceToApply, swingStrength);
        }

        // Phase 3: Return to rest (30% of duration)
        float returnDuration = swingAnimationDuration * 0.3f;
        elapsed = 0f;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnDuration;
            hockeyStick.transform.localRotation = Quaternion.Slerp(forwardSwingRotation, stickOriginalRotation, t);
            yield return null;
        }

        // Ensure we end at exact original rotation
        hockeyStick.transform.localRotation = stickOriginalRotation;
        hockeyStick.transform.localPosition = stickOriginalPosition;

        isSwinging = false;
    }

    private void ApplyPuckForce(Vector3 worldForce, float swingStrength)
    {
        Rigidbody puckRb = puck.GetComponent<Rigidbody>();
        if (puckRb == null)
        {
            Debug.LogError("Puck has no Rigidbody!");
            return;
        }

        // Apply camera shake
        CameraShake.Shake(0.25f, worldForce.magnitude * 0.01f);

        // Apply main force
        puckRb.AddForce(worldForce, ForceMode.Impulse);
        Debug.Log($"Puck Hit! Force: {worldForce.magnitude:F1}");

        // If swing strength is high, add upward force for elevated shots
        if (swingStrength > 0.8f)
        {
            puckRb.AddForce(Vector3.up * swingForce * 0.5f, ForceMode.Impulse);
            puckRb.AddTorque(Vector3.right * swingForce * 0.1f, ForceMode.Impulse);
            Debug.Log("Elevated shot!");
        }
    }
}
