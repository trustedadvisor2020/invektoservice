using Chatinbox.Shared.Logging;

namespace Chatinbox.Tests._Shared.Infrastructure;

/// <summary>
/// Creates JsonLinesLogger instances that write to temp directories.
/// Avoids polluting the real log folders during tests.
/// </summary>
public static class FakeLoggerFactory
{
    private static readonly string TestLogDir = Path.Combine(Path.GetTempPath(), "invekto-tests", "logs");

    public static JsonLinesLogger Create(string serviceName = "Test")
    {
        return new JsonLinesLogger(serviceName, TestLogDir);
    }
}
