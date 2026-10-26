mergeInto(LibraryManager.library, {
  ClearanceSoundMuted: function () { return window.clearanceSoundMuted ? 1 : 0; },
  ClearanceBackToMenu: function () { window.location.reload(); }
});
