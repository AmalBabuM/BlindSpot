# Ground-floor prototype

Open `Assets/Scenes/GroundFloor.unity` and press Play. If gameplay has not been wired yet, use **Clearance > Set Up Ground Floor** (Ctrl+Shift+G) once. It keeps the existing layout, reuses your officer, disables the legacy camera, creates the encounter and saves the scene. It does not replace an existing gameplay setup.

## Controls and walkthrough

WASD moves, mouse looks, Shift walks slowly, F toggles the flashlight, Q issues a command to the person under the crosshair, E interacts/restrains/confirms room clearance, 1 selects stun, 2 selects firearm, left click uses the selected tool. Escape releases the cursor; Resume captures it again. The stun tool starts selected.

Start at the doorway. With your flashlight on, look at each cyan corner marker for 0.7 seconds. The prisoner walks between Patrol_A and Patrol_B. Approach within 4.2 metres with clear line of sight: he stops, turns toward you, and shows the Q prompt. Aim at him and press Q to raise his hands. With the flashlight on, aim at him from within 3.5 metres for 1.25 seconds. Once No weapon found appears, press Q again to order him to kneel. Wait for the kneeling animation to finish. Approach within 2.4 metres, aim at the suspect and press E to restrain. Press E again inside the room to complete. Review your decision timeline and retry. The peaceful route scores 430. Entering before both checks costs 100. You can recover missed checks and finish.

The alternate stun route works within 7 metres. Firearm use is penalized in this tutorial because this suspect never initiates an immediate attack. Scores are a prototype policy, not a simulation of policing standards.

## Architecture

This is a **component-based Unity architecture with explicit serialized references**, a **finite-state machine** for suspect behavior, and a **ScriptableObject** for scoring configuration. There is no global service locator, dependency injection framework, or singleton dependency.

* `FirstPersonOfficer`: CharacterController movement, camera look, cursor ownership; reads the installed Input System's keyboard/mouse devices.
* `OfficerActions`: line-of-sight targeting, command/interact/tool input, flashlight, tool cooldown.
* `SuspectController`: Armed -> Considering -> Surrendered -> Restrained; alternative terminal states Stunned/Down. Response time is Inspector-editable. Visual arms and weapon communicate compliance.
* `ObservationPoint`: flashlight, viewing angle, range, occlusion and continuous observation time. Each check scores once.
* `GroundFloorMission`: entry feedback and completion conditions. The selected room bounds are editable in the Inspector and visible with Gizmos.
* `DecisionScore`: deduplicated score events and timestamped decision timeline. `ScoringRules` contains the editable values.
* `InteractableDoor`: a simple rotation interaction for existing door objects.
* `MissionHud`: prototype IMGUI display for objectives, controls, feedback and results; replace with a Canvas/TMP UI for production.
* `Editor/GroundFloorSetup`: repeat-safe editor setup and imported-material conversion; editor code is excluded from player builds.

Dependencies flow from player actions and mission checks to suspect/score components. The HUD reads state. References are wired once by the editor setup and serialized into the scene, so runtime code does not search for managers every frame. The explicit enum state machine is intentionally small for a 48–72 hour prototype; add state classes only when behaviors become more complex.

## Art and materials

Existing environment: imported Barking Dog modular kit. Officer: supplied Police_officer asset. Suspect: supplied Prisoner model (`Assets/Character/Prisoner/source/Prison.fbx`) and PowerPlantA texture, with a generated URP Lit material. Imported models/textures are not original work. `PrisonerPresentation` drives the Animator using the supplied Mixamo breathing idle, walking, kneeling, and falling clips. It no longer overrides bones with procedural poses. The old primitive visual is inactive for easy reversal. Officer renderers cast shadows only in first person, so the model remains in the player hierarchy without clipping the camera. Set its renderers' Shadow Casting Mode to On to preview the textured model in the editor. The supplied officer has no animation controller attached; locomotion animations are not implemented.

Use **Clearance > Use Prisoner as Ground Floor Suspect** to apply the visual swap to a configured scene. It retains the suspect component and mission references, adds a hit collider, fits the visual to human scale, and uses the prisoner's rig for surrender. Character assets now live under `Assets/Character`.

**Clearance > Fix Imported Materials for URP** converts remaining built-in/custom materials in those two imported asset folders to URP Lit while retaining albedo, normal, metallic, emission and tint data. Existing URP materials are skipped. This fixes the supplied character's incompatible two-sided Standard shader; hair uses alpha clipping.

## Current scope

One playable room, one cooperative suspect, two observation checks, both tool outcomes, a scored completion and retry. Commands are text feedback, not microphone input or recorded dialogue. One assistant provides distance-based following, corner searches and restraint support. No autonomous team combat, suspect attacks, civilians, upper floor or basement yet. The firearm and stun use camera raycasts with HUD feedback; polished weapon models, audio and impact effects remain future work. Corner markers are deliberate tutorial aids and approximate observation, not general spatial reasoning.

## Quick verification

Check the Console for compile/runtime errors. Verify walls stop movement, corner checks require flashlight and visibility, repeated commands/restraint cannot farm points, surrendered people react before force decisions, premature clearance fails, completion unlocks the cursor, and Retry resets the scene and score. The GroundFloor scene is added first to Build Settings by setup.

Use **Clearance > Run Ground Floor Smoke Test** (Ctrl+Shift+T) for automated Play Mode checks. It temporarily enters Play Mode, checks floor support, doorway visibility, both observation points, command delay, score deduplication, peaceful completion at 430, cursor release, and isolated firearm/stun outcomes. Results are written to `Logs/GroundFloor-smoke-test.txt`. It exits Play Mode without saving test mutations. These checks passed during implementation; movement feel, door swing clearance, and full mouse/keyboard walkthrough remain manual checks.

## Patrol behavior
SuspectPatrol controls the A/B route, obstacle checks, proximity and line-of-sight detection, and turning toward the officer. Move Patrol_A and Patrol_B in the scene to tune the route; keep the connecting segment clear. Speed, alert distance, turn speed, and endpoint pause are Inspector settings. The state sequence is Patrolling -> Alerted -> HandsUp -> VerifiedUnarmed -> Considering (kneeling) -> Surrendered -> Restrained. Walking uses the supplied Mixamo Walking clip. This is a direct two-point path, not general navigation around furniture. The smoke test also verifies reaching both endpoints, stopping on approach, facing the player, and the Q prompt.

## Mixamo animation setup
PrisonerAnimations.controller has Idle, Walking, Kneel and Fall states, driven by Speed, Surrender and Fallen parameters. Idle/walk loop; kneel/fall play once and hold their last frame. All clips use the prisoner humanoid avatar. Root motion is disabled and movement is baked into the clips so SuspectPatrol remains the movement authority. The first Q raises the hands over the breathing idle pose. This encounter is unarmed: the synthetic weapon prop is hidden. A close flashlight inspection must complete before the second Q starts kneeling. Restraint unlocks after the kneel clip finishes. The original 10.8-second clip stood back up; its playback is trimmed to hold the last stable kneeling pose (about 6.5 seconds). A bullet hit plays Falling Back Death without tipping the actor root. The same fall is temporarily reused for stun incapacitation because no separate stun clip was supplied. The hit capsule follows the lower kneeling pose and is disabled for fallen actors. Use Clearance > Install Mixamo Prisoner Animations to reapply import/controller wiring.

## Assisting officer

One instance of the supplied police_officer prefab assists the player. At the start choose L / I lead, or C / Assistant first. In follow mode the assistant holds position while you turn or move slightly, catches up beyond 3.2 metres, and settles nearby. C sends the assistant to check both TeamCover_Left/Right positions in sequence with the torch. B returns to distance-based follow. Corner scores are awarded once across player and assistant observations.

The officer uses Standing Torch Walk Forward for movement and Standing Torch Inspect Forward while searching/holding. The old procedural careful-walk arm override is disabled. Root motion stays off; movement remains controlled by navigation. A torch prop and its spotlight are attached to the right hand.

After the first Q hands-up command, the assistant approaches the suspect's side. The player's flashlight inspection and second Q kneel order remain required. Once the suspect finishes kneeling, the assistant moves behind him, reaches toward his wrists for 2.5 seconds, and restrains him automatically. Player E restraint still works and cannot duplicate the reward. No dedicated handcuff clip was supplied: the wrist-reaching action is a procedural approximation layered over the search clip. The assistant remains beside the restrained suspect as a guard.

SquadCoordinator owns orders/navigation; AssistingOfficer owns follow/search/assist behavior. Navigation is baked from _Level colliders; interactive doors are opened en route. Use Clearance > Use One Assistant With Torch Animations to apply the setup, and Clearance > Test Assisting Officers to verify it. Rebuild navigation after material layout changes. The assistant does not fire autonomously.
Follow tuning: AssistingOfficer exposes followStartDistance (3.2 m), followStopDistance (1.9 m), followSpeed (2.3 m/s), and carefulSpeed (0.85 m/s). Cover orders use slower acceleration/turning, the supplied torch-walk/search clips, with procedural wrist reaching only during restraint. Squad Play Mode checks cover rotation and small-movement holding, following after greater separation, returning to idle, cautious speed, corner coverage and regrouping.
