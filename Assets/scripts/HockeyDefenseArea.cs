using UnityEngine;

public class HockeyDefenseArea : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private HockeyDefensiveAgent defensiveAgent;
    [SerializeField] private Rigidbody puckRigidbody;
    [SerializeField] private Transform goalieSpawn;
    [SerializeField] private Transform puckSpawnCenter;
    [SerializeField] private Transform goalCenter;

    [Header("Episode Timing")]
    [SerializeField] private float episodeTimeoutSeconds = 6f;
    [SerializeField] private bool disableWorldScriptInGym = true;
    [SerializeField] private WorldScript worldScript;

    [Header("Goal Line")]
    [SerializeField] private float goalLineZ = 32.5f;
    [SerializeField] private float goalLeftX = -3.64f;
    [SerializeField] private float goalRightX = 3.8f;

    [Header("Puck Reset/Shot Randomization")]
    [SerializeField] private Vector2 puckSpawnXRange = new Vector2(-6f, 6f);
    [SerializeField] private Vector2 puckSpawnZRange = new Vector2(5f, 17f);
    [SerializeField] private Vector2 shotSpeedRange = new Vector2(11f, 24f);
    [SerializeField] private float targetPostPadding = 0.35f;
    [SerializeField] private float maxInitialVerticalVelocity = 0.75f;
    [SerializeField] private float maxAngularVelocity = 4f;

    [Header("Goalie Spawn Fallback")]
    [SerializeField] private Vector3 goalieSpawnFallback = new Vector3(0f, 0f, 25f);

    [Header("Puck Ground Physics")]
    [SerializeField] private bool configurePuckGroundPhysics = true;
    [SerializeField] private float puckFriction = 0.01f;
    [SerializeField] private float puckLinearDamping = 0.02f;
    [SerializeField] private float puckAngularDamping = 0.15f;
    [SerializeField] private bool freezeVerticalMotion = true;
    [SerializeField] private bool freezeTilt = true;
    [SerializeField] private float puckGroundY = 0.5f;

    private float _episodeTimer;
    private PhysicsMaterial _runtimeIceMaterial;

    private void Awake()
    {
        if (defensiveAgent == null)
        {
            defensiveAgent = FindObjectOfType<HockeyDefensiveAgent>();
        }

        if (puckRigidbody == null)
        {
            GameObject puckObject = GameObject.Find("puck");
            if (puckObject != null)
            {
                puckRigidbody = puckObject.GetComponent<Rigidbody>();
            }
        }

        if (goalCenter == null)
        {
            GameObject goalObject = GameObject.Find("goal_cube");
            if (goalObject != null)
            {
                goalCenter = goalObject.transform;
            }
        }

        ConfigurePuckGroundPhysics();

        if (disableWorldScriptInGym)
        {
            if (worldScript == null)
            {
                worldScript = FindObjectOfType<WorldScript>();
            }

            if (worldScript != null)
            {
                worldScript.enabled = false;
            }
        }
    }

    private void FixedUpdate()
    {
        if (defensiveAgent == null || puckRigidbody == null)
        {
            return;
        }

        EnforcePuckGroundPlane();
        _episodeTimer += Time.fixedDeltaTime;

        if (IsGoalConceded())
        {
            defensiveAgent.NotifyGoalConceded();
            return;
        }

        if (_episodeTimer >= episodeTimeoutSeconds)
        {
            defensiveAgent.EndEpisode();
        }
    }

    public void ResetEnvironment()
    {
        _episodeTimer = 0f;

        if (defensiveAgent != null)
        {
            ResetGoaliePose();
        }

        if (puckRigidbody != null)
        {
            ResetAndLaunchPuck();
        }
    }

    private void ResetGoaliePose()
    {
        Transform agentTransform = defensiveAgent.transform;

        if (goalieSpawn != null)
        {
            agentTransform.position = goalieSpawn.position;
            agentTransform.rotation = goalieSpawn.rotation;
        }
        else
        {
            agentTransform.position = goalieSpawnFallback;
            agentTransform.rotation = Quaternion.identity;
        }
    }

    private void ResetAndLaunchPuck()
    {
        Vector3 spawnOrigin = puckSpawnCenter != null
            ? puckSpawnCenter.position
            : new Vector3(0f, 0.5f, 0f);

        Vector3 spawnPos = new Vector3(
            Random.Range(puckSpawnXRange.x, puckSpawnXRange.y),
            spawnOrigin.y,
            Random.Range(puckSpawnZRange.x, puckSpawnZRange.y)
        );

        puckRigidbody.position = spawnPos;
        puckRigidbody.linearVelocity = Vector3.zero;
        puckRigidbody.angularVelocity = Vector3.zero;

        float targetX = Random.Range(goalLeftX + targetPostPadding, goalRightX - targetPostPadding);
        float targetY = goalCenter != null ? goalCenter.position.y : spawnPos.y;
        Vector3 targetPos = new Vector3(targetX, targetY, goalLineZ);

        Vector3 shotDir = (targetPos - spawnPos).normalized;
        float shotSpeed = Random.Range(shotSpeedRange.x, shotSpeedRange.y);
        float verticalNoise = freezeVerticalMotion
            ? 0f
            : Random.Range(-maxInitialVerticalVelocity, maxInitialVerticalVelocity);

        Vector3 launchVelocity = shotDir * shotSpeed;
        launchVelocity.y += verticalNoise;
        puckRigidbody.linearVelocity = launchVelocity;
        puckRigidbody.angularVelocity = Random.insideUnitSphere * maxAngularVelocity;
    }

    private bool IsGoalConceded()
    {
        Vector3 puckPos = puckRigidbody.position;
        bool crossedGoalLine = puckPos.z >= goalLineZ;
        bool inPostBounds = puckPos.x >= goalLeftX && puckPos.x <= goalRightX;
        return crossedGoalLine && inPostBounds;
    }

    private void ConfigurePuckGroundPhysics()
    {
        if (!configurePuckGroundPhysics || puckRigidbody == null)
        {
            return;
        }

        puckRigidbody.linearDamping = puckLinearDamping;
        puckRigidbody.angularDamping = puckAngularDamping;

        RigidbodyConstraints constraints = puckRigidbody.constraints;
        if (freezeVerticalMotion)
        {
            constraints |= RigidbodyConstraints.FreezePositionY;
        }

        if (freezeTilt)
        {
            constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        puckRigidbody.constraints = constraints;
        _runtimeIceMaterial = CreateOrUpdateIceMaterial(_runtimeIceMaterial, puckFriction);

        foreach (Collider collider in puckRigidbody.GetComponentsInChildren<Collider>())
        {
            collider.sharedMaterial = _runtimeIceMaterial;
        }
    }

    private void EnforcePuckGroundPlane()
    {
        if (!configurePuckGroundPhysics || !freezeVerticalMotion || puckRigidbody == null)
        {
            return;
        }

        Vector3 velocity = puckRigidbody.linearVelocity;
        if (Mathf.Abs(velocity.y) > 0.001f)
        {
            velocity.y = 0f;
            puckRigidbody.linearVelocity = velocity;
        }

        Vector3 position = puckRigidbody.position;
        if (Mathf.Abs(position.y - puckGroundY) > 0.001f)
        {
            position.y = puckGroundY;
            puckRigidbody.position = position;
        }
    }

    private static PhysicsMaterial CreateOrUpdateIceMaterial(PhysicsMaterial material, float friction)
    {
        if (material == null)
        {
            material = new PhysicsMaterial("Runtime_Ice");
        }

        float clampedFriction = Mathf.Max(0f, friction);
        material.staticFriction = clampedFriction;
        material.dynamicFriction = clampedFriction;
        material.frictionCombine = PhysicsMaterialCombine.Minimum;
        material.bounciness = 0f;
        material.bounceCombine = PhysicsMaterialCombine.Minimum;
        return material;
    }
}
