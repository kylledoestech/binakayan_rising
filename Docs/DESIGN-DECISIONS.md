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
grep -rn "TODO(design)" binakayan_rising/Assets/_Project/Scripts/
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

**4. Turn versus tick.** The proposal consistently says "AI turn" — terrain regeneration is "per AI
turn" and a quiz buff lasts "for 1 turn" — but auto-battlers in the stated lineage (Arknights,
Teamfight Tactics) run in real time.

The simulation is implemented as discrete turns, which matches the proposal's own wording and
makes the system testable. If the team wants real-time presentation, the recommended path is to
keep the turn-based simulation and have the presentation layer interpolate between turns, rather
than rewriting the simulation. The event log is designed for exactly this.

**5. Grid dimensions and deployment zones.** No grid size, tile size, deployment zone shape, or
squad size cap appears anywhere in the proposal.

Suggested starting point for the vertical slice: a 12×12 grid with the player's deployment zone
occupying the rear three rows, and a squad cap of six units. These are guesses chosen to be easy
to change, not recommendations grounded in the document.

### Priority 2 — blocks the economy and progression systems

**6. Modifier stacking.** Terrain gives +20% Defense and Kapatiran gives +10% Defense. Does the
unit end up at +30% or at +32%? The proposal does not say.

Additive is implemented as the default because it is easier to reason about and easier to explain
during the defense. Multiplicative is available as a policy switch.

**7. Gacha rarity tiers and pull rates.** Entirely undefined — no tiers, no percentages, no pity
system, no pull cost in Reales, no single-versus-ten-pull distinction, no duplicate handling.

Because this is an educational project with an explicitly closed offline economy, keep it simple
and keep it honest. A three-tier system with published rates and a visible pity counter is easier
to defend academically than an opaque one, and it sidesteps the ethical questions a panel is
likely to raise about gacha mechanics in an educational product.

**8. Kapatiran promotion thresholds.** The proposal says ranks are built by placing units adjacent
to each other but never says how many battles or turns it takes to go from C to B to A.

**9. What "adjacent" means.** Four-way, eight-way, or a radius. The resolver takes this as an
injected parameter. Eight-way is the conventional choice for an isometric grid.

**10. Whether Kapatiran ranks persist between battles.** The proposal contradicts itself here. The
Game Design Document calls them "permanent stat boosts"; Table 3 reads as a per-deployment
proximity bonus. These are different systems with different implications for progression pacing.
Resolve this one explicitly — it will be noticed.

**11. Rations economy.** Cap, regeneration rate, and cost per mission are all undefined.

**12. Reales earn rate outside quizzes.** Only the +50 per correct quiz answer is specified. Mission
completion rewards are not.

**13. Synthesis recipes.** The only stated example is a bolo upgrading to a captured Mauser rifle,
"significantly increasing base damage output." Scrap Metal costs, the full weapon tier list, and
the stat delta per weapon tier all need defining.

**14. Farm and Mine generation rates.** Named as encampment facilities, never quantified.

**15. Experience curve and level-up growth.** "Experience levels" and a Roster Training state are
named; no curve, no per-level stat growth, no level cap.

### Priority 3 — content and polish

**16. Quiz trigger cadence.** "Mid-battle interval" is not defined numerically, and the number of
quizzes per mission is unstated.

**17. Wrong-answer behavior.** Undefined. Consider whether a wrong answer costs anything at all —
in an educational game, a penalty-free retry loop often teaches better than a punishment.

**18. Trivia bank size.** Table 4 is explicitly labeled a sample and contains four rows. The
implementation needs an id, a difficulty, and a category column that the sample schema lacks.
Decide the target bank size; roughly thirty questions is a reasonable target for ten sub-quests.

**19. Enemy roster.** The Spanish forces are referred to only as "the Spanish AI." Unit types,
counts, per-mission composition, and stats all need to be created. Artillery is mentioned in the
narrative sections and is the obvious first enemy archetype.

**20. Does Bamboo Barricade block player units?** Table 2 says it "blocks enemy pathfinding" and is
silent about player units. The implementation currently blocks both, which is the intuitive
reading, but the asymmetric reading would be a genuinely interesting mechanic — the Katipuneros
built the barricades, after all.

**21. Level 2 sub-quest mechanics.** "The Silent Sabotage" is tagged stealth/intelligence and
"Forging the Earthworks" is tagged defense/escort. Neither mechanic is described. Both are
substantial features that do not reuse the core auto-battler loop, so scope them early or cut them.

**22. JSON versus SQLite split.** The proposal commits to both without saying which data lives
where. A workable division: SQLite for the trivia bank and the unit catalog, which are read-heavy
and query-shaped; JSON for the save file, which is written whole and read whole.

**23. Audio and settings scope.** BGM and SFX toggles, resolution options, and the historical
glossary tab are named without detail.

## Discrepancies between the proposal and the current project

| Item | Proposal states | Current reality |
| --- | --- | --- |
| Unity version | Unity Engine 6.4 | Project is on 6000.3.22f1 |
| Target platform | Windows 10/11 64-bit | Development is happening on Linux |
| Development tooling | Microsoft Visual Studio | Not installed on the development machine |
| Rendering | 2D sprites in a 3D isometric environment | Project was created from the Universal Render Pipeline **2D** template |

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

## Recommended order of decisions

Settle Priority 1 first. Items 1 through 5 gate the vertical slice, and the vertical slice is what
proves the concept works. Priority 2 can be decided while the slice is being built. Priority 3 is
content work that scales with available time and is the natural place to cut scope if the schedule
tightens.
