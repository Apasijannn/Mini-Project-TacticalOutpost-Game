# Industrial gameplay adaptation

> Update runtime terbaru: [RuntimeImprovements.md](RuntimeImprovements.md). Angka dan status pemeriksaan di bawah merupakan catatan integrasi awal; coverage terkini: 9 dari 23 script inti dimodifikasi, plus Aux_MP vendor.


Source: `Apasijannn/Mini-Project-TacticalOutpost-Game`, commit `fe2f658b2dfccf9541b07e69fbdcbee7d1823cf1` (20 October 2026, 19:21:57 WIB). Work used the already audited separate clone; it did not pull or merge into this project's Git repository.

## Completed in order

1. Preserved the existing source ZIP. Moved eleven map authoring scripts to `Tools/SceneAuthoring` without changing their content. Moved the old draft and artist reference via `AssetDatabase.MoveAsset`, preserving their GUIDs. Existing environment vendor folders retain paths used by the map tools.
2. Imported 201 required gameplay assets/files plus their original `.meta` files. Paths are separated into owned gameplay and third-party gameplay folders. No existing file was overwritten. All 111 required LFS payloads had been fetched and SHA-256 verified during the audit. `ImportedAssets.json` records source paths, destinations, GUIDs and original content hashes.
3. Imported runtime support together because the source player, enemy, audio, minimap and HUD scripts reference one another. Excluded all source Editor builders, watchers, demo scenes and global project settings.
4. Instantiated the original player through Unity and applied all 32 serialized player scene overrides from the friend's actual scene. Saved a prefab variant at `Assets/TacticalOutpost/Gameplay/Prefabs/Adapted/Player_Industrial.prefab`. This retains the source scene's weapon grip/position/hand settings rather than rebuilding the hands. The gameplay scene places that player at the industrial map's PlayerStart and faces the power station.
5. Baked separate navigation into `Assets/TacticalOutpost/Gameplay/Navigation/IndustrialGameplay.asset`, using existing physical colliders within the industrial volume (center 2,5,0; size 160,30,145; default agent type; voxel 0.16 m). The environment's original navigation is untouched. Only the gameplay navigation is enabled during the game session, avoiding overlapping navigation surfaces.
6. Authored 21 enemy spawn markers: seven West, seven North, seven South. Positions use the existing map's approach markers and nearby sampled walkable ground. At authoring time each accepted marker had a complete navigation path to at least one generator attack point. Exact coordinates: `SpawnLayout.json`.
7. Bound the objective to the existing large generator with red floodlights (`076 • Generator_v1 (1)`, center approximately -5.34,4.67,-29.11). Its visual object, lights and transforms remain in the original map. The gameplay scene holds a matching trigger collider, 1000 HP (updated 21 October 2026), health bar, minimap blip and six ground attack points placed outside its renderer bounds. Coordinates: `GeneratorAttackPoints.json`. An initial set contained isolated navigation islands; it was replaced with six reachable points along the accessible front face. All 21 spawn points now have complete paths to all six attack points on the gameplay navigation mesh.
8. Connected the enemy catalog (four normal types and three boss variants), three waves, pickups, drop manager, audio and the source HUD. Wave totals remain 10, 14 and 18 normal zombies, plus one boss in the last wave. Spawns cycle through all three map lanes, one enemy every 0.45 seconds. No debug keyboard spawner component is attached.
9. Connected the modified vendor minimap, player and objective blips. Small view size 27 m; large view size 100 m centered at 2,0,0; camera height 40 m. M toggles the large map. Enemy and pickup blips remain configured in their source prefabs.
10. Added the gameplay scene and environment to Build Settings. The gameplay session loads the environment additively, keeps the environment active for its current lighting, and deactivates the walkthrough player only at runtime. Restart handlers reload their owning gameplay scene instead of the active environment scene. This reinitializes gameplay and loads the environment again.

## Adaptations to source code

- `IndustrialGameSession.cs`: additive environment loading and activation order; no environment prefab/scene mutation in Edit Mode.
- `OutpostMarkers.cs`: retained enums; split its nine MonoBehaviour classes into matching filenames under `Scripts/Environment/`. This makes newly authored markers persist as ordinary Unity script references, avoiding the source scene's embedded MonoScript workaround.
- `WaveDirector.cs`: uses all authored enemy lanes, preserves total wave counts, fails visibly if setup/spawning fails, and stops the wave when the player dies. The serialized `perSide` field name remains compatible; the total is still `perSide * 2`.
- `EnemyDebugSpawner.SpawnAt`: shared spawn helper now refuses a missing prefab, absent nearby NavMesh, wrong elevation or position outside PlayArea. It no longer instantiates a stranded enemy after a failed navigation sample.
- `FpsHud.cs` and `WaveDirector.cs`: restart the owning gameplay scene so additive lighting/environment selection cannot reload only the walkthrough map.
- `MinimapController.cs`: configurable 40 m camera height to clear industrial structures.

Enemy brain, IK, weapon mechanics, stats, animation controllers, pickup rules, and vendor radar patch retain the audited implementation. AI uses navigation and utility/state rules. It does not yet implement explicit climbing/jumping, cover navigation or a repair interaction. NavMesh roofs are not automatic jump links.

## Preservation and authoring checks

- Main industrial scene and asset gallery were clean before integration and remain clean in the Editor.
- The first post-adaptation comparison checked all 2,115 original source files, accounting for the deliberate moves: no files missing; only Build Settings and TagManager had changed at that point. The environment scenes and environment asset contents matched the pre-integration SHA-256 hashes. The legacy documentation was then updated to point to the gameplay entry.
- `PreservationCheck.json` is the final file comparison; `EnvironmentBefore.json` records the initial scene object transforms and active flags.
- The gameplay scene contains no missing MonoBehaviour scripts. The imported enemy and player assets were inspected through Unity for missing script components and required references.
- The Editor completed script compilation without errors. Pipeline log history contains prior command timeout entries; those are separate from the current Unity Console compile status.

## Not run yet

No Play Mode session, automated tests or build was run. The numerical connectivity and reference checks are authoring diagnostics, not proof of runtime combat, completed waves, hand visuals, minimap appearance or frame rate. In particular, boss clearance around containers, exact generator attack animations, perceived difficulty for a solo player, and the large-map framing need an actual gameplay pass before calling the integration finished.

## Evidence

- `PlayerSceneOverrides.json`: source scene's 32 player overrides.
- `ImportedAssets.json`: imported dependency paths/GUIDs/hashes.
- `SpawnLayout.json`: map spawn coordinates.
- `GeneratorAttackPoints.json`: objective attack positions.
- `PreservationCheck.json`: original project file comparison after adaptation.
- `EnvironmentBefore.json`: original scene transform snapshot.
- Full source audit and ZIP recovery instructions are outside this project in `C:/Users/Faiz/Documents/TacticalOutpostAudits/20261020_211802/`.

Additional authoring evidence: NavigationAuthoring.json records connectivity; AuthoringReferences.json records prefab script and required player/enemy reference inspection.

Perbandingan lengkap terhadap repo, komponen yang tidak diadaptasi beserta alasan, serta perubahan logic/config tersedia di [AdaptationDifferences.md](AdaptationDifferences.md). Daftar per-file: [RepoScriptCoverage.json](RepoScriptCoverage.json).
