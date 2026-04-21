using System.Collections.Concurrent;
using FluentAssertions;
using Invekto.Shared.Contracts.Inma;
using Invekto.Shared.Contracts.Inma.Dtos;
using Invekto.Shared.Services;
using Microsoft.Extensions.Caching.Memory;

namespace Invekto.Backend.Tests.UnitTests;

/// <summary>
/// Regression coverage for the 2026-04-22 cancellation-isolation fix in
/// <see cref="InmaDynamicFieldsCache"/>. The prior implementation captured the
/// first caller's <see cref="CancellationToken"/> inside the shared in-flight
/// task, so cancelling caller A's token poisoned every joined awaiter with
/// <see cref="OperationCanceledException"/>. These tests lock in the corrected
/// single-flight + cancellation semantics.
/// <para>
/// <see cref="FakeInmaClient"/> always yields at least once (<c>Task.Yield()</c>)
/// before completing or throwing so the single-flight dictionary registration
/// matches the production path (HttpClient crosses an async boundary before any
/// upstream error surfaces; an unconditional sync throw would race the
/// <c>GetOrAdd</c> slot registration and is not representative of real traffic).
/// </para>
/// </summary>
public class InmaDynamicFieldsCacheTests
{
    private static InmaDynamicFieldsCache BuildCache(FakeInmaClient client) =>
        new(new MemoryCache(new MemoryCacheOptions()), client);

    [Fact]
    public async Task GetOrFetchAsync_CacheHit_ReturnsCachedListWithoutCallingClient()
    {
        var client = new FakeInmaClient(new[] { new InmaDynamicField { FieldKey = "cf1", FieldName = "City" } });
        var cache = BuildCache(client);

        var first = await cache.GetOrFetchAsync(42, "secret");
        var second = await cache.GetOrFetchAsync(42, "secret");

        first.Should().HaveCount(1);
        second.Should().BeSameAs(first);
        client.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOrFetchAsync_EmptyList_IsCachedAsLegitimateState()
    {
        var client = new FakeInmaClient(Array.Empty<InmaDynamicField>());
        var cache = BuildCache(client);

        var first = await cache.GetOrFetchAsync(7, "secret");
        var second = await cache.GetOrFetchAsync(7, "secret");

        first.Should().BeEmpty();
        second.Should().BeEmpty();
        client.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOrFetchAsync_ConcurrentMisses_ShareSingleClientCall()
    {
        var client = new FakeInmaClient(new[] { new InmaDynamicField { FieldKey = "cf1", FieldName = "City" } });
        client.BlockUntilReleased();
        var cache = BuildCache(client);

        var taskA = cache.GetOrFetchAsync(99, "secret");
        var taskB = cache.GetOrFetchAsync(99, "secret");
        var taskC = cache.GetOrFetchAsync(99, "secret");

        await client.WaitForCallAsync();
        client.Release();

        var results = await Task.WhenAll(taskA, taskB, taskC);

        client.CallCount.Should().Be(1);
        results.Should().OnlyContain(r => r.Count == 1);
    }

    [Fact]
    public async Task GetOrFetchAsync_FirstCallerCancels_DoesNotAbortJoinedAwaiter()
    {
        // Regression: prior code passed the first caller's CancellationToken into the
        // shared in-flight Task. Cancelling A poisoned every joined waiter including B.
        var client = new FakeInmaClient(new[] { new InmaDynamicField { FieldKey = "cf1", FieldName = "City" } });
        client.BlockUntilReleased();
        var cache = BuildCache(client);

        using var ctsA = new CancellationTokenSource();
        var taskA = cache.GetOrFetchAsync(123, "secret", ctsA.Token);
        var taskB = cache.GetOrFetchAsync(123, "secret", CancellationToken.None);

        await client.WaitForCallAsync();
        ctsA.Cancel();
        client.Release();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await taskA);

        var resultB = await taskB;
        resultB.Should().HaveCount(1);
        client.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOrFetchAsync_CallerCancels_DoesNotCancelUnderlyingFetch()
    {
        // Verifies that per-caller cancellation isolates: cancelling caller A must not
        // surface an OperationCanceledException on the underlying fake client call.
        var client = new FakeInmaClient(new[] { new InmaDynamicField { FieldKey = "cf1", FieldName = "City" } });
        client.BlockUntilReleased();
        var cache = BuildCache(client);

        using var ctsA = new CancellationTokenSource();
        var taskA = cache.GetOrFetchAsync(321, "secret", ctsA.Token);

        await client.WaitForCallAsync();
        ctsA.Cancel();
        client.Release();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await taskA);

        client.ObservedCancellationTokens.Should().OnlyContain(ct => ct == CancellationToken.None);
    }

    [Fact]
    public async Task Invalidate_ClearsCacheEntry_NextCallFetchesFresh()
    {
        var firstPayload = new[] { new InmaDynamicField { FieldKey = "cf1", FieldName = "Old" } };
        var secondPayload = new[] { new InmaDynamicField { FieldKey = "cf1", FieldName = "New" } };
        var client = new FakeInmaClient(firstPayload);
        var cache = BuildCache(client);

        var first = await cache.GetOrFetchAsync(55, "secret");
        client.NextPayload = secondPayload;
        cache.Invalidate(55);

        var second = await cache.GetOrFetchAsync(55, "secret");

        first.Should().ContainSingle(f => f.FieldName == "Old");
        second.Should().ContainSingle(f => f.FieldName == "New");
        client.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task GetOrFetchAsync_UpstreamException_PropagatesAndClearsInflightSlotSoRetrySucceeds()
    {
        var client = new FakeInmaClient(new[] { new InmaDynamicField { FieldKey = "cf1", FieldName = "Recovered" } });
        client.FailNextCall(new InmaDynamicFieldsFetchException(77, "upstream 500"));
        var cache = BuildCache(client);

        await Assert.ThrowsAsync<InmaDynamicFieldsFetchException>(() =>
            cache.GetOrFetchAsync(77, "secret"));

        // Subsequent call must start a FRESH fetch (inflight cleared in finally).
        // If the slot leaked, the retry would observe the original exception again
        // without the client ever being called a second time.
        var recovered = await cache.GetOrFetchAsync(77, "secret");

        recovered.Should().ContainSingle(f => f.FieldName == "Recovered");
        client.CallCount.Should().Be(2);
    }

    /// <summary>
    /// Hand-rolled fake for <see cref="IInmaDynamicFieldsClient"/> with explicit gate
    /// control (<see cref="BlockUntilReleased"/> + <see cref="Release"/>) so concurrent
    /// single-flight scenarios can be exercised deterministically without timing assumptions.
    /// Always yields once before completing so the single-flight slot registration order
    /// mirrors real HttpClient traffic (an unconditional sync throw would pre-empt the
    /// outer <c>ConcurrentDictionary.GetOrAdd</c>).
    /// </summary>
    private sealed class FakeInmaClient : IInmaDynamicFieldsClient
    {
        private IReadOnlyList<InmaDynamicField> _payload;
        private TaskCompletionSource? _gate;
        private TaskCompletionSource _callObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _callCount;
        private Exception? _nextCallException;
        private readonly ConcurrentQueue<CancellationToken> _observedTokens = new();

        public FakeInmaClient(IReadOnlyList<InmaDynamicField> payload) => _payload = payload;

        public int CallCount => Volatile.Read(ref _callCount);
        public IReadOnlyList<InmaDynamicField> NextPayload { set => _payload = value; }
        public IReadOnlyCollection<CancellationToken> ObservedCancellationTokens => _observedTokens.ToArray();

        public void BlockUntilReleased()
        {
            _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _callObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void Release() => _gate?.TrySetResult();

        public Task WaitForCallAsync() => _callObserved.Task;

        /// <summary>Schedule a single-shot exception for the next call; cleared atomically on consumption.</summary>
        public void FailNextCall(Exception ex) => Interlocked.Exchange(ref _nextCallException, ex);

        public async Task<IReadOnlyList<InmaDynamicField>> GetFieldsAsync(
            int tenantId,
            string secretKey,
            CancellationToken ct = default)
        {
            Interlocked.Increment(ref _callCount);
            _observedTokens.Enqueue(ct);
            _callObserved.TrySetResult();

            // Always cross an async boundary before signalling success/failure so the
            // real async-IO shape is preserved — see class docs.
            await Task.Yield();

            if (_gate is { } gate)
            {
                await gate.Task.ConfigureAwait(false);
            }

            var pending = Interlocked.Exchange(ref _nextCallException, null);
            if (pending is not null)
                throw pending;

            return _payload;
        }
    }
}
