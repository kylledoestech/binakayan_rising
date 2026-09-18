using BinakayanRising.Core.Meta;
using BinakayanRising.UI.Kit;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// A campaign screen: a <see cref="GameScreen"/> that can reach the shell, and through it the
    /// open campaign.
    /// </summary>
    public abstract class ShellScreen : GameScreen
    {
        /// <summary>The shell that owns this screen. Assigned before the screen is first shown.</summary>
        public GameShell Shell { get; set; }

        /// <summary>The open campaign, or null on the main menu.</summary>
        protected MetaGame Game
        {
            get { return Shell != null && Shell.Session != null ? Shell.Session.Game : null; }
        }
    }
}
