using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 4f;
    [SerializeField] private float _rotationSpeed = 10f; // Added for smooth character turning
    [SerializeField] private Animator _animator;

    [SerializeField] private float _gravity = -9.81f;

    private CharacterController _controller;
    private Transform _cameraTransform;
    private float _verticalVelocity;
    private bool _canMove = true;

    // Cache Animator parameter hash for performance
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    void Awake()
    {
        _controller = GetComponent<CharacterController>();

        // Grab the camera that is a child of this player
        _cameraTransform = GetComponentInChildren<Camera>().transform;

        // Fallback check if animator isn't explicitly assigned
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
    }

    public void ToggleMovement(bool canMove)
    {
        _canMove = canMove;
        if (!_canMove)
        {
            _verticalVelocity = 0f; // Reset gravity while paused
            if (_animator != null) _animator.SetFloat(SpeedHash, 0f);
        }
    }

    public static void SetCursorFree_fromCamera(bool free)
    {
        PlayerCamera.SetCursorFree(free);
    }

    void Update()
    {
        if (!_canMove) return;

        Vector2 input = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed) input.x -= 1;
            if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed) input.x += 1;
            if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed) input.y -= 1;
            if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed) input.y += 1;
        }

        // Move relative to where the camera is facing, ignoring vertical tilt
        Vector3 forward = _cameraTransform.forward;
        Vector3 right = _cameraTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = (forward * input.y + right * input.x).normalized;
        Vector3 move = moveDirection * _moveSpeed;

        // Smoothly rotate character toward movement direction
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            // Rotates character mesh smoothly
            _animator.transform.rotation = Quaternion.Slerp(_animator.transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }

        // Pass magnitude to Animator to drive Idle -> Walk transition
        if (_animator != null)
        {
            float currentSpeed = input.magnitude; // 0 when standing still, 1 when moving
            _animator.SetFloat(SpeedHash, currentSpeed, 0.1f, Time.deltaTime); // Dampened parameter transition
        }

        // Gravity
        if (_controller.isGrounded)
            _verticalVelocity = 0f;
        else
            _verticalVelocity += _gravity * Time.deltaTime;

        move.y = _verticalVelocity;

        _controller.Move(move * Time.deltaTime);
    }
}
