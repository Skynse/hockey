# C# for Unity Game Development - Quick Start Guide

A practical guide to C# programming for Unity game development, tailored for your hockey game project.

## Table of Contents
1. [Unity Script Fundamentals](#unity-script-fundamentals)
2. [MonoBehaviour Lifecycle](#monobehaviour-lifecycle)
3. [Variables and Data Types](#variables-and-data-types)
4. [Unity Components and GameObjects](#unity-components-and-gameobjects)
5. [Input Handling](#input-handling)
6. [Physics and Collisions](#physics-and-collisions)
7. [Events and Delegates](#events-and-delegates)
8. [Coroutines](#coroutines)
9. [Common Patterns in Unity](#common-patterns-in-unity)
10. [Best Practices](#best-practices)

---

## Unity Script Fundamentals

### Basic Script Structure

Every Unity C# script inherits from `MonoBehaviour`:

```csharp
using UnityEngine;  // Core Unity functionality

public class MyScript : MonoBehaviour
{
    // Variables go here

    void Start()
    {
        // Runs once when the script first starts
    }

    void Update()
    {
        // Runs every frame
    }
}
```

### Creating a Script

1. In Unity Editor: Right-click in Project window → Create → C# Script
2. Name it (use PascalCase: `PlayerController`, `GameManager`)
3. Double-click to open in your code editor
4. Attach to GameObject by dragging script onto it in Inspector

---

## MonoBehaviour Lifecycle

Unity calls these methods automatically at specific times:

```csharp
public class LifecycleExample : MonoBehaviour
{
    // Called when script instance is being loaded (before Start)
    void Awake()
    {
        Debug.Log("Awake called - initialize references here");
    }

    // Called when script is enabled, just before any Update methods
    void Start()
    {
        Debug.Log("Start called - initialize game logic here");
    }

    // Called every frame (60+ times per second typically)
    void Update()
    {
        Debug.Log($"Update called - Frame: {Time.frameCount}");
    }

    // Called at fixed intervals (default: 50 times per second)
    void FixedUpdate()
    {
        Debug.Log("FixedUpdate called - use for physics");
    }

    // Called every frame AFTER all Updates
    void LateUpdate()
    {
        Debug.Log("LateUpdate called - use for camera following");
    }

    // Called when script/GameObject is disabled or destroyed
    void OnDestroy()
    {
        Debug.Log("OnDestroy called - cleanup here");
    }
}
```

**When to use each:**
- `Awake()`: Initialize references to components
- `Start()`: Initialize game state, subscribe to events
- `Update()`: Handle input, move objects, game logic
- `FixedUpdate()`: Physics calculations, apply forces
- `LateUpdate()`: Camera following (runs after all Updates)
- `OnDestroy()`: Unsubscribe from events, cleanup

---

## Variables and Data Types

### Common C# Data Types

```csharp
public class DataTypesExample : MonoBehaviour
{
    // Numeric types
    int playerHealth = 100;              // Whole numbers (-2,147,483,648 to 2,147,483,647)
    float moveSpeed = 5.5f;              // Decimals (f suffix required)
    double preciseValue = 3.14159265;    // High precision decimals (rarely used)

    // Boolean
    bool isAlive = true;                 // true or false

    // Text
    string playerName = "Player1";       // Text
    char grade = 'A';                    // Single character

    // Unity-specific types
    Vector3 position = new Vector3(0, 1, 0);          // 3D position (x, y, z)
    Vector2 screenPos = new Vector2(100, 200);        // 2D position (x, y)
    Color playerColor = Color.red;                    // Color (RGBA)
    Quaternion rotation = Quaternion.identity;        // Rotation

    // Arrays and Lists
    int[] scores = new int[5];                        // Fixed-size array
    string[] names = { "Alice", "Bob", "Charlie" };   // Array with initial values

    // Lists (dynamic size)
    System.Collections.Generic.List<GameObject> enemies = new System.Collections.Generic.List<GameObject>();

    void Start()
    {
        // Accessing arrays
        scores[0] = 100;
        Debug.Log($"First name: {names[0]}");

        // Using lists
        enemies.Add(GameObject.Find("Enemy1"));
        Debug.Log($"Enemy count: {enemies.Count}");
    }
}
```

### SerializeField - Exposing Variables in Inspector

```csharp
public class PlayerController : MonoBehaviour
{
    // Public variables appear in Inspector (but can be accessed by other scripts)
    public float maxHealth = 100f;

    // Private variables are hidden (preferred for encapsulation)
    private float currentHealth;

    // [SerializeField] makes private variables visible in Inspector
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private GameObject weaponPrefab;

    // [Header] adds a label in Inspector
    [Header("Audio Settings")]
    [SerializeField] private AudioClip jumpSound;

    // [Range] creates a slider in Inspector
    [Range(0, 10)]
    [SerializeField] private float volume = 5f;

    void Start()
    {
        currentHealth = maxHealth;
    }
}
```

---

## Unity Components and GameObjects

### Finding and Accessing GameObjects

```csharp
public class GameObjectExample : MonoBehaviour
{
    // Assign in Inspector (drag and drop)
    [SerializeField] private GameObject puck;
    [SerializeField] private Transform playerTransform;

    void Start()
    {
        // Find GameObject by name (slow, avoid in Update)
        GameObject goal = GameObject.Find("GoalPost");

        // Find GameObject by tag (faster)
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        // Find multiple objects with tag
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        // Get component on same GameObject
        Rigidbody rb = GetComponent<Rigidbody>();

        // Get component on child GameObject
        Animator animator = GetComponentInChildren<Animator>();

        // Get component from specific GameObject
        if (puck != null)
        {
            Rigidbody puckRb = puck.GetComponent<Rigidbody>();
        }
    }
}
```

### Modifying Transforms

```csharp
public class TransformExample : MonoBehaviour
{
    [SerializeField] private float speed = 5f;

    void Update()
    {
        // Position
        transform.position = new Vector3(0, 1, 0);              // Set absolute position
        transform.position += Vector3.forward * speed * Time.deltaTime;  // Move forward

        // Rotation
        transform.rotation = Quaternion.Euler(0, 90, 0);        // Set rotation (degrees)
        transform.Rotate(0, 90 * Time.deltaTime, 0);            // Rotate over time

        // Scale
        transform.localScale = new Vector3(2, 2, 2);            // Set scale (2x bigger)

        // Parent-child relationships
        transform.parent = GameObject.Find("Container").transform;  // Set parent
        Transform child = transform.GetChild(0);                 // Get first child
    }
}
```

---

## Input Handling

### Traditional Input System

```csharp
public class InputExample : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 5f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Keyboard input
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Space pressed!");
        }

        if (Input.GetKey(KeyCode.W))
        {
            Debug.Log("W held down");
        }

        if (Input.GetKeyUp(KeyCode.S))
        {
            Debug.Log("S released");
        }

        // Mouse input
        if (Input.GetMouseButtonDown(0))  // 0 = left, 1 = right, 2 = middle
        {
            Debug.Log("Left mouse clicked");
        }

        // Axis input (returns -1 to 1)
        float horizontal = Input.GetAxis("Horizontal");  // A/D or Arrow keys
        float vertical = Input.GetAxis("Vertical");      // W/S or Arrow keys

        // Move player
        Vector3 movement = new Vector3(horizontal, 0, vertical) * moveSpeed * Time.deltaTime;
        transform.position += movement;
    }
}
```

### New Input System (Unity's modern approach)

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public class NewInputExample : MonoBehaviour
{
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction attackAction;

    void Awake()
    {
        // Get reference to Input System
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        attackAction = playerInput.actions["Attack"];
    }

    void OnEnable()
    {
        // Subscribe to attack action
        attackAction.performed += OnAttack;
    }

    void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        attackAction.performed -= OnAttack;
    }

    void Update()
    {
        // Read move input
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        Debug.Log($"Move: {moveInput}");
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        Debug.Log("Attack performed!");
    }
}
```

---

## Physics and Collisions

### Using Rigidbody

```csharp
public class PhysicsExample : MonoBehaviour
{
    private Rigidbody rb;

    [SerializeField] private float moveForce = 10f;
    [SerializeField] private float jumpForce = 5f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()  // Always use FixedUpdate for physics!
    {
        // Apply force (gradual acceleration)
        rb.AddForce(Vector3.forward * moveForce);

        // Apply impulse (instant velocity change)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        // Direct velocity manipulation (less realistic)
        rb.velocity = new Vector3(5, rb.velocity.y, 0);
    }
}
```

### Collision Detection

```csharp
public class CollisionExample : MonoBehaviour
{
    // Called when collision starts (requires Collider + Rigidbody)
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"Hit: {collision.gameObject.name}");

        // Check what we hit
        if (collision.gameObject.CompareTag("Puck"))
        {
            Debug.Log("Hit the puck!");
        }

        // Get collision force
        float impactForce = collision.impulse.magnitude;
        Debug.Log($"Impact force: {impactForce}");
    }

    // Called while collision continues
    void OnCollisionStay(Collision collision)
    {
        Debug.Log("Still colliding...");
    }

    // Called when collision ends
    void OnCollisionExit(Collision collision)
    {
        Debug.Log("Collision ended");
    }

    // Trigger events (requires Collider with "Is Trigger" checked)
    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Entered trigger: {other.name}");

        if (other.CompareTag("Goal"))
        {
            Debug.Log("GOAL!");
        }
    }

    void OnTriggerExit(Collider other)
    {
        Debug.Log($"Exited trigger: {other.name}");
    }
}
```

**Collider vs Trigger:**
- **Collider**: Physical collision, objects bounce off each other
- **Trigger**: Detection only, objects pass through (for goals, pickups, zones)

---

## Events and Delegates

Events allow scripts to communicate without tight coupling.

### Basic Events

```csharp
public class ScoreManager : MonoBehaviour
{
    // Define event
    public delegate void ScoreChanged(int newScore);
    public event ScoreChanged OnScoreChanged;

    private int score = 0;

    public void AddScore(int points)
    {
        score += points;

        // Trigger event (? prevents null reference if no subscribers)
        OnScoreChanged?.Invoke(score);
    }
}

public class ScoreDisplay : MonoBehaviour
{
    [SerializeField] private ScoreManager scoreManager;

    void Start()
    {
        // Subscribe to event
        scoreManager.OnScoreChanged += UpdateDisplay;
    }

    void OnDestroy()
    {
        // ALWAYS unsubscribe to prevent memory leaks
        scoreManager.OnScoreChanged -= UpdateDisplay;
    }

    private void UpdateDisplay(int newScore)
    {
        Debug.Log($"Score updated: {newScore}");
    }
}
```

### Unity Events (Inspector-friendly)

```csharp
using UnityEngine;
using UnityEngine.Events;

public class Button : MonoBehaviour
{
    // Appears in Inspector - can assign methods by dragging
    [SerializeField] private UnityEvent onClick;

    void OnMouseDown()
    {
        onClick?.Invoke();
    }
}

public class GameManager : MonoBehaviour
{
    public void OnButtonClicked()
    {
        Debug.Log("Button clicked!");
    }
}
```

---

## Coroutines

Coroutines allow you to spread actions over multiple frames or add delays.

```csharp
public class CoroutineExample : MonoBehaviour
{
    void Start()
    {
        // Start a coroutine
        StartCoroutine(CountdownCoroutine(5));
        StartCoroutine(FadeOutCoroutine());
    }

    // Coroutine with delay
    IEnumerator CountdownCoroutine(int seconds)
    {
        for (int i = seconds; i > 0; i--)
        {
            Debug.Log($"Countdown: {i}");
            yield return new WaitForSeconds(1f);  // Wait 1 second
        }
        Debug.Log("GO!");
    }

    // Coroutine running over frames
    IEnumerator FadeOutCoroutine()
    {
        Renderer rend = GetComponent<Renderer>();
        Color color = rend.material.color;

        for (float alpha = 1f; alpha >= 0; alpha -= 0.01f)
        {
            color.a = alpha;
            rend.material.color = color;
            yield return null;  // Wait one frame
        }
    }

    // Coroutine with conditional wait
    IEnumerator WaitForCondition()
    {
        yield return new WaitUntil(() => Input.GetKeyDown(KeyCode.Space));
        Debug.Log("Space was pressed!");
    }

    // Stop a coroutine
    void StopMyCoroutine()
    {
        StopCoroutine(CountdownCoroutine(5));
        StopAllCoroutines();  // Stops all coroutines on this MonoBehaviour
    }
}
```

**Common yield types:**
- `yield return null` - Wait one frame
- `yield return new WaitForSeconds(2f)` - Wait 2 seconds
- `yield return new WaitForFixedUpdate()` - Wait for next physics update
- `yield return new WaitUntil(() => condition)` - Wait until condition is true

---

## Common Patterns in Unity

### Singleton Pattern

For managers that should only have one instance:

```csharp
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    void Awake()
    {
        // Ensure only one instance exists
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);  // Persist across scenes
    }
}

// Access from anywhere:
// GameManager.Instance.SomeMethod();
```

### Object Pooling

Reuse objects instead of constantly creating/destroying:

```csharp
using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private int poolSize = 10;

    private Queue<GameObject> pool = new Queue<GameObject>();

    void Start()
    {
        // Pre-create objects
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(prefab);
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    public GameObject GetObject()
    {
        if (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            obj.SetActive(true);
            return obj;
        }
        else
        {
            // Pool exhausted, create new object
            return Instantiate(prefab);
        }
    }

    public void ReturnObject(GameObject obj)
    {
        obj.SetActive(false);
        pool.Enqueue(obj);
    }
}
```

### State Machine

Manage different game states:

```csharp
public class PlayerStateMachine : MonoBehaviour
{
    public enum PlayerState
    {
        Idle,
        Moving,
        Attacking,
        Defending
    }

    private PlayerState currentState = PlayerState.Idle;

    void Update()
    {
        switch (currentState)
        {
            case PlayerState.Idle:
                HandleIdleState();
                break;
            case PlayerState.Moving:
                HandleMovingState();
                break;
            case PlayerState.Attacking:
                HandleAttackingState();
                break;
            case PlayerState.Defending:
                HandleDefendingState();
                break;
        }
    }

    private void HandleIdleState()
    {
        if (Input.GetAxis("Horizontal") != 0)
        {
            ChangeState(PlayerState.Moving);
        }
    }

    private void HandleMovingState()
    {
        // Movement logic
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ChangeState(PlayerState.Attacking);
        }
    }

    private void HandleAttackingState()
    {
        // Attack logic
    }

    private void HandleDefendingState()
    {
        // Defend logic
    }

    private void ChangeState(PlayerState newState)
    {
        Debug.Log($"State changed: {currentState} -> {newState}");
        currentState = newState;
    }
}
```

---

## Best Practices

### 1. Use Time.deltaTime for Frame-Independent Movement

```csharp
// BAD - speed depends on framerate
transform.position += Vector3.forward * speed;

// GOOD - consistent speed regardless of framerate
transform.position += Vector3.forward * speed * Time.deltaTime;
```

### 2. Cache Component References

```csharp
// BAD - GetComponent every frame is slow
void Update()
{
    GetComponent<Rigidbody>().AddForce(Vector3.up);
}

// GOOD - cache in Start/Awake
private Rigidbody rb;

void Start()
{
    rb = GetComponent<Rigidbody>();
}

void Update()
{
    rb.AddForce(Vector3.up);
}
```

### 3. Avoid Finding Objects in Update

```csharp
// BAD
void Update()
{
    GameObject player = GameObject.Find("Player");  // Slow!
}

// GOOD
[SerializeField] private GameObject player;  // Assign in Inspector

// Or find once
private GameObject player;

void Start()
{
    player = GameObject.Find("Player");
}
```

### 4. Use Null Checks

```csharp
void Start()
{
    Rigidbody rb = GetComponent<Rigidbody>();

    if (rb != null)
    {
        rb.AddForce(Vector3.up);
    }
    else
    {
        Debug.LogError("No Rigidbody found!");
    }
}
```

### 5. Name Variables Clearly

```csharp
// BAD
float s = 5;
GameObject o;

// GOOD
float moveSpeed = 5f;
GameObject enemyPrefab;
```

### 6. Use Debug.Log for Testing

```csharp
void Start()
{
    Debug.Log("Game started!");
    Debug.LogWarning("This is a warning");
    Debug.LogError("This is an error");

    // Log with context (click in console to highlight GameObject)
    Debug.Log("Position: " + transform.position, gameObject);
}
```

### 7. Always Unsubscribe from Events

```csharp
void OnEnable()
{
    EventManager.OnGameOver += HandleGameOver;
}

void OnDisable()
{
    EventManager.OnGameOver -= HandleGameOver;  // Important!
}
```

---

## Example: Complete Player Controller for Hockey

Putting it all together:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public class HockeyPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Shooting")]
    [SerializeField] private float shotPower = 10f;
    [SerializeField] private Transform stick;
    [SerializeField] private float shotCooldown = 1f;

    [Header("References")]
    [SerializeField] private GameObject puck;

    private Rigidbody rb;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction attackAction;
    private bool canShoot = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions["Move"];
        attackAction = playerInput.actions["Attack"];
    }

    void OnEnable()
    {
        attackAction.performed += OnAttack;
    }

    void OnDisable()
    {
        attackAction.performed -= OnAttack;
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        if (input.magnitude > 0.1f)
        {
            // Move
            Vector3 movement = new Vector3(input.x, 0, input.y) * moveSpeed;
            rb.velocity = new Vector3(movement.x, rb.velocity.y, movement.z);

            // Rotate to face movement direction
            Vector3 lookDirection = new Vector3(input.x, 0, input.y);
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (!canShoot || puck == null)
            return;

        ShootPuck();
        StartCoroutine(ShotCooldownRoutine());
    }

    private void ShootPuck()
    {
        Rigidbody puckRb = puck.GetComponent<Rigidbody>();

        if (puckRb != null)
        {
            Vector3 shootDirection = stick != null ? stick.forward : transform.forward;
            puckRb.AddForce(shootDirection * shotPower, ForceMode.Impulse);
            Debug.Log($"Shot puck with power: {shotPower}");
        }
    }

    private System.Collections.IEnumerator ShotCooldownRoutine()
    {
        canShoot = false;
        yield return new WaitForSeconds(shotCooldown);
        canShoot = true;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Puck"))
        {
            Debug.Log("Player touched the puck!");
        }
    }
}
```

---

## Quick Reference Cheat Sheet

### Common Unity Functions
```csharp
Awake()              // Initialize references
Start()              // Initialize game state
Update()             // Every frame
FixedUpdate()        // Fixed time step (physics)
LateUpdate()         // After all Updates
OnDestroy()          // Cleanup
OnEnable()           // When enabled
OnDisable()          // When disabled
```

### Common Components
```csharp
GetComponent<T>()              // Get component on this GameObject
GetComponentInChildren<T>()    // Get component on child
GetComponentInParent<T>()      // Get component on parent
transform.position             // GameObject position
transform.rotation             // GameObject rotation
transform.localScale           // GameObject scale
gameObject.SetActive(bool)     // Enable/disable GameObject
Destroy(gameObject)            // Destroy GameObject
Instantiate(prefab)            // Create new GameObject
```

### Physics
```csharp
rb.AddForce(Vector3)           // Apply force
rb.velocity                    // Current velocity
rb.angularVelocity             // Rotation speed
OnCollisionEnter(Collision)    // Collision started
OnTriggerEnter(Collider)       // Trigger entered
```

### Input
```csharp
Input.GetKey(KeyCode)          // Key held down
Input.GetKeyDown(KeyCode)      // Key pressed this frame
Input.GetKeyUp(KeyCode)        // Key released this frame
Input.GetAxis("Horizontal")    // -1 to 1
Input.GetMouseButtonDown(0)    // Mouse click
```

---

## Next Steps

1. Practice by modifying your `mainPlayer.cs` and `opponentControl.cs` scripts
2. Experiment with physics on the puck GameObject
3. Try implementing a simple scoring system
4. Add sound effects using AudioSource component
5. Create UI elements with Unity's Canvas system

## Additional Resources

- [Unity Learn](https://learn.unity.com/) - Official tutorials
- [Unity Scripting API](https://docs.unity3d.com/ScriptReference/) - Complete reference
- C# Programming Guide - Microsoft documentation
- Unity forums and Stack Overflow for problem-solving

---

Happy coding! Now you have the fundamentals to build your hockey game with gesture controls.
