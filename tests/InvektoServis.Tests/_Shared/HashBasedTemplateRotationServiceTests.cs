using FluentAssertions;
using Invekto.Shared.Services;

namespace InvektoServis.Tests._Shared;

/// <summary>
/// FEAT-WTP AC-2 + AC-3 regression: deterministic variant selection + bounded output.
/// The hash-based rotation service is used by MessageTextHandler (welcome A/B) and by
/// AiFaqHandler's rotation branch to pick a starting index when no prior rotation state
/// exists. Determinism is load-bearing — callers that change the algorithm would need to
/// migrate stored rotation indices.
/// </summary>
public class HashBasedTemplateRotationServiceTests
{
    private readonly ITemplateRotationService _sut = new HashBasedTemplateRotationService();

    [Fact]
    public void PickVariantIndex_SameInputs_IsDeterministic()
    {
        // AC-2: "ayni phone 10 kez denenince ayni variant_index donuyor"
        const string phone = "+905551234567";
        const string nodeId = "welcome_1";

        var first = _sut.PickVariantIndex(phone, nodeId, 3);
        for (var i = 0; i < 10; i++)
            _sut.PickVariantIndex(phone, nodeId, 3).Should().Be(first);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void PickVariantIndex_VariantCountOneOrLess_ReturnsZero(int variantCount)
    {
        _sut.PickVariantIndex("+905551234567", "n1", variantCount).Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PickVariantIndex_NullOrEmptyContactKey_StillDeterministic(string? contactKey)
    {
        // Empty contact collapses to node-only hash (documented fallback).
        var a = _sut.PickVariantIndex(contactKey, "welcome_1", 5);
        var b = _sut.PickVariantIndex(contactKey, "welcome_1", 5);
        a.Should().Be(b);
        a.Should().BeInRange(0, 4);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(100)]
    public void PickVariantIndex_AlwaysWithinRange(int variantCount)
    {
        // Scan a spread of contact keys; index must always modulo to [0, variantCount).
        for (var i = 0; i < 200; i++)
        {
            var idx = _sut.PickVariantIndex($"+9055512{i:D5}", "faq_pricing", variantCount);
            idx.Should().BeGreaterThanOrEqualTo(0).And.BeLessThan(variantCount);
        }
    }

    [Fact]
    public void PickVariantIndex_DifferentContacts_Distribute()
    {
        // Not a statistical test, just a smoke signal: 200 distinct contacts over 3 variants
        // should produce each bucket at least once (FNV-1a avalanche).
        var counts = new int[3];
        for (var i = 0; i < 200; i++)
        {
            var idx = _sut.PickVariantIndex($"+90555{i:D7}", "welcome_1", 3);
            counts[idx]++;
        }
        counts.Should().OnlyContain(c => c > 0);
    }

    [Fact]
    public void PickVariantIndex_DifferentNodes_IsolatedRotation()
    {
        // Same contact across two nodes should not collapse to the same index deterministically —
        // otherwise welcome + faq rotations would lock-step. FNV-1a over "{contact}|{nodeId}" gives
        // us node independence; we assert at least ONE contact out of 50 differs between nodes.
        var differ = 0;
        for (var i = 0; i < 50; i++)
        {
            var contact = $"+90555{i:D7}";
            var a = _sut.PickVariantIndex(contact, "node_a", 4);
            var b = _sut.PickVariantIndex(contact, "node_b", 4);
            if (a != b) differ++;
        }
        differ.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PickVariantIndex_NegativeVariantCountTreatedAsOne()
    {
        // Defensive: spec says "<=1 returns 0" but guardrail needed since callers pass
        // pool.Count which could in theory be 0 in fallback paths.
        _sut.PickVariantIndex("+905551234567", "n1", -5).Should().Be(0);
    }
}
