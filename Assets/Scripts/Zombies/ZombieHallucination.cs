using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Added at runtime by ZombieManager to every spawned zombie. Harmless: no collider, no damage.
///
/// Each zombie has one "featured" behaviour, the thing the player has to check against the
/// Zombie Behaviour Spec page:
///   - genuine zombie      -> the featured behaviour is ALLOWED by this run's rules
///   - hallucinated zombie -> the featured behaviour is FORBIDDEN (everything else it does is allowed)
/// Real and fake zombies follow exactly the same pattern, so only the page tells them apart.
///
/// Pattern:
///   - Featured behaviour is a movement (walk / jog...): the zombie keeps patrolling its stretch of
///     corridor with that movement, so it is visible whenever the player is around.
///   - Featured behaviour is an action (punch, dance...): the zombie patrols with an allowed movement;
///     when the player comes within Show Range it stops, faces them and performs the action,
///     alternating with short idles and (sometimes) another allowed action.
///
/// Flagging uses the existing AmbientHallucination component (same F prompt, scoring and stats).
/// </summary>
public class ZombieHallucination : MonoBehaviour
{
    public static readonly List<ZombieHallucination> All = new List<ZombieHallucination>();

    /// <summary>Short readable summary for logs, e.g. "FAKE, featured: jog (forbidden)".</summary>
    public string Description { get; private set; }

    private List<Vector3> _waypoints;
    private ZombieBehaviourSide _move;
    private AnimationClip _moveClip;
    private bool _featureIsMove;
    private ZombieBehaviourSide _featured;
    private List<ZombieBehaviourSide> _extras;
    private AnimationClip _idle;
    private float _showRange;
    private float _maxActionSeconds;
    private bool _facePlayer;
    private float _turnSpeed = 360f;

    private ZombieAnimator _anim;
    private Transform _player;

    public void Setup(List<Vector3> waypoints, ZombieBehaviourSide move, bool featureIsMove,
                      ZombieBehaviourSide featured, List<ZombieBehaviourSide> extras, AnimationClip idle,
                      float showRange, float maxActionSeconds, bool facePlayer, string description)
    {
        _waypoints = waypoints ?? new List<Vector3>();
        _move = move;
        _featureIsMove = featureIsMove;
        _featured = featured;
        _extras = extras ?? new List<ZombieBehaviourSide>();
        _idle = idle;
        _showRange = showRange;
        _maxActionSeconds = maxActionSeconds;
        _facePlayer = facePlayer;
        Description = description;
    }

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        Animator animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogError($"ZombieHallucination: '{name}' has no Animator. The zombie prefab needs an Animator with its Humanoid avatar.", this);
            enabled = false;
            return;
        }
        // Generic rigs (e.g. a dragon) are fine as long as their clips were made for that rig.
        if (animator.avatar == null)
            Debug.LogWarning($"ZombieHallucination: '{name}' has no avatar on its Animator, so its clips may not play.", this);

        _anim = new ZombieAnimator(animator, name);
        if (_idle != null) _anim.Play(_idle, true);
        StartCoroutine(Brain());
    }

    void Update()
    {
        _anim?.Tick(Time.deltaTime);
    }

    void OnDestroy()
    {
        _anim?.Dispose();
        _anim = null;
    }

    private bool PlayerNear
    {
        get
        {
            if (_player == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p == null) return false;
                _player = p.transform;
            }
            Vector3 d = _player.position - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= _showRange * _showRange;
        }
    }

    private IEnumerator Brain()
    {
        int wp = _waypoints.Count > 1 ? 1 : 0;
        int dir = 1;

        while (true)
        {
            // Action zombies stop and show their featured action when the player is around.
            if (!_featureIsMove && _featured != null && PlayerNear)
            {
                yield return ShowLoop();
                continue;
            }

            if (_move == null || _waypoints.Count < 2)
            {
                PlayIdle();
                yield return null;
                continue;
            }

            // Keep the same movement clip for a whole leg so it doesn't restart every frame.
            if (_moveClip == null) _moveClip = _move.RandomClip();
            _anim.Play(_moveClip, true);

            bool reached = false;
            yield return WalkTo(_waypoints[wp], _move.moveSpeed, r => reached = r);
            if (!reached) continue; // interrupted to show an action

            // Ping-pong along the stretch.
            if (wp + dir >= _waypoints.Count || wp + dir < 0) dir = -dir;
            wp += dir;
            _moveClip = null;

            // Short pause at each waypoint. Movement zombies sometimes show an allowed action here.
            if (_featureIsMove && PlayerNear && _extras.Count > 0 && Random.value < 0.35f)
                yield return PerformAction(_extras[Random.Range(0, _extras.Count)].RandomClip());
            else
            {
                PlayIdle();
                yield return Wait(Random.Range(0.3f, 1.0f), false);
            }
        }
    }

    private IEnumerator WalkTo(Vector3 target, float speed, System.Action<bool> done)
    {
        while (true)
        {
            if (!_featureIsMove && _featured != null && PlayerNear) { done(false); yield break; }

            Vector3 delta = target - transform.position;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist < 0.05f) { done(true); yield break; }

            Vector3 step = delta / dist * Mathf.Min(dist, speed * Time.deltaTime);
            transform.position += step;
            TurnTowards(delta);
            yield return null;
        }
    }

    // Featured action first (so it shows within a second or two of the player arriving),
    // then alternating: idle / sometimes another allowed action, featured again, ...
    private IEnumerator ShowLoop()
    {
        int beat = 0;
        while (PlayerNear)
        {
            if (beat % 2 == 0)
                yield return PerformAction(_featured.RandomClip());
            else if (_extras.Count > 0 && Random.value < 0.3f)
                yield return PerformAction(_extras[Random.Range(0, _extras.Count)].RandomClip());
            else
            {
                PlayIdle();
                yield return Wait(Random.Range(0.8f, 1.4f), _facePlayer);
            }
            beat++;
        }
        _moveClip = null;
    }

    private IEnumerator PerformAction(AnimationClip clip)
    {
        if (clip == null) { yield return Wait(0.5f, false); yield break; }

        // Long clips (dances) are cut after Max Action Seconds; short ones play once.
        bool loop = clip.length < _maxActionSeconds * 0.5f ? false : true;
        _anim.Play(clip, loop, restart: true);
        float duration = Mathf.Clamp(clip.length, 0.5f, _maxActionSeconds);
        yield return Wait(duration, _facePlayer);
    }

    private IEnumerator Wait(float seconds, bool facePlayer)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (facePlayer && _player != null) TurnTowards(_player.position - transform.position);
            t += Time.deltaTime;
            yield return null;
        }
    }

    private void PlayIdle()
    {
        if (_idle != null) _anim.Play(_idle, true);
    }

    private void TurnTowards(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeed * Time.deltaTime);
    }
}
