using UnityEngine;
using SG; // SenseGlove-plugin

// Pakbare fakkel voor SenseGlove.
// - Pakken = trillen (loopt door zolang je vasthoudt), loslaten = stoppen.
// - Hangt zichzelf aan de ObjectGrabbed/ObjectReleased-events van SG_Grabable (in het testproject was dit niet gekoppeld).
// - Eerste keer pakken: fakkel wordt gewone natuurkunde (zwaartekracht), zodat hij valt als je hem loslaat.
[RequireComponent(typeof(SG_Grabable))]
public class TorchHaptics : MonoBehaviour
{
    public SG_CustomWaveform holdWaveform;   // Constantvibration (oneindig herhalen)
    public SG_CustomWaveform stopWaveform;   // Stopvibration
    public VibrationLocation location = VibrationLocation.WholeHand;

    SG_Grabable grab;

    void Awake() { grab = GetComponent<SG_Grabable>(); }

    void OnEnable()
    {
        grab.ObjectGrabbed.AddListener(OnGrabbed);
        grab.ObjectReleased.AddListener(OnReleased);
    }

    void OnDisable()
    {
        grab.ObjectGrabbed.RemoveListener(OnGrabbed);
        grab.ObjectReleased.RemoveListener(OnReleased);
    }

    void OnGrabbed(SG_Interactable obj, SG_GrabScript hand)
    {
        grab.SetDefaultPhysicsParameters(true, false, RigidbodyConstraints.None);
        if (holdWaveform != null) grab.SendCustomWaveform(holdWaveform, location);
    }

    void OnReleased(SG_Interactable obj, SG_GrabScript hand)
    {
        if (grab.IsGrabbed()) return; // andere hand houdt hem nog vast
        if (stopWaveform != null) grab.SendCustomWaveform(stopWaveform, location);
    }
}
