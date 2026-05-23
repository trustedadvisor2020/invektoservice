namespace Invekto.VoiceRuntime.Audio;

/// <summary>
/// Simple PCM resampler between Opus canonical (48kHz) and Realtime native (24kHz).
/// 2:1 ratio — integer downsample (averaging pair) and zero-order hold upsample.
/// F0 PoC quality: adequate for speech intelligibility. F2 may upgrade to polyphase filter if needed.
/// </summary>
public static class PcmResampler
{
    /// <summary>
    /// 48kHz → 24kHz downsample (2:1 averaging).
    /// Input length must be even. Output length = input / 2.
    /// </summary>
    public static short[] Downsample48To24(ReadOnlySpan<short> pcm48k)
    {
        if (pcm48k.Length % 2 != 0)
            throw new ArgumentException($"48kHz PCM input length must be even, got {pcm48k.Length}", nameof(pcm48k));

        var output = new short[pcm48k.Length / 2];
        for (int i = 0; i < output.Length; i++)
        {
            // Average pair (low-pass anti-aliasing approximation)
            var sum = pcm48k[i * 2] + pcm48k[i * 2 + 1];
            output[i] = (short)(sum / 2);
        }
        return output;
    }

    /// <summary>
    /// 24kHz → 48kHz upsample (zero-order hold — sample replication).
    /// Output length = input × 2.
    /// </summary>
    public static short[] Upsample24To48(ReadOnlySpan<short> pcm24k)
    {
        var output = new short[pcm24k.Length * 2];
        for (int i = 0; i < pcm24k.Length; i++)
        {
            output[i * 2] = pcm24k[i];
            output[i * 2 + 1] = pcm24k[i];
        }
        return output;
    }

    /// <summary>
    /// 24kHz mono PCM16 → Base64-encoded byte buffer for OpenAI Realtime input_audio_buffer.append.
    /// Realtime expects PCM16 LE base64 at session-configured sample rate (24kHz default).
    /// </summary>
    public static string PcmToBase64(ReadOnlySpan<short> pcm)
    {
        var bytes = new byte[pcm.Length * 2];
        for (int i = 0; i < pcm.Length; i++)
        {
            bytes[i * 2] = (byte)(pcm[i] & 0xFF);
            bytes[i * 2 + 1] = (byte)((pcm[i] >> 8) & 0xFF);
        }
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Base64 PCM16 LE → short[] (Realtime audio.delta event payload decode).
    /// </summary>
    public static short[] Base64ToPcm(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        if (bytes.Length % 2 != 0)
            throw new ArgumentException($"Base64 PCM byte length must be even, got {bytes.Length}", nameof(base64));

        var output = new short[bytes.Length / 2];
        for (int i = 0; i < output.Length; i++)
        {
            output[i] = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
        }
        return output;
    }
}
