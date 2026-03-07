using System;
using System.Collections;
using UnityEngine;

public class controllerPlayer : MonoBehaviour
{
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
    [SerializeField] private float maxHitDistance = 5.5f;
    [SerializeField] private float minHitDistance = 0.5f;
    [SerializeField] private float swingAnimationDuration = 0.3f;
    [SerializeField] private float swingRotationAngle = 90f;
    private bool isSwinging = false;
    private Quaternion stickOriginalRotation;
    private Vector3 stickOriginalPosition;

    [Header("Controller Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 360f;

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

    void Update()
    {
        // Read controller/keyboard input
        float horizontal = Input.GetAxis("Horizontal"); // Left stick X / A-D keys
        float vertical = Input.GetAxis("Vertical");     // Left stick Y / W-S keys
        Vector2 moveInput = new Vector2(horizontal, vertical);

        bool isMoving = moveInput.sqrMagnitude > 0.01f;
        if (isMoving)
        {
            Vector3 movement = new Vector3(moveInput.x, 0, moveInput.y) * moveSpeed * Time.deltaTime;
            transform.position += movement;
        }
        else
        {
            bobTimer = 0f;
            Vector3 camPos = mainCamera.transform.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, defaultCameraY, Time.deltaTime * 10f);
            mainCamera.transform.localPosition = camPos;
        }

        // Make player look at the goal (smooth rotation)
        if (net != null)
        {
            Vector3 directionToGoal = net.transform.position - transform.position;
            directionToGoal.y = 0;
            if (directionToGoal.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToGoal);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
        }

        // Swing with controller: press to start, release to swing
        // Uses "Fire1" (mapped to left ctrl / joystick button 0 / gamepad south button)
        if (Input.GetButtonDown("Fire1") && !DragStart)
        {
            Debug.Log("Swing started");
            DragStart = true;
            startSwingPos = new Vector2(0f, 0f); // baseline, unused for controller
        }

        if (Input.GetButtonUp("Fire1") && DragStart && !isSwinging)
        {
            DragStart = false;

            float distanceToPuck = Vector3.Distance(transform.position, puck.transform.position);
            if (distanceToPuck > maxHitDistance)
            {
                Debug.Log($"Puck too far! Distance: {distanceToPuck:F2}m (Max: {maxHitDistance}m)");
                return;
            }

            float distanceRatio = Mathf.Clamp01((distanceToPuck - minHitDistance) / (maxHitDistance - minHitDistance));
            float distanceMultiplier = Mathf.Max(1f - distanceRatio, 0.3f);

            // Full-strength swing in the direction of the goal
            float swingStrength = 1f;

            Vector3 cameraForward = Camera.main.transform.forward;
            Vector3 baseDirection = new Vector3(cameraForward.x, 0f, cameraForward.z).normalized;
            float finalForce = swingForce * swingStrength * distanceMultiplier;
            Vector3 worldForce = baseDirection * finalForce;

            Debug.Log($"Swing! Distance: {distanceToPuck:F2}m, Force Multiplier: {distanceMultiplier:F2}, Final Force: {finalForce:F1}");

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

        Vector3 swingAxis = Camera.main.transform.up;
        Quaternion backswingRotation = stickOriginalRotation * Quaternion.AngleAxis(-swingRotationAngle * 0.5f, swingAxis);
        Quaternion forwardSwingRotation = stickOriginalRotation * Quaternion.AngleAxis(swingRotationAngle, swingAxis);

        // Phase 1: Backswing (20%)
        float backswingDuration = swingAnimationDuration * 0.2f;
        float elapsed = 0f;
        while (elapsed < backswingDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / backswingDuration;
            hockeyStick.transform.localRotation = Quaternion.Slerp(stickOriginalRotation, backswingRotation, t);
            yield return null;
        }

        // Phase 2: Forward swing (50%)
        float forwardSwingDuration = swingAnimationDuration * 0.5f;
        elapsed = 0f;
        bool forceApplied = false;
        while (elapsed < forwardSwingDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / forwardSwingDuration;
            float curvedT = 1f - Mathf.Pow(1f - t, 3f);
            hockeyStick.transform.localRotation = Quaternion.Slerp(backswingRotation, forwardSwingRotation, curvedT);

            if (!forceApplied && t >= 0.5f)
            {
                ApplyPuckForce(forceToApply, swingStrength);
                forceApplied = true;
            }
            yield return null;
        }

        if (!forceApplied)
            ApplyPuckForce(forceToApply, swingStrength);

        // Phase 3: Return to rest (30%)
        float returnDuration = swingAnimationDuration * 0.3f;
        elapsed = 0f;
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / returnDuration;
            hockeyStick.transform.localRotation = Quaternion.Slerp(forwardSwingRotation, stickOriginalRotation, t);
            yield return null;
        }

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

        CameraShake.Shake(0.25f, worldForce.magnitude * 0.01f);
        puckRb.AddForce(worldForce, ForceMode.Impulse);
        Debug.Log($"Puck Hit! Force: {worldForce.magnitude:F1}");

        if (swingStrength > 0.8f)
        {
            puckRb.AddForce(Vector3.up * swingForce * 0.5f, ForceMode.Impulse);
            puckRb.AddTorque(Vector3.right * swingForce * 0.1f, ForceMode.Impulse);
            Debug.Log("Elevated shot!");
        }
    }
}
