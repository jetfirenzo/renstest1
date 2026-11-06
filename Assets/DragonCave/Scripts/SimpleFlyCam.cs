using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Rondvliegen in Play-modus: rechtermuisknop ingedrukt = kijken, WASD = bewegen, Q/E = omlaag/omhoog, Shift = sneller.
public class SimpleFlyCam : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float lookSpeed = 0.15f;

    float yaw;
    float pitch;

    void Start()
    {
        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x > 180f ? e.x - 360f : e.x;
    }

    void Update()
    {
        Vector3 move = Vector3.zero;
        bool look = false;
        bool fast = false;
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
        look = Input.GetMouseButton(1);
        delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
#endif

        if (look)
        {
            yaw += delta.x * lookSpeed;
            pitch = Mathf.Clamp(pitch - delta.y * lookSpeed, -85f, 85f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
        transform.position += transform.TransformDirection(move) * moveSpeed * (fast ? 3f : 1f) * Time.deltaTime;
    }
}
