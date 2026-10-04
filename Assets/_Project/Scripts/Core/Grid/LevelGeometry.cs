using System;
using UnityEngine;

namespace Maze.Core.Grid
{
    /// <summary>
    /// Serialized cell layout of a level. Cells are stored row by row: index = y * Width + x.
    /// </summary>
    [Serializable]
    public sealed class LevelGeometry
    {
        [SerializeField] private int _width;
        [SerializeField] private int _height;
        [SerializeField] private CellType[] _cells;

        public LevelGeometry(int width, int height, CellType fill = CellType.Wall)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");

            _width = width;
            _height = height;
            _cells = new CellType[width * height];
            Fill(fill);
        }

        public int Width => _width;
        public int Height => _height;
        public int CellCount => _width * _height;

        /// <summary>False if the serialized data is corrupted (e.g. cell array does not match the size).</summary>
        public bool IsConsistent => _width > 0 && _height > 0 && _cells != null && _cells.Length == _width * _height;

        public bool IsInside(GridPosition position) =>
            position.X >= 0 && position.X < _width && position.Y >= 0 && position.Y < _height;

        public CellType GetCell(GridPosition position) => _cells[ToIndex(position)];

        public int ToIndex(GridPosition position)
        {
            if (!IsInside(position))
                throw new ArgumentOutOfRangeException(nameof(position), position, $"Outside of {_width}x{_height} grid.");

            return position.Y * _width + position.X;
        }

        public GridPosition ToPosition(int index)
        {
            if (index < 0 || index >= CellCount)
                throw new ArgumentOutOfRangeException(nameof(index), index, null);

            return new GridPosition(index % _width, index / _width);
        }

        internal void SetCell(GridPosition position, CellType type) => _cells[ToIndex(position)] = type;

        internal void Fill(CellType type)
        {
            for (var i = 0; i < _cells.Length; i++)
                _cells[i] = type;
        }

        internal CellType[] CopyCells() => (CellType[])_cells.Clone();
    }
}
