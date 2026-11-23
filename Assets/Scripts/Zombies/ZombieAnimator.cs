using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Plays animation clips on a zombie with a short crossfade, without needing an Animator Controller asset.
/// Uses the Playables API, so any Humanoid clip can be played on any Humanoid zombie.
/// Clips that should loop (walk, idle, dances) are wrapped by code even if "Loop Time" is off in their import settings.
/// </summary>
public class ZombieAnimator : System.IDisposable
{
    private PlayableGraph _graph;
    private AnimationMixerPlayable _mixer;
    private readonly AnimationClip[] _clips = new AnimationClip[2];
    private readonly bool[] _loop = new bool[2];
    private int _active = -1;
    private float _fadeSpeed;

    public AnimationClip Current => _active >= 0 ? _clips[_active] : null;

    public ZombieAnimator(Animator animator, string name)
    {
        // The graph drives the animator directly: no controller, no root motion (movement is done in code).
        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        _graph = PlayableGraph.Create(name + " (Zombie)");
        _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        var output = AnimationPlayableOutput.Create(_graph, "Animation", animator);
        _mixer = AnimationMixerPlayable.Create(_graph, 2);
        output.SetSourcePlayable(_mixer);
        _graph.Play();
    }

    /// <summary>
    /// Crossfades to <paramref name="clip"/>. If it is already playing, nothing happens unless
    /// <paramref name="restart"/> is true (then it starts again from the beginning).
    /// </summary>
    public void Play(AnimationClip clip, bool loop, bool restart = false, float fade = 0.25f)
    {
        if (clip == null || !_graph.IsValid()) return;

        if (_active >= 0 && _clips[_active] == clip)
        {
            _loop[_active] = loop;
            if (restart) _mixer.GetInput(_active).SetTime(0);
            return;
        }

        int slot = _active < 0 ? 0 : 1 - _active;
        Playable old = _mixer.GetInput(slot);
        if (old.IsValid())
        {
            _graph.Disconnect(_mixer, slot);
            old.Destroy();
        }

        AnimationClipPlayable playable = AnimationClipPlayable.Create(_graph, clip);
        playable.SetApplyFootIK(true);
        _graph.Connect(playable, 0, _mixer, slot);
        _clips[slot] = clip;
        _loop[slot] = loop;

        if (_active < 0)
        {
            _mixer.SetInputWeight(slot, 1f);
            _active = slot;
            _fadeSpeed = 0f;
            return;
        }

        _mixer.SetInputWeight(slot, 0f);
        _active = slot;
        _fadeSpeed = 1f / Mathf.Max(0.01f, fade);
    }

    /// <summary>Call every frame from the zombie's Update.</summary>
    public void Tick(float deltaTime)
    {
        if (!_graph.IsValid() || _active < 0) return;

        float w = _mixer.GetInputWeight(_active);
        if (w < 1f)
        {
            w = Mathf.Min(1f, w + deltaTime * _fadeSpeed);
            _mixer.SetInputWeight(_active, w);
            _mixer.SetInputWeight(1 - _active, 1f - w);
        }

        // Loop clips whose import settings don't have Loop Time ticked.
        for (int i = 0; i < 2; i++)
        {
            if (!_loop[i] || _clips[i] == null || _clips[i].isLooping) continue;
            Playable input = _mixer.GetInput(i);
            if (!input.IsValid()) continue;
            double length = _clips[i].length;
            if (length > 0.0 && input.GetTime() >= length)
                input.SetTime(input.GetTime() % length);
        }
    }

    public void Dispose()
    {
        if (_graph.IsValid()) _graph.Destroy();
    }
}
