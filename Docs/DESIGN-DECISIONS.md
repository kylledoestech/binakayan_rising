# Binakayan Rising — Open Design Decisions

This document lists every gameplay value the capstone proposal leaves undefined, together with a
recommended default. It exists so the team can ratify these numbers deliberately instead of
discovering them scattered through the source code.

Nothing in this list is a criticism of the proposal. A proposal defines scope; a build needs
constants. These are the constants.

## How the codebase treats undecided values

The implementation never hardcodes a gameplay number. Every value below is exposed either as a
serialized field on a ScriptableObject asset (tunable in the Unity Inspector) or as a field on a
plain-C# config object passed into the simulation. Anywhere a default had to be chosen to make the
code run, the source carries a `// TODO(design): not specified in capstone document` marker.

To find every open decision in the code:

```bash
grep -rn "TODO(design)" Assets/_Project/Scripts/
```

## What the proposal already decided

These are settled and are transcribed into the code as-is. They are listed here only so nobody
re-opens them by accident.

| Area | Source | Value |
| --- | --- | --- |
| Terrain modifiers | Table 2 | Trench +20% Defense, +15% Evasion; Coastal Shallows −15% Movement Speed, −10% Defense; Bamboo Barricade impassable; Encampment Tent +5% HP regeneration per AI turn; Standard Grid none |
| Kapatiran synergy | Table 3 | Four bonded pairs, ranks C / B / A, with the listed percentage effects. Note that Caviteño Marksman + Trench Engineer at rank A grants a flat **+1 Attack Range**, not a percentage |
| Quiz rewards | Table 4 | +50 Reales per correct answer, plus one of: map-wide +10% HP heal, +10% attack buff for one turn, reset enemy AI positions, revive one fallen unit |
| Currencies | Appendix F | Reales (gacha), Rations (stage entry energy), Scrap Metal (synthesis) |
| Campaign | Appendix F | Three levels, ten sub-quests, named and ordered |
| Evaluation targets | Table 5 | 30–60 FPS in combat; 85%+ positive UI feedback; zero corrupted saves; 100% of core mechanics free of game-breaking bugs |
| Platform constraints | Scope and Limitations | Single-player, offline, local storage only. No multiplayer, PvP, cloud save, leaderboards, or real-money transactions |

## Decisions the team must make

### Priority 1 — blocks the combat vertical slice

**1. Damage formula.** The proposal names "damage algorithms" but gives no formula.

Recommended default, already implemented as `StandardDamageFormula` and fully swappable:

1. Evasion roll — if it succeeds, damage is zero and the event log records an evade.
2. Accuracy roll, using the attacker's Ranged Accuracy.
3. Critical hit roll, using the attacker's Critical Hit Chance.
4. Mitigation — `max(minimumDamage, attackDamage − defense)`, multiplied by the crit multiplier
   if step 3 succeeded.

The crit multiplier and the minimum damage floor are config fields, not constants. Ratify the
order of operations before balancing anything, because changing it invalidates every stat value
tuned under the old order.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

**2. Base unit statistics.** The proposal names eight stats and supplies no values for any of them:
MaxHP, AttackDamage, Defense, Evasion, RangedAccuracy, AttackRange, CriticalHitChance,
MovementSpeed.

Eight units need values: Gen. Edilberto Evangelista, Emilio Aguinaldo, Katipunero Vanguard, Field
Medic, Caviteño Marksman, Trench Engineer, Magdalo Infantry, Magdiwang Infantry. The Spanish
roster is not enumerated at all and needs to be defined from scratch.

Suggested approach: fix one unit as the baseline — the Magdalo Infantry is the natural choice —
give it round numbers, and express every other unit as a deviation from it. This makes balance
discussions tractable and makes the numbers defensible during the panel defense.

**3. Targeting rule.** The proposal says units "target enemies" and stops there. Three strategies
are implemented (nearest, lowest HP, highest threat) with nearest as the default. Ties break
deterministically by unit id so that battles stay reproducible.

Also undecided: how often a unit re-evaluates its target, and whether it has an aggro radius at
all or engages across the whole map.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

**4. Turn versus tick.** The proposal consistently says "AI turn" — terrain regeneration is "per AI
turn" and a quiz buff lasts "for 1 turn" — but auto-battlers in the stated lineage (Arknights,
Teamfight Tactics) run in real time.

The simulation is implemented as discrete turns, which matches the proposal's own wording and
makes the system testable. If the team wants real-time presentation, the recommended path is to
keep the turn-based simulation and have the presentation layer interpolate between turns, rather
than rewriting the simulation. The event log is designed for exactly this.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

**5. Grid dimensions and deployment zones.** No grid size, tile size, deployment zone shape, or
squad size cap appears anywhere in the proposal.

Suggested starting point for the vertical slice: a 12×12 grid with the player's deployment zone
occupying the rear three rows, and a squad cap of six units. These are guesses chosen to be easy
to change, not recommendations grounded in the document.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

### Priority 2 — blocks the economy and progression systems

**6. Modifier stacking.** Terrain gives +20% Defense and Kapatiran gives +10% Defense. Does the
unit end up at +30% or at +32%? The proposal does not say.

Additive is implemented as the default because it is easier to reason about and easier to explain
during the defense. Multiplicative is available as a policy switch.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

**7. Gacha rarity tiers and pull rates.** Entirely undefined — no tiers, no percentages, no pity
system, no pull cost in Reales, no single-versus-ten-pull distinction, no duplicate handling.

Because this is an educational project with an explicitly closed offline economy, keep it simple
and keep it honest. A three-tier system with published rates and a visible pity counter is easier
to defend academically than an opaque one, and it sidesteps the ethical questions a panel is
likely to raise about gacha mechanics in an educational product.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

**8. Kapatiran promotion thresholds.** The proposal says ranks are built by placing units adjacent
to each other but never says how many battles or turns it takes to go from C to B to A.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

**9. What "adjacent" means.** Four-way, eight-way, or a radius. The resolver takes this as an
injected parameter. Eight-way is the conventional choice for an isometric grid.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

**10. Whether Kapatiran ranks persist between battles.** The proposal contradicts itself here. The
Game Design Document calls them "permanent stat boosts"; Table 3 reads as a per-deployment
proximity bonus. These are different systems with different implications for progression pacing.
Resolve this one explicitly — it will be noticed.

*Status: resolved in the build (#19) — both readings kept, as one system: the **rank** is permanent
(saved, never drops) and the **bonus** of that rank applies only while the pair stands side by side,
exactly as Table 3 reads. See [As built](#as-built-sept-2026).*

**11. Rations economy.** Cap, regeneration rate, and cost per mission are all undefined.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

**12. Reales earn rate outside quizzes.** Only the +50 per correct quiz answer is specified. Mission
completion rewards are not.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

**13. Synthesis recipes.** The only stated example is a bolo upgrading to a captured Mauser rifle,
"significantly increasing base damage output." Scrap Metal costs, the full weapon tier list, and
the stat delta per weapon tier all need defining.
*Status: built; needs group sign-off — see [Weapons as built](#weapons-as-built-dd-13).*

**14. Farm and Mine generation rates.** Named as encampment facilities, never quantified.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

**15. Experience curve and level-up growth.** "Experience levels" and a Roster Training state are
named; no curve, no per-level stat growth, no level cap.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

### Priority 3 — content and polish

**16. Quiz trigger cadence.** "Mid-battle interval" is not defined numerically, and the number of
quizzes per mission is unstated.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

**17. Wrong-answer behavior.** Undefined. Consider whether a wrong answer costs anything at all —
in an educational game, a penalty-free retry loop often teaches better than a punishment.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

**18. Trivia bank size.** Table 4 is explicitly labeled a sample and contains four rows. The
implementation needs an id, a difficulty, and a category column that the sample schema lacks.
Decide the target bank size; roughly thirty questions is a reasonable target for ten sub-quests.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

**19. Enemy roster.** The Spanish forces are referred to only as "the Spanish AI." Unit types,
counts, per-mission composition, and stats all need to be created. Artillery is mentioned in the
narrative sections and is the obvious first enemy archetype.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

**20. Does Bamboo Barricade block player units?** Table 2 says it "blocks enemy pathfinding" and is
silent about player units. The implementation currently blocks both, which is the intuitive
reading, but the asymmetric reading would be a genuinely interesting mechanic — the Katipuneros
built the barricades, after all.

**21. Level 2 sub-quest mechanics.** "The Silent Sabotage" is tagged stealth/intelligence and
"Forging the Earthworks" is tagged defense/escort. Neither mechanic is described. Both are
substantial features that do not reuse the core auto-battler loop, so scope them early or cut them.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

**22. JSON versus SQLite split.** The proposal commits to both without saying which data lives
where. A workable division: SQLite for the trivia bank and the unit catalog, which are read-heavy
and query-shaped; JSON for the save file, which is written whole and read whole.

*Status: built; needs group sign-off — see [As built](#as-built-sept-2026).*

**23. Audio and settings scope.** BGM and SFX toggles, resolution options, and the historical
glossary tab are named without detail.

## Discrepancies between the proposal and the current project

| Item | Proposal states | Current reality |
| --- | --- | --- |
| Unity version | Unity Engine 6.4 | Project is on 6000.3.22f1 |
| Target platform | Windows 10/11 64-bit | Development is happening on Linux |
| Development tooling | Microsoft Visual Studio | Not installed on the development machine |
| Rendering | 2D sprites in a 3D isometric environment | Project was created from the Universal Render Pipeline **2D** template |
| Windows release | Windows 10/11 64-bit | `.github/workflows/release-windows.yml` builds StandaloneWindows64 with game-ci on every push to `main`, wraps it in an Inno Setup `Setup.exe` (`installer/BinakayanRising.iss`) and publishes a GitHub prerelease |
| Storage | Offline JSON/SQLite | JSON only, atomic write + backup + checksum (`Gameplay/Meta/SaveStore.cs`); no SQLite |
| Grid | 12×12, rear rows deploy | 14×9; deploy zone is the trench column plus the tents; squad cap 6 (3 on q07) |
| Kapatiran adjacency | 8-way recommended (#9) | 4-way, no diagonals (`KapatiranProximityRule.Orthogonal`) |
| Quiz rewards (Table 4) | Reales, heal, buff, reset, revive | All built (#43). Table 4 ties one effect to each sample question; the build lets the player pick any one of the four after a right answer |
| Spanish terrain (Table 2) | Bonuses not limited by side | Spanish get no trench/tent bonus (`SpanishReceivesTerrainBonuses = false`) |

None of these is fatal. The Unity version difference is a minor revision. The 2D template is
arguably the better fit for a sprite-based isometric game and the tilemap packages needed for it
are already installed. But the proposal text and the build should be reconciled before the final
defense, either by updating the document or by changing the project.

## Assumptions already baked into the vertical slice

The combat simulation could not be written without resolving a few ambiguities on the spot. Each
choice below is implemented, tested, and reversible, but the team should ratify or overturn them
explicitly rather than inherit them by default.

**Fractional movement carries across turns.** Table 2's −15% Movement Speed applied to a base speed
of 1.0 yields 0.85 cells per turn, which truncates to zero and would freeze a unit permanently. The
remainder now carries between turns, so the penalty acts as a slowdown rather than a hard stop. Six
turns at 0.85 produce five steps.

**A unit moves or attacks in a single activation, never both.** The proposal does not say.

**Mutual annihilation resolves to a Draw.** The proposal's win condition (all enemies routed) and
lose condition (player roster defeated) both hold simultaneously in that case and contradict each
other. Sequential activation means it cannot arise naturally, but the outcome is configurable.

**A turn cap exists.** The proposal's combat loop has only two exits, victory and defeat. Without a
cap, a battle where both sides are behind impassable terrain never terminates.

**Healing Received is treated as a ninth stat.** Table 3's Vanguard and Field Medic bond grants
"+15% Healing Received", which is not one of the eight unit stats the proposal names. It is modeled
as a bond-only modifier with a base value of 1.0 and is deliberately absent from the unit stat block.

**Rank progression lives outside the simulation.** The simulation receives an already-resolved set
of Kapatiran modifiers; deciding whether a pair currently sits at rank C, B, or A is a save-data
concern. This keeps the battle reproducible from a seed.

**Bonds require both units to be on the same team.** Configurable, but historically sensible.

**Bamboo Barricade passability has exactly one source of truth** — the grid's walkability check.
The terrain modifier table deliberately does not duplicate it, so the two can never disagree.

## As built (Sept 2026)

What the build actually does, checked against the code. Where the sections above only
recommended something, the build has now chosen; every row is **built; needs group sign-off**.
Rows marked ⚠ contradict a recommendation above or the proposal.

| DD | As built | Where |
| --- | --- | --- |
| 1 | Evade → accuracy → crit → `max(minimumDamage, attack − defense)`; crit ×2, floor 1 | `Core/Combat/StandardDamageFormula.cs`, `Gameplay/PlaytestScenario.cs` `Config()` |
| 3 | Nearest enemy, ties broken by lowest unit id; target re-picked every activation; no aggro radius (whole map) | `Core/Combat/TargetingStrategies.cs`, `BattleSimulator.ActivateUnits` |
| 5 ⚠ | 14×9 grid (not 12×12). Deploy zone is set by terrain, not rows: the trench column (x = 10, 7 cells) plus the tents (x = 11, 4 cells). Squad cap 6 per quest, 3 on q07 | `PlaytestScenario.CreateGrid()`, `Core/Content/Campaign.cs` `QuestBattle` |
| 6 | Additive percentages (+20% and +10% = +30%) | `PlaytestScenario.Config()` `StackingPolicy`, `Core/Combat/StatModifier.cs` |
| 7 | Common / Rare / Hero at 70 / 25 / 5; pity guarantees a Hero on the 10th pull without one; 100 Reales per pull, 900 for 10; duplicate Hero → 150 XP | `Core/Meta/MetaRules.cs`, `MetaGame.Roster.cs` |
| 8 | Kapatiran support (#19): +1 per battle fought (won or lost, not retreated) with both partners deployed side by side. Rank C at 1, B at 3, A at 5 — so rank A is reachable within the 7 campaign battles only by keeping a pair together in most of them. Rank C gives no bonus (Table 3: it opens the lore dialogue); B and A give Table 3's bonus, A replacing B. The tutorial battle (q02) still fights every pair at rank A, since it teaches what a bond does. A card after the battle shows each new rank | `Core/Meta/MetaRules.cs` `BondRankSupport`, `Core/Meta/MetaGame.Bonds.cs`, `UI/Shell/BondRankCard.cs` |
| 9 ⚠ | **4-way** adjacency (`KapatiranProximityRule.Orthogonal`), and no diagonal movement. This conflicts with #9's 8-way recommendation — **the group must pick one** | `Core/Combat/KapatiranResolver.cs`, `PlaytestScenario.Config()` |
| 9 (bonds) | Support is earned by the same adjacency the battle's bonus uses (`KapatiranResolver`'s default rule, currently 4-way), so switching #9 to 8-way changes both together | `MetaGame.Bonds.cs` |
| 10 | Rank permanent, saved per pair (`SaveData.bonds`, save version 3; older saves start every pair at no rank); bonus only while side by side | `Core/Meta/SaveData.cs`, `Core/Content/BondCatalog.cs` |
| 11, 12, 14 | Start 300 Reales / 20 Rations / 10 Scrap. Farm: 1 Ration per 20 s, holds 30. Mine: 1 Scrap per 40 s, holds 20. Exchange: 10 Rations → 15 Reales, 10 Scrap → 25 Reales, sold in one trade of 1 to all affordable lots (a −/+ stepper with Max and a live preview). Battles cost 0–12 Rations and pay 100–500 Reales | `Core/Meta/MetaRules.cs`, `Core/Content/Campaign.cs` |
| 13 | Nine weapons, named for what Katipuneros carried in 1896: five tier-1 blades and a spear, each with a small twist and each reforging into a Paltik for 50 Reales + 15 Scrap; then Paltik → Remington → Mauser; and the **Lantaka**, a swivel cannon only the Trench Engineer can hold, which cannot be reforged. Every starter holds its own weapon; a Balaraw waits on the rack for the Field Medic. Rewards: q03 Bolo, q05 Paltik, **q06 Lantaka**, q07 Remington, q09 Mauser. Old saves' `bolo` items stay valid (no migration). Table below | `Core/Content/WeaponCatalog.cs`, `MetaGame.Roster.cs` `StatsOf`/`TryEquip`, `MetaGame.cs` `NewGame`, `UI/Shell/InventoryScreen.cs` |
| 15 | Level cap 10; XP to next = 100 × level; +8% HP, +5% Attack, +4% Defense per level; 60 XP per win, 20 per loss; drill = 100 XP for 80 Reales + 5 Scrap | `Core/Meta/MetaRules.cs` |
| 16 | One quiz per battle, at `QuestBattle.QuizTurn` (turns 3–6); the q02 tutorial battle has none (`QuizTurn = 0`). The battle is still simulated in full first, but a right answer's command **re-fights** it: the same deployment, seed and bonds are run to the turn before the quiz, the command is queued, and the battle runs to its end. Nothing before the command draws a different die, so the new log matches the one already shown up to the quiz turn (checked at run time), and the replay carries on into the new ending — the quiz **can** change the outcome | `Core/Combat/TacticianCommand.cs` `RunWithCommand`, `Gameplay/BattlePlaytest.cs` `ApplyTacticianCommand` |
| 17 | Right answer: +50 Reales (coin and toast), then the player picks **one** Tactician's Command from four (Table 4): heal every ally 10% of Max HP; +10% Attack for all allies for 1 turn; send every enemy back to its starting cell (nearest free cell if taken); revive the ally who fell last at 50% of base Max HP on its deployment cell, or the nearest free deploy cell (greyed out when no one has fallen). The command lands at the start of the quiz turn. Wrong answer: nothing | `MetaGame.Campaign.cs` `RecordQuizAnswer`, `Core/Combat/BattleSimulator.Commands.cs`, `UI/Shell/TacticianCommandCard.cs`, `UI/Shell/CampaignQuizRewards.cs` (`IQuizRewardReceiver`) |
| 18 | 30 questions, 10 per level, each with a difficulty (Easy / Medium / Hard) and a category (Figures, Events, Places, Society, Tactics). Level tests: 5 questions, 60% to pass, 150 Reales on first pass | `Core/Content/Learning.cs`, `MetaRules.cs` |
| 19 | **Five Spanish types** (base stats HP / ATK / DEF / EVA / ACC / RNG / CRIT / MOVE). **Regular** 100 / 14 / 5 / 5% / 85% / 1 / 10% / 1. **Artillery (ART)** 70 / 24 / 2 / 0 / 70% / **4** / 5% / 0.5: a connecting hit also deals **50%** of its damage to every enemy orthogonally beside the target (never its own side), then the gun spends **1 turn reloading** (fires every other turn). **Cazador (CAZ)** 80 / 12 / 3 / **20%** / 80% / **2** / 10% / **2**. **Officer (OFF)** 130 / 13 / 8 / 5% / 85% / 1 / 10% / 1: every other Spanish unit within **2 tiles** gets **+10% attack** (auras from two officers stack, additively). **Marine (MAR)** 105 / 14 / 6 / 5% / 85% / 1 / 10% / 1: ignores the shallows' penalties and gets **+15% attack and defense** while in them. **Sortie** (new rule, so a rooted line is not helpless against ART and CAZ): a Katipunan unit hit from beyond its own reach charges that shooter at 1 tile per turn while no enemy is in its reach (`CombatConfig.SortieSpeed = 1`). **Compositions:** q02 3 REG (tutorial, unchanged); q05 3 REG + 2 MAR; q06 5 REG + 3 CAZ + 1 OFF + 1 ART; q07 2 REG + 1 CAZ + 1 OFF; q08 4 REG + 1 CAZ + 1 OFF + 2 ART; q09 3 REG + 4 MAR + 2 CAZ + 1 OFF; q10 6 REG + 2 MAR + 3 CAZ + 2 OFF + 1 ART. Guns form up in the rear rank, marines in the shore corner. Checked over 200 seeds per quest with a scripted squad at the level a player reaches by then: every battle wins 93–100%. ⚠ **Art needed:** ART, CAZ, OFF and MAR have no figures or portraits yet; they wear the regular's, tinted (grey, green, gold, blue). The supply cart is a tinted token | `Core/Content/UnitCatalog.cs`, `Core/Combat/UnitAbilities.cs`, `BattleSimulator` (`ApplySplash`, `ApplyAuras`, `SortieTargetFor`), `PlaytestScenario.SpanishForce`, `Tests/EditMode/Combat/UnitAbilityTests.cs` |
| 21 | **Two new win rules**, both in the simulator (`BattleObjective`), so the event log ends with the outcome the player got. **Escort (q06, #37):** a **Supply Cart** (160 HP, DEF 6, never acts) stands behind the trench at (12, 7). Losing it loses at once; still standing at the end of **turn 20**, or every Spaniard routed, wins. Under Escort the Spanish are raiders: they strike the cart whenever it is in reach, fight whatever else is in reach, and otherwise walk for the cart. Auto-deploy puts the squad in the cells nearest the cart. **Sabotage (q07, #38):** a squad of **3** must end a turn with any member on the powder magazine (★ on the board) at (0, 4), behind the Spanish line, within **30 turns**. The squad marches at 1 tile per turn (rooted units are brought up to it), attacks only what is already in its reach, and otherwise heads for the magazine; routing the guard does not win on its own. Losing the squad, or reaching the cap, loses. The Hold rule (survive N turns) is kept but no quest uses it | `Core/Combat/BattleObjective.cs`, `BattleSimulator.EvaluateEscort/EvaluateSabotage`, `Core/Content/Campaign.cs`, `PlaytestScenario.SupplyCart/ObjectiveFor`, `Tests/EditMode/Combat/BattleObjectiveTests.cs` |
| 22 ⚠ | **JSON only, no SQLite.** Trivia and units are C# data. Save written whole to a temp file, flushed, swapped in with `File.Replace` keeping a `.bak`; SHA-256 checksum; a bad file is renamed `.corrupt` and the backup loaded. `SaveData.cs:14` cites #22 for "written whole and read whole". The proposal promises SQLite, so the document must be amended | `Gameplay/Meta/SaveStore.cs`, `Docs/SAVE-RELIABILITY.md` |
| 23 | **Audio:** separate Music and SFX sliders (plus master) in Settings; three CC0 loops (menu, camp, battle) crossfade by screen, win/loss stings, coin and hit effects, all loaded from `Resources/` and credited in `Docs/CREDITS.md`. No resolution options beyond what Settings already had. **Glossary:** a Glossary tab in the Library, 35 EN/FIL terms (people, places, factions, weapons, Filipino words), sorted per language, 10 per page. ⚠ Glossary definitions, the Act 1–4 story scenes and the Nov 9–11 aftermath are **SME check pending**; Acts 2–3 are dramatized | `Core/Content/Glossary.cs`, `Core/Content/Cutscenes.cs`, `UI/Shell/LibraryPanel.cs`, `UI/Kit/MusicPlayer.cs`, `UI/Kit/UiSfx.cs` |
| Table 2 ⚠ | Spanish units get **no** trench or tent bonus (`SpanishReceivesTerrainBonuses = false`); penalties such as the shallows still apply to them | `PlaytestScenario.Config()`, `Tests/EditMode/Combat/SpanishTerrainBonusTests.cs` |
| Table 3 lore | The four lore dialogues, one per pair, open at rank C, play in the dialogue box, and can be heard again from **Lore** in the Training Grounds. ⚠ **SME check pending** — see below | `Core/Content/BondLore.cs`, `UI/Shell/BondLorePanel.cs` |
| Ranks | 8 player ranks, Kawal to Heneral, one per quest milestone: start, q02, q04, q05, q06, q07, q09, q10 | `Core/Content/PlayerRanks.cs` |

### Weapons as built (DD 13)

Bonuses add to the holder's stats; accuracy and crit stay within 0–100%. Names and notes are
**SME check pending**.

| Weapon | Tier | ATK | ACC | CRIT | RNG | Held by | Reforges into (cost) | Starts with |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Bolo | 1 | +2 | | | | anyone | Paltik (50 R + 15 Scrap) | Evangelista; q03 reward |
| Talibong | 1 | +2 | | +3% | | anyone | Paltik (50 R + 15 Scrap) | Katipunero Vanguard |
| Gulok | 1 | +3 | −3% | | | anyone | Paltik (50 R + 15 Scrap) | Trench Engineer |
| Sibat | 1 | +1 | +5% | | | anyone | Paltik (50 R + 15 Scrap) | Aguinaldo |
| Balaraw | 1 | +1 | | +6% | | anyone | Paltik (50 R + 15 Scrap) | on the rack (for the Field Medic) |
| Paltik | 2 | +4 | | | | anyone | Remington (120 R + 30 Scrap) | Caviteño Marksman; q05 reward |
| Remington Rifle | 3 | +6 | | | | anyone | Mauser (250 R + 60 Scrap) | q07 reward |
| Mauser Rifle | 4 | +9 | | | | anyone | — | q09 reward |
| Lantaka | 3 | +7 | −5% | | +1 | Trench Engineer only | — | q06 reward |

The q03 reward stays a Bolo: the farmers who answer the rally bring the blade they work with.
The Armory greys out every other soldier when the Lantaka is chosen and says why; `TryEquip`
refuses it, and a save that hands it to anyone else is put right on load (`SaveData.Repair`).

### Conflicts for the group

1. **Adjacency (#9):** built 4-way, doc recommends 8-way.
2. **Grid (#5):** 14×9 with a terrain-defined deploy zone, not 12×12 with the rear three rows.
3. **SQLite (#22):** the proposal commits to SQLite; the build uses none.
4. ~~**Table 4 quiz rewards:** only the +50 Reales exists; the four battle effects do not.~~ **Resolved (#43):** all four are built; the player picks one after a right answer.
5. **Table 2 for the Spanish:** the build withholds trench and tent bonuses from the Spanish; Table 2 does not say they are Katipunan-only.
6. ~~**Quiz cadence (#16):** one fixed-turn quiz per battle, none in the tutorial battle, and it cannot affect the result.~~ **Resolved (#43):** the command re-fights the battle from the quiz turn, so the result can change. Still one fixed-turn quiz per battle, none in the tutorial.

### Kapatiran lore — SME check pending

The four lore dialogues (`Core/Content/BondLore.cs`, #20) are drafts. The conversations are
invented; the history they lean on is kept to what the standard accounts agree on — Evangelista's
engineering studies at Ghent, Aguinaldo as capitán municipal of Kawit, the Magdalo (Kawit) and
Magdiwang (Noveleta) councils with Santiago Álvarez leading Magdiwang's forces, Binakayan and
Dalahican held against Blanco's offensive of November 1896, and the shortage of rifles. The
Vanguard, Field Medic, Marksman, Engineer and the two infantrymen are composites, not named
people. **The adviser or a subject-matter expert should check both the English and the Filipino
before the defense.**

## Recommended order of decisions

Settle Priority 1 first. Items 1 through 5 gate the vertical slice, and the vertical slice is what
proves the concept works. Priority 2 can be decided while the slice is being built. Priority 3 is
content work that scales with available time and is the natural place to cut scope if the schedule
tightens.

## Final decisions (Sept 24, 2026)

The group asked for the open questions to be settled from this document and the build. Each is now
**decided**; the GitHub issue is closed with a link here. To change one, reopen its issue.

| Issue | DD | Decision | Why |
| --- | --- | --- | --- |
| #1 | 2 | Magdalo Infantry is the baseline (110 HP, 12 ATK, 6 DEF, 5% EVA, 80% ACC, range 1, 8% CRIT). Every other unit deviates from it; the full table is below. Spanish types: see the DD 19 row in [As built](#as-built-sept-2026) | DD 2 suggests the Magdalo as the baseline; round numbers make the table defensible |
| #2 | 1 | Evade → accuracy → crit → `max(1, ATK − DEF)`, crit ×2, damage floor 1 | DD 1's recommended order, already built and tested; changing it would invalidate the tuned stats |
| #3 | 3 | Nearest enemy; ties go to the lowest unit id; target re-picked every activation; no aggro radius (the whole map engages) | Deterministic and reproducible from a seed, as DD 3 requires. On a 14×9 board an aggro radius only adds stalls |
| #4 | 4 | Turn-based simulation; the board replays each turn's event log with interpolated movement | DD 4's recommendation. It matches the proposal's own "per AI turn" wording and keeps the simulation testable |
| #5 | 5 | 14×9 grid. The deploy zone comes from the terrain (the trench column and the tents), not from rows. Squad cap 6, or 3 on q07 | DD 5 calls 12×12 "guesses ... not recommendations". The board art, the tutorial and every quest are built around the trench line |
| #6 | 6 | Additive: +20% and +10% make +30% | DD 6's default; easier to explain at the defense |
| #7 | 20 | A Bamboo Barricade blocks every unit on both sides. Walkability is its single source of truth | This is DD 20's "intuitive reading"; the proposal gives no asymmetric rule |
| #8 | 9 | 4-way adjacency: up, down, left, right, never diagonal | Movement is 4-way too, so a bond is a unit you could step to. The tutorial and How-to-Play already teach "never diagonal". The DD 9 note that 8-way is "conventional" is a remark, not a requirement |
| #11 | Assumptions | All ratified: fractional movement carries over; a unit moves or attacks, never both; a mutual wipe is a Draw; the turn cap ends in a Draw, which counts as a loss for rewards except in escort battles, where reaching the cap with the cart alive is the win; Healing Received is a bond-only ninth stat; bonds are same-team only; the Spanish get no trench or tent bonus | Each is already built and tested; none contradicts the proposal |

#9 and #10 (how Kapatiran ranks are earned, and whether the boost is permanent or per battle) are
settled by the Kapatiran progression work (#19, #20) and recorded in the DD 8, DD 9 (bonds), DD 10 and
Table 3 lore rows of [As built](#as-built-sept-2026): support +1 per battle side by side (4-way, per
#8 above), ranks C / B / A at 1 / 3 / 5, rank permanent and saved per pair, bonus only while adjacent.

### Katipunan base stats (#1)

| Unit | HP | ATK | DEF | EVA | ACC | RNG | CRIT |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Magdalo Infantry (baseline) | 110 | 12 | 6 | 5% | 80% | 1 | 8% |
| Magdiwang Infantry | 105 | 13 | 5 | 6% | 80% | 1 | 10% |
| Gen. Edilberto Evangelista | 140 | 16 | 8 | 5% | 90% | 1 | 10% |
| Emilio Aguinaldo | 130 | 15 | 7 | 5% | 90% | 1 | 15% |
| Katipunero Vanguard | 150 | 15 | 10 | 5% | 85% | 1 | 10% |
| Field Medic | 100 | 8 | 6 | 8% | 85% | 2 | 5% |
| Caviteño Marksman | 90 | 14 | 4 | 5% | 75% | 2 | 15% |
| Trench Engineer | 110 | 10 | 12 | 5% | 85% | 1 | 5% |

Katipunan movement is 0: they hold the line from where they deploy (the Sabotage mission is the
exception; see #38). Source: `Core/Content/UnitCatalog.cs`.
