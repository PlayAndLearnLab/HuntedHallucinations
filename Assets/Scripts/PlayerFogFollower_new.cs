using UnityEngine;

/// <summary>
/// Attach to the player GameObject.
/// Writes _PlayerPosition and _PlayerVelocity to the global shader namespace
/// every frame so FogTest3.shader can open and anticipate the clear zone.
///
/// Works with both Rigidbody (physics) and CharacterController (kinematic) movement.
/// Falls back to finite-difference velocity when neither component is found.
/// </summary>
[DefaultExecutionOrder(-100)] // run before particle system LateUpdate
public class PlayerFogFollower_new : MonoBehaviour
{
    [Header("Optional — leave null to auto-detect")]
    [SerializeField] private Rigidbody        _rb;
    [SerializeField] private CharacterController _cc;

    // Finite-difference fallback
    private Vector3 _prevPosition;

    void Awake()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();
        if (_cc == null) _cc = GetComponent<CharacterController>();
        _prevPosition = transform.position;
    }

    void LateUpdate()
    {
        Vector3 pos = transform.position;

        // ── Velocity ──────────────────────────────────────────────────────────
        Vector3 vel;
        if (_rb != null)
        {
            vel = _rb.linearVelocity;
        }
        else if (_cc != null)
        {
            vel = _cc.velocity;
        }
        else
        {
            // Finite-difference: good enough for the ~1-frame anticipation window
            vel = (pos - _prevPosition) / Time.deltaTime;
        }

        _prevPosition = pos;

        // ── Push to GPU ───────────────────────────────────────────────────────
        Shader.SetGlobalVector("_PlayerPosition", pos);
        // Zero out Y so the anticipation stays on the XZ plane
        Shader.SetGlobalVector("_PlayerVelocity", new Vector4(vel.x, 0f, vel.z, 0f));
    }
}

