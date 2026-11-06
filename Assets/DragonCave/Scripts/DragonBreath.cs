using UnityEngine;

// Langzame ademhaling: schaalt een lichaamsdeel zachtjes op en neer.
public class DragonBreath : MonoBehaviour
{
    public float amount = 0.04f;
    public float breathsPerMinute = 9f;
    public float phase = 0f;

    Vector3 baseScale;

    void Start()
    {
        baseScale = transform.localScale;
    }

    void Update()
    {
        float w = Mathf.Sin(Time.time * breathsPerMinute / 60f * Mathf.PI * 2f - phase);
        float s = 1f + amount * w;
        transform.localScale = new Vector3(baseScale.x * s, baseScale.y * (1f + amount * 1.5f * w), baseScale.z * s);
    }
}
