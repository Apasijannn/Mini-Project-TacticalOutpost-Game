# Tactical Outpost

## Open and run

Open `Assets/Scenes/Gameplay/TacticalOutpost_Game.unity`. This is the gameplay entry scene. It loads `Assets/Scenes/TacticalOutpost_Solo.unity` additively at runtime, uses that scene's lighting, and disables the old walkthrough player for that session. The industrial scene remains the source of the environment.

This workstation's Play Mode start scene points to the gameplay entry. The build scene list contains both scenes. Gameplay systems are intentionally inactive in Edit Mode: `Industrial Game Session` enables them after the environment has loaded.

Controls: WASD, mouse look, Shift sprint, Ctrl/C crouch, Space jump; LMB fire, RMB ADS, R reload, 1/2 or wheel switch weapons, Q previous weapon, M large minimap, Esc release cursor. Enter restarts after defeat/victory.

## Directory guide

| Path | Purpose |
|---|---|
| `Assets/Scenes/TacticalOutpost_Solo.unity` | Industrial environment; retain its asset GUIDs and layout |
| `Assets/Scenes/Gameplay/` | Gameplay entry scene, separate from environment authoring |
| `Assets/Scenes/Outpost_AssetGallery.unity` | Asset gallery for manual arrangement |
| `Assets/Scenes/References/` | Artist reference scene |
| `Assets/Scenes/Archive/` | Previous draft, retained with its original GUID |
| `Assets/TacticalOutpost/Gameplay/` | Adapted runtime scripts, configs, player/enemy prefabs, UI, pickups and navigation |
| `Assets/ThirdParty/Gameplay/` | Only the external assets required by the audited gameplay dependency graph |
| `Assets/TacticalOutpost/` (other folders) | Existing industrial terrain, materials, effects, models and lighting tools |
| Other existing asset pack folders | Industrial environment vendor assets; original paths retained because authoring tools use them |
| `Tools/SceneAuthoring/` | Existing map authoring scripts, moved from `AgentScripts/`; run explicitly through Unity CLI |
| `Tools/Integration/` | One-time selective import and staged adaptation tools; not compiled into the game |
| `Docs/Integration/` | Integration status, provenance, asset manifest, spawn coordinates and preservation evidence |

Do not run the friend's `OutpostBuilder` or watcher against this environment. They were not imported. They regenerate scenes/materials/settings. Keep `.meta` files alongside assets when synchronizing with the team.

## Recovery

The pre-integration source backup is outside the project:

`C:/Users/Faiz/Documents/TacticalOutpostAudits/20261020_211802/IndustrialProject_Backup.zip`

Restore instructions are in the same folder's `RESTORE.md`. Preserve that ZIP before attempting any later scene rebuild.

See [integration status](Docs/Integration/README.md) for exactly what was adapted and what remains unverified.
