using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace Sandbox.CharacterSystem.Tests
{
    public sealed class Pcm16WaveTests
    {
        internal static byte[] Wave(short[] samples = null, int channels = 1, int rate = 24000, bool extraChunk = false)
        {
            samples = samples ?? new short[2400 * channels];
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(0);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)channels); writer.Write(rate);
                writer.Write(rate * channels * 2); writer.Write((short)(channels * 2)); writer.Write((short)16);
                if (extraChunk)
                { writer.Write(Encoding.ASCII.GetBytes("JUNK")); writer.Write(1); writer.Write((byte)7); writer.Write((byte)0); }
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
                foreach (short sample in samples) writer.Write(sample);
                writer.Seek(4, SeekOrigin.Begin); writer.Write((int)memory.Length - 8);
                return memory.ToArray();
            }
        }

        [TestCase(1, 16000)]
        [TestCase(2, 48000)]
        public void PreservesRateChannelsAndSignedSampleValues(int channels, int rate)
        {
            var bytes = Wave(new short[] { short.MinValue, 0, short.MaxValue, -16384 }, channels, rate, true);
            Assert.That(Pcm16Wave.TryRead(bytes, out var info, out var error), Is.True, error);
            Assert.That(info.Channels, Is.EqualTo(channels));
            Assert.That(info.SampleRate, Is.EqualTo(rate));
            Assert.That(info.Frames, Is.EqualTo(4 / channels));
            Assert.That(Pcm16Wave.Decode(bytes, info), Is.EqualTo(new[] { -1f, 0f, 32767 / 32768f, -.5f }));
        }

        [TestCase(20, 3)] // IEEE float instead of PCM
        [TestCase(34, 8)] // unsupported bit depth
        [TestCase(22, 3)] // unsupported channel count
        [TestCase(32, 7)] // invalid block alignment
        [TestCase(28, 1)] // invalid byte rate
        public void RejectsUnsupportedOrInconsistentFormat(int position, byte value)
        {
            var bytes = Wave(); bytes[position] = value;
            Assert.That(Pcm16Wave.TryRead(bytes, out _, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void RejectsTruncatedEmptyAndOversizedChunksWithoutReadingPastPayload()
        {
            var bytes = Wave();
            Array.Resize(ref bytes, bytes.Length - 1);
            Assert.That(Pcm16Wave.TryRead(bytes, out _, out _), Is.False);
            Assert.That(Pcm16Wave.TryRead(Wave(Array.Empty<short>()), out _, out _), Is.False);
            bytes = Wave();
            for (int i = 40; i < 44; i++) bytes[i] = 255;
            Assert.That(Pcm16Wave.TryRead(bytes, out _, out _), Is.False);
        }

        [Test]
        public void RejectsLongRepliesAndDataNotAlignedToStereoFrames()
        {
            Assert.That(Pcm16Wave.TryRead(Wave(new short[8000 * 61], 1, 8000), out _, out _), Is.False);
            Assert.That(Pcm16Wave.TryRead(Wave(new short[3], 2), out _, out _), Is.False);
        }

        [Test]
        public void WebResamplingKeepsDurationAndDownmixesStereo()
        {
            var stereo = new float[32000];
            for(int i=0;i<stereo.Length;i+=2){stereo[i]=.75f;stereo[i+1]=.25f;}
            var output=Pcm16Wave.ResampleMono(stereo,2,16000,48000);
            Assert.That(output.Length,Is.EqualTo(48000));
            Assert.That(output[0],Is.EqualTo(.5f));Assert.That(output[47999],Is.EqualTo(.5f));
            Assert.That(Pcm16Wave.ResampleMono(new[]{1f},1,8000,48000).Length,Is.EqualTo(6));
        }
    }
}
