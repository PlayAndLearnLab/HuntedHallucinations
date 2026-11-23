using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The "what you'll practice" screen. Lives in its OWN scene (create it with the menu Maze > Create Learning Objectives Scene),
/// so nothing else is on screen. It builds its UI at runtime, waits for the button / Space, then loads the next scene.
/// Put it between the intro slideshow and the maze: set IntroSlideshowManager's Next Scene Name to "LearningObjectives".
/// All wording is editable in the Inspector.
/// </summary>
public class LearningObjectivesSplash : MonoBehaviour
{
    [Header("Flow")]
    [Tooltip("Scene loaded when the player clicks the button. Must be in Build Settings.")]
    [SerializeField] private string _nextSceneName = "MazeTest";

    [Header("Look")]
    [Tooltip("Optional. Empty = Resources/AIPortrait, or the generated face. See AiPortrait.")]
    [SerializeField] private Sprite _robotSprite;

    [Header("Wording")]
    [SerializeField] private string _title = "THE AI IS HALLUCINATING";
    [TextArea] [SerializeField] private string _intro = "You are trapped inside a corrupted build of your own game. Its AI sounds sure of itself. It is not always right.";
    [SerializeField] private string _goal1Head = "AI can hallucinate";
    [TextArea] [SerializeField] private string _goal1Body = "It can state false things with total confidence. Sounding sure is not the same as being right.";
    [SerializeField] private string _goal2Head = "Make a habit of checking the ground truth";
    [TextArea] [SerializeField] private string _goal2Body = "Before you trust what an AI tells you, compare it with a source you trust. Here, that source is your GDD book.";
    [SerializeField] private string _controls = "[I]  open your GDD book        [F]  flag a hallucination";
    [SerializeField] private string _buttonLabel = "ENTER THE BUILD";

    private bool _done;
    private bool _loading;

    IEnumerator Start()
    {
        Time.timeScale = 1f; // in case a previous scene left it paused
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureEventSystem();
        Build();

        float opened = Time.realtimeSinceStartup;
        while (!_done)
        {
            bool key = Keyboard.current != null &&
                       (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame);
            if (key && Time.realtimeSinceStartup - opened > 0.4f) _done = true;
            yield return null;
        }

        if (_loading) yield break;
        _loading = true;
        SceneManager.LoadScene(_nextSceneName);
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        es.name = "EventSystem (splash)";
    }

    private void Build()
    {
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // dim backdrop
        var bg = Panel("Backdrop", canvasGO.transform, new Color(0.03f, 0.03f, 0.06f, 0.97f));
        Stretch(bg);

        // robot face + title + intro
        var face = new GameObject("Robot", typeof(RectTransform), typeof(Image));
        face.transform.SetParent(bg, false);
        var frt = (RectTransform)face.transform;
        Anchor(frt, 0.5f, 1f); frt.anchoredPosition = new Vector2(0f, -30f); frt.sizeDelta = new Vector2(150f, 150f);
        var fimg = face.GetComponent<Image>();
        fimg.sprite = AiPortrait.Get(_robotSprite);
        fimg.preserveAspect = true;
        fimg.raycastTarget = false;

        Text("Title", bg, _title, 54f, FontStyles.Bold, new Color(1f, 0.4f, 0.35f), TextAlignmentOptions.Center,
             0.5f, 1f, new Vector2(0f, -190f), new Vector2(1500f, 70f));
        Text("Intro", bg, _intro, 28f, FontStyles.Normal, new Color(0.85f, 0.85f, 0.9f), TextAlignmentOptions.Center,
             0.5f, 1f, new Vector2(0f, -265f), new Vector2(1300f, 90f));

        Text("Header", bg, "WHAT YOU WILL PRACTICE", 22f, FontStyles.Bold, new Color(0.6f, 0.65f, 0.8f), TextAlignmentOptions.Center,
             0.5f, 1f, new Vector2(0f, -385f), new Vector2(800f, 34f));

        Goal(bg, "1", _goal1Head, _goal1Body, -440f);
        Goal(bg, "2", _goal2Head, _goal2Body, -610f);

        Text("Controls", bg, _controls, 24f, FontStyles.Normal, new Color(0.75f, 0.85f, 1f), TextAlignmentOptions.Center,
             0.5f, 0f, new Vector2(0f, 190f), new Vector2(1400f, 34f));

        // button
        var btnGO = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(bg, false);
        var brt = (RectTransform)btnGO.transform;
        Anchor(brt, 0.5f, 0f); brt.anchoredPosition = new Vector2(0f, 110f); brt.sizeDelta = new Vector2(420f, 70f);
        var bimg = btnGO.GetComponent<Image>();
        bimg.color = new Color(0.85f, 0.2f, 0.2f, 1f);
        var btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = bimg;
        btn.onClick.AddListener(() => _done = true);
        Text("Label", btnGO.transform, _buttonLabel, 30f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center,
             0.5f, 0.5f, Vector2.zero, new Vector2(420f, 70f));

        Text("Hint", bg, "or press SPACE", 20f, FontStyles.Normal, new Color(0.6f, 0.6f, 0.7f), TextAlignmentOptions.Center,
             0.5f, 0f, new Vector2(0f, 70f), new Vector2(400f, 28f));
    }

    private void Goal(Transform parent, string number, string head, string body, float y)
    {
        var card = Panel("Goal" + number, parent, new Color(1f, 1f, 1f, 0.06f));
        Anchor(card, 0.5f, 1f); card.anchoredPosition = new Vector2(0f, y); card.sizeDelta = new Vector2(1300f, 150f);

        var badge = Panel("Badge", card, new Color(0.85f, 0.2f, 0.2f, 1f));
        Anchor(badge, 0f, 0.5f); badge.anchoredPosition = new Vector2(70f, 0f); badge.sizeDelta = new Vector2(70f, 70f);
        Text("Num", badge, number, 40f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, 0.5f, 0.5f, Vector2.zero, new Vector2(70f, 70f));

        Text("Head", card, head, 34f, FontStyles.Bold, Color.white, TextAlignmentOptions.TopLeft,
             0f, 1f, new Vector2(190f, -22f), new Vector2(1060f, 44f));
        Text("Body", card, body, 26f, FontStyles.Normal, new Color(0.82f, 0.82f, 0.88f), TextAlignmentOptions.TopLeft,
             0f, 1f, new Vector2(190f, -72f), new Vector2(1060f, 70f));
    }

    // ---------- tiny UI helpers ----------

    private static RectTransform Panel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void Anchor(RectTransform rt, float x, float y)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(x, y);
        rt.pivot = new Vector2(x, y);
    }

    private static TMP_Text Text(string name, Transform parent, string text, float size, FontStyles style, Color color,
                                 TextAlignmentOptions align, float ax, float ay, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        Anchor(rt, ax, ay);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }
}
