using System;
using UnityEngine;

public class opponentControl : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject goal_cube;
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject puck;

    [Header("Vision Settings")]
    [SerializeField] private float visionRange = 120f;
    [SerializeField] private float raycastRange = 15f;
    [SerializeField] private int visionRayCount = 5;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 120f;
    [SerializeField] private float goalLeaveRadius = 11f;
    [SerializeField] private float goalBaseOffset = 2f;

    [Header("Goalie Behavior")]
    [SerializeField] private float puckTrackingWeight = 0.7f; // How much to prioritize puck vs player
    [SerializeField] private float reactionSpeed = 8f;
    [SerializeField] private float anticipationDistance = 3f; // How far ahead to position

    [Header("Puck Hitting")]
    [SerializeField] private float hitRange = 2f;
    [SerializeField] private float hitForce = 150f;
    [SerializeField] private float hitCooldown = 1.5f;
    private float lastHitTime = -999f;

    [Header("Animation")]
    [SerializeField] private animation animationController;

    private Vector3 targetPosition;
    private Vector3 goalBasePosition;
    private Vector3 lastPosition;
    private Ray[] visionRays;

    void Start()
    {
        // Cache goal base position
        if (goal_cube != null)
        {
            goalBasePosition = goal_cube.transform.position;
            goalBasePosition.z += goalBaseOffset;
        }

        // Pre-allocate vision rays array for performance
        visionRays = new Ray[visionRayCount];
        targetPosition = transform.position;
        lastPosition = transform.position;

        // Auto-find animation controller on child if not assigned
        if (animationController == null)
            animationController = GetComponentInChildren<animation>();
    }

    void Update()
    {
        if (goal_cube == null) return;

        // Only track the puck — player following handled by agents later
        GameObject trackTarget = puck;
        if (trackTarget == null) return;

        // Calculate optimal defensive position
        Vector3 optimalPosition = CalculateOptimalPosition(trackTarget);
        targetPosition = Vector3.Lerp(targetPosition, optimalPosition, Time.deltaTime * reactionSpeed);

        // Rotate to face the track target
        RotateTowardsTarget(trackTarget.transform.position);

        // Move to target position if within allowed radius
        MoveToTargetPosition();

        // Try to hit the puck if in range
        TryHitPuck();

        // Update animations
        bool isMoving = Vector3.Distance(transform.position, lastPosition) > 0.001f;
        lastPosition = transform.position;
        if (animationController != null)
            animationController.SetStrafing(isMoving);

        // Optional: Draw debug rays to visualize vision
        if (Application.isEditor)
        {
            DrawVisionDebug(trackTarget);
        }
    }

    private Vector3 CalculateOptimalPosition(GameObject threat)
    {
        Vector3 goalCenter = goal_cube.transform.position;
        Vector3 threatPosition = threat.transform.position;

        // Position between the threat and the goal
        Vector3 directionFromGoal = (threatPosition - goalCenter).normalized;
        Vector3 idealPosition = goalCenter + directionFromGoal * goalBaseOffset;

        // If tracking puck and it's moving, anticipate trajectory
        if (threat == puck && puck != null)
        {
            Rigidbody puckRb = puck.GetComponent<Rigidbody>();
            if (puckRb != null && puckRb.linearVelocity.magnitude > 0.5f)
            {
                // Anticipate where puck will be
                Vector3 anticipatedPos = puckRb.position + puckRb.linearVelocity.normalized * anticipationDistance;
                directionFromGoal = (anticipatedPos - goalCenter).normalized;
                idealPosition = goalCenter + directionFromGoal * goalBaseOffset;
            }
        }

        // Clamp position within goal area radius
        float distanceFromGoal = Vector3.Distance(idealPosition, goalCenter);
        if (distanceFromGoal > goalLeaveRadius)
        {
            idealPosition = goalCenter + (idealPosition - goalCenter).normalized * goalLeaveRadius;
        }

        // Keep same Y position
        idealPosition.y = transform.position.y;

        return idealPosition;
    }

    private void RotateTowardsTarget(Vector3 targetPos)
    {
        Vector3 toTarget = targetPos - transform.position;
        toTarget.y = 0; // Keep rotation on horizontal plane

        if (toTarget != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(toTarget);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }

    private void MoveToTargetPosition()
    {
        float distanceToGoal = Vector3.Distance(transform.position, goal_cube.transform.position);

        // Only move if within allowed radius or moving back towards goal
        if (distanceToGoal < goalLeaveRadius)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );
        }
        else
        {
            // Return to goal base if outside radius
            transform.position = Vector3.MoveTowards(
                transform.position,
                goalBasePosition,
                moveSpeed * Time.deltaTime
            );
        }
    }

    private void TryHitPuck()
    {
        if (puck == null) return;
        if (Time.time - lastHitTime < hitCooldown) return;

        float distanceToPuck = Vector3.Distance(transform.position, puck.transform.position);
        if (distanceToPuck > hitRange) return;

        Rigidbody puckRb = puck.GetComponent<Rigidbody>();
        if (puckRb == null) return;

        // Hit puck away from our own goal (toward player's side)
        Vector3 hitDirection = (puck.transform.position - goal_cube.transform.position).normalized;
        hitDirection.y = 0;
        puckRb.AddForce(hitDirection * hitForce, ForceMode.Impulse);
        lastHitTime = Time.time;

        if (animationController != null)
            animationController.PlaySwing();

        Debug.Log("Opponent hit the puck!");
    }

    private bool CanSeeTarget(GameObject target)
    {
        if (target == null) return false;

        Vector3 toTarget = target.transform.position - transform.position;
        float angleToTarget = Vector3.Angle(transform.forward, toTarget);

        if (angleToTarget > visionRange / 2f) return false;

        // Update vision rays
        for (int i = 0; i < visionRayCount; i++)
        {
            float angle = (i - visionRayCount / 2) * (visionRange / (visionRayCount - 1));
            Quaternion rotation = Quaternion.Euler(0, angle, 0);
            Vector3 direction = rotation * transform.forward;
            visionRays[i] = new Ray(transform.position, direction);
        }

        // Check if any ray hits the target
        foreach (Ray ray in visionRays)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, raycastRange))
            {
                if (hit.collider.gameObject == target)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void DrawVisionDebug(GameObject target)
    {
        // Draw vision cone
        for (int i = 0; i < visionRayCount; i++)
        {
            float angle = (i - visionRayCount / 2) * (visionRange / (visionRayCount - 1));
            Quaternion rotation = Quaternion.Euler(0, angle, 0);
            Vector3 direction = rotation * transform.forward;

            Color rayColor = Color.yellow;
            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, raycastRange))
            {
                if (hit.collider.gameObject == target)
                {
                    rayColor = Color.red;
                }
                Debug.DrawLine(transform.position, hit.point, rayColor);
            }
            else
            {
                Debug.DrawRay(transform.position, direction * raycastRange, rayColor);
            }
        }

        // Draw goal radius
        Debug.DrawLine(transform.position, goal_cube.transform.position, Color.cyan);
    }
}
