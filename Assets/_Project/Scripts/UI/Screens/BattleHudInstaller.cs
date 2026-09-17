using BinakayanRising.Gameplay;
using UnityEngine;

namespace BinakayanRising.UI.Screens
{
    /// <summary>
    /// Teaches the battle prototype how to build its interface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="BattlePlaytest"/> lives in the Gameplay assembly and must not reference the UI
    /// assembly, so it cannot name <see cref="BattleHud"/> directly. Instead it exposes a factory
    /// hook, and this installer — which lives on the UI side, where naming both is allowed — fills
    /// it in.
    /// </para>
    /// <para>
    /// Registration runs at <see cref="RuntimeInitializeLoadType.BeforeSceneLoad"/> so it is
    /// guaranteed to happen before the prototype's own <c>AfterSceneLoad</c> bootstrap asks for a
    /// HUD. Doing this from a scene object instead would make the outcome depend on component
    /// order, which is exactly the kind of intermittent failure that is miserable to diagnose.
    /// </para>
    /// </remarks>
    public static class BattleHudInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            BattlePlaytest.HudFactory = host =>
            {
                if (host == null)
                {
                    return null;
                }

                return host.AddComponent<BattleHud>();
            };
        }
    }
}
