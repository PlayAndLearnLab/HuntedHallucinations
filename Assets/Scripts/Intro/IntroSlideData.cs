using UnityEngine;

/// <summary>
/// One slide of the opening story sequence. Create one asset per slide via
/// Assets > Create > Maze > Intro Slide, then drag them into
/// IntroSlideshowManager's _slides array in the order they should play.
///
/// Mirrors the PuzzleData pattern already used elsewhere in the project:
/// data lives in ScriptableObject assets, a manager drives playback.
/// </summary>
[CreateAssetMenu(fileName = "IntroSlide", menuName = "Maze/Intro Slide")]
public class IntroSlideData : ScriptableObject
{
    [Header("Visual")]
    [Tooltip("Full slide image for this beat, exported as one flat PNG from Figma " +
             "(art + text combined). Leave null for a text-only/blank slide if needed.")]
    public Sprite image;

    [Header("Timing (optional)")]
    [Tooltip("If > 0, this slide auto-advances after this many seconds even without input. " +
              "Leave at 0 to require the player to press Continue/Space.")]
    public float autoAdvanceAfterSeconds = 0f;
}