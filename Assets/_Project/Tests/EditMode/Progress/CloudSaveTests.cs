using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Maze.Application.Platform;
using Maze.Application.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Maze.Tests.EditMode.Progress
{
    public sealed class CloudSaveTests
    {
        [Test]
        public void CloudSave_WinsOverLocal_AndLocalFollowsIt()
        {
            var local = new MemoryStorage { Json = Json(kills: 3) };
            var cloud = new FakeCloud { Json = Json(kills: 7) };
            using var save = Start(local, cloud);

            Assert.AreEqual(7, save.Data.TotalKills);
            Assert.IsTrue(save.UsesCloud);
            StringAssert.Contains("\"TotalKills\":7", local.Json, "The browser's copy is replaced by the cloud save.");
            Assert.AreEqual(0, cloud.Writes);
        }

        [Test]
        public void EmptyCloud_GetsTheLocalSave()
        {
            var local = new MemoryStorage { Json = Json(kills: 3) };
            var cloud = new FakeCloud();
            using var save = Start(local, cloud);

            Assert.AreEqual(3, save.Data.TotalKills);
            Assert.AreEqual(1, cloud.Writes);
            StringAssert.Contains("\"TotalKills\":3", cloud.Json);
        }

        [Test]
        public void Saves_GoLocallyAtOnce_AndToTheCloudAsOneWrite()
        {
            var local = new MemoryStorage();
            var cloud = new FakeCloud();
            using var save = Start(local, cloud);
            save.CloudDelay = 3600f; // only the flush writes in this test

            save.Data.TotalKills = 1;
            save.Save();
            save.Data.TotalKills = 2;
            save.Save();

            Assert.AreEqual(2, local.Writes);
            Assert.AreEqual(0, cloud.Writes, "The cloud waits for more changes.");

            save.FlushCloudAsync().GetAwaiter().GetResult();
            Assert.AreEqual(1, cloud.Writes);
            StringAssert.Contains("\"TotalKills\":2", cloud.Json);

            save.FlushCloudAsync().GetAwaiter().GetResult();
            Assert.AreEqual(1, cloud.Writes, "Nothing new: nothing written.");
        }

        [Test]
        public void CloudThatFailedToAnswer_IsNotWritten()
        {
            LogAssert.ignoreFailingMessages = true; // the failure is logged as an error
            var local = new MemoryStorage { Json = Json(kills: 3) };
            var cloud = new FakeCloud { FailRead = true };
            using var save = Start(local, cloud);

            Assert.AreEqual(3, save.Data.TotalKills);
            Assert.IsFalse(save.UsesCloud);
            save.Save();
            save.FlushCloudAsync().GetAwaiter().GetResult();
            Assert.AreEqual(0, cloud.Writes);
        }

        [Test]
        public void NoCloud_OnlyLocal()
        {
            var local = new MemoryStorage { Json = Json(kills: 5) };
            using var save = new SaveService(local, new PlatformService(new GenericPlatform()));
            save.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(5, save.Data.TotalKills);
            Assert.IsFalse(save.UsesCloud);
        }

        private static SaveService Start(MemoryStorage local, FakeCloud cloud)
        {
            var platform = new PlatformService(new CloudPlatform(cloud));
            platform.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
            var save = new SaveService(local, platform);
            save.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
            return save;
        }

        private static string Json(int kills) => JsonUtility.ToJson(new SaveData { TotalKills = kills });

        private sealed class MemoryStorage : ISaveStorage
        {
            public string Json;
            public int Writes;

            public string Read() => Json;

            public void Write(string json)
            {
                Json = json;
                Writes++;
            }
        }

        private sealed class FakeCloud : ICloudSave
        {
            public string Json;
            public int Writes;
            public bool FailRead;

            public UniTask<string> ReadAsync(CancellationToken cancellation) =>
                FailRead ? UniTask.FromException<string>(new InvalidOperationException("No network.")) : UniTask.FromResult(Json);

            public UniTask WriteAsync(string json, CancellationToken cancellation)
            {
                Json = json;
                Writes++;
                return UniTask.CompletedTask;
            }
        }

        private sealed class CloudPlatform : IGamePlatform
        {
            public CloudPlatform(ICloudSave cloud) => CloudSave = cloud;
            public string Name => "Test";
            public UniTask InitializeAsync(CancellationToken cancellation) => UniTask.CompletedTask;
            public string Language => null;
            public ICloudSave CloudSave { get; }
            public void LoadingFinished() { }
            public void GameplayStarted() { }
            public void GameplayStopped() { }
            public void HappyMoment() { }
        }
    }
}
