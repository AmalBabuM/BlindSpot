# WebGL intro

The release uses the ClearanceIntro WebGL template, based on Unity 6.6's Default template. It first shows the supplied office artwork with Start Game, Controls, and Sound On/Off buttons. Controls opens a keyboard/mouse reference with Close and Escape support. Sound selection is saved locally where browser storage is available. The artwork is user-provided and copied unchanged into menu-background.png.

Start Game plays the user-provided 20-second, 1920×1080 MP4 before creating the Unity instance. A rounded Skip intro button remains available throughout. Completion or skipping fades out the video, releases its media resources, and starts normal Unity loading and gameplay. The existing four-second tutorial delay begins after gameplay becomes visible. Video playback starts inside the Start Game click handler to support audio in browsers requiring a user gesture.

Sound Off mutes both the intro and gameplay. WebGLAudioPreference reads the menu preference through ClearanceAudio.jslib before the first scene and applies AudioListener.volume. Menu code runs outside Unity, so there is no game simulation or tutorial countdown while the menu is open.

Browsers may block automatic playback with audio. In that case, Play intro provides the required user gesture; Skip intro is also available. A media error leaves an explanation and the Skip button, so it cannot trap the player. Video preserves its aspect ratio at any viewport size.

Source video: Assets/StreamingAssets/Intro/Intro_Video.mp4 (copied unchanged from the supplied Downloads file, approximately 7.2 MB). Replace that file to change the intro. No re-encoding or third-party video package was used.

Template: Assets/WebGLTemplates/ClearanceIntro/index.html and intro.js. The selected Player setting is PROJECT:ClearanceIntro; Clearance > Build WebGL Release also explicitly selects it. Standard Unity Web builds copy the template and StreamingAssets into the output automatically. Upload the entire build folder, including StreamingAssets. Serve over HTTP/HTTPS rather than opening index.html directly.

The WebGL launch page owns the shipped menu and intro. Unity Editor Play mode now has an EditorPatrolMenu preview of the same flow, including controls, sound, intro, Skip and Back to Menu. Its code is excluded from player builds. Restarting the encounter in-game does not replay the video; returning to the menu starts the launch flow again.
