using FluentAssertions;
using Invekto.Shared.Logging.Reader;

namespace Invekto.Backend.Tests.UnitTests;

public class LogReaderTests
{
    private readonly string _testLogDir;

    public LogReaderTests()
    {
        // Create temp directory for test logs
        _testLogDir = Path.Combine(Path.GetTempPath(), "invekto-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testLogDir);
    }

    [Fact]
    public void Constructor_WithValidPath_DoesNotThrow()
    {
        // Act
        var action = () => new LogReader(new[] { _testLogDir }, 500);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public async Task GetLastErrorsAsync_WithNoLogFiles_ReturnsEmptyList()
    {
        // Arrange
        var reader = new LogReader(new[] { _testLogDir }, 500);

        // Act
        var errors = await reader.GetLastErrorsAsync(10);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryLogsAsync_WithNoFiles_ReturnsEmptyResult()
    {
        // Arrange
        var reader = new LogReader(new[] { _testLogDir }, 500);
        var options = new LogQueryOptions { Limit = 10 };

        // Act
        var result = await reader.QueryLogsAsync(options);

        // Assert
        result.Entries.Should().BeEmpty();
        result.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task GetErrorStatsAsync_WithNoFiles_ReturnsZeroTotal()
    {
        // Arrange
        var reader = new LogReader(new[] { _testLogDir }, 500);

        // Act
        var stats = await reader.GetErrorStatsAsync(24);

        // Assert
        stats.Total.Should().Be(0);
        stats.Buckets.Should().NotBeEmpty(); // Should have buckets even if empty
    }

    [Fact]
    public async Task QueryLogsAsync_WithLogFile_ReturnsEntries()
    {
        // Arrange
        var logFile = Path.Combine(_testLogDir, $"Invekto.Backend-{DateTime.UtcNow:yyyyMMdd}.jsonl");
        var logEntry = """{"ts":"2026-02-03T10:00:00Z","level":"INFO","msg":"Test message","service":"Invekto.Backend"}""";
        await File.WriteAllTextAsync(logFile, logEntry + Environment.NewLine);

        var reader = new LogReader(new[] { _testLogDir }, 500);
        var options = new LogQueryOptions { Limit = 10 };

        // Act
        var result = await reader.QueryLogsAsync(options);

        // Assert
        result.Entries.Should().HaveCount(1);
        result.Entries[0].Message.Should().Be("Test message");
    }

    [Fact]
    public async Task QueryLogsAsync_WithLevelFilter_FiltersCorrectly()
    {
        // Arrange
        var logFile = Path.Combine(_testLogDir, $"Invekto.Backend-{DateTime.UtcNow:yyyyMMdd}.jsonl");
        var logs = string.Join(Environment.NewLine, new[]
        {
            """{"ts":"2026-02-03T10:00:00Z","level":"INFO","msg":"Info message","service":"Invekto.Backend"}""",
            """{"ts":"2026-02-03T10:00:01Z","level":"ERROR","msg":"Error message","service":"Invekto.Backend"}""",
            """{"ts":"2026-02-03T10:00:02Z","level":"WARN","msg":"Warn message","service":"Invekto.Backend"}"""
        });
        await File.WriteAllTextAsync(logFile, logs + Environment.NewLine);

        var reader = new LogReader(new[] { _testLogDir }, 500);
        var options = new LogQueryOptions
        {
            Levels = new[] { "ERROR" },
            Limit = 10
        };

        // Act
        var result = await reader.QueryLogsAsync(options);

        // Assert
        result.Entries.Should().HaveCount(1);
        result.Entries[0].Level.Should().Be("ERROR");
    }

    [Fact]
    public async Task GetLastErrorsAsync_WithErrorLogs_ReturnsErrors()
    {
        // Arrange
        var logFile = Path.Combine(_testLogDir, $"Invekto.Backend-{DateTime.UtcNow:yyyyMMdd}.jsonl");
        var logs = string.Join(Environment.NewLine, new[]
        {
            """{"ts":"2026-02-03T10:00:00Z","level":"INFO","msg":"Info message","service":"Invekto.Backend"}""",
            """{"ts":"2026-02-03T10:00:01Z","level":"ERROR","msg":"Error 1","service":"Invekto.Backend"}""",
            """{"ts":"2026-02-03T10:00:02Z","level":"ERROR","msg":"Error 2","service":"Invekto.Backend"}"""
        });
        await File.WriteAllTextAsync(logFile, logs + Environment.NewLine);

        var reader = new LogReader(new[] { _testLogDir }, 500);

        // Act
        var errors = await reader.GetLastErrorsAsync(10);

        // Assert
        errors.Should().HaveCount(2);
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testLogDir))
        {
            Directory.Delete(_testLogDir, recursive: true);
        }
    }
}
