namespace Maze.Core.Visual
{
    /// <summary>What a visual set represents. Each kind has its own slot in <see cref="VisualTheme"/>.</summary>
    public enum VisualKind
    {
        Floor = 0,
        Wall = 1,
        Door = 2,
        Exit = 3,
        Key = 4,
        Medkit = 5,
        Weapon = 6,
        Zombie = 7,
        MapFragment = 8,
    }

    /// <summary>
    /// Sub-category of a variant. Walls are categorized by their wall neighbours (ТЗ §32–33);
    /// everything else uses <see cref="General"/>. <see cref="Special"/> variants are never picked
    /// automatically: they exist for manual designer overrides only.
    /// </summary>
    public enum VisualCategory
    {
        General = 0,
        Straight = 1,
        Corner = 2,
        TJunction = 3,
        End = 4,
        Cross = 5,
        Isolated = 6,
        Special = 7,
    }
}
