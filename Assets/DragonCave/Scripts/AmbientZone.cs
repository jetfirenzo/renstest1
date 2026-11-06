using System.Collections.Generic;
using UnityEngine;

// Achtergrondgeluid voor een zone (bijv. de gang): vol te horen binnen innerRadius
// van de lijn door 'points', en het vervaagt over fadeDistance meter daarbuiten.
// 2D-geluid (stereo blijft stereo), het volume hangt alleen af van waar de speler is.
[RequireComponent(typeof(AudioSource))]
public class AmbientZone : MonoBehaviour
{
    public List<Vector3> points = new List<Vector3>();   // wereldcoördinaten
    public float innerRadius = 3f;
    public float fadeDistance = 6f;
    [Range(0f, 1f)] public float maxVolume = 0.7f;
    [Range(0f, 1f)] public float outsideVolume = 0f;
    public float volumeSpeed = 2f;                        // volume-eenheden per seconde

    AudioSource src;
    Transform listener;

    void Start()
    {
        src = GetComponent<AudioSource>();
        src.volume = 0f;
        if (src.clip == null) return;
        src.Play();
        src.time = Random.Range(0f, src.clip.length * 0.9f);
        FindListener();
    }

    void FindListener()
    {
        var l = FindAnyObjectByType<AudioListener>();   // alleen actieve (VR-camera of desktop-camera)
        listener = l != null ? l.transform : null;
    }

    void Update()
    {
        if (listener == null || !listener.gameObject.activeInHierarchy)
        {
            FindListener();
            if (listener == null) return;
        }

        Vector3 p = listener.position;
        float best = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            float dx = p.x - points[i].x, dz = p.z - points[i].z;
            float d = dx * dx + dz * dz;
            if (d < best) best = d;
        }
        float t = Mathf.Clamp01((Mathf.Sqrt(best) - innerRadius) / Mathf.Max(0.01f, fadeDistance));
        float target = Mathf.Lerp(maxVolume, outsideVolume, t);
        src.volume = Mathf.MoveTowards(src.volume, target, volumeSpeed * Time.deltaTime);
    }
}
