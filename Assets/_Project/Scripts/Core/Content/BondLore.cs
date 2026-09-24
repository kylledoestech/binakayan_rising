using System.Collections.Generic;
using BinakayanRising.Core.Localization;

namespace BinakayanRising.Core.Content
{
    /// <summary>One Kapatiran lore dialogue: Table 3's "Lore Dialogue 1–4".</summary>
    public sealed class LoreDialogue
    {
        /// <summary>The <see cref="BondCatalog"/> pair whose rank C unlocks it.</summary>
        public readonly string BondId;

        /// <summary>Table 3's dialogue number, 1 to 4.</summary>
        public readonly int Number;

        public readonly LocString Title;

        /// <summary>Where and when, for the list.</summary>
        public readonly LocString Setting;

        /// <summary>The conversation. Speakers are unit archetype ids.</summary>
        public readonly IReadOnlyList<DialogueLine> Lines;

        public LoreDialogue(string bondId, int number, LocString title, LocString setting, IReadOnlyList<DialogueLine> lines)
        {
            BondId = bondId;
            Number = number;
            Title = title;
            Setting = setting;
            Lines = lines;
        }
    }

    /// <summary>
    /// The four Kapatiran lore dialogues (#20), one per bonded pair, each unlocked when its pair
    /// reaches rank C (Capstone Table 3) and re-playable afterwards from the Training Grounds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>SME check pending.</b> These are drafts for the adviser or a subject-matter expert to
    /// check before the defense (DESIGN-DECISIONS, "Kapatiran lore"). The conversations are
    /// invented; the history they lean on is kept to what the standard accounts agree on:
    /// Evangelista studied engineering at Ghent and returned in 1896; Aguinaldo was the
    /// capitán municipal of Kawit and a leader of the Magdalo council; Cavite's Katipunan was split
    /// between the Magdalo (Kawit) and Magdiwang (Noveleta) councils, with Santiago Álvarez
    /// commanding Magdiwang's forces; Binakayan and Dalahican were held against Governor-General
    /// Ramón Blanco's offensive of 9–11 November 1896; the revolutionaries were short of rifles
    /// and armed themselves partly with captured Spanish ones. The Vanguard, the Field Medic, the
    /// Marksman, the Engineer and the two infantrymen are composites, not named people.
    /// </para>
    /// <para>
    /// Every speaker is a unit archetype id, so the dialogue box shows the unit's portrait, name
    /// and role.
    /// </para>
    /// </remarks>
    public static class BondLore
    {
        private static DialogueLine Line(string speaker, string english, string filipino)
        {
            return new DialogueLine(speaker, new LocString(english, filipino));
        }

        private static readonly List<LoreDialogue> all = new List<LoreDialogue>
        {
            new LoreDialogue(
                BondCatalog.EvangelistaAguinaldo, 1,
                new LocString("The Scholar and the Capitán", "Ang Iskolar at ang Kapitan"),
                new LocString("Kawit, Cavite · 1896", "Kawit, Kabite · 1896"),
                new List<DialogueLine>
                {
                    Line(UnitCatalog.Aguinaldo,
                        "Engineer, the men say you studied roads and bridges in Belgium. What use is Ghent to Kawit?",
                        "Inhinyero, sabi ng mga tauhan, nag-aral ka raw ng daan at tulay sa Belhika. Ano ang silbi ng Ghent sa Kawit?"),
                    Line(UnitCatalog.Evangelista,
                        "More than you think, Capitán. At Ghent they taught me how earth holds and how water runs. Both will fight for us, if we let them.",
                        "Higit sa iyong akala, Kapitan. Itinuro sa akin sa Ghent kung paano kumakapit ang lupa at dumadaloy ang tubig. Pareho silang lalaban para sa atin, kung hahayaan natin."),
                    Line(UnitCatalog.Evangelista,
                        "Brave men charging across open ground only feed the Spanish guns. Let the Spanish charge at earth instead.",
                        "Ang matatapang na sumusugod sa lantad na parang ay pinapakain lamang ang mga kanyon ng Kastila. Hayaan nating ang Kastila ang sumugod sa lupa."),
                    Line(UnitCatalog.Aguinaldo,
                        "Then dig. Kawit will give you every spade and every pair of hands it has.",
                        "Kung gayon, humukay ka. Ibibigay ng Kawit ang bawat pala at bawat pares ng kamay na mayroon ito."),
                    Line(UnitCatalog.Evangelista,
                        "Trenches deep enough to stand and fire from, and bamboo stakes in front, so they slow down right where our rifles reach.",
                        "Mga trinserang sapat ang lalim upang makatayo at makapagpaputok, at mga tulos na kawayan sa harap, upang bumagal sila mismo kung saan abot ng ating mga riple."),
                    Line(UnitCatalog.Aguinaldo,
                        "When Blanco's columns come, Engineer, we will be waiting for them inside your earth.",
                        "Pagdating ng mga hanay ni Blanco, Inhinyero, hihintayin natin sila sa loob ng iyong lupa.")
                }),

            new LoreDialogue(
                BondCatalog.VanguardFieldMedic, 2,
                new LocString("Bandages and Bolos", "Benda at Bolo"),
                new LocString("Encampment, Cavite · 1896", "Kampo, Kabite · 1896"),
                new List<DialogueLine>
                {
                    Line(UnitCatalog.FieldMedic,
                        "Hold still. The bullet passed clean through your arm. You are lucky.",
                        "Huwag kang gagalaw. Tumagos nang malinis ang bala sa iyong braso. Suwerte ka."),
                    Line(UnitCatalog.Vanguard,
                        "Lucky? Three men of my line did not get up again.",
                        "Suwerte? Tatlo sa aking hanay ang hindi na muling bumangon."),
                    Line(UnitCatalog.FieldMedic,
                        "Then carry the ones who can still breathe back to me, and I will send them back to you. That is my part of this war.",
                        "Kung gayon, dalhin mo sa akin ang mga humihinga pa, at ibabalik ko sila sa iyo. Iyan ang aking bahagi sa digmaang ito."),
                    Line(UnitCatalog.Vanguard,
                        "Where did you learn to bind wounds like that?",
                        "Saan ka natutong magbenda ng sugat nang ganyan?"),
                    Line(UnitCatalog.FieldMedic,
                        "From my mother: boiled cloth, clean water, guava leaves. Medicine is scarce out here, so we use what grows.",
                        "Sa aking ina: pinakuluang tela, malinis na tubig, dahon ng bayabas. Kakaunti ang gamot dito, kaya ginagamit namin ang tumutubo."),
                    Line(UnitCatalog.Vanguard,
                        "Then I stand in front and you stand behind me. Neither of us falls while the other is there.",
                        "Kung gayon, ako ang nasa harap at ikaw ang nasa likod ko. Walang babagsak sa atin habang naroon ang isa.")
                }),

            new LoreDialogue(
                BondCatalog.MarksmanEngineer, 3,
                new LocString("Earth and Aim", "Lupa at Asinta"),
                new LocString("The Binakayan trench line · 1896", "Hanay ng trinsera sa Binakayan · 1896"),
                new List<DialogueLine>
                {
                    Line(UnitCatalog.Engineer,
                        "Keep your head below the parapet. I did not dig this trench for you to waste it.",
                        "Ibaba mo ang iyong ulo sa ilalim ng pader. Hindi ko hinukay ang trinserang ito para sayangin mo lang."),
                    Line(UnitCatalog.Marksman,
                        "I have to see to shoot, Kuya. And I have only a handful of cartridges.",
                        "Kailangan kong makakita para makabaril, Kuya. At iilan lamang ang aking bala."),
                    Line(UnitCatalog.Engineer,
                        "Then make each one count. Most of our men have a bolo and nothing else.",
                        "Kung gayon, sulitin mo ang bawat isa. Bolo lamang ang hawak ng karamihan sa ating mga tauhan."),
                    Line(UnitCatalog.Marksman,
                        "This rifle was taken from a Spanish post. Every shot I fire is one of theirs coming home to them.",
                        "Nakuha ang ripleng ito sa isang himpilan ng Kastila. Bawat putok ko ay isa sa kanila na bumabalik sa kanila."),
                    Line(UnitCatalog.Engineer,
                        "Fire through the gap I cut for you. The bamboo stakes will slow them right in your sights.",
                        "Magpaputok ka sa siwang na ginawa ko para sa iyo. Pababagalin sila ng mga tulos na kawayan mismo sa iyong asinta."),
                    Line(UnitCatalog.Marksman,
                        "Then you dig and I watch. Between the two of us, this shore stays ours.",
                        "Kung gayon, ikaw ang huhukay at ako ang magbabantay. Sa ating dalawa, mananatiling atin ang baybaying ito.")
                }),

            new LoreDialogue(
                BondCatalog.MagdaloMagdiwang, 4,
                new LocString("Two Councils, One Line", "Dalawang Sanggunian, Iisang Hanay"),
                new LocString("Binakayan and Dalahican · November 1896", "Binakayan at Dalahican · Nobyembre 1896"),
                new List<DialogueLine>
                {
                    Line(UnitCatalog.MagdaloInfantry,
                        "A Magdiwang man in a Magdalo trench? Noveleta must be missing you.",
                        "Isang Magdiwang sa trinsera ng Magdalo? Siguradong hinahanap ka na sa Noveleta."),
                    Line(UnitCatalog.MagdiwangInfantry,
                        "Noveleta sends its men wherever the Spanish are. General Álvarez holds Dalahican; I carry his word to your line.",
                        "Ipinapadala ng Noveleta ang kanyang mga tauhan saanman naroon ang Kastila. Hawak ni Heneral Álvarez ang Dalahican; dala ko ang kanyang salita sa inyong hanay."),
                    Line(UnitCatalog.MagdaloInfantry,
                        "Our councils argue over everything: who leads, who signs, whose orders come first.",
                        "Pinagtatalunan ng ating mga sanggunian ang lahat: kung sino ang mamumuno, kung sino ang pipirma, kung kaninong utos ang mauuna."),
                    Line(UnitCatalog.MagdiwangInfantry,
                        "Let them argue in the town halls. Out here, a Spanish bullet does not ask which council you swore to.",
                        "Hayaan silang magtalo sa mga bulwagan. Dito, hindi itinatanong ng bala ng Kastila kung saang sanggunian ka nanumpa."),
                    Line(UnitCatalog.MagdaloInfantry,
                        "Binakayan and Dalahican. The same fight on two fronts.",
                        "Binakayan at Dalahican. Iisang laban sa dalawang larangan."),
                    Line(UnitCatalog.MagdiwangInfantry,
                        "Then we hold both, kapatid. Magdalo and Magdiwang, one Katipunan.",
                        "Kung gayon, hawakan natin pareho, kapatid. Magdalo at Magdiwang, iisang Katipunan.")
                })
        };

        /// <summary>The four dialogues in Table 3's order.</summary>
        public static IReadOnlyList<LoreDialogue> All
        {
            get { return all; }
        }

        /// <summary>The dialogue <paramref name="bondId"/>'s rank C unlocks, or null.</summary>
        public static LoreDialogue For(string bondId)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].BondId == bondId)
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}
