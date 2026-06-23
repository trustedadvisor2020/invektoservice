using Concentus.Enums;
using Concentus.Structs;
using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;

namespace Chatinbox.VoiceRuntime.Audio;

/// <summary>
/// Opus codec wrapper using Concentus (managed C#, no native libopus DLL).
/// Canonical frame: 48kHz mono, 20ms frame size = 960 PCM samples (Int16).
/// AD-3: Opus 48kHz mono 20ms canonical across all providers (browser/Toniva/WA Calling).
/// </summary>
public sealed class OpusCodec : IDisposable
{
    private const int SampleRate = 48000;
    private const int Channels = 1;
    private const int FrameMs = 20;
    private const int FrameSamples = SampleRate * FrameMs / 1000;  // 960
    private const int MaxOpusPacketBytes = 1275;                    // RFC 6716 max

    private readonly OpusEncoder _encoder;
    private readonly OpusDecoder _decoder;
    private readonly JsonLinesLogger _logger;
    private bool _disposed;

    public OpusCodec(JsonLinesLogger logger)
    {
        _logger = logger;
        _encoder = new OpusEncoder(SampleRate, Channels, OpusApplication.OPUS_APPLICATION_VOIP);
        _encoder.Bitrate = 24000;                  // 24 kbps — VoIP sweet spot for speech
        _encoder.UseInbandFEC = true;              // Forward Error Correction (packet loss recovery)
        _encoder.PacketLossPercent = 5;            // VoIP-realistic loss rate hint

        _decoder = new OpusDecoder(SampleRate, Channels);
    }

    public int FrameSampleCount => FrameSamples;

    /// <summary>
    /// Encode a single 20ms PCM frame (960 Int16 samples) to Opus packet bytes.
    /// </summary>
    public ReadOnlyMemory<byte> EncodeFrame(ReadOnlySpan<short> pcm20ms)
    {
        if (pcm20ms.Length != FrameSamples)
            throw new ArgumentException($"PCM frame must be exactly {FrameSamples} samples (20ms @ 48kHz), got {pcm20ms.Length}", nameof(pcm20ms));

        try
        {
            var output = new byte[MaxOpusPacketBytes];
            var pcmArray = pcm20ms.ToArray();
            var encoded = _encoder.Encode(pcmArray, 0, FrameSamples, output, 0, output.Length);
            if (encoded < 0)
                throw new InvalidOperationException($"{ErrorCodes.VoiceRuntimeOpusEncodeFailed}: Opus encode returned negative size: {encoded}");

            return new ReadOnlyMemory<byte>(output, 0, encoded);
        }
        catch (ArgumentException ex)
        {
            // Concentus may raise ArgumentException on bad frame size / null buffer; rethrow with INV-VR code.
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeOpusEncodeFailed}] [OpusCodec] Encode invalid arguments: {ex.Message}");
            throw new InvalidOperationException($"{ErrorCodes.VoiceRuntimeOpusEncodeFailed}: Opus encode failed (invalid args)", ex);
        }
    }

    /// <summary>
    /// Decode a single Opus packet to 20ms PCM frame (960 Int16 samples).
    /// </summary>
    public short[] DecodeFrame(ReadOnlySpan<byte> opusPacket)
    {
        if (opusPacket.IsEmpty)
            throw new ArgumentException("Opus packet must not be empty", nameof(opusPacket));

        try
        {
            var output = new short[FrameSamples];
            var packetArray = opusPacket.ToArray();
            var decoded = _decoder.Decode(packetArray, 0, packetArray.Length, output, 0, FrameSamples, false);
            if (decoded != FrameSamples)
                throw new InvalidOperationException($"{ErrorCodes.VoiceRuntimeOpusDecodeFailed}: Opus decode returned {decoded} samples, expected {FrameSamples}");

            return output;
        }
        catch (ArgumentException ex)
        {
            // Concentus may raise ArgumentException on corrupt/bad-size packets; rethrow with INV-VR code.
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeOpusDecodeFailed}] [OpusCodec] Decode invalid arguments (packet_size={opusPacket.Length}): {ex.Message}");
            throw new InvalidOperationException($"{ErrorCodes.VoiceRuntimeOpusDecodeFailed}: Opus decode failed (invalid args)", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Concentus encoder/decoder are managed; no native resources to free.
    }
}
