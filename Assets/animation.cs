using UnityEngine;

public class animation : MonoBehaviour
{
    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
            Debug.LogWarning("animation.cs: No Animator found on this GameObject.");
    }

    public void SetStrafing(bool isMoving)
    {
        if (animator == null) return;
        if (isMoving)
            animator.Play("Strafe");
        else
            animator.Play("Idle"); // fall back to idle when still — adjust name if needed
    }

    public void PlaySwing()
    {
        if (animator == null) return;
        animator.Play("swing");
    }
}
