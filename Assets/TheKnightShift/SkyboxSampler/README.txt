SKYBOX SAMPLER - 9 free stylized skies - The_KnightShift
========================================================

QUICK START: open Demo/Demo.unity, press Play, and use the Left / Right arrow keys to
step through all 9 skies. (Importing the package does not open the scene by itself.)
To use a sky in your own scene: Window > Rendering > Lighting > Environment > Skybox Material.

THIS IS A FREE SAMPLE
9 skies, 3 from each of our three sky packs (Space Skyboxes, Stylized Sky Skyboxes and
Alien Worlds Skyboxes). All 52 skies of the three packs, with 1024x1024 cube faces, are in
our paid package "Stylized Skybox Collection" on the Unity Asset Store (publisher The_KnightShift).
This sampler has the same folders and material names, so a sky you try here keeps its name
there.

THE 9 SKIES (the pack each one comes from, and its material Rotation preset)
   1. Teal & Magenta Nebula      Space Skyboxes         Rotation   0
   2. Ringed Gas Giant           Space Skyboxes         Rotation   0
   3. Supernova Remnant          Space Skyboxes         Rotation   0
   4. Golden Hour                Stylized Sky Skyboxes  Rotation  15
   5. Thunderstorm               Stylized Sky Skyboxes  Rotation 348
   6. Moonlit Night              Stylized Sky Skyboxes  Rotation 335
   7. Ringed Giant over Ocean    Alien Worlds Skyboxes  Rotation 356
   8. Volcanic Ash World         Alien Worlds Skyboxes  Rotation   0
   9. Tidally-Locked Terminator  Alien Worlds Skyboxes  Rotation  55

WHAT IS INCLUDED
- 9 skies: three space skies (a nebula, a ringed gas giant, a supernova remnant),
  three outdoor skies (golden hour, a thunderstorm, a moonlit night) and three alien
  worlds (a ringed giant over an ocean, a volcanic ash world, a tidally-locked terminator).
- For every sky: a ready-made Skybox/6 Sided material with its six 512x512 PNG cube faces
  (Front, Back, Left, Right, Up, Down) assigned, and one 2048x1024 equirectangular PNG
  panorama.
- 9 materials, 54 face textures, 9 panoramas. All lossless PNG (8-bit sRGB, not HDR).
- Demo/Demo.unity: demo scene (camera tilted 10 degrees up, light, sky switcher, no meshes);
  press Play and use the arrow keys to cycle all 9 skies.
- Scripts/SkyboxCycler.cs: the small script the demo uses (namespace TheKnightShift.SkyboxSampler).
- Documentation/SkyboxSampler_Documentation.pdf: setup guide and script reference.

FOLDERS (under Assets/TheKnightShift/SkyboxSampler)
  Space/, StylizedSky/, AlienWorlds/ - one folder per pack, each with
    Materials/<Pack>_<sky>.mat
    Cubemaps/<sky>/Front.png ... Down.png
    Panoramas/<sky>.png

HOW TO USE
1. Import the package.
2. Open Window > Rendering > Lighting > Environment and assign any material from a
   Materials folder to "Skybox Material". For lighting from the sky set
   Environment Lighting > Source to Skybox.
3. Optional: add SkyboxCycler to any GameObject, fill its Skyboxes array with materials,
   press Play and use Left / Right arrow keys.
(The cycler works with the classic Input Manager, the Input System package, or Both.)

FACING
Each sky's main feature (the nebula, the planet, the sun, the moon ...) sits at its own
bearing in the images. Every material's Rotation is preset (table above) so that this
feature is in front of a camera looking along +Z, the default direction of a new Unity
camera. The features are about 4 to 37 degrees above the horizon (Moonlit Night's moon
is the highest): the demo camera looks 10 degrees up. To face another way, change the
material's Rotation (0-360). Sky 9, Tidally-Locked Terminator, has files and a material
named TidallyLockedTwilight.

PANORAMAS
The 2048x1024 PNGs are plain textures oriented for Skybox/Panoramic, matching the 6-sided
images of the same sky. To use one: create a material with shader Skybox/Panoramic, set
Mapping to Latitude Longitude Layout and Image Type to 360 Degrees, drag the PNG into
Spherical (HDR), and give it the same Rotation as the 6-sided material (table above) if
you want the same facing. For a Cubemap asset set the texture's Shape to Cube.

IMPORT SETTINGS (already set)
Cube faces: sRGB on, Wrap Mode Clamp, no mipmaps, Bilinear, uncompressed.
Panoramas: Wrap Mode Repeat, no mipmaps, Bilinear, uncompressed.

RENDER PIPELINES
Built-in: use the materials directly (Skybox/6 Sided is a built-in shader; no custom
shaders in this package). URP renders Unity's built-in Skybox shaders too, but whether this
release was checked in URP is stated on its Asset Store page (render pipeline table); if
not, treat URP as untested. The demo scene has no meshes, only a camera, a light and the
sky switcher, so it shows nothing pipeline-specific. HDRP does not use skybox materials:
set a panorama's Texture Shape to Cube and use it with the HDRI Sky volume override
(Unity's documented route; not tested by us in HDRP).

UNITY VERSION
Uploaded from Unity 6 (6000.0). It uses only Unity's built-in skybox shaders and one small
script; older Unity versions are not tested.

WHERE THE SKIES COME FROM
Each sky is a free sample of one of our three paid sky packs; all three packs are inside
"Stylized Skybox Collection" on the Unity Asset Store. The packs also have pages on itch.io:
  Space Skyboxes         https://the-knightshift.itch.io/space-skyboxes
  Stylized Sky Skyboxes  https://the-knightshift.itch.io/stylized-skyboxes
  Alien Worlds Skyboxes  https://the-knightshift.itch.io/alien-skyboxes

SUPPORT
Use the support contact on this package's Asset Store page, or leave a comment on
https://the-knightshift.itch.io/skybox-sampler

AI USE
These skies were generated by procedural Python code that was written with an AI coding
assistant (Claude, by Anthropic). No photos and no image-generation model were used. The
SkyboxCycler script, demo scene, material files, documentation and this file were also
written with AI assistance (Claude). The publisher opens the package in the Unity Editor and checks it by eye
before release. The package contains no AI-generated audio or 3D models.

LICENCE
Free. Your licence is the Unity Asset Store End User License Agreement
(https://unity.com/legal/as-terms). See LICENSE.txt.
