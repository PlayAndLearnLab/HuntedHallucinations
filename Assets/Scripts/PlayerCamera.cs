using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    [SerializeField] private float _mouseSensitivity = 100f;
    [SerializeField] private float _defaultDistance = 2f;
    [SerializeField] private float _minDistance = 0.2f;
    [SerializeField] private float _heightOffset = 1f;
    [SerializeField] private LayerMask _collisionMask;

    private float _xRotation = 0f;
    private float _yRotation = 0f;
    private Transform _playerBody;
    private Camera _camera;

    private Vector3 PivotPosition => _playerBody.position + Vector3.up * _heightOffset;

    void Awake()
    {
        _playerBody = transform.parent;
        _camera = GetComponent<Camera>();

        transform.SetParent(null);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;
        
        float mouseX = Mouse.current.delta.x.ReadValue() * _mouseSensitivity * Time.deltaTime;
        float mouseY = Mouse.current.delta.y.ReadValue() * _mouseSensitivity * Time.deltaTime;

        _yRotation += mouseX;
        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -80f, 80f);
    }

    void LateUpdate()
    {
        Quaternion rotation = Quaternion.Euler(_xRotation, _yRotation, 0f);
        Vector3 directionToCamera = rotation * Vector3.back;

        // Find the shortest safe distance by checking multiple rays
        float safeDistance = GetSafeDistance(directionToCamera, rotation);

        transform.position = PivotPosition + directionToCamera * safeDistance;
        transform.rotation = rotation;
    }

    private float GetSafeDistance(Vector3 directionToCamera, Quaternion rotation)
    {
        float safeDistance = _defaultDistance;

        // Check 1: SphereCast down the center — handles the main case
        if (Physics.SphereCast(
            PivotPosition,
            0.2f,
            directionToCamera,
            out RaycastHit centerHit,
            _defaultDistance,
            _collisionMask))
        {
            safeDistance = Mathf.Min(safeDistance, Mathf.Max(centerHit.distance - 0.05f, _minDistance));
        }

        // Check 2: Raycast to each corner of the near clip plane
        // This catches walls that appear when rotating while against a surface
        Vector3[] clipCorners = GetNearClipCorners(rotation, safeDistance);
        foreach (Vector3 corner in clipCorners)
        {
            Vector3 directionToCorner = (corner - PivotPosition);
            float distanceToCorner = directionToCorner.magnitude;

            if (Physics.Raycast(
                PivotPosition,
                directionToCorner.normalized,
                out RaycastHit cornerHit,
                distanceToCorner,
                _collisionMask))
            {
                // Pull in so this corner no longer clips
                float pulledDistance = cornerHit.distance / (distanceToCorner / safeDistance);
                safeDistance = Mathf.Min(safeDistance, Mathf.Max(pulledDistance - 0.05f, _minDistance));
            }
        }

        return safeDistance;
    }

    public static void SetCursorFree(bool free)
    {
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }

    // Returns the 4 corners of the camera's near clip plane at the candidate position
    private Vector3[] GetNearClipCorners(Quaternion rotation, float candidateDistance)
    {
        float nearClip = _camera.nearClipPlane;
        float halfFOV = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float halfH = Mathf.Tan(halfFOV) * nearClip;
        float halfW = halfH * _camera.aspect;

        // Camera position at the candidate distance
        Vector3 camPos = PivotPosition + rotation * Vector3.back * candidateDistance;

        // Four corners offset in camera-local space
        Vector3 forward = rotation * Vector3.forward;
        Vector3 up = rotation * Vector3.up;
        Vector3 right = rotation * Vector3.right;

        Vector3 nearCenter = camPos + forward * nearClip;

        return new Vector3[]
        {
            nearCenter + up * halfH + right * halfW,   // top right
            nearCenter + up * halfH - right * halfW,   // top left
            nearCenter - up * halfH + right * halfW,   // bottom right
            nearCenter - up * halfH - right * halfW    // bottom left
        };
    }
}
