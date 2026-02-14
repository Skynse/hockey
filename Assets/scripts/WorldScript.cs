using UnityEngine;
using TMPro;

public class WorldScript : MonoBehaviour
{
    [Header("Goal Detection Settings")]
    [SerializeField] private float goalLineZ = 33f;
    [SerializeField] private float goalLeftX = -3.8f;
    [SerializeField] private float goalRightX = 3.8f;

    [Header("Game Objects")]
    [SerializeField] private GameObject puck;
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject opponent;

    [Header("Reset Positions")]
    [SerializeField] private Vector3 puckResetPosition = new Vector3(0, 0.5f, 0);
    [SerializeField] private Vector3 playerResetPosition = new Vector3(0, 0, -10);
    [SerializeField] private Vector3 opponentResetPosition = new Vector3(0, 0, 25);

    [Header("Score Settings")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private float resetDelay = 2f; // Delay before resetting after goal

    private int playerScore = 0;
    private bool goalScored = false;
    private Rigidbody puckRigidbody;

    void Start()
    {
        // Auto-find puck if not assigned
        if (puck == null)
        {
            puck = GameObject.FindGameObjectWithTag("Puck");
            if (puck == null)
            {
                Debug.LogError("WorldScript: Puck not found! Please assign it or tag it as 'Puck'.");
            }
        }

        if (puck != null)
        {
            puckRigidbody = puck.GetComponent<Rigidbody>();
        }

        // Auto-find player if not assigned
        if (player == null)
        {
            var mainPlayerScript = FindObjectOfType<mainPlayer>();
            if (mainPlayerScript != null)
            {
                player = mainPlayerScript.gameObject;
            }
        }

        // Update score display
        UpdateScoreDisplay();
    }

    void Update()
    {
        if (puck == null || goalScored) return;

        // Check if puck crossed the goal line
        if (puck.transform.position.z >= goalLineZ)
        {
            CheckForGoal();
        }
    }

    private void CheckForGoal()
    {
        float puckX = puck.transform.position.x;

        // Check if puck is within the net bounds
        if (puckX >= goalLeftX && puckX <= goalRightX)
        {
            // GOAL!
            GoalScored();
        }
        else
        {
            // Puck went wide/out of bounds
            Debug.Log("Puck went wide! No goal.");
            ResetState(false);
        }
    }

    private void GoalScored()
    {
        goalScored = true;
        playerScore++;

        Debug.Log($"GOAL!!! Score: {playerScore}");
        UpdateScoreDisplay();

        // Show goal celebration message
        if (scoreText != null)
        {
            string originalText = scoreText.text;
            scoreText.text = "GOAL!!!";
            scoreText.color = Color.yellow;

            // Reset text after delay
            Invoke(nameof(RestoreScoreText), 1f);
        }

        // Reset after delay
        Invoke(nameof(ResetAfterGoal), resetDelay);
    }

    private void RestoreScoreText()
    {
        if (scoreText != null)
        {
            scoreText.color = Color.white;
            UpdateScoreDisplay();
        }
    }

    private void ResetAfterGoal()
    {
        ResetState(true);
    }

    private void ResetState(bool wasGoal)
    {
        goalScored = false;

        // Reset puck position and velocity
        if (puck != null)
        {
            puck.transform.position = puckResetPosition;
            if (puckRigidbody != null)
            {
                puckRigidbody.linearVelocity = Vector3.zero;
                puckRigidbody.angularVelocity = Vector3.zero;
            }
        }

        // Reset player position
        if (player != null)
        {
            player.transform.position = playerResetPosition;
        }

        // Reset opponent position
        if (opponent != null)
        {
            opponent.transform.position = opponentResetPosition;
        }

        Debug.Log(wasGoal ? "State reset after goal. Play continues!" : "State reset. Play continues!");
    }

    private void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score: {playerScore}";
        }
    }

    // Public methods for external access
    public void ResetGame()
    {
        playerScore = 0;
        UpdateScoreDisplay();
        ResetState(false);
        Debug.Log("Game reset!");
    }

    public int GetScore()
    {
        return playerScore;
    }

    // Visualize goal line and net bounds in editor
    private void OnDrawGizmos()
    {
        // Draw goal line
        Gizmos.color = Color.red;
        Vector3 leftPost = new Vector3(goalLeftX, 0, goalLineZ);
        Vector3 rightPost = new Vector3(goalRightX, 0, goalLineZ);
        Vector3 leftPostTop = new Vector3(goalLeftX, 2, goalLineZ);
        Vector3 rightPostTop = new Vector3(goalRightX, 2, goalLineZ);

        // Draw goal posts
        Gizmos.DrawLine(leftPost, leftPostTop);
        Gizmos.DrawLine(rightPost, rightPostTop);
        Gizmos.DrawLine(leftPostTop, rightPostTop);
        Gizmos.DrawLine(leftPost, rightPost);

        // Draw goal line extending outward
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(-10, 0, goalLineZ), new Vector3(10, 0, goalLineZ));

        // Draw reset positions
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(puckResetPosition, 0.5f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(playerResetPosition, 1f);
        if (opponent != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(opponentResetPosition, 1f);
        }
    }
}
