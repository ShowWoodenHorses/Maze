using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Application.Assets;
using Maze.Application.Services;
using Maze.Core.Level;

namespace Maze.Application.Levels
{
    public interface ILevelCatalog
    {
        IReadOnlyList<LevelCatalogEntry> Levels { get; }

        /// <summary>Null when the level is not in the catalog.</summary>
        LevelCatalogEntry Find(string levelId);
    }

    /// <summary>Loads the Addressable <see cref="LevelCatalog"/> once and keeps it resident for the application lifetime.</summary>
    public sealed class LevelCatalogService : ILevelCatalog, IApplicationService, IDisposable
    {
        private readonly IAddressablesService _addressables;
        private IAssetOwner _assets;
        private LevelCatalog _catalog;

        public LevelCatalogService(IAddressablesService addressables)
        {
            _addressables = addressables;
        }

        public string Name => "Level Catalog";

        public IReadOnlyList<LevelCatalogEntry> Levels =>
            _catalog != null ? _catalog.Levels : (IReadOnlyList<LevelCatalogEntry>)Array.Empty<LevelCatalogEntry>();

        public LevelCatalogEntry Find(string levelId) => _catalog != null ? _catalog.Find(levelId) : null;

        public async UniTask InitializeAsync(CancellationToken cancellation)
        {
            _assets = _addressables.CreateOwner("Application: Level Catalog");
            _catalog = await _assets.LoadAsync<LevelCatalog>(LevelCatalog.Address, cancellation);
        }

        public void Dispose()
        {
            _assets?.Dispose();
            _assets = null;
            _catalog = null;
        }
    }
}
