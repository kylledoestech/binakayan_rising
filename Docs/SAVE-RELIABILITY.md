# Save Reliability

Table 5 target: **zero corrupted saves.** This page says how the save is protected, what the
automated tests prove, and how the group runs the manual force-quit test.

## How the save is written

`Assets/_Project/Scripts/Gameplay/Meta/SaveStore.cs`, one file: `campaign.json` in
`Application.persistentDataPath` (Windows: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\binakayan_rising\`, from ProjectSettings `companyName` / `productName`).

1. Serialize to `campaign.json.tmp`, flush to disk.
2. Swap it in with `File.Replace`, which keeps the previous save as `campaign.json.bak`.
3. The file carries a SHA-256 of its payload. On load, a file that will not parse or fails the
   checksum is renamed `campaign.json.corrupt` and the `.bak` is loaded instead.

Worst case after a crash: the last save or two are lost. The campaign is not.

## Automated tests

`Assets/_Project/Tests/EditMode/Save/SaveStoreTests.cs`. Each test uses its own temp directory.
They need Unity's `JsonUtility`, so they run in the editor's Test Runner (Window > General >
Test Runner > EditMode), **not** in `Tools/battle-sim/test.sh`.

| Test | Proves |
| --- | --- |
| `SaveThenLoadRoundTrips` | Save then load returns the same campaign; no `.tmp` left behind |
| `LoadWithNoFilesReturnsNull` | A fresh install loads nothing, without errors |
| `TruncatedMainFileLoadsFromBackup` | Main file cut in half → backup loaded, main set aside as `.corrupt` |
| `GarbageMainFileLoadsFromBackup` | Main file not JSON at all → backup loaded |
| `EditedPayloadFailsChecksumAndIsQuarantined` | A hand-edited value fails the checksum, is not accepted, and the backup loads |
| `EditedPayloadWithNoBackupIsRejected` | With no backup, a failed checksum loads nothing rather than bad data |
| `LeftoverTempFileIsIgnoredOnLoadAndReplacedOnSave` | A stale `.tmp` from a dead write is ignored on load and consumed by the next save |

What they do **not** cover: a real process kill or power cut mid-write, and the Windows file
system's behavior under one. That is the manual test below.

## Automated kill test (Sept 24, 2026)

`Tools/qa/save-stress.sh` runs the player with `-brSaveStress`, which rewrites the save every frame
through the real `SaveStore`. The script kills the process with **SIGKILL** at a random moment
(0.05–0.95 s after writing starts), relaunches it, and checks the save. That covers a kill in the
middle of the temp-file write, in the middle of `File.Replace`, and between the two.

| Rounds | Saves written | Loaded clean | Loaded from `.bak` | Corrupt | Counter went back |
| --- | --- | --- | --- | --- | --- |
| 100 | ~166,000 | 99 of 99 relaunches | 0 | **0** | 0 |

Linux player, ext4, Ryzen 7 6800H. After round 100 the folder held only `campaign.json` and
`campaign.json.bak`: no leftover `.tmp` and no `.corrupt`. To rerun:

```bash
Tools/qa/save-stress.sh 100    # prints "rounds 100, corrupt 0, ..."; exits non-zero on any corruption
```

The manual Windows run below is still worth doing once on the release build, because NTFS
`File.Replace` is a different code path from ext4 `rename`.

## Manual test: force-quit mid-save

Run on the Windows release build (the installer from the GitHub prerelease), not in the editor.

**Setup**

1. Install the build. Start a new campaign; clear q01 and q02 so the save has real content.
2. Open the save folder (above). Confirm `campaign.json` and `campaign.json.bak` exist.
3. Note the Reales total and cleared quests shown in the encampment.

**Each trial**

1. Do something that saves (harvest, recruit, finish a battle).
2. Kill the game at that moment: Task Manager > End task, or `taskkill /F /IM "Binakayan Rising.exe"`.
   Vary the timing: right as the result screen appears, mid-harvest, mid-recruit.
3. Relaunch and choose Continue.
4. Record in the table: did it load? Is progress the latest, or one step back? Which files are in
   the folder (`.tmp`, `.bak`, `.corrupt`)?

**Also run once each**

- Delete `campaign.json` only → game must load from `.bak`.
- Open `campaign.json` in Notepad, change one number, save → game must reject it and load `.bak`.
- Cut `campaign.json` to half its length → game must load `.bak`.
- Pull the power (laptop battery out, or a VM hard reset) during a save, if available.

**Pass:** 20 kill trials plus the four one-offs, and every relaunch loads a campaign that is the
latest or at most one save behind. Any failure to load, any crash on Continue, or any lost level
is a fail: keep the save folder and file an issue with it attached.

| # | Action killed during | Loaded? | Latest / one back | Files present | Notes |
| --- | --- | --- | --- | --- | --- |
| 1 | | | | | |
