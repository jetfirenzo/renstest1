using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Lopen op de grond (desktop): WASD = lopen, rechtermuisknop ingedrukt = rondkijken, Shift = rennen.
// F = wisselen naar vliegmodus (de oude flycam: door muren, Q/E = omlaag/omhoog) en terug.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonWalker : MonoBehaviour
{
    public Transform cam;
    public float walkSpeed = 2.2f;      // rustig sluipen
    public float runMultiplier = 2f;
    public float flySpeed = 3f;
    public float lookSpeed = 0.15f;
    public float gravity = 15f;

    CharacterController cc;
    float yaw, pitch, vy;
    bool flying;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        yaw = transform.eulerAngles.y;
        if (cam == null && Camera.main != null) cam = Camera.main.transform;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

    }

    void Update()
    {
        Vector3 move = Vector3.zero;
        bool look = false, fast = false, toggleFly = false;
        Vector2 delta = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;
        if (k != null)
        {
            if (k.wKey.isPressed) move.z += 1f;
            if (k.sKey.isPressed) move.z -= 1f;
            if (k.dKey.isPressed) move.x += 1f;
            if (k.aKey.isPressed) move.x -= 1f;
            if (k.eKey.isPressed) move.y += 1f;
            if (k.qKey.isPressed) move.y -= 1f;
            fast = k.leftShiftKey.isPressed;
            toggleFly = k.fKey.wasPressedThisFrame;
        }
        if (m != null)
        {
            look = m.rightButton.isPressed;
            delta = m.delta.ReadValue();
        }
#else
        move.z = Input.GetAxisRaw("Vertical");
        move.x = Input.GetAxisRaw("Horizontal");
        if (Input.GetKey(KeyCode.E)) move.y += 1f;
        if (Input.GetKey(KeyCode.Q)) move.y -= 1f;
        fast = Input.GetKey(KeyCode.LeftShift);
        toggleFly = Input.GetKeyDown(KeyCode.F);
        look = Input.GetMouseButton(1);
        delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
#endif

        if (toggleFly)
        {
            flying = !flying;
            cc.enabled = !flying;   // vliegen = geen botsingen
            vy = 0f;
        }

        if (look)
        {
            yaw += delta.x * lookSpeed;
            pitch = Mathf.Clamp(pitch - delta.y * lookSpeed, -85f, 85f);
        }
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cam != null) cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        if (flying)
        {
            Vector3 dir = cam != null ? cam.TransformDirection(new Vector3(move.x, 0f, move.z)) : transform.TransformDirection(move);
            dir.y += move.y;
            transform.position += dir * flySpeed * (fast ? 3f : 1f) * Time.deltaTime;
            return;
        }

        Vector3 flat = transform.TransformDirection(new Vector3(move.x, 0f, move.z));
        if (flat.sqrMagnitude > 1f) flat.Normalize();
        flat *= walkSpeed * (fast ? runMultiplier : 1f);

        if (cc.isGrounded && vy < 0f) vy = -1f;
        vy -= gravity * Time.deltaTime;
        cc.Move((flat + Vector3.up * vy) * Time.deltaTime);
    }
}
