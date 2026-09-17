using BinakayanRising.UI.Styleguide;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BinakayanRising.EditorTools
{
    /// <summary>
    /// Opens the interface styleguide in a scratch scene and enters play mode.
    /// </summary>
    /// <remarks>
    /// The styleguide builds itself at runtime, so it needs a play session to exist at all. A
    /// throwaway scene is used rather than the project's own so that entering the styleguide never
    /// dirties <c>SampleScene</c> — the harness should be reachable without leaving a diff behind.
    /// </remarks>
    public static class StyleguideLauncher
    {
        [MenuItem("Tools/Binakayan Rising/Open Styleguide", priority = 40)]
        public static void Open()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                Debug.Log("Styleguide: left play mode. Run the menu item again to reopen.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "Styleguide";

            var host = new GameObject("Styleguide");
            host.AddComponent<StyleguideScreen>();

            EditorApplication.isPlaying = true;
            Debug.Log("Styleguide: entering play mode.");
        }
    }
}
