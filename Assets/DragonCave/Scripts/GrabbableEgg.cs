using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Het drakenei: ligt eerst stil in het nest (kinematic), wordt na de eerste keer oppakken een gewoon natuurkundig object.
// VR: XRGrabInteractable (grijpknop), knijpen = trekker terwijl je hem vasthoudt.
// Desktop: DesktopGrabber op de camera (linkermuisknop pakken, B = kapotknijpen).
// Breekt ook bij een harde klap (vallen, gooien, tegen de muur). Dan schreeuwt de draak.
[RequireComponent(typeof(Rigidbody))]
public class GrabbableEgg : MonoBehaviour
{
    [Header("Breken")]
    public float breakSpeed = 3.5f;          // m/s botssnelheid. Laag vasthouden en loslaten = heel, van hoger vallen of gooien = kapot
    public int shardCount = 16;

    [Header("Draak (zet de builder)")]
    public AudioSource screamSource;
    public AudioSource snoreSource;
    public AudioSource crackSource;
    public float screamDelay = 0.35f;        // eerst de krak, dan de draak

    public bool IsHeld { get; private set; }
    public bool HasBeenTaken { get; private set; }   // voor later: draak wordt wakker als je het ei pakt
    public bool IsBroken { get; private set; }

    Rigidbody rb;
    XRGrabInteractable xrGrab;
    bool releaseNextFrame;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        xrGrab = GetComponent<XRGrabInteractable>();
        if (xrGrab != null)
        {
            xrGrab.selectEntered.AddListener(_ => OnGrab());
            xrGrab.selectExited.AddListener(_ => { IsHeld = false; releaseNextFrame = true; });
            xrGrab.activated.AddListener(_ => Break(Vector3.zero));   // trekker indrukken terwijl je hem vasthoudt = knijpen
        }
    }

    void LateUpdate()
    {
        // XRI zet bij loslaten de oude kinematic-stand terug; daarna pas natuurkunde aanzetten.
        if (!IsBroken && releaseNextFrame && (xrGrab == null || !xrGrab.isSelected))
        {
            releaseNextFrame = false;
            rb.isKinematic = false;
            rb.WakeUp();
        }
    }

    void OnCollisionEnter(Collision c)
    {
        if (IsBroken || !HasBeenTaken) return;
        if (c.relativeVelocity.magnitude >= breakSpeed) Break(rb.linearVelocity);
    }

    void OnGrab()
    {
        IsHeld = true;
        if (!HasBeenTaken) { HasBeenTaken = true; Debug.Log("[Dragon Cave] Het ei is gepakt!"); }
    }

    // ---------------------------------------------------------------- breken

    public void Break(Vector3 impactVelocity)
    {
        if (IsBroken) return;
        IsBroken = true;
        IsHeld = false;
        Debug.Log("[Dragon Cave] Het ei is gebroken! De draak schreeuwt.");

        if (xrGrab != null) xrGrab.enabled = false;   // laat de hand los
        rb.isKinematic = true;
        foreach (var col in GetComponents<Collider>()) col.enabled = false;
        var rend = GetComponent<Renderer>();
        if (rend != null) rend.enabled = false;

        SpawnShards(rend != null ? rend.sharedMaterial : null, impactVelocity);

        var glow = GetComponentInChildren<Light>();
        if (glow != null) StartCoroutine(FadeGlow(glow));

        if (crackSource != null) crackSource.Play();
        if (snoreSource != null) StartCoroutine(FadeOut(snoreSource, 0.4f));
        if (screamSource != null) screamSource.PlayDelayed(screamDelay);
    }

    void SpawnShards(Material mat, Vector3 impactVelocity)
    {
        Vector3 size = transform.lossyScale;
        var parent = new GameObject("Eischerven").transform;
        parent.position = transform.position;
        for (int i = 0; i < shardCount; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            var s = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube);
            s.name = "Scherf_" + i;
            s.transform.SetParent(parent, true);
            s.transform.position = transform.position + Vector3.Scale(dir * 0.42f, size);
            s.transform.rotation = Random.rotation;
            float k = Random.Range(0.14f, 0.32f);
            s.transform.localScale = new Vector3(k, k * 0.22f, k * Random.Range(0.7f, 1.2f));
            if (mat != null) s.GetComponent<Renderer>().sharedMaterial = mat;
            var srb = s.AddComponent<Rigidbody>();
            srb.mass = 0.1f;
            srb.linearVelocity = dir * Random.Range(1.2f, 2.8f) + Vector3.up * 0.8f + impactVelocity * 0.3f;
            srb.angularVelocity = Random.insideUnitSphere * 10f;
        }
    }

    IEnumerator FadeGlow(Light l)
    {
        l.transform.SetParent(null, true);   // licht blijft bij de scherven hangen
        float start = l.intensity;
        l.intensity = start * 3f;            // korte flits
        for (float t = 0f; t < 2.5f; t += Time.deltaTime)
        {
            l.intensity = Mathf.Lerp(start * 3f, 0f, t / 2.5f);
            yield return null;
        }
        l.enabled = false;
    }

    IEnumerator FadeOut(AudioSource a, float time)
    {
        float v = a.volume;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            a.volume = Mathf.Lerp(v, 0f, t / time);
            yield return null;
        }
        a.Stop();
    }

    // ---------------------------------------------------------------- desktop

    public bool CanDesktopGrab => !IsHeld && !IsBroken;

    public void DesktopGrab()
    {
        OnGrab();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void DesktopHold(Vector3 target, Quaternion rot)
    {
        if (IsBroken) return;
        rb.MovePosition(Vector3.Lerp(rb.position, target, 0.35f));
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, rot, 0.2f));
    }

    public void DesktopRelease(Vector3 velocity)
    {
        if (IsBroken) return;
        IsHeld = false;
        rb.isKinematic = false;
        rb.linearVelocity = velocity;
        rb.WakeUp();
    }
}
