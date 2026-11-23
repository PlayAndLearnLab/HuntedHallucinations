#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-click setup for the learning-objectives scene:
///   Maze > Create Learning Objectives Scene
/// Creates Assets/Scenes/LearningObjectives.unity (a camera + the LearningObjectivesSplash component),
/// adds it to Build Settings, and points the Intro scene's slideshow at it so the flow becomes
/// Intro > LearningObjectives > MazeTest.
/// </summary>
public static class CreateLearningObjectivesScene
{
    private const string ScenePath = "Assets/Scenes/LearningObjectives.unity";
    private const string IntroPath = "Assets/Scenes/Intro.unity";
    private const string SceneName = "LearningObjectives";

    [MenuItem("Maze/Create Learning Objectives Scene")]
    public static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 1) the scene
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.03f, 0.06f);

        var go = new GameObject("LearningObjectives");
        go.AddComponent<LearningObjectivesSplash>();

        EditorSceneManager.SaveScene(scene, ScenePath);

        // 2) build settings
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool inBuild = scenes.Exists(s => s.path == ScenePath);
        if (!inBuild)
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // 3) point the intro slideshow at the new scene
        string result = "Intro scene not found: set IntroSlideshowManager > Next Scene Name to '" + SceneName + "' yourself.";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(IntroPath) != null)
        {
            Scene intro = EditorSceneManager.OpenScene(IntroPath, OpenSceneMode.Single);
            var manager = Object.FindFirstObjectByType<IntroSlideshowManager>();
            if (manager != null)
            {
                var so = new SerializedObject(manager);
                SerializedProperty next = so.FindProperty("_nextSceneName");
                if (next != null)
                {
                    next.stringValue = SceneName;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(intro);
                    EditorSceneManager.SaveScene(intro);
                    result = "The Intro slideshow now loads '" + SceneName + "' (flow: Intro > " + SceneName + " > MazeTest).";
                }
            }
            else result = "No IntroSlideshowManager in the Intro scene: set its Next Scene Name to '" + SceneName + "' yourself.";
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("[LearningObjectives] Created " + ScenePath + ", added to Build Settings. " + result);
        EditorUtility.DisplayDialog("Learning Objectives scene", "Created " + ScenePath + ".\n\n" + result, "OK");
    }
}
#endif
