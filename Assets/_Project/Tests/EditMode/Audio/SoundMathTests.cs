using Maze.Core.Audio;
using Maze.Core.Common;
using NUnit.Framework;
using UnityEngine;

namespace Maze.Tests.EditMode.Audio
{
    public class SoundMathTests
    {
        [Test]
        public void Attenuation_FullNear_SilentAtRange_FallsSmoothly()
        {
            const float range = 10f;
            Assert.AreEqual(1f, SoundMath.Attenuation(0f, range));
            Assert.AreEqual(1f, SoundMath.Attenuation(range * SoundMath.NearShare, range));
            Assert.AreEqual(0f, SoundMath.Attenuation(range, range));
            Assert.AreEqual(0f, SoundMath.Attenuation(range * 3f, range));

            var previous = 1f;
            for (var d = 1.5f; d < range; d += 0.5f)
            {
                var value = SoundMath.Attenuation(d, range);
                Assert.That(value, Is.LessThanOrEqualTo(previous), $"Never louder farther away ({d}).");
                Assert.That(value, Is.InRange(0f, 1f));
                previous = value;
            }
        }

        [Test]
        public void Attenuation_ZeroRange_IsEverywhere()
        {
            Assert.AreEqual(1f, SoundMath.Attenuation(100f, 0f));
        }

        [Test]
        public void Pan_SideOfTheListener_NeverOneEarOnly()
        {
            Assert.AreEqual(0f, SoundMath.Pan(0f));
            Assert.That(SoundMath.Pan(3f), Is.GreaterThan(0f), "East = right.");
            Assert.That(SoundMath.Pan(-3f), Is.LessThan(0f), "West = left.");
            Assert.AreEqual(SoundMath.MaxPan, SoundMath.Pan(100f), 1e-6f);
            Assert.AreEqual(-SoundMath.MaxPan, SoundMath.Pan(-100f), 1e-6f);
        }

        [Test]
        public void PickVariant_NeverRepeatsThePrevious_AndCoversAll()
        {
            var random = new DeterministicRandom(7);
            var seen = new bool[4];
            var previous = -1;
            for (var i = 0; i < 400; i++)
            {
                var index = SoundMath.PickVariant(4, previous, random.NextDouble());
                Assert.That(index, Is.InRange(0, 3));
                Assert.AreNotEqual(previous, index, "Same variant twice in a row.");
                seen[index] = true;
                previous = index;
            }

            CollectionAssert.DoesNotContain(seen, false, "Every variant plays.");
            Assert.AreEqual(0, SoundMath.PickVariant(1, 0, 0.9), "A single variant repeats.");
        }

        [Test]
        public void Catalog_SilencedWeapons_AndZombiePitch_ById()
        {
            var catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            try
            {
                catalog.MutableSilencedWeapons.Add("pistol_silenced");
                catalog.MutableZombieVoices.Add(new ZombieVoice { ZombieId = "hunter", Pitch = 0.9f });

                Assert.IsTrue(catalog.IsSilenced("pistol_silenced"));
                Assert.IsFalse(catalog.IsSilenced("smg"));
                Assert.IsFalse(catalog.IsSilenced(null));
                Assert.AreEqual(0.9f, catalog.ZombiePitch("hunter"));
                Assert.AreEqual(1f, catalog.ZombiePitch("walker"), "Not listed = normal pitch.");
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }
    }
}
