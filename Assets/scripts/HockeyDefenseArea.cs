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

    private float _episodeTimer;

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
        float verticalNoise = Random.Range(-maxInitialVerticalVelocity, maxInitialVerticalVelocity);

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
}
