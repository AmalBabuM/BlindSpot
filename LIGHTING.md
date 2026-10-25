# Office lighting

Use **Clearance > Repair And Bake Office Lighting** (or **Bake Office Lighting**) outside Play Mode after changing geometry or fixed lights. Let the bake finish; the tool saves the scene and generated assets. **Clearance > Verify Saved Office Lighting** checks that the saved bake survives a Play Mode round trip.

## What was corrected

- The Very Low quality preset used vertex lighting and disabled main-light rendering. A dedicated OfficeLightingPipeline/OfficeRenderer uses per-pixel Forward lighting consistently across the existing quality levels. The WebGL budget disables HDR, soft shadows and additional-light realtime shadows, with an 85% render scale. Baked shadows, lightmaps, probes and mixed-light illumination remain available. Original imported pipeline assets remain unchanged.
- The GroundFloor scene referenced the imported Test_Map lighting data. Its new bake is stored in Assets/Scenes/GroundFloor/LightingData.asset with the scene's own lightmaps and reflection cubemaps. The imported Test_Map data is retained.
- Architecture, desk and static lift cabin/frame receive baked lightmaps. Moving door panels, push doors, animated fans and characters use baked light probes rather than stale static maps.
- Fixed modular-kit fixture lights are Baked. Office/door/lift fill lights are Mixed, and flashlights remain realtime. The desk front receives a soft fill without recoloring its wood material.
- A light-probe grid covers the rooms, with extra samples inside the lift. Room and lift reflection probes supply reflections for shaded/metal surfaces.

## Backups and layout

Each repair writes a timestamped folder under LightingBackups containing the saved scene, lighting settings, graphics settings and quality settings. If a previous GroundFloor bake exists, its generated files and metadata are copied into the backup's BakedLighting folder. The original pre-repair snapshot is LightingBackups/2026-10-25-183256. The latest backup path is also written to Logs/Office-lighting-backup.txt.

Room geometry, furniture/character transforms and gameplay components are kept intact. The repair changes lighting/probe objects, GI flags, lightmap assignments and the dedicated rendering profile. Existing material colors and textures are retained.

Generated outputs live in Assets/Scenes/GroundFloor and Assets/Clearance/Lighting and should be included when sharing the project. Logs/Office-lighting-bake.txt records bake output; Logs/Office-lighting-verification.txt records persistence checks. Do not enter Play Mode or edit C# while the lighting bake is running.
