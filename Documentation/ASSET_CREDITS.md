# Asset credits - Heart of Eternity

Game design, programming and level design: **Muhammad Zahoor Ul Haq**, Roll No. **CT-24148**, NED University of Engineering & Technology, Karachi (Game Programming).

Everything below is free to use. The "Where it is used" column says where each asset appears so it can be checked in the project.

## 3D textures and models - Poly Haven (CC0, https://polyhaven.com)
| Asset | Where it is used |
|---|---|
| Texture `mossy_sandstone` | maze walls, heart chamber walls |
| Texture `old_sandstone_02` | hall walls, rune chamber walls, door slabs, trims |
| Texture `mossy_stone_wall` | walls of the gallery and vaults, sentinel bodies |
| Texture `mossy_brick_floor` | maze floors |
| Texture `marble_mosaic_tiles` | rune chamber and heart chamber floors |
| Texture `patterned_paving_02` | entrance hall floor |
| Texture `slate_floor_03` | ceilings |
| Texture `forest_ground_04` | jungle courtyard ground |
| Texture `roots` | jungle tree trunks |
| Texture `rock_pitted_mossy` | the rolling boulder |
| Model `gothic_statue` | statues in the entrance hall |
| Models `lion_head`, `bull_head` | animal heads on the hall wall |
| Model `marble_bust_01` | rune chamber |
| Model `stone_fire_pit` | braziers |
| Model `treasure_chest` | sun vault decoration |
| Models `fern_02`, `moss_01`, `rock_moss_set_01`, `boulder_01` | jungle and maze decoration |

Poly Haven assets are released under CC0 (public domain). The textures were converted (smoothness packed into an alpha channel, normal maps converted to PNG) with a Python script.

## Character and animation
- Explorer character: custom model made in Blender 4.5. Its skin, hair, eyebrow and eye textures come from the MPFB / MakeHuman asset pack bundled with the Blender add-on. The licence of those bundled textures was not re-checked.
- Animations: **Quaternius**, Universal Animation Library [Standard] and Universal Animation Library 2 [Standard] (CC0), https://opengameart.org/content/universal-animation-library . Retargeted to the character in Unity.

## Fonts (SIL Open Font License, Google Fonts)
- Cinzel (titles), Rajdhani (interface text and numbers).

## Created for this game
- All sound effects, the cave ambience and the game and menu music: synthesised with Python and NumPy (`Documentation/Report_Assets/make_audio.py`).
- Rune glyphs (sun, moon, serpent, eye), scan ring, chevron, glow textures and the interface icons: drawn with Python Pillow (`Documentation/Report_Assets/make_art.py`).
- The Hat, satchel, Echo Lens band, keys, relics, the Heart of Eternity, spike fields, blades, doors, pedestals and the sentinel guardians are built from primitive shapes and generated meshes in the level builder (`Assets/_Project/Scripts/Editor/`).

## Emblem
- The NED University emblem on the report cover is from the university website, https://www.neduet.edu.pk/ .

## Software
Unity 6 (6000.3.24f1) with Universal Render Pipeline, Cinemachine 3.1 and AI Navigation 2.0.
