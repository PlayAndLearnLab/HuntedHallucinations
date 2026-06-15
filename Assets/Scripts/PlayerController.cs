using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 4f;
    [SerializeField] private float _gravity = -9.81f;

    private CharacterController _controller;
    private Transform _cameraTransform;
    private float _verticalVelocity;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();

        // Grab the camera that is a child of this player
        _cameraTransform = GetComponentInChildren<Camera>().transform;
    }

    void Update()
    {
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

        Vector3 move = (forward * input.y + right * input.x).normalized * _moveSpeed;

        // Gravity
        if (_controller.isGrounded)
            _verticalVelocity = 0f;
        else
            _verticalVelocity += _gravity * Time.deltaTime;

        move.y = _verticalVelocity;

        _controller.Move(move * Time.deltaTime);
    }
}
