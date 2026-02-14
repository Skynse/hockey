using UnityEngine;

public class PuckHighlight : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject player;
    [SerializeField] private GameObject puck;

    [Header("Highlight Settings")]
    [SerializeField] private float highlightRange = 4f; // Match hitRange from mainPlayer
    [SerializeField] private Color highlightColor = new Color(0f, 1f, 1f, 1f); // Cyan glow
    [SerializeField] private float emissionIntensity = 2f;
    [SerializeField] private bool enablePulsing = true;
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField] private float pulseAmount = 0.5f;

    [Header("Optional Ring Highlight")]
    [SerializeField] private bool useRingHighlight = true;
    [SerializeField] private float ringScale = 1.3f;
    [SerializeField] private Color ringColor = new Color(1f, 1f, 0f, 0.3f); // Yellow transparent

    private Renderer puckRenderer;
    private Material puckMaterial;
    private Color originalEmissionColor;
    private bool wasInRange = false;

    // Optional highlight ring
    private GameObject highlightRing;
    private Material ringMaterial;

    void Start()
    {
        // Auto-find references if not assigned
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                var mainPlayerScript = FindObjectOfType<mainPlayer>();
                if (mainPlayerScript != null)
                    player = mainPlayerScript.gameObject;
            }
        }

        if (puck == null)
        {
            puck = GameObject.FindGameObjectWithTag("Puck");
        }

        if (puck == null)
        {
            Debug.LogError("PuckHighlight: Puck not found! Please assign it in the Inspector.");
            enabled = false;
            return;
        }

        // Get puck renderer and create a copy of its material
        puckRenderer = puck.GetComponent<Renderer>();
        if (puckRenderer != null)
        {
            // Create a material instance so we don't affect other objects
            puckMaterial = new Material(puckRenderer.material);
            puckRenderer.material = puckMaterial;

            // Enable emission keyword
            puckMaterial.EnableKeyword("_EMISSION");

            // Store original emission
            if (puckMaterial.HasProperty("_EmissionColor"))
            {
                originalEmissionColor = puckMaterial.GetColor("_EmissionColor");
            }
        }

        // Create optional highlight ring
        if (useRingHighlight)
        {
            CreateHighlightRing();
        }
    }

    void Update()
    {
        if (player == null || puck == null) return;

        float distance = Vector3.Distance(player.transform.position, puck.transform.position);
        bool inRange = distance <= highlightRange;

        if (inRange)
        {
            ActivateHighlight();
        }
        else
        {
            DeactivateHighlight();
        }

        // Update pulsing effect if in range
        if (inRange && enablePulsing)
        {
            UpdatePulsingEffect();
        }
    }

    private void ActivateHighlight()
    {
        if (!wasInRange)
        {
            wasInRange = true;
        }

        // Enable emission glow
        if (puckMaterial != null && puckMaterial.HasProperty("_EmissionColor"))
        {
            Color emissionColor = highlightColor * emissionIntensity;
            puckMaterial.SetColor("_EmissionColor", emissionColor);
        }

        // Show highlight ring
        if (highlightRing != null)
        {
            highlightRing.SetActive(true);
        }
    }

    private void DeactivateHighlight()
    {
        if (wasInRange)
        {
            wasInRange = false;

            // Reset emission
            if (puckMaterial != null && puckMaterial.HasProperty("_EmissionColor"))
            {
                puckMaterial.SetColor("_EmissionColor", originalEmissionColor);
            }

            // Hide highlight ring
            if (highlightRing != null)
            {
                highlightRing.SetActive(false);
            }
        }
    }

    private void UpdatePulsingEffect()
    {
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;

        // Pulse emission intensity
        if (puckMaterial != null && puckMaterial.HasProperty("_EmissionColor"))
        {
            Color emissionColor = highlightColor * emissionIntensity * pulse;
            puckMaterial.SetColor("_EmissionColor", emissionColor);
        }

        // Pulse ring scale
        if (highlightRing != null)
        {
            float scale = ringScale * pulse;
            highlightRing.transform.localScale = Vector3.one * scale;
        }
    }

    private void CreateHighlightRing()
    {
        // Create a simple torus-like ring using a flattened sphere
        highlightRing = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        highlightRing.name = "PuckHighlightRing";
        highlightRing.transform.SetParent(puck.transform);
        highlightRing.transform.localPosition = Vector3.zero;
        highlightRing.transform.localScale = Vector3.one * ringScale;

        // Flatten it to make a ring shape
        highlightRing.transform.localScale = new Vector3(ringScale, 0.1f, ringScale);

        // Remove collider so it doesn't interfere with physics
        Collider ringCollider = highlightRing.GetComponent<Collider>();
        if (ringCollider != null)
            Destroy(ringCollider);

        // Create transparent glowing material
        ringMaterial = new Material(Shader.Find("Standard"));
        ringMaterial.SetFloat("_Mode", 3); // Transparent mode
        ringMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        ringMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        ringMaterial.SetInt("_ZWrite", 0);
        ringMaterial.DisableKeyword("_ALPHATEST_ON");
        ringMaterial.EnableKeyword("_ALPHABLEND_ON");
        ringMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        ringMaterial.renderQueue = 3000;

        ringMaterial.SetColor("_Color", ringColor);
        ringMaterial.EnableKeyword("_EMISSION");
        ringMaterial.SetColor("_EmissionColor", ringColor * 2f);

        highlightRing.GetComponent<Renderer>().material = ringMaterial;
        highlightRing.SetActive(false);
    }

    private void OnDestroy()
    {
        // Clean up created materials
        if (puckMaterial != null)
            Destroy(puckMaterial);
        if (ringMaterial != null)
            Destroy(ringMaterial);
    }

    // Optional: Visualize range in editor
    private void OnDrawGizmosSelected()
    {
        if (player != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(player.transform.position, highlightRange);
        }
    }
}
