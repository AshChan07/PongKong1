using System.Collections;
using TMPro;
using UnityEngine;

public class RetroScoreUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI p1ScoreText;
    [SerializeField] private TextMeshProUGUI p2ScoreText;
    [SerializeField] private TextMeshProUGUI totalScoreText;
    [SerializeField] private ScoreManager scoreManager;

    [Header("Retro Theme")]
    [SerializeField] private Color baseColor = new Color(0f, 1f, 0f, 1f);
    [SerializeField] private bool useAmberTheme;

    [Header("Flash")]
    [SerializeField] [Range(1, 10)] private int flashCycles = 4;
    [SerializeField] private float flashDuration = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float flashDimFactor = 0.3f;

    [Header("Pulse")]
    [SerializeField] private bool enablePulse = true;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] [Range(0.2f, 1f)] private float pulseMinAlpha = 0.7f;

    private int lastP1Score;
    private int lastP2Score;
    private Coroutine p1FlashRoutine;
    private Coroutine p2FlashRoutine;
    private Coroutine pulseRoutine;
    private bool isFlashing;
    private static readonly Color AmberColor = new Color(1f, 0.69f, 0f, 1f);

    private void Start()
    {
        if (scoreManager == null)
            scoreManager = FindObjectOfType<ScoreManager>();

        if (scoreManager == null)
        {
            Debug.LogWarning("RetroScoreUI: ScoreManager not found in scene.");
            return;
        }

        lastP1Score = scoreManager.P1Score;
        lastP2Score = scoreManager.P2Score;
        ApplyRetroStyling();
        RefreshDisplay();
    }

    private void OnEnable()
    {
        if (scoreManager == null) return;
        lastP1Score = scoreManager.P1Score;
        lastP2Score = scoreManager.P2Score;
        if (enablePulse)
            pulseRoutine = StartCoroutine(PulseEffect());
    }

    private void OnDisable()
    {
        StopFlashRoutines();
        StopPulseRoutine();
    }

    private void Update()
    {
        if (scoreManager == null) return;

        int p1 = scoreManager.P1Score;
        int p2 = scoreManager.P2Score;

        if (p1 == lastP1Score && p2 == lastP2Score) return;

        bool p1Scored = p1 > lastP1Score;
        bool p2Scored = p2 > lastP2Score;

        lastP1Score = p1;
        lastP2Score = p2;

        RefreshDisplay();

        if (p1Scored) StartFlash(p1ScoreText, ref p1FlashRoutine);
        if (p2Scored) StartFlash(p2ScoreText, ref p2FlashRoutine);
    }

    private void RefreshDisplay()
    {
        if (p1ScoreText != null)
            p1ScoreText.text = $"P1: {scoreManager.P1Score:00}";
        if (p2ScoreText != null)
            p2ScoreText.text = $"P2: {scoreManager.P2Score:00}";
        if (totalScoreText != null)
            totalScoreText.text = $"{scoreManager.TotalMatchScore}";
    }

    private void ApplyRetroStyling()
    {
        Color color = useAmberTheme ? AmberColor : baseColor;

        if (p1ScoreText != null)
        {
            p1ScoreText.color = color;
            p1ScoreText.alignment = TextAlignmentOptions.MidlineLeft;
            p1ScoreText.fontStyle = FontStyles.Normal;
        }

        if (p2ScoreText != null)
        {
            p2ScoreText.color = color;
            p2ScoreText.alignment = TextAlignmentOptions.MidlineRight;
            p2ScoreText.fontStyle = FontStyles.Normal;
        }
    }

    private void StartFlash(TextMeshProUGUI target, ref Coroutine routine)
    {
        if (routine != null)
            StopCoroutine(routine);
        routine = StartCoroutine(FlashText(target, routine));
    }

    private IEnumerator FlashText(TextMeshProUGUI target, Coroutine routine)
    {
        if (target == null) yield break;

        isFlashing = true;
        Color full = useAmberTheme ? AmberColor : baseColor;
        Color dim = new Color(full.r, full.g, full.b, full.a * flashDimFactor);
        float interval = flashDuration / (flashCycles * 2f);
        var delay = new WaitForSeconds(interval);

        for (int i = 0; i < flashCycles; i++)
        {
            target.color = dim;
            yield return delay;
            target.color = full;
            yield return delay;
        }

        isFlashing = false;
    }

    private IEnumerator PulseEffect()
    {
        Color full = useAmberTheme ? AmberColor : baseColor;

        while (true)
        {
            if (!isFlashing)
            {
                float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
                float alpha = Mathf.Lerp(pulseMinAlpha, 1f, t);
                Color pulsed = new Color(full.r, full.g, full.b, alpha);
                if (p1ScoreText != null) p1ScoreText.color = pulsed;
                if (p2ScoreText != null) p2ScoreText.color = pulsed;
            }

            yield return null;
        }
    }

    private void StopFlashRoutines()
    {
        if (p1FlashRoutine != null) { StopCoroutine(p1FlashRoutine); p1FlashRoutine = null; }
        if (p2FlashRoutine != null) { StopCoroutine(p2FlashRoutine); p2FlashRoutine = null; }
    }

    private void StopPulseRoutine()
    {
        if (pulseRoutine != null) { StopCoroutine(pulseRoutine); pulseRoutine = null; }
    }
}
