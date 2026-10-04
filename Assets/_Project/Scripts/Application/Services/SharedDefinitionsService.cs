using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Application.Assets;
using Maze.Core.Definitions;
using Maze.Core.Visual;

namespace Maze.Application.Services
{
    /// <summary>
    /// Definitions shared by every level (player parameters and look), loaded once and kept resident
    /// for the application lifetime (ТЗ §84: shared assets may stay resident).
    /// </summary>
    public sealed class SharedDefinitionsService : IApplicationService, IDisposable
    {
        private readonly IAddressablesService _addressables;
        private IAssetOwner _assets;

        public SharedDefinitionsService(IAddressablesService addressables)
        {
            _addressables = addressables;
        }

        public string Name => "Shared Definitions";

        public PlayerDefinition Player { get; private set; }
        public PlayerVisualDefinition PlayerVisual { get; private set; }

        public async UniTask InitializeAsync(CancellationToken cancellation)
        {
            _assets = _addressables.CreateOwner("Application: Shared Definitions");
            (Player, PlayerVisual) = await UniTask.WhenAll(
                _assets.LoadAsync<PlayerDefinition>(PlayerDefinition.Address, cancellation),
                _assets.LoadAsync<PlayerVisualDefinition>(PlayerVisualDefinition.Address, cancellation));
        }

        public void Dispose()
        {
            _assets?.Dispose();
            _assets = null;
            Player = null;
            PlayerVisual = null;
        }
    }
}
