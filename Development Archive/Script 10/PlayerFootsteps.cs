using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerFootsteps : MonoBehaviour
{
    [Header("Sound")]
    [SerializeField] private AudioClip footstepClip;
    [Tooltip("Effective loudness. Can go ABOVE 1 to punch through music.")]
    [SerializeField] private float volume = 1.5f;
    [Tooltip("Random pitch wobble so repeated steps don't sound identical.")]
    [SerializeField] private float pitchJitter = 0.1f;

    [Header("Pace")]
    [SerializeField] private float stepInterval = 0.32f;
    [SerializeField] private float moveThreshold = 0.1f;
    [SerializeField] private bool stopStepWhenIdle = true;

    [Header("Grounding (optional)")]
    [SerializeField] private CharacterController controller;

    private AudioSource source;
    private float timer;
    private bool wasMoving;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;  // 2D
        source.clip = footstepClip;

        if (controller == null) controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        bool moving = new Vector2(h, v).magnitude > moveThreshold;
        bool grounded = controller == null || controller.isGrounded;

        if (moving && grounded)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                PlayStep();
                timer = stepInterval;
            }
        }
        else
        {
            if (wasMoving && stopStepWhenIdle && source.isPlaying)
                source.Stop();
            timer = 0f;
        }

        wasMoving = moving && grounded;
    }

    private void PlayStep()
    {
        if (footstepClip == null) return;

        source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);

        // A single AudioSource caps at volume 1. To go louder, split the
        // volume across as many stacked one-shots as needed. e.g. volume 1.5
        // plays one full step (1.0) plus a second at 0.5 = 1.5 effective.
        float remaining = Mathf.Max(0f, volume);
        while (remaining > 0f)
        {
            float chunk = Mathf.Min(1f, remaining);
            source.PlayOneShot(footstepClip, chunk);
            remaining -= chunk;
        }
    }
}