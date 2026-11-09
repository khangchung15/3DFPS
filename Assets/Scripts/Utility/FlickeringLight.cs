using UnityEngine;

public class LightFlicker : MonoBehaviour
{
    [Header("Flicker Timing")]
    [Tooltip("Minimum time (seconds) the light stays in one state")]
    public float minInterval = 0.05f;

    [Tooltip("Maximum time (seconds) the light stays in one state")]
    public float maxInterval = 0.5f;

    [Header("Flicker Behavior")]
    [Range(0f, 1f)]
    [Tooltip("Chance the light will be ON after each interval")]
    public float onProbability = 0.7f;

    [Tooltip("If true, the flicker starts with the light ON")]
    public bool startOn = true;

    private float timer;

    private void OnEnable()
    {
        // Reset timer whenever the object gets re-enabled
        timer = Random.Range(minInterval, maxInterval);
    }

    private void Start()
    {
        // Initial state
        SetActive(startOn);
        timer = Random.Range(minInterval, maxInterval);
    }

    private void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            // Randomly decide next state
            bool nextState = Random.value < onProbability;
            SetActive(nextState);

            // Pick a new random interval
            timer = Random.Range(minInterval, maxInterval);
        }
    }

    private Light lightComponent;

    private void Awake()
    {
        lightComponent = GetComponent<Light>();
    }

    private void SetActive(bool state)
    {
        if (lightComponent != null) lightComponent.enabled = state;
    }
}