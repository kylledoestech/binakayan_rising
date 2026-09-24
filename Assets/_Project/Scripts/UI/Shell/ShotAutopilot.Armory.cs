using System.Collections;
using BinakayanRising.Core.Content;
using BinakayanRising.Core.Localization;
using BinakayanRising.Core.Meta;
using BinakayanRising.UI.Kit;

namespace BinakayanRising.UI.Shell
{
    /// <summary>
    /// The named period weapons, as part of the "issues" route: the Armory list with the opening
    /// kit and a Lantaka greyed out for everyone but the Trench Engineer, a blade's bonuses, the
    /// list's second page, and the Training Grounds weapon line, in English and Filipino.
    /// </summary>
    public sealed partial class ShotAutopilot
    {
        private IEnumerator ArmoryWeapons()
        {
            MetaGame game = Shell.Session.Game;

            // The q06 reward, staged into the middle of the rack so the first page shows it
            // beside the blades; the seventh weapon turns the list to a second page.
            var lantaka = new OwnedWeapon { id = game.Data.nextWeaponId++, weapon = WeaponCatalog.Lantaka };
            game.Data.weapons.Insert(3, lantaka);

            Shell.Camp.ClickSite(Places.Armory);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            Hub().Dialogue.Finish();
            yield return Wait(0.4f);
            ClickIn("Tab 1");
            Armory().SelectedWeapon = lantaka.id;
            yield return Shot("a_01_armory_lantaka");

            Armory().SelectedWeapon = WeaponOn(game, UnitCatalog.Vanguard);
            yield return Shot("a_02_armory_talibong");

            ClickIn("Button Weapon Page Next");
            yield return Shot("a_03_armory_page2");

            UserPrefs.ChooseLanguage(Language.Filipino);
            Armory().SelectedWeapon = lantaka.id;
            yield return Shot("a_04_armory_lantaka_fil");
            Armory().SelectedWeapon = WeaponOn(game, UnitCatalog.Engineer);
            yield return Shot("a_05_armory_gulok_fil");
            UserPrefs.ChooseLanguage(Language.English);
            ClickIn("Button Back To Camp");
            yield return Wait(0.6f);

            // The Engineer takes the Lantaka: the Training line shows all three of its twists.
            OwnedUnit engineer = null;
            foreach (OwnedUnit unit in game.Units)
            {
                if (unit.archetype == UnitCatalog.Engineer)
                {
                    engineer = unit;
                }
            }

            game.TryEquip(engineer.id, lantaka.id);
            Shell.Camp.ClickSite(Places.Training);
            yield return WaitWhile(() => Shell.Camp.IsWalking, 8f);
            Hub().Dialogue.Finish();
            yield return Wait(0.4f);
            Training().SelectedUnit = engineer.id;
            yield return Shot("a_06_training_lantaka");
            UserPrefs.ChooseLanguage(Language.Filipino);
            yield return Shot("a_07_training_lantaka_fil");
            UserPrefs.ChooseLanguage(Language.English);
            ClickIn("Button Back To Camp");
            yield return Wait(0.6f);
        }

        private InventoryScreen Armory()
        {
            return Shell.Router.Get<InventoryScreen>(Gameplay.Flow.GameState.Inventory);
        }

        private static int WeaponOn(MetaGame game, string archetype)
        {
            foreach (OwnedUnit unit in game.Units)
            {
                if (unit.archetype == archetype)
                {
                    return unit.weaponId;
                }
            }

            return 0;
        }
    }
}
