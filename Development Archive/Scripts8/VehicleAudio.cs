using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class VehicleAudio : MonoBehaviour
{
    [Header("Clips for this vehicle type")]
    [Tooltip("Horns, pass-bys, etc. One is picked at random each time.")]
    [SerializeField] private AudioClip[] clips;

    [Header("Timing")]
    [Tooltip("Shortest gap between sounds (seconds).")]
    [SerializeField] private float minInterval = 4f;
    [Tooltip("Longest gap between sounds (seconds).")]
    [SerializeField] private float maxInterval = 10f;
    [Tooltip("Wait at least this long before the FIRST sound after spawning.")]
    [SerializeField] private float firstDelayMin = 0.5f;
    [SerializeField] private float firstDelayMax = 4f;

    [Header("3D Sound")]
    [Tooltip("Volume at the source.")]
    [Range(0f, 1f)][SerializeField] private float volume = 0.8f;
    [Tooltip("Beyond this distance the sound is inaudible.")]
    [SerializeField] private float maxDistance = 40f;
    [Tooltip("Randomise pitch a little so cars don't sound identical.")]
    [SerializeField] private float pitchJitter = 0.08f;

    private AudioSource source;
    private float timer;

    private void Awake()
    {
        source = GetComponent<AudioSource>();

        // Configure it as a 3D source (proximity-based volume).
        source.playOnAwake = false;
        source.spatialBlend = 1f;                 // fully 3D
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 3f;
        source.maxDistance = maxDistance;
        source.volume = volume;
    }

    private void OnEnable()
    {
        timer = Random.Range(firstDelayMin, firstDelayMax);
    }

    private void Update()
    {
        if (clips == null || clips.Length == 0) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            PlayOne();
            timer = Random.Range(minInterval, maxInterval);
        }
    }

    private void PlayOne()
    {
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        source.PlayOneShot(clip, volume);
    }
}