# battle-sim

Runs the Binakayan Rising combat resolver headlessly, with the Unity Editor closed.

Everything compiles with the C# compiler and Mono runtime that ship inside the Unity
editor install, so there is nothing to install. This directory lives outside `Assets/`,
so Unity ignores it entirely.

## Play a battle

```bash
./run.sh
```

Options:

| Flag | Meaning |
| --- | --- |
| `--seed N` | Battle seed. The same seed always replays the same battle, exactly. |
| `--spanish N` | Size of the assaulting Spanish column, 1-14 (default 6). |
| `--turns N` | Turn cap before a draw is declared (default 120). |
| `--quiet` | Print only the final outcome. |
| `--log` | Dump the raw structured event log at the end. |

The scenario is a fixed vertical slice: five entrenched Katipunan units holding
Evangelista's trench line, with Kapatiran bonds active between the Caviteño Marksman and
the Trench Engineer (rank A, `+1 Attack Range`) and between Gen. Evangelista and Emilio
Aguinaldo (`+15% Attack Damage`, `+10% Defense`), against a Spanish column advancing
across open ground and the Dalahican tidal shallows.

Every stat value in `BattleDemo.cs` is a placeholder. The capstone document publishes no
base stat block, so nothing here is a balance proposal.

## Run the tests

```bash
./test.sh
```

This does two things: a strict `netstandard2.1` build of `BinakayanRising.Core` with
`-warnaserror+ -warn:4`, which proves the assembly has no `UnityEngine` dependency, and
then the full EditMode suite through a small reflection-based NUnit runner.

## If the toolchain is not found

The scripts search `~/Unity/Hub/Editor` for an editor install. Override with:

```bash
UNITY_VERSION=6000.3.22f1 ./run.sh
UNITY_ROOT=/path/to/editors ./run.sh
```

`test.sh` also needs `nunit.framework.dll`, which comes from `Library/PackageCache`.
Open the project in Unity once if that directory is empty.
