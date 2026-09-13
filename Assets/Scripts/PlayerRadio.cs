using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class PlayerRadio : MonoBehaviour
{
    [Header("Tracks")]
    [Tooltip("Add your 3 (or more) music tracks here.")]
    [SerializeField] private AudioClip[] tracks;
    [Tooltip("Optional names shown in the bar. Falls back to 'Track 1' etc.")]
    [SerializeField] private string[] trackNames;

    [Header("Controls")]
    [SerializeField] private KeyCode cycleKey = KeyCode.M;
    [Range(0f, 1f)][SerializeField] private float volume = 0.5f;
    [Tooltip("Start playing the first track automatically.")]
    [SerializeField] private bool startOnFirstTrack = true;

    [Header("Bar")]
    [SerializeField] private Vector2 barSize = new Vector2(220f, 44f);
    [SerializeField] private float barMargin = 16f;
    [SerializeField] private Color barColor = new Color(0.05f, 0.06f, 0.09f, 0.85f);
    [SerializeField] private Color accentColor = new Color(0.3f, 0.75f, 1f);

    private AudioSource source;
    private int state = 0;   // 0 = off, 1..N = track

    private GUIStyle titleStyle, nameStyle;
    private Texture2D panel, dot;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;   // 2D
        source.volume = volume;
    }

    private void Start()
    {
        if (startOnFirstTrack && tracks != null && tracks.Length > 0)
        {
            state = 1;
            ApplyState();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(cycleKey))
        {
            int stateCount = (tracks != null ? tracks.Length : 0) + 1;
            state = (state + 1) % stateCount;
            ApplyState();
        }
    }

    private void ApplyState()
    {
        if (state == 0 || tracks == null || tracks.Length == 0)
        {
            source.Stop();
            source.clip = null;
            return;
        }

        AudioClip clip = tracks[state - 1];
        if (clip == null) { source.Stop(); return; }

        source.clip = clip;
        source.volume = volume;
        source.Play();
    }

    private string CurrentName()
    {
        if (state == 0) return "Off";
        int i = state - 1;
        if (trackNames != null && i < trackNames.Length && !string.IsNullOrEmpty(trackNames[i]))
            return trackNames[i];
        return $"Track {state}";
    }

    private void EnsureGui()
    {
        if (titleStyle != null) return;

        titleStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 12, fontStyle = FontStyle.Bold };
        titleStyle.normal.textColor = new Color(0.6f, 0.65f, 0.72f);

        nameStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 18, fontStyle = FontStyle.Bold };
        nameStyle.normal.textColor = Color.white;

        panel = new Texture2D(1, 1); panel.SetPixel(0, 0, Color.white); panel.Apply();

        dot = new Texture2D(16, 16);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float dx = x - 7.5f, dy = y - 7.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 8f;
                dot.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1f - Mathf.InverseLerp(0.8f, 1f, d))));
            }
        dot.Apply();
    }

    private void OnGUI()
    {
        EnsureGui();

        float w = barSize.x, h = barSize.y;
        float x = Screen.width - w - barMargin;
        float y = Screen.height - h - barMargin;

        // Panel.
        GUI.color = barColor;
        GUI.DrawTexture(new Rect(x, y, w, h), panel);
        GUI.color = Color.white;

        float pad = 12f;

        // Status dot: accent + pulsing when playing, dim when off.
        bool playing = state != 0;
        Color dotCol = playing
            ? Color.Lerp(accentColor * 0.5f, accentColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f))
            : new Color(0.4f, 0.4f, 0.4f, 0.8f);
        GUI.color = dotCol;
        GUI.DrawTexture(new Rect(x + pad, y + h * 0.5f - 6f, 12f, 12f), dot);
        GUI.color = Color.white;

        float textX = x + pad + 22f;
        float textW = w - (pad + 22f) - pad;

        GUI.Label(new Rect(textX, y + 6f, textW, 16f), "RADIO  [M]", titleStyle);
        GUI.Label(new Rect(textX, y + 20f, textW, 22f), CurrentName(), nameStyle);
    }
}