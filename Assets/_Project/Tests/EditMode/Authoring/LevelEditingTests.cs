using System.Linq;
using Maze.Core.Authoring;
using Maze.Core.Grid;
using Maze.Core.Level;
using Maze.Core.Validation;
using Maze.Core.Visual;
using Maze.Tests.EditMode.Visual;
using NUnit.Framework;

namespace Maze.Tests.EditMode.Authoring
{
    public class LevelEditingTests
    {
        private VisualFixture _fixture;
        private LevelData Level => _fixture.Level;

        // Generated 21x21 maze: (1,1) is a room (floor), (2,2) a pillar (always wall).
        private static readonly GridPosition Room = new GridPosition(1, 1);
        private static readonly GridPosition Pillar = new GridPosition(2, 2);

        [SetUp]
        public void SetUp()
        {
            _fixture = new VisualFixture();
            LevelAuthoring.GenerateNew(Level);
        }

        [TearDown]
        public void TearDown() => _fixture.Dispose();

        private VisualChoice[] AllCellVisuals() =>
            Enumerable.Range(0, Level.Geometry.CellCount)
                .SelectMany(i => CellLayers.All.Select(layer => VisualResolver.ResolveCell(Level, Level.Geometry.ToPosition(i), layer)))
                .ToArray();

        [Test]
        public void PaintDoor_CreatesDoorData_PaintFloor_RemovesIt()
        {
            Assert.IsTrue(LevelEditing.SetCellType(Level, Pillar, CellType.Door));
            var door = Level.Doors.Single();
            Assert.AreEqual(Pillar, door.Position);
            Assert.IsFalse(VisualResolver.ResolveObject(Level, door).IsEmpty);

            Assert.IsTrue(LevelEditing.SetCellType(Level, Pillar, CellType.Floor));
            Assert.IsEmpty(Level.Doors);
            Assert.IsTrue(Level.VisualData.GetObjectAssignment(door.Id).IsEmpty);
            Assert.IsFalse(LevelEditing.SetCellType(Level, Pillar, CellType.Floor), "No-op returns false.");
        }

        [Test]
        public void Paint_UpdatesVisualsLocally_LikeFullRegenerate()
        {
            var floorUnderPillar = VisualResolver.ResolveCell(Level, Pillar, CellLayer.Floor);
            LevelEditing.SetCellType(Level, Pillar, CellType.Floor);
            var local = AllCellVisuals();

            LevelAuthoring.RegenerateVisuals(Level, clearOverrides: false);
            CollectionAssert.AreEqual(AllCellVisuals(), local);

            Assert.IsTrue(VisualResolver.ResolveCell(Level, Pillar, CellLayer.Wall).IsEmpty);
            Assert.AreEqual(floorUnderPillar, VisualResolver.ResolveCell(Level, Pillar, CellLayer.Floor),
                "The floor that was under the wall stays the same when the wall is removed.");
        }

        [Test]
        public void WallOverride_DroppedWhenCellStopsBeingWall_FloorOverrideAlwaysKept()
        {
            Level.VisualData.SetCellOverride(Pillar, CellLayer.Wall, new VisualChoice("wall_cross"));
            Level.VisualData.SetCellOverride(Pillar, CellLayer.Floor, new VisualChoice("floor_special_blood"));

            LevelEditing.SetCellType(Level, Pillar, CellType.Floor);
            Assert.IsFalse(Level.VisualData.TryGetCellOverride(Pillar, CellLayer.Wall, out _));
            Assert.IsTrue(Level.VisualData.TryGetCellOverride(Pillar, CellLayer.Floor, out _));

            LevelEditing.SetCellType(Level, Pillar, CellType.Door);
            LevelEditing.SetCellType(Level, Pillar, CellType.Wall);
            Assert.AreEqual("floor_special_blood", VisualResolver.ResolveCell(Level, Pillar, CellLayer.Floor).VariantId,
                "Floor under any cell type keeps the designer's choice.");
        }

        [Test]
        public void KeyForDoor_IsLinkedAndColoured_RemovingKeyUnlocksDoor()
        {
            LevelEditing.SetCellType(Level, Pillar, CellType.Door);
            var door = Level.Doors.Single();
            Assert.IsNull(VisualAssigner.ResolvedColor(Level, door));

            var key = LevelEditing.AddKey(Level, Room, door);
            Assert.AreEqual(key.Id, door.KeyId);
            var color = VisualAssigner.ResolvedColor(Level, door);
            Assert.IsNotNull(color);
            Assert.AreEqual(color, _fixture.Key.FindVariant(VisualResolver.ResolveObject(Level, key).VariantId).ColorTag);

            LevelEditing.Remove(Level, key);
            Assert.IsFalse(door.RequiresKey);
            Assert.IsNull(VisualAssigner.ResolvedColor(Level, door), "Unlocked door loses its colour.");
        }

        [Test]
        public void LinkingKeyToSecondDoor_UnlinksFirst_OneKeyOneDoor()
        {
            LevelEditing.SetCellType(Level, new GridPosition(2, 4), CellType.Door);
            LevelEditing.SetCellType(Level, new GridPosition(4, 2), CellType.Door);
            var first = Level.Doors[0];
            var second = Level.Doors[1];
            var key = LevelEditing.AddKey(Level, Room, first);

            LevelEditing.LinkKey(Level, second, key);

            Assert.IsFalse(first.RequiresKey);
            Assert.AreEqual(key.Id, second.KeyId);
        }

        [Test]
        public void DoorOverride_RecoloursItsKey()
        {
            LevelEditing.SetCellType(Level, Pillar, CellType.Door);
            var door = Level.Doors.Single();
            var key = LevelEditing.AddKey(Level, Room, door);
            var other = VisualAssigner.ResolvedColor(Level, door) == "red" ? "blue" : "red";

            LevelEditing.SetObjectOverride(Level, door, new VisualChoice("door_" + other));

            Assert.AreEqual("key_" + other, VisualResolver.ResolveObject(Level, key).VariantId);
        }

        [Test]
        public void RemovingDoor_OpensPassage()
        {
            LevelEditing.SetCellType(Level, Pillar, CellType.Door);
            LevelEditing.Remove(Level, Level.Doors.Single());
            Assert.AreEqual(CellType.Floor, Level.Geometry.GetCell(Pillar));
            Assert.IsEmpty(Level.Doors);
        }

        [Test]
        public void MovingDoor_MovesItsCell()
        {
            LevelEditing.SetCellType(Level, Pillar, CellType.Door);
            var door = Level.Doors.Single();
            var target = new GridPosition(2, 4);

            Assert.IsTrue(LevelEditing.Move(Level, door, target));

            Assert.AreEqual(CellType.Floor, Level.Geometry.GetCell(Pillar));
            Assert.AreEqual(CellType.Door, Level.Geometry.GetCell(target));
            Assert.AreEqual(target, door.Position);
        }

        [Test]
        public void Rename_UpdatesReferencesAndKeepsVisuals_RejectsTakenIds()
        {
            LevelEditing.SetCellType(Level, Pillar, CellType.Door);
            var door = Level.Doors.Single();
            var key = LevelEditing.AddKey(Level, Room, door);
            var keyVisual = VisualResolver.ResolveObject(Level, key);

            Assert.IsTrue(LevelEditing.Rename(Level, key, "key_gold"));
            Assert.AreEqual("key_gold", door.KeyId);
            Assert.AreEqual(keyVisual, VisualResolver.ResolveObject(Level, key));

            Assert.IsFalse(LevelEditing.Rename(Level, key, door.Id));
            Assert.IsFalse(LevelEditing.Rename(Level, key, "  "));
        }

        [Test]
        public void Patrol_CreatedOnFirstPoint_RemovedWithZombie()
        {
            var zombie = LevelEditing.AddZombie(Level, Room, _fixture.Walker, Direction.East);
            var patrol = LevelEditing.AddPatrolPoint(Level, zombie, new GridPosition(1, 3));
            LevelEditing.AddPatrolPoint(Level, zombie, new GridPosition(3, 1));

            Assert.AreEqual(patrol.Id, zombie.PatrolId);
            Assert.AreEqual(2, patrol.Points.Count);

            LevelEditing.RemoveLastPatrolPoint(Level, zombie);
            Assert.AreEqual(1, patrol.Points.Count);

            LevelEditing.Remove(Level, zombie);
            Assert.IsEmpty(Level.Patrols);
        }

        [Test]
        public void EditedLevel_StaysValid()
        {
            var room2 = new GridPosition(3, 1);
            var wallBetween = new GridPosition(2, 1);
            LevelEditing.SetCellType(Level, wallBetween, CellType.Floor);
            LevelEditing.SetCellType(Level, wallBetween, CellType.Door);
            var door = Level.Doors.Single();

            // Place the key next to the start so it is reachable from it.
            var start = Level.PlayerStarts[0].Position;
            var keyCell = DirectionExtensions.All.Select(start.Neighbour)
                .First(p => Level.Geometry.GetCell(p) == CellType.Floor);
            LevelEditing.AddKey(Level, keyCell, door);
            LevelEditing.AddWeapon(Level, room2 == start ? Room : room2, _fixture.Pistol);

            var report = LevelValidator.Validate(Level);
            Assert.IsFalse(report.Has(ValidationCodes.StaleWallVisual), string.Join("\n", report.Issues));
            Assert.IsFalse(report.Has(ValidationCodes.KeyDoorColorMismatch));
            Assert.IsFalse(report.Has(ValidationCodes.MissingVisual));
            Assert.IsFalse(report.Has(ValidationCodes.DoorCellWithoutDoor));
        }
    }
}
