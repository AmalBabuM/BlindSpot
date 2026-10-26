# Stair encounter and pause

Press Escape to pause, then Resume patrol or Back to Menu. In WebGL, Back to Menu reloads the launch page and resets the current run, retaining the saved sound preference. Editor Play mode reloads GroundFloor into its Editor menu preview. Retry mission starts the encounter directly.

Reception retains its existing sequence. After both people are evacuated and the assistant returns, the stair instruction explains the new entry choice. E opens a stair door at 25 degrees/second, still limited to the existing 85-degree stop. Colliding with the door without first using E triggers the ambush. Only player contact triggers this penalty; the assistant cannot accidentally start the ambush. Before reception is secured, E displays the reception objective instead of granting careful entry.

The stair suspect reuses the supplied prisoner model, animator and equipment, instantiated independently at Stairs_NextFloor and placed on the saved navigation mesh. The existing reception actor, level layout, lightmaps and navigation asset are preserved. The pistol uses the existing hand-attached weapon with a simple added grip; muzzle flash and a short generated gunshot provide attack feedback. No additional shadowed lights or post-processing are added.

Careful path: E, Q to drop the pistol and raise hands, wait for the weapon to settle, Q to kneel, then cover the assistant as he moves behind the suspect and applies cuffs. E within restraint range also applies cuffs. The dropped weapon remains on the floor. The mission ends after handcuffs, with a scrollable review of recorded positive and negative decisions and total points.

Sudden-entry path: first-person camera falls toward the floor, a dark red overlay pulses and fades toward black, and the report appears after 3.5 seconds. The report never pauses the fall partway through. Shooting/incapacitating the stair suspect also ends the authored surrender objective after the suspect's fall finishes.

Scores: careful stair entry +100, accepted commands +80 each, handcuffs +150, securing the stairs +200; sudden entry -300. Stair actions have their own score IDs, so reception rewards do not suppress them. Reports use DecisionScore.Timeline rather than fixed claims about player behavior.

Architecture: StairEncounter owns the final encounter state and report; PushDoor owns bounded physical motion; SuspectController owns suspect states; AssistingOfficer reuses surrender support for either suspect; PatrolPause owns pause and menu return. MissionHud creates these runtime extensions without rewriting the scene. The existing OfficeGuidance explains the transition to stairs.

Validation: Clearance > Test Stair Encounter And Pause exercises both endings, pause/resume, entry speed, dropped gun, score deduplication, assistant route/cuffs and reports. Results and rendered gameplay captures are written under Logs. Clearance > Test Office Encounter verifies reception regression. Build with Clearance > Build WebGL Release.
