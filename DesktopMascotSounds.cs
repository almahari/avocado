using System.IO;
using System.Media;

namespace Avocado;

public enum DesktopMascotSound { Poke, Pop, Cheer, Nom, Knock, Yawn, Hello }

/// <summary>
/// Tiny procedural sound effects (soft sine chirps) so the mascot needs no audio assets.
/// </summary>
public static class DesktopMascotSounds
{
    private const int SampleRate = 22050;
    private static readonly Dictionary<DesktopMascotSound, byte[]> Cache = [];
    private static SoundPlayer? _player;

    public static void Play(DesktopMascotSound sound, ReminderSoundMode mode)
    {
        if (mode == ReminderSoundMode.Silent) return;
        var volume = mode == ReminderSoundMode.Soft ? 0.16 : 0.28;
        try
        {
            if (!Cache.TryGetValue(sound, out var wave))
                Cache[sound] = wave = Build(sound);
            _player?.Stop();
            _player = new SoundPlayer(new MemoryStream(Scale(wave, volume)));
            _player.Play();
        }
        catch (InvalidOperationException) { }
        catch (IOException) { }
    }

    // Each note: (start Hz, end Hz, milliseconds).
    private static (double From, double To, int Ms)[] Notes(DesktopMascotSound sound) => sound switch
    {
        DesktopMascotSound.Poke => [(700, 1100, 70), (1100, 900, 60)],
        DesktopMascotSound.Pop => [(500, 900, 45)],
        DesktopMascotSound.Cheer => [(660, 660, 90), (830, 830, 90), (990, 990, 90), (1320, 1320, 160)],
        DesktopMascotSound.Nom => [(420, 360, 70), (0, 0, 40), (420, 340, 80)],
        DesktopMascotSound.Knock => [(180, 140, 60), (0, 0, 90), (180, 140, 60)],
        DesktopMascotSound.Yawn => [(520, 300, 420)],
        DesktopMascotSound.Hello => [(600, 900, 110), (900, 760, 120)],
        _ => [(600, 600, 60)]
    };

    private static byte[] Build(DesktopMascotSound sound)
    {
        var samples = new List<short>();
        foreach (var (from, to, ms) in Notes(sound))
        {
            var count = SampleRate * ms / 1000;
            var phase = 0.0;
            for (var i = 0; i < count; i++)
            {
                var t = (double)i / count;
                var frequency = from + (to - from) * t;
                phase += 2 * Math.PI * frequency / SampleRate;
                // Soft attack/release envelope avoids clicks.
                var envelope = Math.Min(1, Math.Min(t * 12, (1 - t) * 6));
                var value = frequency <= 0 ? 0 : (Math.Sin(phase) + 0.25 * Math.Sin(phase * 2)) / 1.25 * envelope;
                samples.Add((short)(value * short.MaxValue));
            }
        }
        return ToWave(samples);
    }

    private static byte[] Scale(byte[] wave, double volume)
    {
        var copy = (byte[])wave.Clone();
        for (var i = 44; i + 1 < copy.Length; i += 2)
        {
            var sample = (short)(copy[i] | copy[i + 1] << 8);
            var scaled = (short)(sample * volume);
            copy[i] = (byte)(scaled & 0xFF);
            copy[i + 1] = (byte)(scaled >> 8 & 0xFF);
        }
        return copy;
    }

    private static byte[] ToWave(IReadOnlyList<short> samples)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var dataLength = samples.Count * 2;
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataLength);
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(dataLength);
        foreach (var sample in samples) writer.Write(sample);
        writer.Flush();
        return stream.ToArray();
    }
}
