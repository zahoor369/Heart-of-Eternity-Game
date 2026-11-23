# Heart of Eternity - The Lost Temple Escape

Third-person temple maze adventure made in Unity 6 for the Game Programming course, NED University of Engineering & Technology, Karachi.

**Author:** Muhammad Zahoor Ul Haq - Roll No. **CT-24148**

**Story in one line:** an explorer enters a forgotten jungle temple, the entrance door slams shut, and the only way out is through the maze: find the keys, wake the runes, take the Heart of Eternity and run back before the temple seals itself.

## Play it
Run `Windows_Build/HeartOfEternity.exe` (Windows 64-bit). No Unity Editor needed.

| Key | Action |
|---|---|
| W A S D | move |
| Mouse | look |
| Left Shift | sprint (sentinels can hear it) |
| Space | jump |
| E | use doors, rune pedestals, secret walls, the Heart |
| Q | Echo Lens: reveals hidden carvings, points to the goal, stuns sentinels |
| F | lantern |
| M | temple map (fills in as you explore) |
| Esc | pause |

## What is in the game
- A 31 x 31 tile temple generated from a fixed seed: a maze plus six special rooms, five doors, secret walls and relics
- Three puzzle types: key hunt, a hidden-order rune puzzle, a timed trap gallery
- Moving traps (spikes, swinging blades, flame vents), stone sentinels with NavMesh AI, a rolling boulder
- Score system, explorer name profile, saved top-5 high-score table, checkpoints and saved progress
- Win (escape with the Heart) and lose (death or the temple sealing) screens
- 62 automated playtest checks run inside the built game (`Documentation/TESTING_REPORT.md`)

## Open the project
Unity Hub -> Add -> select this repository folder (it is the Unity project) (Unity 6000.3.24f1). Open `Assets/_Project/Scenes/MainMenu.unity`.
The `NED2024` menu in the editor rebuilds the scenes from code (`Build Everything`) and makes the Windows build (`Build Windows Player`).

## Documents
`Documentation/`: `Design/` (mood board and storyboard), `Project_Report.pdf` / `.docx`, `Gameplay_Video.mp4`, `GAME_PROGRAMMING_CONCEPTS.md`, `TESTING_REPORT.md`, `CONTROLS.md`, `ASSET_CREDITS.md`.

## Where to change things (for the evaluation)
| Change | File |
|---|---|
| Score values, escape time, relic count | `Scripts/Managers/GameManager.cs` (constants at the top) |
| Trap damage and timing | `Scripts/Temple/SpikeTrap.cs`, `SwingingBlade.cs`, `FlameVent.cs` |
| Sentinel speed, sight, damage | `Scripts/Temple/SentinelAI.cs` |
| Rune order | `Scripts/Temple/RunePuzzle.cs` (`solution`) |
| Echo Lens cooldown and range | `Scripts/Temple/EchoLens.cs` |
| Temple layout and rooms | `Scripts/Temple/TempleLayout.cs` |
| Player speeds | `Scripts/Player/PlayerController.cs` |

