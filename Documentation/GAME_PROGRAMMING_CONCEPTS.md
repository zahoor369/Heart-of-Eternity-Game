# Game Programming concepts: where each topic appears

Paths are relative to `Unity_Project/Assets/_Project/`. "Tested" means the automated playtest in `TESTING_REPORT.md` exercised it in the built game. Items marked **Not done** are listed so nothing is claimed that does not exist.

## UI
| Topic | Where | Notes |
|---|---|---|
| Anchors, canvas scaler | `Scripts/Editor/UIKit.cs`, `GameBuilder.TempleUI.cs` | Scale With Screen Size (1920x1080 reference); panels anchored to corners |
| Buttons, sliders, dropdown, toggle, input field | `UI/MainMenuController.cs`, `UI/SettingsPanel.cs` | Tested: name field, volume slider |
| Radial fill, filled bars | `UI/HUDController.cs` | Echo Lens cooldown ring, vitality and escape bars |
| Masks and runtime textures | `UI/MapController.cs` | Minimap uses a Mask; the map is a runtime texture that is revealed tile by tile |
| Loading screen | `UI/LoadingScreen.cs` | Async scene loading with a progress bar and tips |

## Cameras
| Topic | Where | Notes |
|---|---|---|
| Cinemachine Orbital Follow, Hard Look At, Deoccluder | `Scripts/Editor/GameBuilder.Core.cs` (`BuildVirtualCamera`), `Camera/ThirdPersonCameraController.cs` | Wall avoidance in 4 m corridors |
| Camera shake | `ThirdPersonCameraController.Shake` | Noise offset added then removed, so no drift |

## Player and animation
| Topic | Where | Notes |
|---|---|---|
| Character controller, camera-relative movement | `Player/PlayerController.cs` | Tested: move, sprint, jump |
| Humanoid retargeting, blend tree | `Scripts/Editor/AssetKit.Hero.cs` (`BuildExplorerController`) | Idle / walk / jog / sprint by the Speed parameter |
| Procedural animation | `Temple/SentinelAI.cs` (`Animate`) | Sentinel limbs moved by code |

## Physics, AI and gameplay
| Topic | Where | Notes |
|---|---|---|
| NavMesh agents | `Temple/SentinelAI.cs`, `GameBuilder.Scene.cs` (`AddNavMesh`) | Baked from the generated geometry. Tested: every sentinel on the NavMesh |
| AI state machine, sight (line cast), hearing | `Temple/SentinelAI.cs` | Patrol, Alert, Chase, Attack, Search, Stunned. Tested: stun and attack |
| Triggers and overlap tests | `Temple/ZoneTrigger.cs`, `Temple/SpikeTrap.cs`, `Interactions/PlayerInteractor.cs` | Tested |
| Moving hazards | `Temple/SwingingBlade.cs`, `FlameVent.cs`, `RollingBoulder.cs` | Tested |
| Events and delegates | `Managers/GameManager.cs`, `Core/Health.cs` | Score, items, objective and state events |
| Interfaces | `Interactions/IInteractable.cs` | Doors, pedestals, secret walls and the Heart share one interface |

## Procedural generation
| Topic | Where | Notes |
|---|---|---|
| Seeded maze (recursive back-tracker), braiding, room reservation, path finding | `Temple/TempleLayout.cs` | Same plan used by the builder, the map and the tests |
| Mesh generation | `Scripts/Editor/GameBuilder.Structure.cs`, `GameBuilder.Meshes.cs` | Walls, floors, trims, spike fields, rings and crystals |

## Rendering and audio
| Topic | Where | Notes |
|---|---|---|
| URP, Forward+, post-processing volume | `Scripts/Editor/GraphicsSetup.cs` | Bloom, ACES, colour grade, vignette, grain |
| PBR materials, emissive, property blocks, particles | `Scripts/Editor/GameBuilder.Materials.cs`, `GameBuilder.Props.cs` | Doors, runes, traps change colour per state |
| Audio manager, volume sliders | `Core/AudioManager.cs`, `Core/Sfx.cs`, `Core/GameSettings.cs` | Sounds loaded by name from `Resources/Audio` |

## Data and saving
| Topic | Where | Notes |
|---|---|---|
| PlayerPrefs + JSON | `Core/PlayerProfile.cs`, `Managers/ProgressSaver.cs` | Name, top-5 table, statistics, checkpoints. Tested: profile, table, Retry |
| Scoring | `Managers/GameManager.cs` | Constants at the top of the file |

## Not done
Gamepad support, an audio mixer with buses, a second level, State Driven Cameras, animation events for the sentinels.
