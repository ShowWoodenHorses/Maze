namespace Maze.Core.Validation
{
    public static class ValidationCodes
    {
        // Structural
        public const string GeometryCorrupted = "GeometryCorrupted";
        public const string OddSize = "OddSize";
        public const string NoPlayerStart = "NoPlayerStart";
        public const string NoExit = "NoExit";
        public const string EmptyId = "EmptyId";
        public const string DuplicateId = "DuplicateId";
        public const string OutOfBounds = "OutOfBounds";
        public const string InsideWall = "InsideWall";
        public const string DoorNotOnDoorCell = "DoorNotOnDoorCell";
        public const string DoorCellWithoutDoor = "DoorCellWithoutDoor";
        public const string Intersection = "Intersection";
        public const string ZombieOnPlayerStart = "ZombieOnPlayerStart";
        public const string MissingKeyReference = "MissingKeyReference";
        public const string UnusedKey = "UnusedKey";
        public const string KeySharedByDoors = "KeySharedByDoors";
        public const string MissingPatrolReference = "MissingPatrolReference";
        public const string InvalidPatrolPoint = "InvalidPatrolPoint";
        public const string PatrolTooShort = "PatrolTooShort";
        public const string UnusedPatrol = "UnusedPatrol";
        public const string MissingDefinition = "MissingDefinition";
        public const string InvalidFragmentRegion = "InvalidFragmentRegion";
        public const string FragmentOverlap = "FragmentOverlap";

        // Visual
        public const string NoVisualTheme = "NoVisualTheme";
        public const string MissingVisualSet = "MissingVisualSet";
        public const string VisualSetKindMismatch = "VisualSetKindMismatch";
        public const string DuplicateVariantId = "DuplicateVariantId";
        public const string BrokenPrefabReference = "BrokenPrefabReference";
        public const string InvalidDefaultVariant = "InvalidDefaultVariant";
        public const string AssignmentsOutOfSync = "AssignmentsOutOfSync";
        public const string MissingVisual = "MissingVisual";
        public const string UnknownVariant = "UnknownVariant";
        public const string VariantDefinitionMismatch = "VariantDefinitionMismatch";
        public const string WallCategoryWithoutVariants = "WallCategoryWithoutVariants";
        public const string StaleWallVisual = "StaleWallVisual";
        public const string OverrideOutOfBounds = "OverrideOutOfBounds";
        public const string OverrideForMissingEntity = "OverrideForMissingEntity";
        public const string UniformDistribution = "UniformDistribution";
        public const string KeyDoorColorMismatch = "KeyDoorColorMismatch";
        public const string RepeatedKeyColor = "RepeatedKeyColor";
        public const string UnlockedDoorWithColor = "UnlockedDoorWithColor";

        // Gameplay
        public const string NoReachableExit = "NoReachableExit";
        public const string ExitUnreachable = "ExitUnreachable";
        public const string KeyUnreachable = "KeyUnreachable";
        public const string ObjectUnreachable = "ObjectUnreachable";
        public const string DoorWithoutPassage = "DoorWithoutPassage";
        public const string PatrolPointUnreachable = "PatrolPointUnreachable";
        public const string ShortestPath = "ShortestPath";
    }
}
