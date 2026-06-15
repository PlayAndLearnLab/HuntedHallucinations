using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    [SerializeField] private float _mouseSensitivity = 100f;
    [SerializeField] private float _defaultDistance = 2f;  // how far behind the player
    [SerializeField] private float _minDistance = 0.2f;    // closest the cam can get
    [SerializeField] private float _heightOffset = 1f;     // how high above the player pivot
    [SerializeField] private LayerMask _collisionMask;     // set this to your walls layer

    private float _xRotation = 0f;
    private float _yRotation = 0f;
    private Transform _playerBody;

    // The point we rotate around — sits at head height on the player
    private Vector3 PivotPosition => _playerBody.position + Vector3.up * _heightOffset;

    void Awake()
    {
        _playerBody = transform.parent;

        // Detach the camera from the player so we can move it freely
        // It will follow manually in LateUpdate
        transform.SetParent(null);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        float mouseX = Mouse.current.delta.x.ReadValue() * _mouseSensitivity * Time.deltaTime;
        float mouseY = Mouse.current.delta.y.ReadValue() * _mouseSensitivity * Time.deltaTime;

        _yRotation += mouseX;
        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -80f, 80f);
    }

    void LateUpdate()
    {
        // The rotation we want the camera to have
        Quaternion rotation = Quaternion.Euler(_xRotation, _yRotation, 0f);

        // The ideal position: directly behind and above the player at _defaultDistance
        Vector3 desiredPosition = PivotPosition + rotation * new Vector3(0, 0, -_defaultDistance);

        // Cast a sphere from the pivot toward the desired position.
        // If it hits a wall, stop the camera just before the hit.
        float actualDistance = _defaultDistance;
        Vector3 directionToCamera = (desiredPosition - PivotPosition).normalized;

        if (Physics.SphereCast(
            PivotPosition,
            0.2f,                  // small sphere radius — prevents clipping at edges
            directionToCamera,
            out RaycastHit hit,
            _defaultDistance,
            _collisionMask))
        {
            // Pull the camera in to just before the wall
            actualDistance = Mathf.Max(hit.distance - 0.05f, _minDistance);
        }

        // Apply final position and always look at the pivot
        transform.position = PivotPosition + directionToCamera * actualDistance;
        transform.rotation = rotation;
    }
}

//using UnityEngine;
//using UnityEngine.InputSystem;


//public class PlayerCamera : MonoBehaviour
//{
//    [SerializeField] private float _mouseSensitivity = 100f;

//    private float _xRotation = 0f;
//    private Transform _playerBody;

//    void Awake()
//    {
//        _playerBody = transform.parent;

//        // Lock and hide the cursor
//        Cursor.lockState = CursorLockMode.Locked;
//        Cursor.visible = false;
//    }

//    void Update()
//    {
//        float mouseX = Mouse.current.delta.x.ReadValue() * _mouseSensitivity * Time.deltaTime;
//        float mouseY = Mouse.current.delta.y.ReadValue() * _mouseSensitivity * Time.deltaTime;

//        // Vertical look (clamp so you can't flip upside down)
//        _xRotation -= mouseY;
//        _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);
//        transform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

//        // Horizontal look rotates the whole player body
//        _playerBody.Rotate(Vector3.up * mouseX);
//    }
//}