using Maze.Core.Grid;
using Maze.Core.Visibility;
using NUnit.Framework;

namespace Maze.Tests.EditMode.Visibility
{
    public class FieldOfViewTests
    {
        /// <summary>
        /// Map rows from top (highest y) to bottom. '#' wall, '.' floor, 'D' closed door, 'O' open door, '@' player.
        /// </summary>
        private sealed class Map
        {
            public readonly int Width;
            public readonly int Height;
            public readonly bool[] Opaque;
            public GridPosition Player;

            public Map(params string[] rows)
            {
                Height = rows.Length;
                Width = rows[0].Length;
                Opaque = new bool[Width * Height];
                for (var row = 0; row < Height; row++)
                {
                    var y = Height - 1 - row;
                    for (var x = 0; x < Width; x++)
                    {
                        var c = rows[row][x];
                        Opaque[y * Width + x] = c == '#' || c == 'D';
                        if (c == '@') Player = new GridPosition(x, y);
                    }
                }
            }

            public CellMaskOpacity Opacity => new CellMaskOpacity(Width, Height, Opaque);

            public FieldOfView Compute(int radius = FieldOfView.DefaultRadius)
            {
                var fov = new FieldOfView(Width, Height);
                fov.Compute(Player, radius, Opacity);
                return fov;
            }
        }

        private static Map OpenField(int size, GridPosition player)
        {
            var rows = new string[size];
            for (var i = 0; i < size; i++)
                rows[i] = new string('.', size);
            return new Map(rows) { Player = player };
        }

        [Test]
        public void OpenField_SeesExactly11x11_ClippedToGrid()
        {
            var fov = OpenField(21, new GridPosition(10, 10)).Compute();
            Assert.AreEqual(121, fov.VisibleCells.Count);
            Assert.IsTrue(fov.IsVisible(new GridPosition(5, 15)));
            Assert.IsFalse(fov.IsVisible(new GridPosition(4, 10)), "6 cells away is outside the window.");
            Assert.IsFalse(fov.IsVisible(new GridPosition(10, 16)));

            fov = OpenField(21, new GridPosition(0, 0)).Compute();
            Assert.AreEqual(36, fov.VisibleCells.Count, "Window is clipped by the grid border.");
        }

        [Test]
        public void Wall_BlocksSight_ButIsVisibleItself()
        {
            var fov = new Map(
                "#########",
                "#...#...#",
                "#.@.#...#",
                "#...#...#",
                "#########").Compute();

            Assert.IsTrue(fov.IsVisible(new GridPosition(4, 2)), "The blocking wall is seen.");
            Assert.IsTrue(fov.IsVisible(new GridPosition(0, 0)), "Room corner wall is seen.");
            for (var y = 1; y <= 3; y++)
            for (var x = 5; x <= 7; x++)
                Assert.IsFalse(fov.IsVisible(new GridPosition(x, y)), $"({x}, {y}) is behind the wall.");
        }

        [Test]
        public void ClosedDoor_BlocksSight_OpenDoorDoesNot()
        {
            var closed = new Map(
                "#######",
                "#.@D..#",
                "#######").Compute();
            Assert.IsTrue(closed.IsVisible(new GridPosition(3, 1)), "The closed door itself is seen.");
            Assert.IsFalse(closed.IsVisible(new GridPosition(4, 1)));
            Assert.IsFalse(closed.IsVisible(new GridPosition(5, 1)));

            var open = new Map(
                "#######",
                "#.@O..#",
                "#######").Compute();
            Assert.IsTrue(open.IsVisible(new GridPosition(4, 1)));
            Assert.IsTrue(open.IsVisible(new GridPosition(5, 1)));
            Assert.IsTrue(open.IsVisible(new GridPosition(6, 1)), "Wall at the end of the corridor.");
        }

        [Test]
        public void StraightCorridor_ShowsBothSideWallsUpToTheWindow()
        {
            var fov = new Map(
                "###",
                "#.#",
                "#.#",
                "#.#",
                "#.#",
                "#.#",
                "#.#",
                "#.#",
                "#@#",
                "###").Compute();

            for (var y = 0; y <= 6; y++)
            {
                Assert.IsTrue(fov.IsVisible(new GridPosition(0, y)), $"West wall y={y}.");
                Assert.IsTrue(fov.IsVisible(new GridPosition(1, y)), $"Corridor y={y}.");
                Assert.IsTrue(fov.IsVisible(new GridPosition(2, y)), $"East wall y={y}.");
            }

            Assert.IsFalse(fov.IsVisible(new GridPosition(1, 7)), "Beyond 5 cells.");
        }

        [Test]
        public void SideOpening_EntranceVisible_CorridorBehindItHidden()
        {
            var fov = new Map(
                "#.###",
                "#.###",
                "#....",
                "#.###",
                "#.###",
                "#@###",
                "#####").Compute();

            Assert.IsTrue(fov.IsVisible(new GridPosition(2, 4)), "Entrance of the side corridor.");
            Assert.IsFalse(fov.IsVisible(new GridPosition(3, 4)), "Deeper cells of the side corridor are around the corner.");
            Assert.IsFalse(fov.IsVisible(new GridPosition(4, 4)));
        }

        [Test]
        public void DiagonalGapBetweenWalls_BlocksSight()
        {
            var fov = new Map(
                "#.",
                "@#").Compute();

            Assert.IsFalse(fov.IsVisible(new GridPosition(1, 1)), "Walls touching at a corner close the gap.");
            Assert.IsTrue(fov.IsVisible(new GridPosition(1, 0)));
            Assert.IsTrue(fov.IsVisible(new GridPosition(0, 1)));
        }

        [Test]
        public void LineThroughCorner_BlockedOnlyWhenBothSidesOpaque()
        {
            var bothWalls = new Map("#.", "@#");
            Assert.IsFalse(GridLineOfSight.IsClear(0f, 0f, 1f, 1f, bothWalls.Opacity));

            var oneWall = new Map("..", "@#");
            Assert.IsTrue(GridLineOfSight.IsClear(0f, 0f, 1f, 1f, oneWall.Opacity));
        }

        [Test]
        public void Recompute_ClearsPreviousResult()
        {
            var map = OpenField(31, new GridPosition(5, 5));
            var fov = new FieldOfView(map.Width, map.Height);
            fov.Compute(map.Player, 5, map.Opacity);
            Assert.IsTrue(fov.IsVisible(new GridPosition(0, 0)));

            fov.Compute(new GridPosition(25, 25), 5, map.Opacity);
            Assert.IsFalse(fov.IsVisible(new GridPosition(0, 0)));
            Assert.AreEqual(121, fov.VisibleCells.Count);
            Assert.AreEqual(new GridPosition(25, 25), fov.Origin);
        }
    }
}
