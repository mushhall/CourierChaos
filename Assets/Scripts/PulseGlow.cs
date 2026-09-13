using UnityEngine;

public class PulseGlow : MonoBehaviour
{
    [Header("Pulse")]
    [Tooltip("Dimmest emission strength.")]
    [SerializeField] private float minIntensity = 0.4f;
    [Tooltip("Brightest emission strength.")]
    [SerializeField] private float maxIntensity = 2.5f;
    [Tooltip("Pulses per second-ish (higher = faster breathing).")]
    [SerializeField] private float pulseSpeed = 2.5f;

    private Material mat;
    private Color baseEmission = Color.white;

    private void Awake()
    {
        Renderer r = GetComponent<Renderer>();
        if (r != null) mat = r.material;

        if (mat != null)
        {
            mat.EnableKeyword("_EMISSION");

            // Read the emission colour you set on the material, normalised so
            // we can drive brightness ourselves.
            Color c = mat.GetColor("_EmissionColor");
            float max = Mathf.Max(c.r, c.g, c.b);
            baseEmission = max > 0.001f ? c / max : Color.white;
        }
    }

    private void Update()
    {
        if (mat == null) return;

        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, t);

        mat.SetColor("_EmissionColor", baseEmission * intensity);
    }
}