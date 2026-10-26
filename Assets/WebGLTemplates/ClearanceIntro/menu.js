window.playClearanceIntro = function (startGame) {
  const game = document.querySelector('#unity-container');
  game.style.visibility = 'hidden'; game.inert = true;
  try { window.clearanceSoundMuted = localStorage.getItem('clearance-muted') === 'true'; }
  catch (_) { window.clearanceSoundMuted = false; }
  const menu = document.createElement('main');
  menu.id = 'clearance-menu';
  menu.innerHTML = `
    <div class="menu-content">
      <p class="menu-eyebrow">TACTICAL DECISIONS. REAL CONSEQUENCES.</p>
      <h1>BLIND<span>SPOT</span></h1>
      <p class="menu-subtitle">CLEARANCE BUILDING</p>
      <p class="menu-description">Enter carefully. Verify the threat.<br>Bring everyone out safely.</p>
      <nav aria-label="Main menu">
        <button id="menu-start" class="menu-primary" type="button">Start Game <span aria-hidden="true">→</span></button>
        <button id="menu-controls" type="button">Controls <span aria-hidden="true">i</span></button>
        <button id="menu-sound" type="button" aria-pressed="false"></button>
      </nav>
      <p class="menu-footer">GROUND FLOOR · RECEPTION</p>
    </div>
    <dialog id="menu-help" aria-labelledby="menu-help-title">
      <p class="menu-eyebrow">PREPARE FOR ENTRY</p><h2 id="menu-help-title">Field controls</h2>
      <dl>
        <div><dt>W A S D</dt><dd>Move</dd></div><div><dt>Mouse</dt><dd>Look around</dd></div>
        <div><dt>Shift</dt><dd>Move carefully</dd></div><div><dt>F</dt><dd>Toggle flashlight</dd></div>
        <div><dt>Q</dt><dd>Voice command</dd></div><div><dt>E</dt><dd>Interact</dd></div>
        <div><dt>1 / 2</dt><dd>Stun device / firearm</dd></div><div><dt>Left click</dt><dd>Use selected tool</dd></div>
        <div><dt>I</dt><dd>Instructions and controls</dd></div><div><dt>Esc</dt><dd>Pause / Back to Menu</dd></div>
      </dl><p class="help-note">Use E to open stair doors carefully. Pushing in risks an ambush. Verify identities before using force.</p>
      <button id="menu-help-close" class="menu-primary" type="button">Close</button>
    </dialog>`;
  document.body.appendChild(menu);
  const sound = menu.querySelector('#menu-sound');
  function updateSound() {
    sound.textContent = window.clearanceSoundMuted ? 'Sound: Off' : 'Sound: On';
    sound.setAttribute('aria-label', window.clearanceSoundMuted ? 'Turn sound on' : 'Turn sound off');
    sound.setAttribute('aria-pressed', String(window.clearanceSoundMuted));
  }
  sound.onclick = () => {
    window.clearanceSoundMuted = !window.clearanceSoundMuted;
    try { localStorage.setItem('clearance-muted', String(window.clearanceSoundMuted)); } catch (_) {}
    updateSound();
  };
  updateSound();
  const help = menu.querySelector('#menu-help');
  menu.querySelector('#menu-controls').onclick = () => help.showModal();
  menu.querySelector('#menu-help-close').onclick = () => help.close();
  let starting = false;
  menu.querySelector('#menu-start').onclick = () => {
    if (starting) return;
    starting = true;
    menu.remove();
    // Keep this in the click handler so the browser permits video audio.
    window.playClearanceVideo(startGame);
  };
};
