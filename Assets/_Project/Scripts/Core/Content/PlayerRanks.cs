using System.Collections.Generic;
using BinakayanRising.Core.Localization;

namespace BinakayanRising.Core.Content
{
    /// <summary>One step of the player's rank ladder.</summary>
    public sealed class PlayerRank
    {
        public readonly int Index;

        /// <summary>The Filipino rank title, the same in both languages.</summary>
        public readonly string Title;

        /// <summary>What the title means.</summary>
        public readonly LocString Gloss;

        /// <summary>
        /// What the rank commands and answers for, for the Journal's rank ladder (panel feedback:
        /// "# of ranks - as description").
        /// </summary>
        public readonly LocString Duty;

        public PlayerRank(int index, string title, LocString gloss, LocString duty)
        {
            Index = index;
            Title = title;
            Gloss = gloss;
            Duty = duty;
        }
    }

    /// <summary>
    /// The Tactician's rank: eight titles, each earned by clearing a milestone sub-quest (panel
    /// feedback: "name identification per level", then "8 ranks"). Revolutionary-army titles, in
    /// the Filipino forms of the Spanish ranks the Katipunan adopted.
    /// </summary>
    public static class PlayerRanks
    {
        private static readonly PlayerRank[] ladder =
        {
            new PlayerRank(0, "Kawal", new LocString("Private", "Sundalo"), new LocString(
                "The rank and file of the revolutionary army. A Kawal obeys the officers, keeps a weapon ready and holds a place in the line.",
                "Ang karaniwang sundalo ng hukbong rebolusyonaryo. Sumusunod ang Kawal sa mga opisyal, laging handa ang sandata at hindi umaalis sa puwesto sa hanay.")),
            new PlayerRank(1, "Kabo", new LocString("Corporal", "Pinuno ng pulutong"), new LocString(
                "Leads a squad of a few soldiers. A Kabo passes orders down, keeps the squad together and leads it by example in the fight.",
                "Namumuno sa isang pulutong ng ilang kawal. Ipinapasa ng Kabo ang mga utos, pinananatiling buo ang pulutong at nangunguna rito sa laban.")),
            new PlayerRank(2, "Sarhento", new LocString("Sergeant", "Katuwang ng pinuno ng pulutong"), new LocString(
                "The officers' right hand. A Sarhento drills the soldiers, keeps discipline and sees that every squad is armed and fed.",
                "Ang kanang kamay ng mga opisyal. Sinasanay ng Sarhento ang mga kawal, pinananatili ang disiplina at tinitiyak na may armas at pagkain ang bawat pulutong.")),
            new PlayerRank(3, "Tenyente", new LocString("Lieutenant", "Pinuno ng hanay"), new LocString(
                "The first rank of officer. A Tenyente commands a platoon of several squads and takes the captain's place if the captain falls.",
                "Ang unang ranggo ng opisyal. Pinamumunuan ng Tenyente ang isang hanay ng ilang pulutong at pumapalit sa kapitan kapag ito ay nasawi.")),
            new PlayerRank(4, "Kapitan", new LocString("Captain", "Pinuno ng kompanya"), new LocString(
                "Commands a company. A Kapitan plans its part in the battle, chooses where its soldiers stand and answers for every one of them.",
                "Namumuno sa isang kompanya. Pinaplano ng Kapitan ang bahagi nito sa labanan, pinipili kung saan tatayo ang mga kawal at nananagot sa bawat isa sa kanila.")),
            new PlayerRank(5, "Komandante", new LocString("Major", "Pinuno ng batalyon"), new LocString(
                "Commands a battalion of several companies. A Komandante moves them together across the field and holds a whole stretch of the line.",
                "Namumuno sa isang batalyon ng ilang kompanya. Sabay-sabay silang pinakikilos ng Komandante sa larangan at ipinagtatanggol ang isang buong bahagi ng hanay.")),
            new PlayerRank(6, "Koronel", new LocString("Colonel", "Pinuno ng rehimyento"), new LocString(
                "Commands a regiment. A Koronel helps the generals draw up the plan of battle and sees it carried out along the whole front.",
                "Namumuno sa isang rehimyento. Katuwang ang Koronel ng mga heneral sa pagbuo ng plano ng labanan at tinitiyak na naisasagawa ito sa buong harapan.")),
            new PlayerRank(7, "Heneral", new LocString("General", "Pinuno ng hukbo"), new LocString(
                "Commands an army. A Heneral decides where and when to fight, and answers to the leaders of the revolution for the whole campaign.",
                "Namumuno sa isang hukbo. Ang Heneral ang nagpapasya kung saan at kailan lalaban, at nananagot sa mga pinuno ng himagsikan para sa buong kampanya."))
        };

        /// <summary>
        /// The sub-quest whose first clear earns each rank; the first is held from the start.
        /// Spread so every level promotes the player at least twice.
        /// </summary>
        private static readonly string[] milestones = { null, "q02", "q04", "q05", "q06", "q07", "q09", "q10" };

        /// <summary>What Tadah, the Senador, says as each rank is earned.</summary>
        private static readonly LocString[] announcements =
        {
            new LocString(
                "Every soldier of the revolution began as a Kawal. So do you.",
                "Nagsimula bilang Kawal ang bawat sundalo ng himagsikan. Ganoon ka rin."),
            new LocString(
                "You held the trench at Kawit. From today you are a Kabo, and a squad will follow you.",
                "Naipagtanggol mo ang trinsera sa Kawit. Mula ngayon ikaw ay Kabo, at susunod sa iyo ang isang pulutong."),
            new LocString(
                "An army that is fed can fight. For keeping the camp alive, you are now a Sarhento.",
                "Nakakalaban ang hukbong busog. Sa pagpapanatiling buhay ng kampo, ikaw ay Sarhento na."),
            new LocString(
                "The landing party is beaten back at Bacoor. Rise, Tenyente.",
                "Naitaboy ang pangkat na dumaong sa Bacoor. Tumindig ka, Tenyente."),
            new LocString(
                "The earthworks stand because you held them. You are a Kapitan of the revolution.",
                "Nakatayo ang mga muog dahil ipinagtanggol mo sila. Ikaw ay Kapitan na ng himagsikan."),
            new LocString(
                "Three slipped behind the lines and all three came home. Komandante - the men trust you now.",
                "Tatlo ang pumuslit sa likod ng kaaway at tatlo ang nakauwi. Komandante - may tiwala na sa iyo ang mga kawal."),
            new LocString(
                "Dalahican did not fall. Koronel, one dawn remains.",
                "Hindi bumagsak ang Dalahican. Koronel, isang bukang-liwayway na lamang ang natitira."),
            new LocString(
                "Binakayan is free. The first great victory of the revolution is yours, Heneral.",
                "Malaya ang Binakayan. Iyo ang unang dakilang tagumpay ng himagsikan, Heneral.")
        };

        public static int Count
        {
            get { return ladder.Length; }
        }

        /// <summary>The rank at <paramref name="index"/>, clamped to the ladder.</summary>
        public static PlayerRank At(int index)
        {
            return ladder[index < 0 ? 0 : (index >= ladder.Length ? ladder.Length - 1 : index)];
        }

        /// <summary>Tadah's line for reaching rank <paramref name="index"/>.</summary>
        public static LocString AnnouncementOf(int index)
        {
            return announcements[index < 0 ? 0 : (index >= announcements.Length ? announcements.Length - 1 : index)];
        }

        /// <summary>The sub-quest that earns rank <paramref name="index"/>, or null for the first.</summary>
        public static string MilestoneOf(int index)
        {
            return index > 0 && index < milestones.Length ? milestones[index] : null;
        }

        /// <summary>The highest rank whose milestone is in <paramref name="cleared"/>.</summary>
        public static PlayerRank ForCleared(ICollection<string> cleared)
        {
            int index = 0;
            for (int i = 1; i < milestones.Length; i++)
            {
                if (cleared != null && cleared.Contains(milestones[i]))
                {
                    index = i;
                }
            }

            return ladder[index];
        }

        /// <summary>
        /// Where a rank shown under the old four-title ladder (one per level cleared) sits on this
        /// one, for saves written before it.
        /// </summary>
        public static int FromLevelLadder(int oldIndex)
        {
            switch (oldIndex)
            {
                case 1: return 2;
                case 2: return 5;
                case 3: return 7;
                default: return 0;
            }
        }
    }
}
