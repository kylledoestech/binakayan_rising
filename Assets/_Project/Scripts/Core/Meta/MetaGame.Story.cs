using BinakayanRising.Core.Content;

namespace BinakayanRising.Core.Meta
{
    /// <summary>
    /// The story scenes the encampment owes the player: a hub-task quest's opening scene, played
    /// once, the first time that quest is current (#34).
    /// </summary>
    /// <remarks>
    /// A battle's opening scene is played by the Mission Tent when the battle is launched, every
    /// time, so it is not tracked here. A hub-task quest has no launch, so without this its scene
    /// (Act 1, Act 3) would never be seen.
    /// </remarks>
    public sealed partial class MetaGame
    {
        /// <summary>The flag that records <paramref name="sceneId"/> as seen.</summary>
        public static string SceneFlag(string sceneId)
        {
            return "scene." + sceneId;
        }

        /// <summary>
        /// The id of the story scene the camp should play now, or null: the current quest's opening
        /// scene when the quest is played in the encampment and the scene has not been seen.
        /// </summary>
        public string PendingStoryScene
        {
            get
            {
                Quest quest = CurrentQuest;
                if (quest == null || quest.Kind != QuestKind.HubTask || string.IsNullOrEmpty(quest.PreCutscene))
                {
                    return null;
                }

                if (Cutscenes.Find(quest.PreCutscene) == null || Data.HasFlag(SceneFlag(quest.PreCutscene)))
                {
                    return null;
                }

                return quest.PreCutscene;
            }
        }

        /// <summary>Records a story scene as seen. False if it already was.</summary>
        public bool MarkSceneSeen(string sceneId)
        {
            return !string.IsNullOrEmpty(sceneId) && SetFlag(SceneFlag(sceneId));
        }
    }
}
