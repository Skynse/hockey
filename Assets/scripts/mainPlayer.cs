using System;
using UnityEngine;

public class mainPlayer : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private bool DragStart = false;
    [SerializeField] private float swingForce = 200;
    [SerializeField] private float fov = 60f;
    [SerializeField] private float hitRange = 4f;
    [SerializeField] private GameObject puck;
    private Vector2 startSwingPos;
    private Vector2 endSwingPos;
    public LayerMask groundLayer;

    [Header("Pose Movement Settings")]
    [SerializeField] private PoseController poseController;
    [SerializeField] private float moveSpeed = 5f;
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
        // Apply head tracking movement
        bool isMoving = currentMoveInput != Vector2.zero;
        if (isMoving)
        {
            Vector3 movement = new Vector3(currentMoveInput.x, 0, currentMoveInput.y) * moveSpeed * Time.deltaTime;
            transform.position += movement;

            // Head bob while moving
            bobTimer += Time.deltaTime * bobFrequency;
            float bobOffset = Mathf.Sin(bobTimer) * bobAmplitude;
            Vector3 camPos = mainCamera.transform.localPosition;
            camPos.y = defaultCameraY + bobOffset;
            mainCamera.transform.localPosition = camPos;
        }
        else
        {
            // Reset head bob when not moving
            bobTimer = 0f;
            Vector3 camPos = mainCamera.transform.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, defaultCameraY, Time.deltaTime * 10f);
            mainCamera.transform.localPosition = camPos;
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

        if (Input.GetMouseButtonUp(0) && DragStart)
        {
            Debug.Log("Swinged");
            DragStart = false;
            endSwingScreenPos = Input.mousePosition;
            Vector2 vec = endSwingScreenPos - startSwingScreenPos;
            // Get mouse position in screen space with same distance as start
            float y_component = vec.y;
            float forceMultiplier = y_component / Screen.height;

            // Use camera's forward direction as the base force direction
            Vector3 cameraForward = Camera.main.transform.forward;
            Vector3 cameraRight = Camera.main.transform.right;
            Vector3 cameraUp = Camera.main.transform.up;

            // Map screen space movement to camera space
            float screenXRatio = vec.x / Screen.width;
            float screenYRatio = vec.y / Screen.height;

            // Create force based on camera orientation and screen movement
            Vector3 worldForce = (cameraForward * screenYRatio + cameraRight * screenXRatio).normalized * swingForce * forceMultiplier;

            Debug.Log($"Swing Direction: {worldForce}, Magnitude: {worldForce.magnitude}");

            bool canHit = true;

            if (canHit)
            {
                Rigidbody puckRb = puck.GetComponent<Rigidbody>();
                if (puckRb == null)
                {
                    Debug.LogError("Puck has no Rigidbody!");
                    return;
                }

                CameraShake.Shake(0.25f, worldForce.magnitude * 0.01f);
                // Apply force in the direction of the swing
                Debug.Log($"Applying Force: {worldForce}");
                puckRb.AddForce(worldForce, ForceMode.Impulse);
                Debug.Log("Puck Hit!");

                // if ratio above 80%, move puck up in the air
                if (forceMultiplier > 0.8f)
                {
                    puckRb.AddForce(Vector3.up * swingForce * 0.5f, ForceMode.Impulse);
                    // toque
                    puckRb.AddTorque(Vector3.right * swingForce * 0.1f, ForceMode.Impulse);
                }
            }
            else
            {
                Debug.Log("Missed the Puck!");
            }
        }
    }
}
