using UnityEngine;

// Laat een fakkellicht flikkeren (intensiteit + kleine positie-jitter).
public class TorchFlicker : MonoBehaviour
{
    public Light torchLight;
    public float baseIntensity = 8f;
    [Range(0f, 1f)] public float flickerAmount = 0.35f;
    public float speed = 7f;

    float seed;
    Vector3 basePos;

    void Start()
    {
        if (torchLight == null) torchLight = GetComponentInChildren<Light>();
        seed = Random.value * 100f;
        if (torchLight != null) basePos = torchLight.transform.localPosition;
    }

    void Update()
    {
        if (torchLight == null) return;
        float t = Time.time * speed;
        float n = Mathf.PerlinNoise(seed, t);
        torchLight.intensity = baseIntensity * (1f - flickerAmount * 0.5f + flickerAmount * n);
        const float jitter = 0.04f;
        torchLight.transform.localPosition = basePos + new Vector3(
            (Mathf.PerlinNoise(seed + 1f, t) - 0.5f) * jitter,
            (Mathf.PerlinNoise(seed + 2f, t) - 0.5f) * jitter,
            0f);
    }
}
