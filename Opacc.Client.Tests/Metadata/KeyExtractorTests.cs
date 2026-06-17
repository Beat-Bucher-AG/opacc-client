using FluentAssertions;
using Opacc.Client.Enums;
using Opacc.Client.Metadata;
using Opacc.Client.Metadata.Cache;
using Opacc.Client.Tests.TestModels;

namespace Opacc.Client.Tests.Metadata;

/// <summary>
/// Direct tests for the shared <see cref="KeyExtractor"/> — the single source of truth for
/// composing BO index start keys. Start keys are a search context, so dates use yyyyMMdd /
/// yyyyMMddHHmmss (not the dd.MM.yyyy assignment format).
/// </summary>
public class KeyExtractorTests
{
    private static PropertyMeta Prop(string name, OpaccDataType dt = OpaccDataType.None) =>
        new() { ClrName = name, ClrType = typeof(object), OoExpression = name, DataType = dt };

    // ── FormatSegment ──────────────────────────────────────────────────

    [Fact]
    public void FormatSegment_Null_IsEmpty() =>
        KeyExtractor.FormatSegment(null, Prop("X")).Should().Be("");

    [Fact]
    public void FormatSegment_BoolTrue_IsOne() =>
        KeyExtractor.FormatSegment(true, Prop("X")).Should().Be("1");

    [Fact]
    public void FormatSegment_BoolFalse_IsZero() =>
        KeyExtractor.FormatSegment(false, Prop("X")).Should().Be("0");

    [Fact]
    public void FormatSegment_DateType_UsesYyyymmdd() =>
        KeyExtractor.FormatSegment(new DateTime(2024, 3, 15), Prop("X", OpaccDataType.Date))
            .Should().Be("20240315");

    [Fact]
    public void FormatSegment_NonDateDateTime_UsesTimestamp() =>
        KeyExtractor.FormatSegment(new DateTime(2024, 3, 15, 8, 9, 10), Prop("X"))
            .Should().Be("20240315080910");

    [Fact]
    public void FormatSegment_Int_IsInvariant() =>
        KeyExtractor.FormatSegment(87709765, Prop("X")).Should().Be("87709765");

    [Fact]
    public void FormatSegment_String_PassedThrough() =>
        KeyExtractor.FormatSegment("AB,C", Prop("X")).Should().Be("AB,C");

    // ── SegmentsFromModel ──────────────────────────────────────────────

    [Fact]
    public void SegmentsFromModel_MultiSegment_OrderedBySegment()
    {
        var md = EntityMetadataCache.Get<FakeSalDocItem>();
        var model = new FakeSalDocItem { SalDocInternalNo = 10, InternalNo = 5 };

        KeyExtractor.SegmentsFromModel(md, 4, model).Should().Equal("10", "5");
    }

    // ── LeadingSegmentsFromValues ──────────────────────────────────────

    [Fact]
    public void LeadingSegments_BothProvided_ReturnsBoth()
    {
        var md = EntityMetadataCache.Get<FakeSalDocItem>();
        var values = new Dictionary<string, object?>
        {
            ["SalDocInternalNo"] = 10,
            ["InternalNo"] = 5,
        };

        KeyExtractor.LeadingSegmentsFromValues(md, 4, values).Should().Equal("10", "5");
    }

    [Fact]
    public void LeadingSegments_LeadingOnly_ReturnsPrefix()
    {
        var md = EntityMetadataCache.Get<FakeSalDocItem>();
        var values = new Dictionary<string, object?> { ["SalDocInternalNo"] = 10 };

        KeyExtractor.LeadingSegmentsFromValues(md, 4, values).Should().Equal("10");
    }

    [Fact]
    public void LeadingSegments_NonLeadingOnly_ReturnsEmpty()
    {
        var md = EntityMetadataCache.Get<FakeSalDocItem>();
        // Only segment 2 set, leading segment 1 missing → no contiguous leading prefix.
        var values = new Dictionary<string, object?> { ["InternalNo"] = 5 };

        KeyExtractor.LeadingSegmentsFromValues(md, 4, values).Should().BeEmpty();
    }
}
