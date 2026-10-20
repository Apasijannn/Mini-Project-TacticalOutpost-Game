# Tactical Outpost — Industrial Environment

Scene: `Assets/Scenes/TacticalOutpost_Solo.unity`  
Target: one player, industrial zombie defense. This scene stores the industrial environment. The adapted player, combat, zombies, waves, boss, HUD and minimap are authored separately in `Assets/Scenes/Gameplay/TacticalOutpost_Game.unity`, which loads this environment at runtime.

## Run and controls

Open `Assets/Scenes/Gameplay/TacticalOutpost_Game.unity` and press Play. This workstation's Editor Play Mode start scene also points to that entry. The old walkthrough player is disabled only for the runtime gameplay session. Open this environment scene directly for map editing and the lighting preset window.

- `WASD`: move; mouse: look
- `Shift`: sprint; `Ctrl`: crouch; `Space`: jump
- `Esc`: release mouse; click: capture mouse
- LMB fire, RMB ADS, `R` reload, `1`/`2` or wheel switch weapons, `M` minimap

See the root `README.md` and `Docs/Integration/README.md` for the folder guide, adaptation details and current limitations. The layout notes below describe the original map authoring; gameplay now targets the large red-lit generator and uses separate spawn markers/navigation.

## Layout guide

The west road is the readable main assault lane and climbs 2 m to the command deck using the artist's HD road ramp. The north cargo lane reaches the deck by a 4 m gravel ramp; the south utility lane uses an 8 m ramp. A 24 × 18 m open courtyard remains available for a boss encounter. The three perimeter gates map to `Spawn_W_Main`, `Spawn_N_Flank`, and `Spawn_S_Utility`.

Ground datums: road carriageway 0 m; low yard/curb 0.18364 m; raised deck 2.18364 m. Artist prefab scales are preserved. Fence v2 repeats use the native 5.67248 m span. Road ramp rises across local X and is rotated to climb east.

Scene hierarchy is grouped under `TacticalOutpost`: `World`, `RoadsAndDecks`, `Buildings`, `Perimeter`, `Cargo`, `Utilities`, `Props`, `Nature`, `Lighting`, `Navigation`, `GameplayMarkers`, and `Player`.

## Nature sources and license

Poly Haven assets are CC0. Diffuse and normal 2K maps are in `Assets/TacticalOutpost/Textures`; the terrain uses physical repeat widths of 1.3 m (soil), 2.3 m (gravel), and 1.5 m (rock).

- [Brown Mud Dry](https://polyhaven.com/a/brown_mud_dry)
- [Gravel Floor](https://polyhaven.com/a/gravel_floor)
- [Rock Ground](https://polyhaven.com/a/rock_ground)
- [Namaqualand Boulders 01](https://polyhaven.com/a/namaqualand_boulders_01)
- [Poly Haven CC0 license](https://polyhaven.com/license)

The 34 exterior rocks use both low-poly variants from the Namaqualand Boulders FBX; the imported model and its diffuse/normal maps are kept under `Assets/TacticalOutpost/Models` and `Textures`.

## Known follow-up

The main pipe run uses three exact 4.584 m H-set spans. Terminal elbow/vertical-riser auxiliary socket fit still needs a close-up review; the spans are kept away from the tank meshes. The outside watch hangar has a flattened graded pad. NavMesh is baked on the TacticalOutpost hierarchy only; the default Unity agent radius/height are 0.5/2.0 m. The solo walkthrough is for desktop controls.

## Screenshots

- `Map_TopDown.png`: top-down terrain and layout
- `Scene_Overview.png` and `Scene_Overview_Alt.png`: two isometric views
- `Walkthrough_PlayerCamera.png`, `FPS_WestGate.png`, `FPS_BossCourt.png`, `FPS_NorthCargo.png`, and `FPS_SouthUtility.png`: five player-height views
