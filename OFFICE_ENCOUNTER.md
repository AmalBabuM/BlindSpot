# Office entrance encounter

Open GroundFloor and press Play. Choose L to lead or C to send your assistant first.

1. Both people start kneeling behind the reception desk. Walk closer (within 3 metres of the desk) while facing it; only the suspect stands and looks at you. Turning toward the desk from the original spawn alone does not trigger this.
2. Aim at either person and press Q. Both raise their hands; the employee stays kneeling.
3. With the flashlight on, inspect each person from within 3.5 metres for 1.25 seconds. Move around the desk to see the kneeling employee clearly. Identities stay hidden until checked: the Employee is the office admin; the other person matches the suspect briefing.
4. After BOTH are identified, aim at the suspect and press Q again to order kneeling. The employee stays kneeling until the suspect has been restrained, and is never handcuffed.
5. The assistant approaches, waits for kneeling, and restrains the suspect. E still allows player restraint. Rewards cannot be duplicated.
6. The employee stands for evacuation. The suspect walks in front, the employee follows, and the assistant accompanies both from behind. They wait if the group gets separated. At the closed exit door both people are hidden for the custody/evacuation handoff, then the assistant returns and resumes distance-based following.
7. The objective changes to STAIRS / NEXT FLOOR. Push the Open Door leaves by walking into them, or press E while aiming at them. Follow the stair waypoint. The upper-floor encounter is not built by this change.

Shooting or stunning the admin incurs a civilian-hit penalty. Incapacitating either participant fails this office procedure; R restarts. Identification is an authored prototype check (administrator ID versus suspect briefing), not an inference from clothing alone.

## Scene settings

OfficeEncounter owns the desk approach trigger, two-person procedure and objective. OfficePerson owns initial kneeling, identification and the admin hands-up pose. Existing SuspectController/PrisonerPresentation retain the kneel/restraint clips. OfficeEscort temporarily overrides the assistant's follow/search behavior, navigates both people to the exit, handles handoff and return, then restores follow mode. The employee receives a runtime NavMeshAgent for evacuation. Hands-up and handcuff reaching are procedural poses; supplied clips provide idle, kneeling and walking. Standing blends from the kneeling pose into the supplied idle animation.

Move the OfficeEncounter custody waypoint (OutsideCustodyPoint / DoorCustodyPoint) to change handoff location and Stairs_NextFloor to change the next objective. The handoff waypoint is inside the exit doorway, so no exterior platform is required. After moving architecture/desk/custody ground, use Clearance > Rebuild Office Navigation. The bake includes environment/desk colliders and excludes actors/moving doors. Open Door contains the physical door leaves, each using a bounded, damped pivot rotation and kinematic Rigidbody. The old door controllers and unstable physics joints are removed/disabled on these leaves. CharacterController contact or E applies a push to the controlled pivot.

## Lighting and baking

OfficeLighting is a saved lighting-settings asset with baked GI enabled, real-time GI disabled, directional lightmaps and the Progressive CPU lightmapper. Static architecture/props contribute GI; secondary lightmap UVs are generated for their imported meshes. Fixed room/fill lights are Mixed; character flashlights remain realtime. Movable characters and push doors are excluded from baked geometry.

Use Clearance > Bake Office Lighting outside Play Mode, or Window > Rendering > Lighting > Generate Lighting. This starts an asynchronous Unity bake; let it complete and save the scene. The lighting repair has generated and saved scene-specific lightmaps; see LIGHTING.md for the current rendering profile and backup details. Use Clearance > Repair And Bake Office Lighting to update the settings and bake safely. The office has realtime direct fill while awaiting the bake.

## Verification and credits

Clearance > Test Office Encounter runs the procedure and writes Logs/Office-smoke-test.txt. It checks two-person commands, identification gating, civilian treatment, restraint, outside escort, return, next-floor guidance, physical door response and baking settings.

Existing environment, desk, admin (Floreswa male01_1), police model, prisoner and imported animation assets were supplied/imported. The procedure, navigation adaptation, physics setup, lighting configuration and prototype poses were implemented for this project.
