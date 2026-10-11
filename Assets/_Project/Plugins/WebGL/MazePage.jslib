// The page around the game (Maze.Application.Platform.WebPage).
mergeInto(LibraryManager.library, {
  // Calls back with 1 when the tab becomes visible and 0 when it is hidden.
  MazePage_ListenVisibility: function (callback) {
    document.addEventListener('visibilitychange', function () {
      {{{ makeDynCall('vi', 'callback') }}}(document.hidden ? 0 : 1);
    });
  }
});
