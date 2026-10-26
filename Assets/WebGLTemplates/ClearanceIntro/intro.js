// Keep the mission unloaded until the intro finishes; no tutorial timers run behind it.
window.playClearanceVideo = function (startGame) {
  const style = document.createElement('style');
  style.textContent = `
    #clearance-intro { position:fixed; inset:0; z-index:10000; background:#05080c;
      color:#fff; font-family:Arial,sans-serif; transition:opacity .35s ease; }
    #clearance-intro video { width:100%; height:100%; object-fit:contain; }
    #clearance-intro button { border:1px solid #64717f; border-radius:12px;
      background:rgba(15,25,35,.9); color:white; padding:13px 24px; font-size:15px;
      cursor:pointer; min-height:48px; }
    #clearance-intro button:hover { background:#235454; border-color:#65d4cb; }
    #clearance-intro button:focus-visible { outline:3px solid #65d4cb; outline-offset:4px; }
    #intro-skip { position:absolute; bottom:28px; right:28px; }
    #intro-prompt { position:absolute; inset:0; display:flex; align-items:center;
      justify-content:center; flex-direction:column; gap:16px; background:rgba(0,0,0,.35); }
    #intro-prompt[hidden] { display:none; }
    #intro-status { position:absolute; bottom:38px; left:28px; font-size:14px; color:#c1ccd5; }
    @media(prefers-reduced-motion:reduce) { #clearance-intro { transition:none; } }
  `;
  document.head.appendChild(style);
  const overlay = document.createElement('section');
  overlay.id = 'clearance-intro';
  overlay.setAttribute('aria-label', 'Mission introduction');
  overlay.innerHTML = `
    <video playsinline preload="auto" aria-label="Mission intro video"></video>
    <div id="intro-prompt" hidden><p id="intro-message"></p><button id="intro-play" type="button">Play intro</button></div>
    <span id="intro-status" role="status">Loading intro…</span>
    <button id="intro-skip" type="button">Skip intro →</button>`;
  document.body.appendChild(overlay);
  const video = overlay.querySelector('video');
  video.muted = !!window.clearanceSoundMuted;
  const prompt = overlay.querySelector('#intro-prompt');
  const message = overlay.querySelector('#intro-message');
  const status = overlay.querySelector('#intro-status');
  const game = document.querySelector('#unity-container');
  game.style.visibility = 'hidden';
  game.inert = true;
  let finished = false;
  function finish() {
    if (finished) return;
    finished = true;
    video.pause();
    overlay.style.opacity = '0';
    overlay.style.pointerEvents = 'none';
    setTimeout(() => {
      video.removeAttribute('src'); video.load();
      overlay.remove(); style.remove();
      game.style.visibility = ''; game.inert = false;
      startGame();
    }, 350);
  }
  function requestPlay() {
    prompt.hidden = true;
    status.textContent = 'Loading intro…';
    video.play().catch(() => {
      if (finished) return;
      status.textContent = '';
      message.textContent = 'Play the introduction, or skip to your mission.';
      prompt.hidden = false;
    });
  }
  overlay.querySelector('#intro-skip').onclick = finish;
  overlay.querySelector('#intro-play').onclick = requestPlay;
  video.onended = finish;
  video.onplaying = () => { status.textContent = ''; prompt.hidden = true; };
  video.onwaiting = () => { if (!finished) status.textContent = 'Buffering intro…'; };
  video.onerror = () => {
    if (finished) return;
    status.textContent = '';
    message.textContent = 'The intro could not load. You can still skip to the mission.';
    prompt.hidden = false;
  };
  video.src = 'StreamingAssets/Intro/Intro_Video.mp4';
  requestPlay();
};
