using BinakayanRising.UI.Kit;
using UnityEditor;
using UnityEngine;

namespace BinakayanRising.EditorTools
{
    /// <summary>
    /// Editor shortcuts for the guided tutorial.
    /// </summary>
    /// <remarks>
    /// PlayerPrefs survive between play sessions in the editor, so once the tutorial has been seen
    /// it never starts on its own again. This puts it back to first-run without digging through
    /// the registry or a plist.
    /// </remarks>
    public static class TutorialMenu
    {
        [MenuItem("Tools/Binakayan Rising/Reset First-Run Tutorial", priority = 60)]
        public static void ResetTutorial()
        {
            UserPrefs.ResetTutorial();
            Debug.Log("Tutorial: reset. It starts on the next play session.");
        }
    }
}
