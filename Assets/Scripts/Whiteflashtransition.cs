using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Self-contained "blinding light" scene transition. No scene setup needed:
/// call WhiteFlashTransition.Play("CongratsScene") and it will
///   1. build its own overlay canvas (DontDestroyOnLoad),
///   2. ramp the screen to full white,
///   3. load the target scene while everything is white,
///   4. fade the white away to reveal the new scene.
/// Uses unscaled time, so it still works if Time.timeScale is 0.
/// </summary>
public class WhiteFlashTransition : MonoBehaviour
{
    private static WhiteFlashTransition _instance;
    private Image _image;

    public static void Play(string sceneName,
                            float fadeInTime = 1.2f,
                            float holdTime = 0.25f,
                            float fadeOutTime = 1.5f)
    {
        if (_instance != null) return; // already running

        GameObject go = new GameObject("WhiteFlashTransition");
        DontDestroyOnLoad(go);

        _instance = go.AddComponent<WhiteFlashTransition>();
        _instance.BuildOverlay();
        _instance.StartCoroutine(_instance.Run(sceneName, fadeInTime, holdTime, fadeOutTime));
    }

    private void BuildOverlay()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue; // above every other UI

        GameObject imageObj = new GameObject("WhiteFlash", typeof(RectTransform), typeof(Image));
        imageObj.transform.SetParent(transform, false);

        RectTransform rt = (RectTransform)imageObj.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _image = imageObj.GetComponent<Image>();
        _image.color = new Color(1f, 1f, 1f, 0f);
        _image.raycastTarget = false;
    }

    private IEnumerator Run(string sceneName, float fadeIn, float hold, float fadeOut)
    {
        // 1. Ramp to white. Squared easing = slow build, then a blinding surge.
        for (float t = 0f; t < fadeIn; t += Time.unscaledDeltaTime)
        {
            float k = t / fadeIn;
            SetAlpha(k * k);
            yield return null;
        }
        SetAlpha(1f);

        // 2. Load the ending scene while the screen is fully white.
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op != null)
        {
            while (!op.isDone) yield return null;
        }
        else
        {
            Debug.LogError($"WhiteFlashTransition: could not load scene '{sceneName}'.");
        }

        yield return new WaitForSecondsRealtime(hold);

        // 3. Fade the white away to reveal the new scene.
        for (float t = 0f; t < fadeOut; t += Time.unscaledDeltaTime)
        {
            float k = 1f - (t / fadeOut);
            SetAlpha(k * k);
            yield return null;
        }

        _instance = null;
        Destroy(gameObject);
    }

    private void SetAlpha(float a)
    {
        Color c = _image.color;
        c.a = Mathf.Clamp01(a);
        _image.color = c;
    }
}