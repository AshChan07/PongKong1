using System.Collections;
using TMPro;
using UnityEngine;

public class RetroScoreUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI p1ScoreText;
    [SerializeField] private TextMeshProUGUI p2ScoreText;
    [SerializeField] private TextMeshProUGUI totalScoreText;
    [SerializeField] private ScoreStateGameEvent onScoreStateUpdated;

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
    private readonly bool[] flashing = new bool[2];
    private static readonly Color AmberColor = new Color(1f, 0.69f, 0f, 1f);

    private void Start()
    {
        ApplyRetroStyling();
    }

    private void OnEnable()
    {
        if (onScoreStateUpdated != null)
            onScoreStateUpdated.OnRaised += HandleScoreStateUpdated;

        if (enablePulse)
            pulseRoutine = StartCoroutine(PulseEffect());
    }

    private void OnDisable()
    {
        if (onScoreStateUpdated != null)
            onScoreStateUpdated.OnRaised -= HandleScoreStateUpdated;

        StopFlashRoutines();
        StopPulseRoutine();
    }

    private void HandleScoreStateUpdated(ScoreStateData data)
    {
        bool p1Scored = data.p1Score > lastP1Score;
        bool p2Scored = data.p2Score > lastP2Score;

        lastP1Score = data.p1Score;
        lastP2Score = data.p2Score;

        if (p1ScoreText != null)
            p1ScoreText.text = $"P1: {data.p1Score:00}";
        if (p2ScoreText != null)
            p2ScoreText.text = $"P2: {data.p2Score:00}";
        if (totalScoreText != null)
            totalScoreText.text = $"{data.totalMatchScore}";

        if (p1Scored) StartFlash(p1ScoreText, ref p1FlashRoutine, PlayerSide.P1);
        if (p2Scored) StartFlash(p2ScoreText, ref p2FlashRoutine, PlayerSide.P2);
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

    private void StartFlash(TextMeshProUGUI target, ref Coroutine routine, PlayerSide side)
    {
        if (routine != null)
            StopCoroutine(routine);
        routine = StartCoroutine(FlashText(target, side));
    }

    private IEnumerator FlashText(TextMeshProUGUI target, PlayerSide side)
    {
        if (target == null) yield break;

        int idx = (int)side;
        flashing[idx] = true;
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

        flashing[idx] = false;
    }

    private IEnumerator PulseEffect()
    {
        Color full = useAmberTheme ? AmberColor : baseColor;

        while (true)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(pulseMinAlpha, 1f, t);
            Color pulsed = new Color(full.r, full.g, full.b, alpha);

            if (!flashing[0] && p1ScoreText != null) p1ScoreText.color = pulsed;
            if (!flashing[1] && p2ScoreText != null) p2ScoreText.color = pulsed;

            yield return null;
        }
    }

    private void StopFlashRoutines()
    {
        if (p1FlashRoutine != null) { StopCoroutine(p1FlashRoutine); p1FlashRoutine = null; }
        if (p2FlashRoutine != null) { StopCoroutine(p2FlashRoutine); p2FlashRoutine = null; }
        flashing[0] = false;
        flashing[1] = false;
    }

    private void StopPulseRoutine()
    {
        if (pulseRoutine != null) { StopCoroutine(pulseRoutine); pulseRoutine = null; }
    }
}
