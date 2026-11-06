using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Desktop-pakken: kijk naar het ei (stip in het midden), linkermuisknop = oppakken, nog een keer = neerleggen.
// Ingedrukt houden en loslaten tijdens bewegen = gooien met je beweging mee.
public class DesktopGrabber : MonoBehaviour
{
    public float reach = 3f;
    public Vector3 holdOffset = new Vector3(0f, -0.45f, 1.4f);   // voor je, iets omlaag (ei is groot)

    GrabbableEgg held, hover;
    Vector3 lastPos, vel;

    void Update()
    {
        bool click = false, squeeze = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null) click = Mouse.current.leftButton.wasPressedThisFrame;
        if (Keyboard.current != null) squeeze = Keyboard.current.bKey.wasPressedThisFrame;
#else
        click = Input.GetMouseButtonDown(0);
        squeeze = Input.GetKeyDown(KeyCode.B);
#endif
        Vector3 target = transform.TransformPoint(holdOffset);

        if (held != null && held.IsBroken) held = null;

        if (held != null)
        {
            if (squeeze) { held.Break(Vector3.zero); held = null; return; }
            vel = Vector3.Lerp(vel, (target - lastPos) / Mathf.Max(Time.deltaTime, 0.0001f), 0.3f);
            lastPos = target;
            if (click) { held.DesktopRelease(Vector3.ClampMagnitude(vel, 6f)); held = null; }
            return;
        }

        hover = null;
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore))
            hover = hit.collider.GetComponentInParent<GrabbableEgg>();

        if (click && hover != null && hover.CanDesktopGrab)
        {
            held = hover;
            held.DesktopGrab();
            lastPos = target;
            vel = Vector3.zero;
        }
    }

    void FixedUpdate()
    {
        if (held != null)
            held.DesktopHold(transform.TransformPoint(holdOffset), Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
    }

    void OnGUI()
    {
        // kleine richtstip; lichter als je iets kunt pakken
        float s = (hover != null || held != null) ? 8f : 4f;
        GUI.color = (hover != null && held == null) ? new Color(0.5f, 1f, 0.9f, 0.95f) : new Color(1f, 1f, 1f, 0.5f);
        GUI.DrawTexture(new Rect(Screen.width / 2f - s / 2f, Screen.height / 2f - s / 2f, s, s), Texture2D.whiteTexture);
        if (hover != null && held == null)
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width / 2f + 12f, Screen.height / 2f - 10f, 200f, 20f), "Klik: pak het ei");
        }
        else if (held != null)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(Screen.width / 2f + 12f, Screen.height / 2f - 10f, 260f, 20f), "Klik: neerleggen   B: kapotknijpen");
        }
    }
}
