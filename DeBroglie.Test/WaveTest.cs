﻿using NUnit.Framework;
using System;
using DeBroglie;
using DeBroglie.Wfc;
using DeBroglie.Trackers;

namespace DeBroglie.Test
{
    [TestFixture]
    public class WaveTest
    {
        [Test]
        public void TestWave()
        {
            var r = new Random();
            var frequencies = new double[] { 2, 1, 1 };
            var wave = new Wave(frequencies.Length, 2);
            var entropyTracker = new EntropyTracker();
            entropyTracker.Init(wave, frequencies, null);

            Assert.IsFalse(wave.RemovePossibility(0, 0));
            entropyTracker.DoBan(0, 0);
            Assert.AreEqual(0, entropyTracker.GetRandomIndex(r.NextDouble));

            Assert.IsFalse(wave.RemovePossibility(1, 2));
            entropyTracker.DoBan(1, 2);
            Assert.AreEqual(1, entropyTracker.GetRandomIndex(r.NextDouble));

            Assert.IsFalse(wave.RemovePossibility(1, 0));
            entropyTracker.DoBan(1, 0);
            Assert.AreEqual(0, entropyTracker.GetRandomIndex(r.NextDouble));

            Assert.IsFalse(wave.RemovePossibility(0, 1));
            entropyTracker.DoBan(0, 1);
            Assert.AreEqual(-1, entropyTracker.GetRandomIndex(r.NextDouble));

            Assert.IsTrue(wave.RemovePossibility(0, 2));
            entropyTracker.DoBan(0, 2);
        }

        [Test]
        public void TestWave2()
        {
            var r = new Random();
            var frequencies = new double[] { 2, 1, 1 };
            var wave = new Wave(frequencies.Length, 2);
            var entropyTracker = new HeapEntropyTracker();
            entropyTracker.Init(wave, frequencies, null, r.NextDouble);

            Assert.IsFalse(wave.RemovePossibility(0, 0));
            entropyTracker.DoBan(0, 0);
            Assert.AreEqual(0, entropyTracker.GetRandomIndex(r.NextDouble));

            Assert.IsFalse(wave.RemovePossibility(1, 2));
            entropyTracker.DoBan(1, 2);
            Assert.AreEqual(1, entropyTracker.GetRandomIndex(r.NextDouble));

            Assert.IsFalse(wave.RemovePossibility(1, 0));
            entropyTracker.DoBan(1, 0);
            Assert.AreEqual(0, entropyTracker.GetRandomIndex(r.NextDouble));

            Assert.IsFalse(wave.RemovePossibility(0, 1));
            entropyTracker.DoBan(0, 1);
            Assert.AreEqual(-1, entropyTracker.GetRandomIndex(r.NextDouble));

            Assert.IsTrue(wave.RemovePossibility(0, 2));
            entropyTracker.DoBan(0, 2);
        }
    }
}
