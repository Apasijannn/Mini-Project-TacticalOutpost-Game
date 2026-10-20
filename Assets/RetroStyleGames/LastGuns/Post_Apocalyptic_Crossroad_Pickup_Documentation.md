# Post-Apocalyptic Crossroad Pickup – Documentation

## Overview
This package includes a high-quality **post-apocalyptic pickup truck** designed for real-time applications such as games, cinematics, and simulations.

The vehicle is a heavily modified crew-cab pickup with a welded ram bar, roof rack, cargo cage, spare wheel and stowed gear. It supports multiple Unity render pipelines and ships with a rigged, drivable prefab for each of them.

---

## Render Pipeline Compatibility

This asset is fully compatible with:

- Built-in Render Pipeline (BRP)
- Universal Render Pipeline (URP)
- High Definition Render Pipeline (HDRP)

Each pipeline includes properly configured materials.

---

## Technical Details

- **Vertices:** 81,093
- **Triangles:** 86,413
- **Meshes:** 155
- **Materials:** 5 per render pipeline (Body, Hull, Equipment, Wheels, Details)
- **Textures:** 15 (Base Color, Normal, MAS)
- **Texture Resolution:** 4096 × 4096 px
- **Rigged:** Yes
- **Animations:** No
- **UV Mapping:** Yes
- **LOD:** No

---

## Model

- **File:** SK_RSG_Crossroad_Pickup.fbx (rigged)

The mesh is split into five material groups:

| Material group | Meshes | Vertices | Triangles |
|---|---|---|---|
| Body (chassis, cabin, bed, panels) | 17 | 17,599 | 18,520 |
| Hull (armour plating) | 6 | 6,315 | 6,638 |
| Equipment (cage, rack, chains, stowed gear) | 113 | 24,768 | 30,535 |
| Wheels (tyres, rims, spare) | 10 | 29,353 | 27,212 |
| Details (mounted truck parts) | 9 | 3,058 | 3,508 |
| **Total** | **155** | **81,093** | **86,413** |

The rig is a simple vehicle skeleton — `Root` with a steering-wheel bone, four suspension bones and four wheel bones (`F.L`, `F.R`, `R.L`, `R.R`) — driven by the included controller.

---

## Textures

15 textures at 4096 × 4096 px — Base Color, Normal and MAS maps for each of the five sets:

- **Crossroad Pickup** – the vehicle's own atlas (body, cabin, bed, panels)
- **Hull**, **Equipment**, **Wheels** – shared post-apocalyptic part sets
- **Details** – mounted truck parts

MAS packing: **R** = Metallic, **G** = Ambient Occlusion, **A** = Smoothness.

---

## Features

- Detailed post-apocalyptic pickup with welded ram bar, roof rack and cargo cage
- Optimized for real-time rendering
- Fully UV-mapped model
- Cross-pipeline support (BRP, URP, HDRP)
- Ready-to-use materials for each render pipeline
- **Vehicle control system included**

---

## Included Content

- 1 Pickup model – SK_RSG_Crossroad_Pickup.fbx (155 meshes)
- 5 Materials per render pipeline
- 15 Textures
- One ready-to-drive prefab per render pipeline, with configured materials
- Vehicle controller setup (wheel colliders, anti-roll bar, crash handling)

---

## Setup Instructions

1. Import the package into your Unity project.
2. Ensure your project is using one of the supported render pipelines (BRP, URP, or HDRP).
3. Open the prefab for your pipeline in `LastGuns/<BRP|URP|HDRP>/Crossroad_Pickup/Prefabs`.
4. If needed, select the correct material variant for your render pipeline.
5. Drag and drop the pickup prefab into your scene.

---

## Control System

The included control system allows basic vehicle interaction:

- Movement control
- Steering
- Ready for further customization

The pickup is set up as an all-wheel-drive vehicle with a front-biased torque split. Handling values (torque, steering angles, brake torque, suspension and anti-roll rates) are exposed on the prefab root and can be retuned freely.

> Note: The control system is a base implementation and may require adjustments depending on your project.

---

## Notes

- No animations are included.
- No LODs are provided.
- Uses 4K textures, recommended for desktop/high-end projects.

---

## Support

If you have any questions or need assistance, please contact support via the Unity Asset Store page.

---

## License

This asset is provided under the standard Unity Asset Store license agreement.
