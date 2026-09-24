# Proposal errata: text to change before the final defense (#56)

The manuscript and the build disagree in the places below. For each one, the build is correct
and the manuscript should be amended. Paste the replacement text into the manuscript, and tick
the row when it's done.

| # | Section | Manuscript says | Replace with |
| --- | --- | --- | --- |
| 1 | Tools / Software requirements | "Unity Engine 6.4" | "Unity 6 (6000.3 LTS) with the Universal Render Pipeline" |
| 2 | Tools / Software requirements | "Microsoft Visual Studio" | "A C# code editor (e.g. Visual Studio, Visual Studio Code or JetBrains Rider)". The build does not depend on any one IDE |
| 3 | Technical design / Rendering | "2D sprites in a 3D isometric environment" | "2D sprites on an isometric tile grid, rendered with the URP 2D renderer". The board is a 2D isometric projection, not a 3D scene |
| 4 | Data storage | "offline JSON/SQLite" | "Offline JSON. The save is written atomically with a backup copy and a SHA-256 checksum; the trivia bank and unit catalog are compiled into the game as C# data". See `Docs/SAVE-RELIABILITY.md` |
| 5 | Combat system | (grid not specified) | "Battles take place on a 14×9 isometric grid. The player deploys up to six units (three in *The Silent Sabotage*) on the trench line and the encampment tents" |
| 6 | Kapatiran System / Table 3 | "permanent evasion/accuracy stat boosts" | "A pair earns 1 support point for each battle it fights deployed side by side (4-way adjacency); ranks C, B and A are reached at 1, 3 and 5 points and are kept permanently. Rank C opens the pair's lore dialogue; B and A give the Table 3 bonus (A replacing B), which applies only while the pair stands side by side. The tutorial battle fights every pair at rank A". This matches #9, #10, #19 and #20 in `Docs/DESIGN-DECISIONS.md` (As built rows DD 8, 9 (bonds), 10 and Table 3 lore) |
| 7 | Target platform | "Windows 10/11 64-bit" | Unchanged. Add: "Developed on Linux; the Windows 64-bit installer is built automatically by CI (`.github/workflows/release-windows.yml`)" |
| 8 | Table 4 / Quiz | (cadence not specified) | "One question per battle, at a fixed turn of the replay (turns 3–6; none in the tutorial battle). A right answer gives +50 Reales and lets the player choose one of four Tactician's Commands (heal all allies 10%, +10% attack for 1 turn, reset enemy positions, revive the last fallen ally); the battle is re-simulated from the quiz turn with the command applied, so the outcome can change. A wrong answer gives nothing" |
| 9 | Enemy AI | "the Spanish AI" | "Five Spanish unit types (Regular, Cazador, Officer, Marine, Artillery); stats in `Docs/DESIGN-DECISIONS.md`" |
| 10 | Level 2 sub-quests | "stealth/intelligence", "defense/escort" | "*Forging the Earthworks*: keep the supply cart alive until turn 20. *The Silent Sabotage*: three units must reach the Spanish powder magazine" |

The decisions behind rows 5–10 are recorded, with reasons, under "Final decisions" and "As built"
in `Docs/DESIGN-DECISIONS.md`.
