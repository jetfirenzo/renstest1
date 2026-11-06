using UnityEngine;

// Start een loopend geluid op een willekeurig punt, met een kleine toonhoogte-variatie,
// zodat meerdere fakkels niet exact gelijk knetteren.
[RequireComponent(typeof(AudioSource))]
public class RandomLoopStart : MonoBehaviour
{
    public float pitchVariation = 0.05f;

    void Start()
    {
        var s = GetComponent<AudioSource>();
        if (s.clip == null) return;
        s.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        s.Play();
        s.time = Random.Range(0f, s.clip.length * 0.95f);
    }
}
