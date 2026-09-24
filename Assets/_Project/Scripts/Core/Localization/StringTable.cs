namespace BinakayanRising.Core.Localization
{
    /// <summary>
    /// English and Filipino text for every <see cref="TextKey"/>, authored side by side.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One <c>Add</c> call per key, both languages on the same line, so a translator reviewing the
    /// file never has to hunt for a string's counterpart and a missing translation is visible at a
    /// glance. <c>StringTableTests</c> fails the build if a key is left empty or if the two
    /// languages disagree about <c>{0}</c> placeholders or rich-text tags.
    /// </para>
    /// <para>
    /// <b>Glyphs.</b> Display and body fonts are static SDF atlases baked from a fixed character
    /// set (<c>ThemeSetup.BakedCharacters</c>). Anything outside it renders as a box — use a plain
    /// hyphen, never U+2212 minus, and no arrow glyphs. The UI checks every string against the
    /// fonts at start-up in the editor.
    /// </para>
    /// <para>
    /// The Filipino text is a first draft and needs review by a native speaker before release.
    /// </para>
    /// </remarks>
    public static class StringTable
    {
        private static readonly string[] English = new string[(int)TextKey.Count];
        private static readonly string[] Filipino = new string[(int)TextKey.Count];

        static StringTable()
        {
            // ---------------------------------------------------------------- HUD — top bar
            Add(TextKey.HudSeed, "Seed", "Binhi");
            Add(TextKey.HudSpeed, "Speed", "Bilis");
            Add(TextKey.HudSkip, "Skip", "Laktaw");
            Add(TextKey.HudRedeploy, "Redeploy", "Ipuwesto Muli");

            // ---------------------------------------------------------------- HUD — phase
            Add(TextKey.PhaseDeployment, "Deployment", "Pagpupuwesto");
            Add(TextKey.PhaseCombat, "Combat — AI Turn {0}", "Labanan — Yugto {0}");
            Add(TextKey.PhaseResolved, "Resolved", "Nalutas");

            // ---------------------------------------------------------------- HUD — deployment
            Add(TextKey.DeployTitle, "Deploy Troops", "Ipuwesto ang Hukbo");
            Add(TextKey.DeployHint,
                "Choose a unit, then click a lit tile on the trench or a tent. Right-click a placed unit to lift it.",
                "Pumili ng yunit, at i-click ang nakailaw na tile sa trinsera o tolda. I-right-click ang nakapuwestong yunit upang alisin.");
            Add(TextKey.RosterStats, "HP {0}   ATK {1}   DEF {2}   RNG {3}", "HP {0}   ATK {1}   DEF {2}   RNG {3}");
            Add(TextKey.RosterDeployed, "✓ deployed", "✓ nakapuwesto");
            Add(TextKey.SpanishColumn, "Spanish Column", "Hanay ng Kastila");
            Add(TextKey.AutoDeploy, "Auto-deploy", "Kusang Ipuwesto");
            Add(TextKey.BeginAssault, "Begin Assault", "Simulan ang Salakay");
            Add(TextKey.ShowTips, "Show Tips", "Ipakita ang Tip");
            Add(TextKey.HideTips, "Hide Tips", "Itago ang Tip");
            Add(TextKey.TipsBody,
                "<b>KAPATIRAN BONDS</b>\n"
                + "MRK + ENG side by side — +20% accuracy, +1 range\n"
                + "EVA + AGU side by side — +15% attack, +10% defense\n\n"
                + "<b>TERRAIN</b> (your troops only)\n"
                + "Trench — +20% defense, +15% evasion\n"
                + "Tent — heals 5% HP per turn",
                "<b>BUKLOD NG KAPATIRAN</b>\n"
                + "MRK + ENG magkatabi — +20% asinta, +1 layon\n"
                + "EVA + AGU magkatabi — +15% atake, +10% depensa\n\n"
                + "<b>LUPAIN</b> (para sa iyong hukbo)\n"
                + "Trinsera — +20% depensa, +15% ilag\n"
                + "Tolda — nagpapagaling ng 5% HP bawat yugto");

            // ---------------------------------------------------------------- HUD — combat
            Add(TextKey.OrderOfBattle, "Order of Battle", "Hanay ng Labanan");
            Add(TextKey.TeamKatipunan, "Katipunan", "Katipunan");
            Add(TextKey.TeamSpanish, "Spanish", "Kastila");
            Add(TextKey.FieldReport, "Field Report", "Ulat ng Larangan");

            // ---------------------------------------------------------------- HUD — outcome
            Add(TextKey.OutcomeVictory, "Victory", "Tagumpay");
            Add(TextKey.OutcomeDefeat, "Defeat", "Pagkatalo");
            Add(TextKey.OutcomeDraw, "Draw", "Tabla");
            Add(TextKey.OutcomeTurns, "{0} AI turns", "{0} yugto");
            Add(TextKey.OutcomeSurvivors,
                "{0} Katipuneros standing   ·   {1} Spanish left",
                "{0} Katipunerong nakatayo   ·   {1} Kastilang natira");
            Add(TextKey.OutcomeSeed, "{0} events replayed from seed {1}", "{0} pangyayari mula sa binhi {1}");
            Add(TextKey.NewSeed, "New Seed", "Bagong Binhi");

            // ---------------------------------------------------------------- units
            Add(TextKey.UnitMarksman, "Caviteño Marksman", "Asintadong Caviteño");
            Add(TextKey.UnitEngineer, "Trench Engineer", "Inhinyero ng Trinsera");
            Add(TextKey.UnitEvangelista, "Gen. Evangelista", "Hen. Evangelista");
            Add(TextKey.UnitAguinaldo, "Emilio Aguinaldo", "Emilio Aguinaldo");
            Add(TextKey.UnitVanguard, "Katipunero Vanguard", "Katipunero sa Unahan");
            Add(TextKey.UnitSpanishRegular, "Spanish Regular {0}", "Sundalong Kastila {0}");

            // ---------------------------------------------------------------- field report
            Add(TextKey.LogAssaultBegan,
                "The Spanish column advances on the trench line.",
                "Sumusulong ang hanay ng Kastila sa trinsera.");
            Add(TextKey.LogRouted, "{0} is routed.", "Nagapi ang {0}.");
            Add(TextKey.LogCriticalHit, "{0} lands a critical hit on {1}.", "Malubhang tama ng {0} sa {1}.");
            Add(TextKey.LogResolved, "Battle resolved: {0} after {1} AI turns.", "Nalutas ang labanan: {0} matapos ang {1} yugto.");
            Add(TextKey.PopupDodge, "DODGE", "ILAG");
            Add(TextKey.PopupMiss, "MISS", "SABLAY");

            // ---------------------------------------------------------------- tutorial
            Add(TextKey.TutNext, "Next", "Susunod");
            Add(TextKey.TutSkip, "Skip Tutorial (Esc)", "Laktawan (Esc)");
            Add(TextKey.TutDone, "Done", "Tapos");
            Add(TextKey.TutStep, "Step {0} of {1}", "Hakbang {0} ng {1}");

            Add(TextKey.TutWelcomeTitle, "Binakayan, 1896", "Binakayan, 1896");
            Add(TextKey.TutWelcomeBody,
                "A Spanish column is marching on Evangelista's trench line. Place your Katipuneros, then watch them hold it.\n\n"
                + "Pick a language. You can switch anytime from the top bar.",
                "Sumusulong ang hanay ng Kastila sa trinsera ni Evangelista. Ipuwesto ang iyong mga Katipunero, at saksihan ang kanilang pagtatanggol.\n\n"
                + "Pumili ng wika. Maaari itong palitan anumang oras sa itaas.");

            Add(TextKey.TutPickTitle, "Pick a Unit", "Pumili ng Yunit");
            Add(TextKey.TutPickBody,
                "This is your roster. Click the <b>Caviteño Marksman</b> to select it.",
                "Ito ang iyong hanay. I-click ang <b>Asintadong Caviteño</b> upang piliin.");

            Add(TextKey.TutPlaceTitle, "Place It on the Line", "Ipuwesto sa Linya");
            Add(TextKey.TutPlaceBody,
                "Click any lit tile on the <b>trench</b> or a <b>tent</b>. Only lit tiles take units.\n"
                + "Right-click a placed unit to lift it off again.",
                "I-click ang alinmang nakailaw na tile sa <b>trinsera</b> o <b>tolda</b>. Tanging nakailaw na tile ang tumatanggap ng yunit.\n"
                + "I-right-click ang nakapuwestong yunit upang alisin.");

            Add(TextKey.TutTerrainTitle, "Read the Ground", "Basahin ang Lupain");
            Add(TextKey.TutTerrainBody,
                "<b>Trench</b>  +20% defense, +15% evasion\n"
                + "<b>Tent</b>  heals 5% HP each turn\n"
                + "<b>Shallows</b>  -15% movement, -10% defense\n"
                + "<b>Bamboo</b>  nobody passes\n"
                + "Trench and tent only help your troops.\n\n"
                + "Mouse wheel zooms. WASD or middle-drag pans.",
                "<b>Trinsera</b>  +20% depensa, +15% ilag\n"
                + "<b>Tolda</b>  nagpapagaling ng 5% HP bawat yugto\n"
                + "<b>Mababaw na dagat</b>  -15% kilos, -10% depensa\n"
                + "<b>Kawayan</b>  walang makalusot\n"
                + "Ang trinsera at tolda ay para sa iyong hukbo.\n\n"
                + "Mouse wheel upang mag-zoom. WASD o middle-drag upang igalaw ang tanaw.");

            Add(TextKey.TutBondsTitle, "Kapatiran Bonds", "Buklod ng Kapatiran");
            Add(TextKey.TutBondsBody,
                "Brothers-in-arms fight harder side by side (not diagonal):\n"
                + "<b>MRK + ENG</b>  +20% accuracy, +1 range\n"
                + "<b>EVA + AGU</b>  +15% attack, +10% defense",
                "Higit na lumalaban ang magkapatid sa armas kapag magkatabi (hindi pahilis):\n"
                + "<b>MRK + ENG</b>  +20% asinta, +1 layon\n"
                + "<b>EVA + AGU</b>  +15% atake, +10% depensa");

            Add(TextKey.TutAutoTitle, "Fill the Line", "Punan ang Linya");
            Add(TextKey.TutAutoBody,
                "Click <b>Auto-deploy</b> to place everyone still in reserve. Units you already placed stay put.",
                "I-click ang <b>Kusang Ipuwesto</b> upang ipuwesto ang natitirang yunit. Hindi gagalaw ang mga yunit na iyong ipinuwesto.");

            Add(TextKey.TutAssaultTitle, "Begin the Assault", "Simulan ang Salakay");
            Add(TextKey.TutAssaultBody,
                "Click <b>Begin Assault</b> or press Space. From here the battle fights itself — your deployment decides it.",
                "I-click ang <b>Simulan ang Salakay</b> o pindutin ang Space. Mula rito, kusang naglalaban ang mga yunit — ang iyong pagpupuwesto ang magpapasya.");

            Add(TextKey.TutWatchTitle, "Watch the Battle", "Saksihan ang Labanan");
            Add(TextKey.TutWatchBody,
                "Change the pace with the speed buttons or keys 1, 2, 3. <b>Skip</b> jumps to the result.\n"
                + "The <b>Field Report</b> narrates routs and critical hits.",
                "Baguhin ang bilis gamit ang mga pindutan o ang 1, 2, 3. Ang <b>Laktaw</b> ay tumatalon sa resulta.\n"
                + "Isinasalaysay ng <b>Ulat ng Larangan</b> ang mga nagapi at malubhang tama.");

            Add(TextKey.TutOutcomeTitle, "Victory or Defeat", "Tagumpay o Pagkatalo");
            Add(TextKey.TutOutcomeBody,
                "Rout every Spanish regular to win. Lose all your troops and you are defeated.\n"
                + "<b>New Seed</b> replays your formation with fresh luck. <b>Redeploy</b> lets you rearrange.\n"
                + "Open <b>?</b> anytime for the full rules.",
                "Gapiin ang lahat ng sundalong Kastila upang magwagi. Kung nagapi ang lahat ng iyong hukbo, natalo ka.\n"
                + "Ang <b>Bagong Binhi</b> ay muling naglalaro sa iyong pormasyon na may bagong suwerte. Ang <b>Ipuwesto Muli</b> ay upang magsaayos.\n"
                + "Buksan ang <b>?</b> anumang oras para sa buong patakaran.");

            // ---------------------------------------------------------------- how-to-play deck
            Add(TextKey.DeckTitle, "How to Play", "Paano Maglaro");
            Add(TextKey.DeckBack, "Back", "Bumalik");
            Add(TextKey.DeckNext, "Next", "Susunod");
            Add(TextKey.DeckClose, "Close", "Isara");
            Add(TextKey.DeckReplay, "Replay Tutorial", "Ulitin ang Tutorial");

            Add(TextKey.DeckBattleTitle, "The Battle of Binakayan", "Ang Labanan sa Binakayan");
            Add(TextKey.DeckBattleBody,
                "November 1896. Spanish regulars march on the Katipunan trench line at Binakayan–Dalahican.\n\n"
                + "<b>Victory</b>  rout every Spanish regular.\n"
                + "<b>Defeat</b>  all your Katipuneros fall.\n"
                + "<b>Draw</b>  both sides fall together, or 120 AI turns pass.",
                "Nobyembre 1896. Sumusulong ang mga sundalong Kastila sa trinsera ng Katipunan sa Binakayan–Dalahican.\n\n"
                + "<b>Tagumpay</b>  gapiin ang lahat ng sundalong Kastila.\n"
                + "<b>Pagkatalo</b>  nagapi ang lahat ng iyong Katipunero.\n"
                + "<b>Tabla</b>  sabay na nagapi ang dalawa, o lumipas ang 120 yugto.");

            Add(TextKey.DeckDeployTitle, "Deployment", "Pagpupuwesto");
            Add(TextKey.DeckDeployBody,
                "1.  Click a unit in the roster.\n"
                + "2.  Click a lit tile on the trench or a tent.\n"
                + "3.  Click or right-click a placed unit to lift it.\n\n"
                + "<b>Auto-deploy</b> fills the empty tiles and keeps your placements.\n"
                + "<b>Spanish Column - / +</b> sets how many regulars attack, from 1 to 14.",
                "1.  I-click ang yunit sa hanay.\n"
                + "2.  I-click ang nakailaw na tile sa trinsera o tolda.\n"
                + "3.  I-click o i-right-click ang nakapuwestong yunit upang alisin.\n\n"
                + "Pinupunan ng <b>Kusang Ipuwesto</b> ang mga bakanteng tile nang hindi ginagalaw ang iyong ipinuwesto.\n"
                + "Itinatakda ng <b>Hanay ng Kastila - / +</b> ang bilang ng umaatake, mula 1 hanggang 14.");

            Add(TextKey.DeckUnitsTitle, "Your Katipuneros", "Ang Iyong mga Katipunero");
            Add(TextKey.DeckUnitsBody,
                "Your troops are dug in and never move — where you place them is where they fight.\n\n"
                + "<b>MRK</b>  Marksman: range 2, fragile\n"
                + "<b>ENG</b>  Engineer: toughest defense\n"
                + "<b>EVA</b>  Evangelista: hardest hitter\n"
                + "<b>AGU</b>  Aguinaldo: strong all-rounder\n"
                + "<b>VAN</b>  Vanguard: most HP\n\n"
                + "HP health   ·   ATK attack   ·   DEF defense   ·   RNG range",
                "Nakabaon ang iyong hukbo at hindi gumagalaw — kung saan ipinuwesto, roon lumalaban.\n\n"
                + "<b>MRK</b>  Asintado: layon 2, marupok\n"
                + "<b>ENG</b>  Inhinyero: pinakamatibay na depensa\n"
                + "<b>EVA</b>  Evangelista: pinakamalakas na atake\n"
                + "<b>AGU</b>  Aguinaldo: balanseng lakas\n"
                + "<b>VAN</b>  Unahan: pinakamataas na HP\n\n"
                + "HP buhay   ·   ATK atake   ·   DEF depensa   ·   RNG layon");

            Add(TextKey.DeckMoreUnitsTitle, "Medics and Infantry", "Mga Mediko at Impanteriya");
            Add(TextKey.DeckMoreUnitsBody,
                "<b>MED</b>  Field Medic: heals the most wounded ally within 2 tiles for 14 HP\n"
                + "        HP 100   ·   ATK 8   ·   DEF 6   ·   RNG 2\n\n"
                + "<b>MGD</b>  Magdalo Infantry: reliable all-rounder\n"
                + "        HP 110   ·   ATK 12   ·   DEF 6   ·   RNG 1\n\n"
                + "<b>MGW</b>  Magdiwang Infantry: a little more attack, a little less defense\n"
                + "        HP 105   ·   ATK 13   ·   DEF 5   ·   RNG 1\n\n"
                + "The medic heals only an ally below three quarters of its HP. Otherwise it fights.",
                "<b>MED</b>  Mediko: pinagagaling ng 14 HP ang pinakasugatang kakampi sa loob ng 2 tile\n"
                + "        HP 100   ·   ATK 8   ·   DEF 6   ·   RNG 2\n\n"
                + "<b>MGD</b>  Magdalo: maaasahan sa lahat ng bagay\n"
                + "        HP 110   ·   ATK 12   ·   DEF 6   ·   RNG 1\n\n"
                + "<b>MGW</b>  Magdiwang: bahagyang mas malakas umatake, bahagyang mas mahina ang depensa\n"
                + "        HP 105   ·   ATK 13   ·   DEF 5   ·   RNG 1\n\n"
                + "Nagpapagaling lamang ang mediko kapag bumaba sa tatlong-kapat ng HP ang kakampi. Kung hindi, lumalaban ito.");

            Add(TextKey.DeckTerrainTitle, "Terrain", "Lupain");
            Add(TextKey.DeckTerrainBody,
                "<b>Trench</b>  +20% defense, +15% evasion\n"
                + "<b>Tent</b>  heals 5% of max HP at the start of each turn\n"
                + "<b>Coastal Shallows</b>  -15% movement, -10% defense\n"
                + "<b>Bamboo Barricade</b>  impassable for everyone\n\n"
                + "Trench and tent bonuses only help your troops. The shallows slow the Spanish advance.",
                "<b>Trinsera</b>  +20% depensa, +15% ilag\n"
                + "<b>Tolda</b>  nagpapagaling ng 5% ng HP sa simula ng bawat yugto\n"
                + "<b>Mababaw na Dagat</b>  -15% kilos, -10% depensa\n"
                + "<b>Barikadang Kawayan</b>  walang makalusot\n\n"
                + "Ang bonus ng trinsera at tolda ay para sa iyong hukbo. Pinababagal ng mababaw na dagat ang Kastila.");

            Add(TextKey.TerrainTrench, "Trench", "Trinsera");
            Add(TextKey.TerrainTent, "Tent", "Tolda");
            Add(TextKey.TerrainShallows, "Shallows", "Mababaw");
            Add(TextKey.TerrainBamboo, "Bamboo", "Kawayan");

            Add(TextKey.DeckBondsTitle, "Kapatiran Bonds", "Buklod ng Kapatiran");
            Add(TextKey.DeckBondsBody,
                "Bonded pairs standing side by side — up, down, left or right, never diagonal — both gain:\n\n"
                + "<b>MRK + ENG</b>  +20% accuracy, +1 attack range\n"
                + "<b>EVA + AGU</b>  +15% attack, +10% defense\n"
                + "<b>VAN + MED</b>  +25% healing received, +5% max HP\n"
                + "<b>MGD + MGW</b>  +15% critical chance, +10% evasion\n\n"
                + "A bond breaks when either partner falls.",
                "Ang magkapares na magkatabi — itaas, ibaba, kaliwa o kanan, hindi pahilis — ay kapwa tumatanggap ng:\n\n"
                + "<b>MRK + ENG</b>  +20% asinta, +1 layon\n"
                + "<b>EVA + AGU</b>  +15% atake, +10% depensa\n"
                + "<b>VAN + MED</b>  +25% natatanggap na lunas, +5% pinakamataas na HP\n"
                + "<b>MGD + MGW</b>  +15% tsansa ng malubhang tama, +10% ilag\n\n"
                + "Napuputol ang buklod kapag nagapi ang isa.");

            Add(TextKey.DeckCombatTitle, "Combat", "Labanan");
            Add(TextKey.DeckCombatBody,
                "Each AI turn, every living unit acts once: it attacks the nearest enemy in range, or steps toward it. Your troops act first.\n\n"
                + "Attacks can MISS or be DODGED. Critical hits deal double damage.\n\n"
                + "The same <b>seed</b> always replays the same battle. <b>New Seed</b> tries your formation with fresh luck.",
                "Sa bawat yugto, kumikilos nang minsan ang bawat buhay na yunit: umaatake sa pinakamalapit na kaaway na nasa layon, o humahakbang patungo rito. Unang kumikilos ang iyong hukbo.\n\n"
                + "Maaaring SABLAY o ILAGan ang atake. Dobleng pinsala ang malubhang tama.\n\n"
                + "Ang parehong <b>binhi</b> ay laging nagbubunga ng parehong labanan. Sinusubok ng <b>Bagong Binhi</b> ang iyong pormasyon na may bagong suwerte.");

            Add(TextKey.DeckControlsTitle, "Controls", "Kontrol");
            Add(TextKey.DeckControlsBody,
                "<b>Left click</b>  select, place, lift\n"
                + "<b>Right click</b>  lift a unit, deselect, close panels\n"
                + "<b>Mouse wheel</b>  zoom\n"
                + "<b>WASD / arrows / middle-drag</b>  pan\n"
                + "<b>Space</b>  begin assault\n"
                + "<b>1 / 2 / 3</b>  speed 0.5x / 1x / 3x\n"
                + "<b>Esc</b>  close this, skip the tutorial or replay, or pause\n"
                + "<b>P</b>  pause menu\n"
                + "<b>M</b>  show or hide the map\n"
                + "<b>EN / FIL</b>  switch language",
                "<b>Left click</b>  pumili, ipuwesto, alisin\n"
                + "<b>Right click</b>  alisin ang yunit o pinili, isara ang panel\n"
                + "<b>Mouse wheel</b>  zoom\n"
                + "<b>WASD / arrow / middle-drag</b>  igalaw ang tanaw\n"
                + "<b>Space</b>  simulan ang salakay\n"
                + "<b>1 / 2 / 3</b>  bilis 0.5x / 1x / 3x\n"
                + "<b>Esc</b>  isara, laktawan ang tutorial o replay, o ihinto\n"
                + "<b>P</b>  menu ng paghinto\n"
                + "<b>M</b>  ipakita o itago ang mapa\n"
                + "<b>EN / FIL</b>  palitan ang wika");

            // ---------------------------------------------------------------- shared buttons
            Add(TextKey.CommonConfirm, "Confirm", "Ituloy");
            Add(TextKey.CommonCancel, "Cancel", "Kanselahin");
            Add(TextKey.CommonClose, "Close", "Isara");
            Add(TextKey.CommonBack, "Back", "Bumalik");
            Add(TextKey.CommonOn, "On", "Bukas");
            Add(TextKey.CommonOff, "Off", "Sarado");

            // ---------------------------------------------------------------- main menu
            Add(TextKey.MenuTagline, "Cavite, November 1896", "Cavite, Nobyembre 1896");
            Add(TextKey.MenuContinue, "Continue", "Magpatuloy");
            Add(TextKey.MenuNewCampaign, "New Campaign", "Bagong Kampanya");
            Add(TextKey.MenuSettings, "Settings", "Mga Setting");
            Add(TextKey.MenuQuit, "Quit", "Umalis");
            Add(TextKey.MenuSaveSummary, "{0}  ·  {1}  ·  {2}", "{0}  ·  {1}  ·  {2}");
            Add(TextKey.MenuOverwriteTitle, "Start a new campaign?", "Magsimula ng bagong kampanya?");
            Add(TextKey.MenuOverwriteBody,
                "Your current campaign will be erased.",
                "Mabubura ang kasalukuyang kampanya.");
            Add(TextKey.MenuSaveRestored,
                "Your last save was damaged. The backup was loaded instead.",
                "Nasira ang huling save. Ang backup ang binuksan.");

            // ---------------------------------------------------------------- settings
            Add(TextKey.SetTitle, "Settings", "Mga Setting");
            Add(TextKey.SetAudio, "Audio", "Tunog");
            Add(TextKey.SetMusic, "Music", "Musika");
            Add(TextKey.SetSfx, "Sound effects", "Mga epekto");
            Add(TextKey.SetMute, "Mute all", "I-mute lahat");
            Add(TextKey.SetDisplay, "Display", "Screen");
            Add(TextKey.SetResolution, "Resolution", "Resolusyon");
            Add(TextKey.SetFullscreen, "Fullscreen", "Buong screen");
            Add(TextKey.SetLanguageText, "Language & Text", "Wika at Teksto");
            Add(TextKey.SetLanguage, "Language", "Wika");
            Add(TextKey.SetTextSpeed, "Text speed", "Bilis ng teksto");
            Add(TextKey.SetSaveTutorial, "Save & Tutorial", "Save at Tutorial");
            Add(TextKey.SetReplayTutorial, "Replay Tutorial", "Ulitin ang Tutorial");
            Add(TextKey.SetReplayDone,
                "The tutorial will play again in your next battle.",
                "Lalabas muli ang tutorial sa susunod na labanan.");
            Add(TextKey.SetDeleteSave, "Delete Save", "Burahin ang Save");
            Add(TextKey.SetDeleteTitle, "Delete your campaign?", "Burahin ang kampanya?");
            Add(TextKey.SetDeleteBody,
                "Every unit, weapon and cleared mission will be lost. This cannot be undone.",
                "Mawawala ang lahat ng yunit, armas at natapos na misyon. Hindi na ito maibabalik.");
            Add(TextKey.SetNoSave, "No saved campaign.", "Walang naka-save na kampanya.");
            Add(TextKey.LangEnglish, "English", "Ingles");
            Add(TextKey.LangFilipino, "Filipino", "Filipino");
            Add(TextKey.SpeedSlow, "Slow", "Mabagal");
            Add(TextKey.SpeedNormal, "Normal", "Katamtaman");
            Add(TextKey.SpeedFast, "Fast", "Mabilis");
            Add(TextKey.SpeedInstant, "Instant", "Agad");

            // ---------------------------------------------------------------- encampment chrome
            Add(TextKey.HubMenu, "Menu", "Menu");
            Add(TextKey.HubObjective, "Objective", "Layunin");
            Add(TextKey.HubRank, "Rank", "Ranggo");
            Add(TextKey.CurReales, "Reales", "Reales");
            Add(TextKey.CurRations, "Rations", "Rasyon");
            Add(TextKey.CurScrap, "Scrap", "Bakal");
            Add(TextKey.HubToTitle, "Main Menu", "Pangunahing Menu");
            Add(TextKey.HubSaved, "Saved", "Na-save");
            Add(TextKey.HubQuestDone, "Sub-quest complete: {0}", "Natapos ang gawain: {0}");
            Add(TextKey.HubRewardReales, "+{0} Reales", "+{0} Reales");
            Add(TextKey.HubComingSoon, "The {0} opens in the next update.", "Magbubukas ang {0} sa susunod na update.");
            Add(TextKey.HubNoPath, "No way through from here.", "Walang madaanan mula rito.");
            Add(TextKey.DlgNext, "Next", "Susunod");
            Add(TextKey.DlgSkip, "Skip", "Laktawan");
            Add(TextKey.DlgHint, "Click or press Space", "I-click o pindutin ang Space");
            Add(TextKey.ResStored, "In storage", "Nakaimbak");
            Add(TextKey.ResNext, "Next in {0}s", "Susunod sa {0}s");
            Add(TextKey.ResFull, "Full. Production has stopped", "Puno na. Tumigil ang ani");
            Add(TextKey.ResHarvest, "Harvest", "Umani");
            Add(TextKey.ResNothing, "Nothing to harvest yet.", "Wala pang maaani.");
            Add(TextKey.ResGot, "+{0} {1}", "+{0} {1}");
            Add(TextKey.ResRate, "Makes 1 every {0}s. Holds {1}.", "Gumagawa ng 1 bawat {0}s. Hanggang {1}.");
            Add(TextKey.ResBack, "Back to camp", "Bumalik sa kampo");
            Add(TextKey.ExLot, "{0} {1} buys {2} Reales", "{0} {1} = {2} Reales");
            Add(TextKey.ExHave, "You have {0}", "Mayroon kang {0}");
            Add(TextKey.ExSellOne, "Sell 1 lot", "Magbenta ng 1");
            Add(TextKey.ExSellAll, "Sell all", "Ibenta lahat");
            Add(TextKey.ExSold, "Sold for {0} Reales", "Naibenta sa {0} Reales");
            Add(TextKey.ExShort, "Not enough for a lot.", "Kulang para sa isang lote.");
            Add(TextKey.InvResources, "Resources", "Mga Rekurso");
            Add(TextKey.InvWeapons, "Weapons", "Mga Sandata");
            Add(TextKey.InvRealesUse, "Coin. Recruits and drills soldiers.", "Salapi. Pangalap at pagsasanay ng sundalo.");
            Add(TextKey.InvRationsUse, "Food. Every mission costs Rations to march.", "Pagkain. May bayad na Rasyon ang bawat misyon.");
            Add(TextKey.InvScrapUse, "Iron. Drills and weapon reforging use it.", "Bakal. Gamit sa pagsasanay at pagpapanday.");
            Add(TextKey.InvWhere, "From the {0}", "Mula sa {0}");
            Add(TextKey.InvHeldBy, "Held by {0}", "Hawak ni {0}");
            Add(TextKey.InvOnRack, "On the rack", "Nasa lalagyan");
            Add(TextKey.InvAttack, "+{0} attack", "+{0} atake");
            Add(TextKey.InvTier, "Tier {0}", "Antas {0}");
            Add(TextKey.InvGiveTo, "Give to", "Ibigay kay");
            Add(TextKey.InvReforge, "Reforge", "Ipanday");
            Add(TextKey.InvReforgeInto, "Reforge into {0}", "Ipanday bilang {0}");
            Add(TextKey.InvTopTier, "The finest of its kind.", "Pinakamahusay sa uri nito.");
            Add(TextKey.InvNone, "No weapons yet. Missions reward them.", "Wala pang sandata. Gantimpala ito ng mga misyon.");
            Add(TextKey.InvPick, "Choose a weapon.", "Pumili ng sandata.");
            Add(TextKey.InvEquipped, "{0} now carries the {1}.", "Hawak na ni {0} ang {1}.");
            Add(TextKey.InvReforged, "Reforged into {0}.", "Napanday bilang {0}.");
            Add(TextKey.InvCantAfford, "Not enough to pay for it.", "Kulang ang pambayad.");
            Add(TextKey.TrnLevel, "Level {0}", "Antas {0}");
            Add(TextKey.TrnXp, "{0} / {1} XP", "{0} / {1} XP");
            Add(TextKey.TrnTopLevel, "Top level reached", "Naabot na ang pinakamataas na antas");
            Add(TextKey.TrnHealth, "Health", "Buhay");
            Add(TextKey.TrnAttack, "Attack", "Atake");
            Add(TextKey.TrnDefense, "Defence", "Depensa");
            Add(TextKey.TrnNow, "Now", "Ngayon");
            Add(TextKey.TrnNext, "Next level", "Susunod na antas");
            Add(TextKey.TrnWeapon, "Carries: {0}", "Hawak: {0}");
            Add(TextKey.TrnNoWeapon, "Carries no weapon", "Walang hawak na sandata");
            Add(TextKey.TrnDrill, "Drill", "Sanayin");
            Add(TextKey.TrnDrillGives, "One drill gives +{0} XP", "Isang pagsasanay: +{0} XP");
            Add(TextKey.TrnDrilled, "{0} gained {1} XP.", "Nagkamit si {0} ng {1} XP.");
            Add(TextKey.TrnKeeperNote, "Pick a soldier, then drill. Battles give XP too.", "Pumili ng sundalo, saka sanayin. May XP din sa labanan.");
            Add(TextKey.TrnPage, "{0} / {1}", "{0} / {1}");
            Add(TextKey.PromoTitle, "Promoted", "Umangat ang Antas");
            Add(TextKey.PromoLevel, "Level {0}  ▸  Level {1}", "Antas {0}  ▸  Antas {1}");
            Add(TextKey.RecRates, "Published rates", "Nakapaskil na tsansa");
            Add(TextKey.RecPity, "A Hero is guaranteed within {0} recruits.", "Tiyak ang Bayani sa loob ng {0} pangangalap.");
            Add(TextKey.RecPityNext, "Your next recruit is a guaranteed Hero.", "Tiyak na Bayani ang susunod mong makakalap.");
            Add(TextKey.RecDuplicate, "A Hero you already have trains instead: +{0} XP.", "Kung nasa iyo na ang Bayani, sasanayin siya: +{0} XP.");
            Add(TextKey.RecOne, "Recruit 1", "Mangalap ng 1");
            Add(TextKey.RecTen, "Recruit {0}", "Mangalap ng {0}");
            Add(TextKey.RecCost, "{0} Reales", "{0} Reales");
            Add(TextKey.RecSave, "Saves {0} Reales", "Tipid na {0} Reales");
            Add(TextKey.RecIntro, "New recruits join your roster at level 1.", "Nagsisimula sa antas 1 ang bawat bagong kasapi.");
            Add(TextKey.RecResults, "New recruits", "Mga bagong kasapi");
            Add(TextKey.RecNew, "New", "Bago");
            Add(TextKey.RecDupXp, "Trained +{0} XP", "Sinanay +{0} XP");
            Add(TextKey.RecGuaranteed, "Guaranteed", "Tiyak");
            Add(TextKey.RecCantAfford, "Not enough Reales. Sell goods at the Exchange.", "Kulang ang Reales. Magbenta sa Palitan.");
            Add(TextKey.RecTapToReveal, "Click to reveal", "I-click upang ipakita");
            Add(TextKey.RecRevealAll, "Reveal all", "Ipakita lahat");
            Add(TextKey.RecRoster, "Your roster: {0} units", "Iyong hukbo: {0} yunit");

            // Campaign battles
            Add(TextKey.HudSquad, "Squad {0}/{1}", "Pangkat {0}/{1}");
            Add(TextKey.MissionReturn, "Return to Camp", "Bumalik sa Kampo");
            Add(TextKey.MissionRetreat, "Retreat", "Umatras");
            Add(TextKey.OutcomeHeld, "The line held for {0} turns.", "Nanindigan ang hanay nang {0} yugto.");
            Add(TextKey.MissionWonNote, "The rewards are waiting at camp.", "Naghihintay sa kampo ang gantimpala.");
            Add(TextKey.MissionLostNote, "Your soldiers still learned from the fight.", "May natutunan pa rin ang iyong mga kawal.");
            Add(TextKey.MissionRetreated, "You pulled back to camp. No Rations were spent.", "Umatras ka pabalik sa kampo. Walang Rasyong nagastos.");
            Add(TextKey.MissionLostToast, "The line broke. Train, re-arm and try again.", "Nabasag ang hanay. Magsanay, mag-armas at subukang muli.");
            Add(TextKey.RankUpTitle, "A New Rank", "Bagong Ranggo");
            Add(TextKey.RankUpStep, "Rank {0} of {1}", "Ranggo {0} sa {1}");
            Add(TextKey.CutNext, "Next", "Susunod");
            Add(TextKey.CutSkip, "Skip", "Laktawan");
            Add(TextKey.CutDone, "Begin", "Simulan");
            Add(TextKey.MapTitle, "Mission Tent", "Tolda ng Misyon");
            Add(TextKey.MapLevel, "Level {0} · {1}", "Antas {0} · {1}");
            Add(TextKey.MapEnemies, "Enemies: {0}", "Kaaway: {0}");
            Add(TextKey.MapSquad, "Squad: up to {0}", "Pangkat: hanggang {0}");
            Add(TextKey.MapWinRout, "Win: rout every enemy", "Panalo: itaboy ang lahat ng kaaway");
            Add(TextKey.MapWinHold, "Win: hold the line for {0} turns", "Panalo: manindigan nang {0} yugto");
            Add(TextKey.MapCost, "Cost: {0} Rations", "Gastos: {0} Rasyon");
            Add(TextKey.MapReward, "Reward: {0} Reales", "Gantimpala: {0} Reales");
            Add(TextKey.MapRewardLesson, "Unlocks a Library lesson", "Nagbubukas ng aralin sa Aklatan");
            Add(TextKey.MapDeploy, "Deploy", "Humayo");
            Add(TextKey.MapReplay, "Fight Again", "Lumaban Muli");
            Add(TextKey.MapLocked, "Clear the quest before this one to unlock it.", "Tapusin muna ang naunang misyon upang mabuksan ito.");
            Add(TextKey.MapCleared, "Cleared", "Natapos");
            Add(TextKey.MapInCamp, "Done in the encampment:", "Ginagawa sa kampo:");
            Add(TextKey.MapNoRations, "Not enough Rations. Harvest at the Farm.", "Kulang ang Rasyon. Umani sa Bukid.");
            Add(TextKey.MapComplete, "The campaign is won. Replay any battle.", "Napagtagumpayan ang kampanya. Ulitin ang anumang labanan.");
            Add(TextKey.QuizTitle, "Field Question", "Tanong sa Larangan");
            Add(TextKey.QuizProgress, "Question {0} of {1}", "Tanong {0} sa {1}");
            Add(TextKey.QuizRight, "Correct!", "Tama!");
            Add(TextKey.QuizWrong, "Not quite. The answer is {0}.", "Hindi tama. Ang sagot ay {0}.");
            Add(TextKey.QuizReales, "+{0} Reales for a right answer.", "+{0} Reales sa tamang sagot.");
            Add(TextKey.LibTitle, "Library", "Aklatan");
            Add(TextKey.LibLocked, "Clear quest {0} to unlock this lesson.", "Tapusin ang misyon {0} upang mabuksan ang araling ito.");
            Add(TextKey.LibTest, "Level {0} Test", "Pagsusulit sa Antas {0}");
            Add(TextKey.LibTestLocked, "Clear Level {0} first.", "Tapusin muna ang Antas {0}.");
            Add(TextKey.LibTestReady, "Ready to take.", "Handa nang sagutan.");
            Add(TextKey.LibBest, "Best: {0}/{1}", "Pinakamataas: {0}/{1}");
            Add(TextKey.LibPassed, "Passed", "Pumasa");
            Add(TextKey.LibResult, "You scored {0} of {1}.", "Nakakuha ka ng {0} sa {1}.");
            Add(TextKey.LibFirstPass, "Passed! +{0} Reales.", "Pumasa! +{0} Reales.");
            Add(TextKey.LibFail, "You need {0}% to pass. Read the lessons and try again.", "Kailangan ng {0}% upang pumasa. Basahin ang mga aralin at subukang muli.");
            Add(TextKey.HudMinimap, "Map  (M)", "Mapa  (M)");
            Add(TextKey.LibLockedShort, "Locked · clear quest {0}", "Sarado · tapusin ang misyon {0}");
            Add(TextKey.PauseTitle, "Paused", "Nakahinto");
            Add(TextKey.PauseResume, "Resume", "Ituloy");
            Add(TextKey.PauseHint, "Esc or P to resume", "Esc o P para ituloy");
            Add(TextKey.TrnStatHp, "HP", "BUHAY");
            Add(TextKey.TrnStatAtk, "ATK", "ATAKE");
            Add(TextKey.TrnStatDef, "DEF", "DEPENSA");
            Add(TextKey.TrnStatEva, "EVA", "IWAS");
            Add(TextKey.TrnStatAcc, "ACC", "ASINTA");
            Add(TextKey.TrnStatRng, "RNG", "ABOT");
            Add(TextKey.TrnStatCrit, "CRIT", "KRITIKAL");
            Add(TextKey.TrnStatMove, "MOVE", "GALAW");
            Add(TextKey.TrnStatHeal, "HEALING", "LUNAS");
            Add(TextKey.TrnBond, "Bond: {0}", "Kabuklod: {0}");
            Add(TextKey.TrnNoBond, "No Kapatiran bond", "Walang kabuklod sa Kapatiran");
        }

        /// <summary>The text for a key, falling back to English and then to the key's name.</summary>
        public static string Get(Language language, TextKey key)
        {
            int index = (int)key;
            if (index <= 0 || index >= (int)TextKey.Count)
            {
                return string.Empty;
            }

            string text = language == Language.Filipino ? Filipino[index] : English[index];
            if (string.IsNullOrEmpty(text))
            {
                text = English[index];
            }

            return string.IsNullOrEmpty(text) ? key.ToString() : text;
        }

        /// <summary>
        /// The raw authored text, with no fallback. For tests that check completeness.
        /// </summary>
        public static string GetAuthored(Language language, TextKey key)
        {
            int index = (int)key;
            if (index <= 0 || index >= (int)TextKey.Count)
            {
                return null;
            }

            return language == Language.Filipino ? Filipino[index] : English[index];
        }

        private static void Add(TextKey key, string english, string filipino)
        {
            English[(int)key] = english;
            Filipino[(int)key] = filipino;
        }
    }
}
