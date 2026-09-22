using UnityEngine;

public class FreeCameraController : MonoBehaviour
{
    [System.NonSerialized] public float moveSpeed;
    [System.NonSerialized] public float lookSpeed;

    private float yaw;
    private float pitch;
    private bool lookMode = false;


    void Update()
    {
        // 右クリックで視点操作モードをトグル
        if (Input.GetMouseButtonDown(1))
        {
            lookMode = !lookMode;

            if (lookMode)
            {
                // 今のカメラ角度を yaw/pitch に反映する
                Vector3 angles = transform.eulerAngles;
                yaw = angles.y;
                pitch = angles.x;
                if (pitch > 180f) pitch -= 360f;

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        // Escキーでも視点操作モードを解除できる
        if (lookMode && Input.GetKeyDown(KeyCode.Escape))
        {
            lookMode = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // 視点操作モード中は視点回転
        if (lookMode)
        {
            yaw += lookSpeed * Input.GetAxis("Mouse X");
            pitch -= lookSpeed * Input.GetAxis("Mouse Y");
            pitch = Mathf.Clamp(pitch, -90f, 90f);

            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }
        // 水平移動（WASD）
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 move = (transform.right * x + transform.forward * z) * moveSpeed;

        if (Input.GetKey(KeyCode.Space))
            move += Vector3.up * moveSpeed;

        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            move += Vector3.down * moveSpeed;

        transform.position += move * Time.deltaTime;
    }

}
