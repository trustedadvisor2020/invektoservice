using Chatinbox.Shared.Constants;
using Chatinbox.Shared.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Chatinbox.VoiceRuntime.Audio;

/// <summary>
/// Silero VAD v5 wrapper (HuggingFace snakers4/silero-vad, MIT).
/// Expects 16kHz mono PCM in 512-sample windows (32ms).
/// We downsample 48kHz Opus PCM → 16kHz (3:1 decimation) internally.
///
/// Per-session state (LSTM hidden) must be kept per call — use CreateSession() per voice call.
/// </summary>
public sealed class SileroVad : IDisposable
{
    private const int VadSampleRate = 16000;
    private const int VadWindowSamples = 512;       // 32ms @ 16kHz
    private const int StateShape0 = 2;
    private const int StateShape1 = 1;
    private const int StateShape2 = 128;

    private readonly InferenceSession _session;
    private readonly JsonLinesLogger _logger;
    private bool _disposed;

    public SileroVad(string modelPath, JsonLinesLogger logger)
    {
        _logger = logger;
        if (!File.Exists(modelPath))
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeVadModelMissing}] [SileroVad] ONNX model file missing: {modelPath}. Run Models/README.md download script.");
            throw new FileNotFoundException($"{ErrorCodes.VoiceRuntimeVadModelMissing}: Silero VAD model file missing at {modelPath}", modelPath);
        }

        try
        {
            // SessionOptions implements IDisposable — wrap in using so it is released after session construction.
            using var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                InterOpNumThreads = 1,
                IntraOpNumThreads = 1
            };
            _session = new InferenceSession(modelPath, sessionOptions);
            _logger.SystemInfo($"[SileroVad] Model loaded: {modelPath} (input/output: {string.Join(",", _session.InputNames)} → {string.Join(",", _session.OutputNames)})");
        }
        catch (OnnxRuntimeException ex)
        {
            _logger.SystemError($"[{ErrorCodes.VoiceRuntimeVadInferenceFailed}] [SileroVad] ONNX session init failed: {ex.Message}");
            throw new InvalidOperationException($"{ErrorCodes.VoiceRuntimeVadInferenceFailed}: Silero VAD ONNX session init failed", ex);
        }
    }

    /// <summary>
    /// Create a new per-session VAD state. Reset whenever a new call starts.
    /// </summary>
    public VadSessionState CreateSession() => new VadSessionState();

    /// <summary>
    /// Process a single 48kHz 20ms PCM frame (960 samples). Internally downsamples to 16kHz,
    /// accumulates to 512-sample window, runs inference when window is full.
    /// Returns the most recent speech probability (0..1) if a window was processed, else null.
    /// </summary>
    public float? ProcessFrame48k(VadSessionState state, ReadOnlySpan<short> pcm48k20ms)
    {
        if (pcm48k20ms.Length != 960)
            throw new ArgumentException($"Expected 48kHz 20ms frame = 960 samples, got {pcm48k20ms.Length}", nameof(pcm48k20ms));

        // 48kHz → 16kHz (3:1 averaging)
        var pcm16k = new short[320];
        for (int i = 0; i < 320; i++)
        {
            int sum = pcm48k20ms[i * 3] + pcm48k20ms[i * 3 + 1] + pcm48k20ms[i * 3 + 2];
            pcm16k[i] = (short)(sum / 3);
        }

        // Append to rolling buffer
        state.AppendSamples(pcm16k);

        // Run inference when at least one window (512 samples) is available
        float? lastProb = null;
        while (state.BufferLength >= VadWindowSamples)
        {
            var window = state.TakeWindow(VadWindowSamples);
            lastProb = RunInference(window, state);
        }

        return lastProb;
    }

    private float RunInference(ReadOnlySpan<short> window16k, VadSessionState state)
    {
        // Convert PCM16 → float32 [-1, 1]
        var floatWindow = new float[window16k.Length];
        for (int i = 0; i < window16k.Length; i++)
            floatWindow[i] = window16k[i] / 32768f;

        try
        {
            // Inputs: input [1, 512], state [2, 1, 128], sr [1]
            var inputTensor = new DenseTensor<float>(floatWindow, new[] { 1, window16k.Length });
            var stateTensor = new DenseTensor<float>(state.LstmState, new[] { StateShape0, StateShape1, StateShape2 });
            var srTensor = new DenseTensor<long>(new long[] { VadSampleRate }, new[] { 1 });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", inputTensor),
                NamedOnnxValue.CreateFromTensor("state", stateTensor),
                NamedOnnxValue.CreateFromTensor("sr", srTensor)
            };

            using var results = _session.Run(inputs);

            // Output 1 ("output"): float32 [1, 1] — speech probability
            // Output 2 ("stateN"): float32 [2, 1, 128] — updated LSTM state
            var probTensor = results.First(r => r.Name == "output").AsTensor<float>();
            var newStateTensor = results.First(r => r.Name == "stateN").AsTensor<float>();

            var prob = probTensor.GetValue(0);

            // Persist updated state into session
            var newStateArr = newStateTensor.ToArray();
            Array.Copy(newStateArr, state.LstmState, newStateArr.Length);

            return prob;
        }
        catch (OnnxRuntimeException ex)
        {
            // INV-VR-007 fail-open: emit WARN (non-fatal) so Realtime native VAD continues to gate turns.
            _logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeVadInferenceFailed}] [SileroVad] Inference failed (fail-open 0.5f): {ex.Message}");
            return 0.5f;
        }
        catch (InvalidOperationException ex)
        {
            // results.First(...) can throw if expected output names are missing — treat as fail-open WARN.
            _logger.SystemWarn($"[{ErrorCodes.VoiceRuntimeVadInferenceFailed}] [SileroVad] Inference output missing (fail-open 0.5f): {ex.Message}");
            return 0.5f;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.Dispose();
    }
}

/// <summary>
/// Per-session VAD state — LSTM hidden + rolling 16kHz buffer.
/// </summary>
public sealed class VadSessionState
{
    public float[] LstmState { get; } = new float[2 * 1 * 128];
    private readonly List<short> _buffer = new(2048);

    public int BufferLength => _buffer.Count;

    public void AppendSamples(ReadOnlySpan<short> samples)
    {
        for (int i = 0; i < samples.Length; i++)
            _buffer.Add(samples[i]);
    }

    public short[] TakeWindow(int count)
    {
        if (_buffer.Count < count)
            throw new InvalidOperationException($"Buffer has {_buffer.Count} samples, requested {count}");

        var window = _buffer.GetRange(0, count).ToArray();
        _buffer.RemoveRange(0, count);
        return window;
    }

    public void Reset()
    {
        Array.Clear(LstmState, 0, LstmState.Length);
        _buffer.Clear();
    }
}
