using System;

namespace Sandbox.CharacterSystem
{
    /// <summary>Complete RIFF/WAVE PCM16 files; no platform codecs or background threads.</summary>
    public static class Pcm16Wave
    {
        public const int MaxBytes = 16 * 1024 * 1024;

        public readonly struct Info
        {
            public readonly int Channels, SampleRate, DataOffset, DataLength;
            public int Frames => DataLength / (Channels * 2);
            public float Duration => (float)Frames / SampleRate;
            internal Info(int channels, int rate, int offset, int length)
            { Channels = channels; SampleRate = rate; DataOffset = offset; DataLength = length; }
        }

        public static bool TryRead(byte[] bytes, out Info info, out string error)
        {
            info = default;
            error = "Expected a complete PCM16 RIFF/WAVE file.";
            if (bytes == null || bytes.Length < 12 || bytes.Length > MaxBytes
                || !Tag(bytes, 0, "RIFF") || !Tag(bytes, 8, "WAVE")) return false;
            long end = (long)U32(bytes, 4) + 8;
            if (end != bytes.Length) { error = "WAV RIFF length does not match the payload."; return false; }
            int channels = 0, rate = 0, offset = -1, length = 0;
            bool formatFound = false;
            for (int cursor = 12; cursor < end;)
            {
                if (end - cursor < 8) return false;
                uint size = U32(bytes, cursor + 4);
                long next = cursor + 8L + size + (size & 1);
                if (next > end) { error = "WAV contains a truncated chunk."; return false; }
                int start = cursor + 8;
                if (Tag(bytes, cursor, "fmt "))
                {
                    if (formatFound || size < 16) return false;
                    formatFound = true;
                    channels = U16(bytes, start + 2);
                    uint sampleRate = U32(bytes, start + 4);
                    if (U16(bytes, start) != 1 || U16(bytes, start + 14) != 16
                        || (channels != 1 && channels != 2) || sampleRate < 8000 || sampleRate > 96000)
                    { error = "Use uncompressed PCM16 WAV, mono/stereo, 8–96 kHz."; return false; }
                    rate = (int)sampleRate;
                    if (U16(bytes, start + 12) != channels * 2 || U32(bytes, start + 8) != rate * channels * 2)
                    { error = "WAV block alignment or byte rate is invalid."; return false; }
                }
                else if (Tag(bytes, cursor, "data"))
                {
                    if (offset >= 0) { error = "Multiple WAV data chunks are unsupported."; return false; }
                    offset = start;
                    length = (int)size;
                }
                cursor = (int)next;
            }
            if (!formatFound || offset < 0 || length == 0 || length % (channels * 2) != 0) return false;
            info = new Info(channels, rate, offset, length);
            if (info.Duration > 60) { error = "A demo reply must be at most 60 seconds."; info = default; return false; }
            error = null;
            return true;
        }

        public static float[] Decode(byte[] bytes, Info info)
        {
            var samples = new float[info.DataLength / 2];
            for (int i = 0, p = info.DataOffset; i < samples.Length; i++, p += 2)
                samples[i] = (short)U16(bytes, p) / 32768f;
            return samples;
        }

        public static float[] ResampleMono(float[] input, int channels, int sourceRate, int targetRate)
        {
            int sourceFrames = input.Length / channels;
            int frames = Math.Max(1, (int)Math.Round((double)sourceFrames * targetRate / sourceRate));
            var output = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                double position = Math.Min(sourceFrames - 1, (double)i * sourceRate / targetRate);
                int a = (int)position, b = Math.Min(a + 1, sourceFrames - 1);
                float t = (float)(position - a);
                for (int c = 0; c < channels; c++) output[i] += (input[a * channels + c] * (1 - t) + input[b * channels + c] * t) / channels;
            }
            return output;
        }

        static bool Tag(byte[] b, int p, string tag)
            => b[p] == tag[0] && b[p + 1] == tag[1] && b[p + 2] == tag[2] && b[p + 3] == tag[3];
        static int U16(byte[] b, int p) => b[p] | b[p + 1] << 8;
        static uint U32(byte[] b, int p) => (uint)(b[p] | b[p + 1] << 8 | b[p + 2] << 16 | b[p + 3] << 24);
    }
}
