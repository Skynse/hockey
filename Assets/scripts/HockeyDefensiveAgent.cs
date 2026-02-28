using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class HockeyDefensiveAgent : Agent
{
    [Header("References")]
    [SerializeField] private HockeyDefenseArea trainingArea;
    [SerializeField] private Rigidbody puckRigidbody;
    [SerializeField] private Transform goalCenter;
    [SerializeField] private Transform leftGoalPost;
    [SerializeField] private Transform rightGoalPost;

    [Header("Movement")]
    [SerializeField] private float maxMoveSpeed = 7.5f;
    [SerializeField] private float maxRotationSpeed = 240f;
    [SerializeField] private bool clampToCrease = true;
    [SerializeField] private float minX = -5.25f;
    [SerializeField] private float maxX = 5.25f;
    [SerializeField] private float minZ = 20f;
    [SerializeField] private float maxZ = 31.5f;

    [Header("Goal Fallback (When Goal Post Transforms Are Unset)")]
    [SerializeField] private float goalLineZ = 32.5f;
    [SerializeField] private float goalLeftX = -3.64f;
    [SerializeField] private float goalRightX = 3.8f;
    [SerializeField] private float goalY = 0.9f;

    [Header("Observation Normalization")]
    [SerializeField] private float positionScale = 0.05f;
    [SerializeField] private float velocityScale = 0.1f;
    [SerializeField] private float speedScale = 0.05f;

    [Header("Stance Settings")]
    [SerializeField] private float butterflyMoveMultiplier = 0.45f;
    [SerializeField] private float butterflyWidthScale = 1.75f;
    [SerializeField] private float pokeRange = 2.1f;
    [SerializeField] private float pokeImpulse = 6.5f;
    [SerializeField] private float pokeCooldownSeconds = 0.3f;
    [SerializeField] private float pokeForwardDotThreshold = 0.2f;

    [Header("Rewards")]
    [SerializeField] private float onPuckInterceptReward = 1.0f;
    [SerializeField] private float inPassingLaneRewardPerStep = 0.01f;
    [SerializeField] private float facingPuckRewardPerStep = 0.005f;
    [SerializeField] private float goalConcededPenalty = 1.0f;
    [SerializeField] private float distanceToLanePenaltyScale = 0.01f;
    [SerializeField] private float unnecessaryBlockPenaltyPerStep = 0.005f;
    [SerializeField] private float passingLaneThreshold = 0.75f;
    [SerializeField] private float farPuckDistance = 8f;

    [Header("Episode")]
    [SerializeField] private int maxDecisionSteps = 1200;

    [Header("Training Mode")]
    [SerializeField] private bool enableTrainingAreaResets = true;

    [Header("Behavior Auto-Setup")]
    [SerializeField] private bool autoConfigureBehaviorParameters = true;
    [SerializeField] private string behaviorName = "HockeyDefense";
    [SerializeField] private int decisionPeriod = 5;

    private const int IdleStance = 0;
    private const int ButterflyStance = 1;
    private const int PokeStance = 2;
    private const float Epsilon = 1e-4f;

    private Vector3 _baseScale;
    private Vector3 _lastPosition;
    private float _agentSpeed;
    private float _nextPokeAllowedTime;
    private int _currentStance;
    private int _decisionStepCount;
    private bool _episodeEnded;

    protected override void OnEnable()
    {
        if (autoConfigureBehaviorParameters)
        {
            EnsureMlAgentsComponents();
        }

        base.OnEnable();
    }

    public void NotifyGoalConceded()
    {
        if (_episodeEnded)
        {
            return;
        }

        AddReward(-goalConcededPenalty);
        _episodeEnded = true;
        EndEpisode();
    }

    public override void Initialize()
    {
        opponentControl legacyController = GetComponent<opponentControl>();
        if (legacyController != null && legacyController.enabled)
        {
            legacyController.enabled = false;
        }

        if (enableTrainingAreaResets && trainingArea == null)
        {
            trainingArea = FindObjectOfType<HockeyDefenseArea>();
            if (trainingArea == null)
            {
                GameObject areaObject = new GameObject("HockeyDefenseArea");
                trainingArea = areaObject.AddComponent<HockeyDefenseArea>();
            }
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

        _baseScale = transform.localScale;
        _lastPosition = transform.position;
        _currentStance = IdleStance;
    }

    private void EnsureMlAgentsComponents()
    {
        BehaviorParameters behaviorParameters = GetComponent<BehaviorParameters>();
        bool createdBehaviorParameters = false;
        if (behaviorParameters == null)
        {
            behaviorParameters = gameObject.AddComponent<BehaviorParameters>();
            createdBehaviorParameters = true;
        }

        behaviorParameters.BehaviorName = behaviorName;
        if (createdBehaviorParameters)
        {
            behaviorParameters.BehaviorType = BehaviorType.Default;
        }

        behaviorParameters.BrainParameters.VectorObservationSize = 26;
        behaviorParameters.BrainParameters.NumStackedVectorObservations = 1;
        behaviorParameters.BrainParameters.ActionSpec = new ActionSpec(3, new[] { 3 });

        DecisionRequester decisionRequester = GetComponent<DecisionRequester>();
        bool createdDecisionRequester = false;
        if (decisionRequester == null)
        {
            decisionRequester = gameObject.AddComponent<DecisionRequester>();
            createdDecisionRequester = true;
        }

        if (createdDecisionRequester || decisionRequester.DecisionPeriod < 1)
        {
            decisionRequester.DecisionPeriod = Mathf.Max(1, decisionPeriod);
            decisionRequester.DecisionStep = 0;
            decisionRequester.TakeActionsBetweenDecisions = true;
        }
    }

    public override void OnEpisodeBegin()
    {
        _episodeEnded = false;
        _nextPokeAllowedTime = 0f;
        _decisionStepCount = 0;
        SetStance(IdleStance);

        if (enableTrainingAreaResets && trainingArea != null)
        {
            trainingArea.ResetEnvironment();
        }

        _lastPosition = transform.position;
        _agentSpeed = 0f;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 puckPos = GetPuckPosition();
        Vector3 puckVel = GetPuckVelocity();
        float puckSpeed = puckVel.magnitude;

        Vector3 agentPos = transform.position;
        Vector3 agentForward = transform.forward.normalized;
        float agentSpeed = _agentSpeed;

        Vector3 goalCenterPos = GetGoalCenterPosition();
        Vector3 leftPost = GetLeftPostPosition();
        Vector3 rightPost = GetRightPostPosition();
        Vector3 interceptPoint = ComputeGoalLineIntercept(puckPos, puckVel);

        sensor.AddObservation(puckPos * positionScale);
        sensor.AddObservation(puckVel * velocityScale);
        sensor.AddObservation(puckSpeed * speedScale);

        sensor.AddObservation(agentPos * positionScale);
        sensor.AddObservation(agentForward);
        sensor.AddObservation(agentSpeed * speedScale);

        sensor.AddObservation(goalCenterPos * positionScale);
        sensor.AddObservation(leftPost * positionScale);
        sensor.AddObservation(rightPost * positionScale);
        sensor.AddObservation(interceptPoint * positionScale);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (_episodeEnded)
        {
            return;
        }

        _decisionStepCount++;
        if (_decisionStepCount >= maxDecisionSteps)
        {
            EndEpisode();
            return;
        }

        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float rotationInput = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);
        int stance = actions.DiscreteActions[0];

        SetStance(stance);

        float stanceMoveMultiplier = _currentStance == ButterflyStance ? butterflyMoveMultiplier : 1f;
        Vector3 moveDelta = new Vector3(moveX, 0f, moveZ) * (maxMoveSpeed * stanceMoveMultiplier * Time.fixedDeltaTime);
        transform.position += moveDelta;

        float yaw = rotationInput * maxRotationSpeed * Time.fixedDeltaTime;
        transform.Rotate(0f, yaw, 0f, Space.World);

        if (clampToCrease)
        {
            Vector3 clamped = transform.position;
            clamped.x = Mathf.Clamp(clamped.x, minX, maxX);
            clamped.z = Mathf.Clamp(clamped.z, minZ, maxZ);
            transform.position = clamped;
        }

        if (_currentStance == PokeStance)
        {
            TryPokeCheck();
        }

        AddShapedRewards();
        _agentSpeed = Vector3.Distance(_lastPosition, transform.position) / Mathf.Max(Time.fixedDeltaTime, Epsilon);
        _lastPosition = transform.position;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuous = actionsOut.ContinuousActions;
        ActionSegment<int> discrete = actionsOut.DiscreteActions;

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        float rotate = 0f;

        if (Input.GetKey(KeyCode.Q))
        {
            rotate = -1f;
        }
        else if (Input.GetKey(KeyCode.E))
        {
            rotate = 1f;
        }

        continuous[0] = horizontal;
        continuous[1] = vertical;
        continuous[2] = rotate;

        int stance = IdleStance;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            stance = ButterflyStance;
        }
        else if (Input.GetKey(KeyCode.Space))
        {
            stance = PokeStance;
        }

        discrete[0] = stance;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_episodeEnded || puckRigidbody == null)
        {
            return;
        }

        if (collision.rigidbody == puckRigidbody || collision.gameObject == puckRigidbody.gameObject)
        {
            AddReward(onPuckInterceptReward);
            _episodeEnded = true;
            EndEpisode();
        }
    }

    private void SetStance(int stance)
    {
        int clamped = Mathf.Clamp(stance, IdleStance, PokeStance);
        _currentStance = clamped;

        if (_currentStance == ButterflyStance)
        {
            transform.localScale = new Vector3(_baseScale.x * butterflyWidthScale, _baseScale.y, _baseScale.z);
        }
        else
        {
            transform.localScale = _baseScale;
        }
    }

    private void TryPokeCheck()
    {
        if (puckRigidbody == null || Time.time < _nextPokeAllowedTime)
        {
            return;
        }

        Vector3 toPuck = puckRigidbody.position - transform.position;
        float puckDistance = toPuck.magnitude;
        if (puckDistance > pokeRange)
        {
            return;
        }

        Vector3 toPuckDir = toPuck / Mathf.Max(puckDistance, Epsilon);
        if (Vector3.Dot(transform.forward, toPuckDir) < pokeForwardDotThreshold)
        {
            return;
        }

        Vector3 clearDirection = (puckRigidbody.position - GetGoalCenterPosition()).normalized;
        clearDirection.y = 0f;
        if (clearDirection.sqrMagnitude < Epsilon)
        {
            clearDirection = -transform.forward;
        }

        puckRigidbody.AddForce((clearDirection.normalized + Vector3.up * 0.06f) * pokeImpulse, ForceMode.Impulse);
        _nextPokeAllowedTime = Time.time + pokeCooldownSeconds;
    }

    private void AddShapedRewards()
    {
        Vector3 puckPos = GetPuckPosition();
        Vector3 goalPos = GetGoalCenterPosition();
        Vector3 agentPos = transform.position;

        float laneDistance = DistanceToSegment(agentPos, puckPos, goalPos, out float laneT);
        AddReward(-distanceToLanePenaltyScale * laneDistance);

        if (laneT > 0f && laneT < 1f && laneDistance <= passingLaneThreshold)
        {
            AddReward(inPassingLaneRewardPerStep);
        }

        Vector3 puckVelocity = GetPuckVelocity();
        if (puckVelocity.sqrMagnitude > Epsilon)
        {
            float facingScore = Mathf.Max(0f, Vector3.Dot(transform.forward, -puckVelocity.normalized));
            AddReward(facingPuckRewardPerStep * facingScore);
        }

        if (_currentStance == ButterflyStance)
        {
            float puckDistance = Vector3.Distance(agentPos, puckPos);
            if (puckDistance > farPuckDistance)
            {
                AddReward(-unnecessaryBlockPenaltyPerStep);
            }
        }
    }

    private Vector3 ComputeGoalLineIntercept(Vector3 puckPos, Vector3 puckVel)
    {
        if (Mathf.Abs(puckVel.z) < Epsilon)
        {
            return GetDefensiveBisectorTarget(puckPos);
        }

        float t = (goalLineZ - puckPos.z) / puckVel.z;
        Vector3 intercept = puckPos + puckVel * t;
        intercept.z = goalLineZ;
        intercept.y = goalY;
        return intercept;
    }

    private Vector3 GetDefensiveBisectorTarget(Vector3 puckPos)
    {
        Vector3 left = GetLeftPostPosition();
        Vector3 right = GetRightPostPosition();

        Vector3 toLeft = (left - puckPos).normalized;
        Vector3 toRight = (right - puckPos).normalized;
        Vector3 bisector = (toLeft + toRight).normalized;
        if (bisector.sqrMagnitude < Epsilon)
        {
            bisector = (GetGoalCenterPosition() - puckPos).normalized;
        }

        float dz = Mathf.Abs(bisector.z) < Epsilon ? 0f : (goalLineZ - puckPos.z) / bisector.z;
        Vector3 projected = puckPos + bisector * dz;
        projected.z = goalLineZ;
        projected.y = goalY;
        return projected;
    }

    private Vector3 GetPuckPosition()
    {
        return puckRigidbody != null ? puckRigidbody.position : Vector3.zero;
    }

    private Vector3 GetPuckVelocity()
    {
        return puckRigidbody != null ? puckRigidbody.linearVelocity : Vector3.zero;
    }

    private Vector3 GetGoalCenterPosition()
    {
        if (goalCenter != null)
        {
            return goalCenter.position;
        }

        return new Vector3((goalLeftX + goalRightX) * 0.5f, goalY, goalLineZ);
    }

    private Vector3 GetLeftPostPosition()
    {
        if (leftGoalPost != null)
        {
            return leftGoalPost.position;
        }

        return new Vector3(goalLeftX, goalY, goalLineZ);
    }

    private Vector3 GetRightPostPosition()
    {
        if (rightGoalPost != null)
        {
            return rightGoalPost.position;
        }

        return new Vector3(goalRightX, goalY, goalLineZ);
    }

    private static float DistanceToSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd, out float t)
    {
        Vector3 segment = segmentEnd - segmentStart;
        float segmentLengthSq = segment.sqrMagnitude;
        if (segmentLengthSq < Epsilon)
        {
            t = 0f;
            return Vector3.Distance(point, segmentStart);
        }

        t = Vector3.Dot(point - segmentStart, segment) / segmentLengthSq;
        float clampedT = Mathf.Clamp01(t);
        Vector3 projection = segmentStart + segment * clampedT;
        return Vector3.Distance(point, projection);
    }
}
