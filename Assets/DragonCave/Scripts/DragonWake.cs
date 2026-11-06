using System.Collections;
using UnityEngine;

// Dragon Cave v2: de echte draak slaapt tot je het ei pakt (of breekt).
// Dan stopt het snurken, gaat de Animator naar "Scream" en daarna naar "Idle".
public class DragonWake : MonoBehaviour
{
    public GrabbableEgg egg;
    public Animator animator;
    public AudioSource snoreSource;
    public AudioSource screamSource;
    public float wakeDelay = 0.6f;     // even stilte voordat hij schreeuwt: spanning

    public bool IsAwake { get; private set; }

    void Update()
    {
        if (IsAwake || egg == null) return;
        if (egg.HasBeenTaken || egg.IsBroken)
        {
            IsAwake = true;
            StartCoroutine(Wake());
        }
    }

    IEnumerator Wake()
    {
        Debug.Log("[Dragon Cave v2] De draak wordt wakker!");
        if (snoreSource != null)
        {
            float v = snoreSource.volume;
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                snoreSource.volume = Mathf.Lerp(v, 0f, t / 0.4f);
                yield return null;
            }
            snoreSource.Stop();
        }
        yield return new WaitForSeconds(wakeDelay);
        if (animator != null) animator.SetTrigger("Wake");
        if (screamSource != null && !screamSource.isPlaying) screamSource.Play();
    }
}
