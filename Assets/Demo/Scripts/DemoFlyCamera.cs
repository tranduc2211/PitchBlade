using UnityEngine;

public class DemoFlyCamera : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float fastMultiplier = 5f;
    public float lookSensitivity = 3f;
    public float minHeight = 0.1f;

    float yaw;
    float pitch;

    void Start()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = Mathf.DeltaAngle(0, euler.x);
    }

    void Update()
    {
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * lookSensitivity;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * lookSensitivity, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }

        Vector3 move = transform.TransformDirection(new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")));
        if (Input.GetKey(KeyCode.E)) move += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) move += Vector3.down;

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);
        Vector3 pos = transform.position + move * (speed * Time.deltaTime);
        pos.y = Mathf.Max(pos.y, minHeight);
        transform.position = pos;
    }
}
