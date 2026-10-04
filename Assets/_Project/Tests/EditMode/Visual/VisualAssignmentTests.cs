using System.Linq;
using Maze.Core.Authoring;
using Maze.Core.Grid;
using Maze.Core.Level;
using Maze.Core.Visual;
using NUnit.Framework;

namespace Maze.Tests.EditMode.Visual
{
    public class VisualAssignmentTests
    {
        private static VisualChoice[] ResolveAllCells(LevelData level)
        {
            var geometry = level.Geometry;
            return Enumerable.Range(0, geometry.CellCount)
                .Select(i => VisualResolver.ResolveCell(level, geometry.ToPosition(i)))
                .ToArray();
        }

        [Test]
        public void GenerateNew_AssignsEveryCell()
        {
            using (var f = new VisualFixture())
            {
                LevelAuthoring.GenerateNew(f.Level);

                Assert.AreEqual(f.Level.Geometry.CellCount, f.Level.VisualData.CellAssignmentCount);
                for (var i = 0; i < f.Level.Geometry.CellCount; i++)
                    Assert.IsFalse(f.Level.VisualData.GetCellAssignment(i).IsEmpty, $"Cell {f.Level.Geometry.ToPosition(i)}");

                foreach (var exit in f.Level.Exits)
                    Assert.AreEqual("exit_01", VisualResolver.ResolveObject(f.Level, exit).VariantId);
            }
        }

        [Test]
        public void SameSeedsAndSets_GiveIdenticalVisuals()
        {
            using (var a = new VisualFixture(mazeSeed: 4, visualSeed: 9))
            using (var b = new VisualFixture(mazeSeed: 4, visualSeed: 9))
            {
                LevelAuthoring.GenerateNew(a.Level);
                LevelAuthoring.GenerateNew(b.Level);
                CollectionAssert.AreEqual(ResolveAllCells(a.Level), ResolveAllCells(b.Level));
            }
        }

        [Test]
        public void VisualSeed_IsIndependentFromMazeSeed()
        {
            using (var a = new VisualFixture(mazeSeed: 4, visualSeed: 1))
            using (var b = new VisualFixture(mazeSeed: 4, visualSeed: 2))
            {
                LevelAuthoring.GenerateNew(a.Level);
                LevelAuthoring.GenerateNew(b.Level);

                CollectionAssert.AreEqual(a.Level.Geometry.CopyCells(), b.Level.Geometry.CopyCells(), "Same maze.");
                CollectionAssert.AreNotEqual(ResolveAllCells(a.Level), ResolveAllCells(b.Level), "Different visuals.");
            }
        }

        [Test]
        public void ChangingVisualSeed_DoesNotChangeFinishedLevel_UntilRegenerate()
        {
            using (var f = new VisualFixture())
            {
                LevelAuthoring.GenerateNew(f.Level);
                var before = ResolveAllCells(f.Level);

                f.Level.Generation.VisualSeed = 777;
                CollectionAssert.AreEqual(before, ResolveAllCells(f.Level));

                LevelAuthoring.RegenerateVisuals(f.Level, clearOverrides: false);
                CollectionAssert.AreNotEqual(before, ResolveAllCells(f.Level));
            }
        }

        [Test]
        public void Weights_ApproximateDistribution_AndZeroOrSpecialNeverPicked()
        {
            using (var f = new VisualFixture(width: 80, height: 80, loopDensity: 1f))
            {
                LevelAuthoring.GenerateNew(f.Level);
                var counts = VisualResolver.CountCellVariants(f.Level, VisualKind.Floor);
                var total = (double)counts.Values.Sum();

                Assert.Greater(total, 3000);
                Assert.AreEqual(0.80, counts["floor_01"] / total, 0.03);
                Assert.AreEqual(0.10, counts["floor_02"] / total, 0.03);
                Assert.AreEqual(0.10, counts["floor_03"] / total, 0.03);
                Assert.IsFalse(counts.ContainsKey("floor_unused"));
                Assert.IsFalse(counts.ContainsKey("floor_special_blood"));
            }
        }

        [Test]
        public void Walls_UseVariantOfTheirContextCategory_OrExplicitDefault()
        {
            using (var f = new VisualFixture(loopDensity: 1f))
            {
                LevelAuthoring.GenerateNew(f.Level);
                var geometry = f.Level.Geometry;
                var sawDefault = false;

                for (var i = 0; i < geometry.CellCount; i++)
                {
                    var p = geometry.ToPosition(i);
                    if (geometry.GetCell(p) != CellType.Wall)
                        continue;

                    var (category, rotation) = WallShapes.Classify(new CellVisualContext(geometry, p).WallConnections);
                    var choice = VisualResolver.ResolveCell(f.Level, p);
                    var variant = f.Wall.FindVariant(choice.VariantId);

                    Assert.AreEqual(rotation, choice.Rotation, $"Rotation at {p}");
                    if (category == VisualCategory.Isolated)
                    {
                        // The test set has no Isolated variants: explicit default is used, never a random other variant.
                        Assert.AreEqual("wall_default", choice.VariantId);
                        sawDefault = true;
                    }
                    else
                    {
                        Assert.AreEqual(category, variant.Category, $"Category at {p}");
                    }
                }

                Assert.IsTrue(sawDefault, "Full loop density should produce isolated pillars.");
            }
        }

        [Test]
        public void NoMatchingVariant_AndNoDefault_LeavesCellEmpty()
        {
            using (var f = new VisualFixture())
            {
                f.Floor.MutableVariants.Clear();
                LevelAuthoring.GenerateNew(f.Level);

                var floor = f.Level.PlayerStarts[0].Position;
                Assert.IsTrue(VisualResolver.ResolveCell(f.Level, floor, out var source).IsEmpty);
                Assert.AreEqual(VisualSource.None, source);
            }
        }

        [Test]
        public void Overrides_WinOverAssignments_SurviveRegenerate_ClearedOnRequestOrGenerateNew()
        {
            using (var f = new VisualFixture())
            {
                LevelAuthoring.GenerateNew(f.Level);
                var cell = f.Level.PlayerStarts[0].Position;
                var exit = f.Level.Exits[0];

                f.Level.VisualData.SetCellOverride(cell, new VisualChoice("floor_special_blood"));
                f.Level.VisualData.SetObjectOverride(exit.Id, new VisualChoice("exit_01", 2));

                Assert.AreEqual("floor_special_blood", VisualResolver.ResolveCell(f.Level, cell, out var source).VariantId);
                Assert.AreEqual(VisualSource.Override, source);

                f.Level.Generation.VisualSeed = 99;
                LevelAuthoring.RegenerateVisuals(f.Level, clearOverrides: false);
                Assert.AreEqual("floor_special_blood", VisualResolver.ResolveCell(f.Level, cell).VariantId);
                Assert.AreEqual(2, VisualResolver.ResolveObject(f.Level, exit).Rotation);

                LevelAuthoring.RegenerateVisuals(f.Level, clearOverrides: true);
                Assert.AreNotEqual(VisualSource.Override, Resolve(f.Level, cell));
                Assert.IsEmpty(f.Level.VisualData.ObjectOverrides);

                f.Level.VisualData.SetCellOverride(cell, new VisualChoice("floor_special_blood"));
                LevelAuthoring.GenerateNew(f.Level);
                Assert.IsEmpty(f.Level.VisualData.CellOverrides);
            }
        }

        [Test]
        public void ClearCellOverride_RestoresSavedAssignment()
        {
            using (var f = new VisualFixture())
            {
                LevelAuthoring.GenerateNew(f.Level);
                var cell = f.Level.PlayerStarts[0].Position;
                var saved = VisualResolver.ResolveCell(f.Level, cell);

                f.Level.VisualData.SetCellOverride(cell, new VisualChoice("floor_03"));
                Assert.IsTrue(f.Level.VisualData.ClearCellOverride(cell));

                Assert.AreEqual(saved, VisualResolver.ResolveCell(f.Level, cell, out var source));
                Assert.AreEqual(VisualSource.Assignment, source);
            }
        }

        [Test]
        public void MissingAssignment_FallsBackToSetDefault()
        {
            using (var f = new VisualFixture())
            {
                LevelAuthoring.GenerateNew(f.Level);
                f.Level.VisualData.ResetCellAssignments(f.Level.Geometry.CellCount);

                var corner = new GridPosition(0, 0);
                var choice = VisualResolver.ResolveCell(f.Level, corner, out var source);
                Assert.AreEqual(VisualSource.Default, source);
                Assert.AreEqual("wall_default", choice.VariantId);
                Assert.AreEqual(WallShapes.Classify(new CellVisualContext(f.Level.Geometry, corner).WallConnections).Rotation,
                    choice.Rotation);
            }
        }

        [Test]
        public void ReassignAround_MatchesFullReassignment_AfterGeometryEdit()
        {
            using (var f = new VisualFixture())
            {
                LevelAuthoring.GenerateNew(f.Level);
                var edited = new GridPosition(2, 1);
                f.Level.Geometry.SetCell(edited, f.Level.Geometry.GetCell(edited) == CellType.Wall ? CellType.Floor : CellType.Wall);

                VisualAssigner.ReassignAround(f.Level, edited);
                var local = ResolveAllCells(f.Level);

                LevelAuthoring.RegenerateVisuals(f.Level, clearOverrides: false);
                CollectionAssert.AreEqual(ResolveAllCells(f.Level), local);
            }
        }

        [Test]
        public void Objects_RespectDefinitionFilter_DoorOrientation_AndRemoval()
        {
            using (var f = new VisualFixture(width: 12, height: 12, loopDensity: 0f))
            {
                LevelAuthoring.GenerateNew(f.Level);
                var level = f.Level;

                // Horizontal corridor cell between rooms (1,1) and (3,1) -> walls North/South -> rotation 1.
                level.Geometry.SetCell(new GridPosition(1, 1), CellType.Floor);
                level.Geometry.SetCell(new GridPosition(2, 1), CellType.Door);
                level.Geometry.SetCell(new GridPosition(3, 1), CellType.Floor);
                level.Geometry.SetCell(new GridPosition(2, 0), CellType.Wall);
                level.Geometry.SetCell(new GridPosition(2, 2), CellType.Wall);

                level.MutableDoors.Add(new DoorData("door_1", new GridPosition(2, 1)));
                for (var i = 0; i < 20; i++)
                    level.MutableWeapons.Add(new WeaponPickupData($"weapon_{i + 1}", new GridPosition(1, 1), i % 2 == 0 ? f.Pistol : f.Knife));

                LevelAuthoring.RegenerateVisuals(level, clearOverrides: false);

                Assert.AreEqual(1, VisualResolver.ResolveObject(level, level.Doors[0]).Rotation);
                foreach (var weapon in level.Weapons)
                {
                    var id = VisualResolver.ResolveObject(level, weapon).VariantId;
                    Assert.AreSame(weapon.Definition, f.Weapon.FindVariant(id).Definition, weapon.Id);
                }

                level.MutableDoors.Clear();
                LevelAuthoring.RegenerateVisuals(level, clearOverrides: false);
                Assert.IsTrue(level.VisualData.GetObjectAssignment("door_1").IsEmpty);
            }
        }

        [Test]
        public void NoTheme_ProducesNoAssignmentsWithoutErrors()
        {
            using (var f = new VisualFixture())
            {
                f.Level.VisualTheme = null;
                LevelAuthoring.GenerateNew(f.Level);
                Assert.IsTrue(VisualResolver.ResolveCell(f.Level, new GridPosition(0, 0)).IsEmpty);
            }
        }

        private static VisualSource Resolve(LevelData level, GridPosition cell)
        {
            VisualResolver.ResolveCell(level, cell, out var source);
            return source;
        }
    }
}
