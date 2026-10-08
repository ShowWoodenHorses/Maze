using System.Collections.Generic;

namespace Maze.Presentation.UI
{
    /// <summary>One level tile of the level select screen.</summary>
    public readonly struct LevelTileData
    {
        public LevelTileData(string levelId, int number, int stars, bool unlocked, bool playable)
        {
            LevelId = levelId;
            Number = number;
            Stars = stars;
            Unlocked = unlocked;
            Playable = playable;
            IsNext = false;
        }

        private LevelTileData(in LevelTileData source, bool isNext)
        {
            this = source;
            IsNext = isNext;
        }

        public string LevelId { get; }

        /// <summary>1-based position in the catalog: the tile's caption.</summary>
        public int Number { get; }

        /// <summary>Best stars; above 0 means completed (the exit is always a star).</summary>
        public int Stars { get; }

        public bool Unlocked { get; }

        /// <summary>Can be started: unlocked, or any level in development builds.</summary>
        public bool Playable { get; }

        /// <summary>The level to play next (highlighted).</summary>
        public bool IsNext { get; }

        public bool Completed => Stars > 0;

        public LevelTileData AsNext() => new LevelTileData(this, true);
    }

    /// <summary>Paging of the level select screen: 10 levels per page (two rows of five).</summary>
    public static class LevelPages
    {
        public const int PerPage = 10;

        public static int PageCount(int levels) => levels <= 0 ? 1 : (levels + PerPage - 1) / PerPage;

        public static int PageOf(int index) => index < 0 ? 0 : index / PerPage;

        /// <summary>The first unlocked level not completed yet, or −1 (all done, or none open).</summary>
        public static int NextLevel(IReadOnlyList<LevelTileData> levels)
        {
            for (var i = 0; i < levels.Count; i++)
                if (levels[i].Unlocked && !levels[i].Completed)
                    return i;
            return -1;
        }

        /// <summary>Page the screen opens on: the next level's, else the last page with an open level, else the first.</summary>
        public static int StartPage(IReadOnlyList<LevelTileData> levels)
        {
            var next = NextLevel(levels);
            if (next >= 0) return PageOf(next);
            for (var i = levels.Count - 1; i >= 0; i--)
                if (levels[i].Unlocked)
                    return PageOf(i);
            return 0;
        }
    }
}
