# Testing report - Heart of Eternity

All results below come from running the **built game** (`Windows_Build/HeartOfEternity.exe`) with the command line argument `-autotest`. The test (`Scripts/Testing/AutoPlaytest.cs`) feeds scripted keyboard and mouse input through the game's own input class and teleports the explorer between the stages of the temple, so the real movement, trap, puzzle, AI, mission, score, save and interface code runs. It takes screenshots and writes a report. It does not replace testing by a human player, and the game has not been played by other people yet.

## Result of the final build
**62 of 62 checks passed.** The raw report is `AutoTest_Final_62checks.txt` and the log is `AutoTest_Final_player.log`.
Frame rate while playing: average 45 fps, 1% low 28 fps (3772 frames) on AMD Radeon(TM) Vega 8 Graphics

## How the build was verified over the iterations
| Build | Checks passed | What was fixed |
|---|---|---|
| First complete version | 54 of 62 | map texture size (fog of war), a wrong-rune shock that was too short-ranged, test thresholds |
| After the first fixes | 57 of 62 | sentinels killing the explorer during later checks; the Sun Door check stood too far from the door |
| Second run with the same fixes | 62 of 62 | |
| Final build (heart colours, hat, metals, menu backdrop, demo bot) | **62 of 62** | |

## The 62 checks
1. Main menu shows its buttons
2. Main menu has a player name field
3. The typed player name is saved
4. High Scores screen opens and shows the empty table
5. Settings panel opens with sliders
6. Master volume slider changes the saved setting
7. New Game loads the Game scene
8. The intro finishes and play begins
9. Player exists and is tagged Player
10. The temple has its doors, traps and collectibles
11. Every sentinel stands on the NavMesh
12. WASD move moves the explorer forward
13. Sprint is faster than walking
14. Jump lifts the explorer
15. Mouse look orbits the camera
16. The entrance door slams shut when the explorer enters the hall
17. The Echo Lens is ready
18. The Echo pulse reveals the hidden rune mural
19. The Echo Lens goes on cooldown
20. A spike tile damages the explorer when it strikes
21. Taking the Sun Key gives the key and points
22. The objective moves on to the Sun Door
23. A closed door shows a prompt
24. The Sun Door opens with the Sun Key
25. Entering the rune chamber advances the mission
26. There are four rune pedestals
27. A wrong rune shocks the explorer and resets the puzzle
28. The right order solves the puzzle and opens the gate
29. Solving the runes gives points and moves the mission on
30. A swinging blade cuts the explorer standing in its path
31. A flame vent burns the explorer in its jet
32. Taking the Moon Key gives the key
33. The one-way vault exit opens after the Moon Key
34. The objective moves on to the Moon Door
35. The Moon Door opens with the Moon Key
36. The objective moves on to the Heart
37. Taking the Heart starts the escape timer
38. Taking the Heart gives points
39. The entrance opens for the escape
40. The boulder starts rolling
41. Running out of time ends the run (the temple seals itself)
42. Reaching the entrance with the Heart completes the mission
43. Escaping adds time, escape and health bonuses
44. The finished run is saved on the high-score table under the player's name
45. A Sun relic is collected for score
46. The score shows on the HUD
47. Player death leads to the Game Over screen
48. A death is recorded on the profile table
49. Retry puts the explorer back at the checkpoint and skips the intro
50. Retry restores keys, relics and score
51. Collected relics stay collected after a Retry
52. The Echo pulse stuns a nearby sentinel
53. A sentinel strikes an explorer who stays close
54. A life orb restores health
55. The M key opens the temple map
56. Esc closes the map first (does not pause)
57. Esc pauses the game
58. Esc resumes the game
59. Exploring reveals the map
60. Back in the menu
61. The High Scores screen lists the saved runs
62. The profile statistics are updated

## Known gaps (not verified)
- Not tested by other human players; the controls and balance were judged only by running the scripted test and watching the gameplay video.
- Measured on one laptop with integrated graphics (AMD Radeon Vega 8). Other hardware was not tried.
- The boulder kill, the sentinel kill and the spike kill were not separately tested to death; damage from each of them was.
- Gamepad input does not exist.
- `Gameplay_Video.mp4` was recorded from a self-playing demo mode (`-demo`) in which the explorer cannot die; the automated test above runs without that mode.
