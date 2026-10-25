# Guided reception UI

The assistant follows automatically. The entry-choice screen and office C/L entry-selection shortcuts are removed.

The HUD has a small decision-points badge at the top right, a brief queued reward notification near the top, a crosshair/context prompt when needed, and an `i` button at the bottom left. Click `i` with the cursor released, or press I, to open/close the controls drawer. Escape also closes the drawer. Reviewing controls during an instruction does not bypass that instruction.

OfficeGuidance owns the decision gates: flashlight, careful desk approach, hands-up command, checking both identities, surrender, covering handcuffs, escorting both people, and taking the stairs. Cards pause game time and release the cursor until the player selects an action. Flashlight F works while its card is open. The initial dark arrival is a temporary screen fade during that first instruction; it preserves the scene's baked lighting and light activation settings. Turning on the flashlight removes the fade. Other cards explain the next physical action, such as aiming and pressing Q; selecting Continue does not skip identification or surrender requirements.

First flashlight use awards 50 points, choosing to cover restraint 25, choosing to escort both people 50, and a justified kneel command uses the command reward (80). Existing identification, corner and restraint rewards remain. DecisionScore rejects duplicate IDs. MissionHud subscribes to reward events, queues at most six messages, and dismisses each toast automatically after 3.2 seconds using unscaled time. Zero-point bookkeeping messages do not occupy the HUD.

FieldUi provides shared rounded panels and buttons for the HUD and lift dialog using Unity's built-in rounded GUI drawing. GUI styles are cached; no generated textures, post-processing or third-party UI assets are required. UI scaling follows viewport height to preserve corner and font proportions. Layout, character placements, bake files and WebGL rendering settings are unchanged.

Clearance > Test Office Encounter verifies auto-follow, help open/close, flashlight reward and duplicate protection, instruction pause/resume, reward toast, identification gates, cuffing, escort, return and stairs. Clearance > Build WebGL Release updates Builds/WebGL after source changes.
