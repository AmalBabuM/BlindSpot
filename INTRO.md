# Menu and intro hosting

PatrolMainMenu runs inside the Unity player on WebGL and in Editor. Start Game, Controls, Sound, intro playback and Skip no longer depend on custom hosting-page HTML. The red menu artwork and Liberation Sans font are bundled under Assets/Resources/Launch. The font license is included under StreamingAssets/ThirdParty.

The 20-second user-provided MP4 remains at Assets/StreamingAssets/Intro/Intro_Video.mp4 and is played by VideoPlayer using Application.streamingAssetsPath. Skip remains visible while loading or playing. A media error leaves Skip available. Browser/host permission restrictions can still prevent video/audio playback; they cannot trap the player on the intro.

Back to Menu sets an in-player return request and reloads GroundFloor. It does not reload the hosting iframe or page. Retry mission restarts gameplay directly. Sound uses PlayerPrefs and AudioListener.volume, plus the video's direct mute setting.

ClearanceIntro now only boots Unity. Unity Play can use its own loader without losing our menu or intro controls. The obsolete HTML menu/intro scripts remain as reference files but are not loaded by index.html. Unity Play's own poster/Play button belongs to the website and appears before our player launches.

Build with Clearance > Build WebGL Release, then update the existing Unity Play game's build using the entire build output or the prepared ZIP. Keep StreamingAssets, including the video, in the upload. Do not upload only TemplateData.

Clearance > Test Editor Main Menu checks actual decoded video, Skip, gameplay activation, original lighting activation states and return to menu. The rendered build is separately checked in the browser because browser video permissions differ from Editor playback.
