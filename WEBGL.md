# WebGL performance

WebGL is the shipping target. The office uses the same browser rendering budget in the Editor so gameplay preview does not depend on desktop-only quality settings.

## Changes

- OfficeRenderer uses Forward rendering with per-pixel lights. Additional-light realtime shadows are disabled: the earlier profile generated up to 19 shadow views per frame. Existing bake files, probe assignments, light activation states and material colors are retained. The current saved scene has Office_BakedLightProbes disabled and its most recent bake contains no light probes; that existing user setting was preserved. Re-enable that group before a future lighting bake if moving doors/characters need baked probe lighting.
- HDR and soft shadows are off. Render scale is 0.85, reducing 3D pixel count by about 28% at a given canvas size. The HUD retains full canvas resolution. This slightly softens the 3D image; adjust OfficeLightingPipeline's Render Scale if browser measurements justify a different balance.
- Assistant door scans use a reusable collider buffer at most once per 0.15 seconds. Corner visibility uses a reusable raycast buffer and fails closed if it fills. Sleeping door pivots no longer submit Rigidbody rotations every physics tick.
- HUD status text refreshes at about 7 Hz; input, movement and identification still update every frame. The HUD avoids creating assistant arrays and formatted corner labels on every GUI event. Lift dialog GUI styles are cached.
- Initial WASM memory is 256 MB instead of 32 MB, with geometric growth retained. This trades a larger initial reservation for fewer potential heap-growth steps; it does not impose a fixed 256 MB memory ceiling.
- Browser startup enables VSync and uses the native refresh rate instead of a software FPS cap. The ClearanceIntro launch template caps devicePixelRatio at 1 so high-DPI screens do not silently multiply rendering work. Existing 0.85 URP render scale is retained.
- Carefully opened doors stop submitting Rigidbody rotations once they reach their stop. Guidance/pause/mission and cuff animator references are cached. Suspect status strings refresh at most every 0.15 seconds (immediately on suspect state changes), and the stairs marker distance at most every 0.2 seconds. Movement, aiming, commands and identity checks still run each frame.
- WebGL caps Time.maximumDeltaTime at 0.1 seconds, bounding physics catch-up to five existing 50 Hz steps after a long browser frame. It does not change normal physics frequency.
- FrameBudgetSample uses a fixed 600-value buffer and reports mean/p95 frame time and frames above 50 ms once, after six active warm-up seconds. Paused/unfocused frames are excluded; reporting then disables itself. The browser console contains the sample; Editor samples go to Logs/Frame-budget-sample.txt. These samples describe the specific viewport and activity, not all hardware or gameplay.
- Repair And Bake Office Lighting retains these rendering choices during future bakes.

## Validation and build

Use Clearance > Check WebGL Performance Settings for the saved rendering/bake/camera checks, and Clearance > Test Office Encounter for the playable procedure. Results are written to Logs/WebGL-performance-check.txt and Logs/Office-smoke-test.txt. These are configuration and gameplay checks, not a browser FPS benchmark.

Web Build Support is installed for Unity 6000.6.7f1. Use Clearance > Build WebGL Release to select Web, build GroundFloor (rather than the imported elevator demo) and write Builds/WebGL. Development Build is off. The build report is written to Logs/WebGL-release-build.txt. Test the output served over HTTP; use a separate development build for browser/Unity profiling. Check frame time and stalls during desk approach, handcuffing, the two-person escort, door pushing and lift opening; Editor FPS does not establish browser performance.

Keep the default compressed build/server configuration consistent when hosting. Download compression affects loading and hosting; it does not fix expensive gameplay frames.

## Menu and navigation

Open GroundFloor and press Play to see the Editor menu preview. It uses the same supplied artwork, has Start/Controls/Sound, plays the intro and supports Skip. Gameplay roots remain inactive until the intro finishes or is skipped. Back to Menu also works in Editor. EditorPatrolMenu is excluded from WebGL; the browser uses the lightweight HTML menu. Automated gameplay tests bypass the Editor menu.

Clearance > Rebake Navigation And Check Performance backs up OfficeNavigation.asset under NavigationBackups, rebuilds navigation from the current scene, and verifies complete entrance-to-evacuation and entrance-to-stairs routes. It saves navigation without rebaking lighting or re-enabling disabled probes. Results: Logs/Navigation-optimization.txt. The Unity splash-screen setting is preserved as supplied.

The optimized release's focused reception sample at 960×600 measured 7.29 ms mean and 9.00 ms p95 across 600 frames, with one frame above 50 ms. This is an idle/reception browser sample; it does not establish performance throughout every encounter or on other hardware. See Logs/WebGL-frame-sample.txt. Editor menu flow, reception/escort and stair success/failure tests passed after the navigation rebuild.

Unity references: [Web Player settings](https://docs.unity.com/en-us/engine/6000.5/manual/platform-specific/webgl/develop/class-player-settings-web-gl) and [frame pacing](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-targetFrameRate.html).
