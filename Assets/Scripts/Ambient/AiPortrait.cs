using UnityEngine;

/// <summary>
/// Picks the picture used for the AI everywhere (HUD panel, learning-objectives scene).
/// Order of choice:
///   1. an explicit Sprite assigned in the Inspector (AuditScoreManager > Ai Sprite, or the splash scene's Robot Sprite)
///   2. a Sprite named "AIPortrait" inside any folder called Resources (e.g. Assets/Resources/AIPortrait.png)
///   3. the generated placeholder face.
/// To change the picture: import your PNG, set its Texture Type to "Sprite (2D and UI)", name it AIPortrait,
/// and put it in Assets/Resources. No other setup needed.
/// </summary>
public static class AiPortrait
{
    private const string ResourceName = "AIPortrait";
    private static Sprite _generated;

    public static Sprite Get(Sprite explicitSprite = null)
    {
        if (explicitSprite != null) return explicitSprite;

        Sprite fromResources = Resources.Load<Sprite>(ResourceName);
        if (fromResources != null) return fromResources;

        if (_generated == null) _generated = AuditHUD.MakeEyeSprite();
        return _generated;
    }
}
