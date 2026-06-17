using FluentAssertions;
using Opacc.Client.Helper;
using Opacc.Client.Operations.Exceptions;
using Opacc.Client.Tests.Helpers;
using OpaccWebservice;

namespace Opacc.Client.Tests.Parsing;

/// <summary>
/// Tests for <see cref="OpaccResponseParser"/> — column-store transposition,
/// cursor extraction, count parsing, and error detection.
/// </summary>
public class ResponseParserTests
{
    // ================================================================
    // ParseRecords — column-store transposition
    // ================================================================

    [Fact]
    public void ParseRecords_NullResponse_ReturnsEmptyList()
    {
        var result = OpaccResponseParser.ParseRecords(null, "Addr");
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseRecords_ZeroRows_ReturnsEmptyList()
    {
        var result = OpaccResponseParser.ParseRecords(FlatResponseBuilder.Empty(), "Addr");
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseRecords_TwoColumns_TwoRows_TransposesCorrectly()
    {
        var data = FlatResponseBuilder.FromColumns(
            ("Addr.Number", ["1001", "1002"]),
            ("Addr.FullName", ["Hans Müller", "Anna Schmidt"]));

        var records = OpaccResponseParser.ParseRecords(data, "Addr");

        records.Should().HaveCount(2);
        records[0]["Addr.Number"].Should().Be("1001");
        records[0]["Addr.FullName"].Should().Be("Hans Müller");
        records[1]["Addr.Number"].Should().Be("1002");
        records[1]["Addr.FullName"].Should().Be("Anna Schmidt");
    }

    [Fact]
    public void ParseRecords_RegistersShortNameAlias()
    {
        // "Addr.Number" should also be accessible under short key "Number"
        var data = FlatResponseBuilder.SingleRow(("Addr.Number", "42"));

        var records = OpaccResponseParser.ParseRecords(data, "Addr");

        records[0].Should().ContainKey("Number");
        records[0]["Number"].Should().Be("42");
    }

    [Fact]
    public void ParseRecords_ShortNameAlias_DoesNotOverwriteExistingKey()
    {
        // If "Number" is already registered (direct column), TryAdd must not overwrite
        var data = FlatResponseBuilder.FromColumns(
            ("Number", ["direct"]),
            ("Addr.Number", ["via_prefix"]));

        var records = OpaccResponseParser.ParseRecords(data, "Addr");

        // "Number" key was added first from the "Number" column; TryAdd should not overwrite
        records[0]["Number"].Should().Be("direct");
    }

    [Fact]
    public void ParseRecords_SingleColumn_SingleRow()
    {
        var data = FlatResponseBuilder.SingleRow(("Addr.City", "Bern"));

        var records = OpaccResponseParser.ParseRecords(data, "Addr");

        records.Should().HaveCount(1);
        records[0]["Addr.City"].Should().Be("Bern");
    }

    // ================================================================
    // ParseRedoData
    // ================================================================

    [Fact]
    public void ParseRedoData_NoRedoColumn_ReturnsNull()
    {
        var data = FlatResponseBuilder.SingleRow(("Addr.Number", "1"));
        OpaccResponseParser.ParseRedoData(data).Should().BeNull();
    }

    [Fact]
    public void ParseRedoData_NullInput_ReturnsNull()
    {
        OpaccResponseParser.ParseRedoData(null).Should().BeNull();
    }

    [Fact]
    public void ParseRedoData_EmptyRedoRows_ReturnsNull()
    {
        var data = new FlatResponseData
        {
            RowCount = 0,
            ColumnCount = 1,
            Columns = [new Column { Name = "#RedoData", Rows = [] }],
        };
        OpaccResponseParser.ParseRedoData(data).Should().BeNull();
    }

    [Fact]
    public void ParseRedoData_WithRedoColumn_ReturnsRows()
    {
        var data = FlatResponseBuilder.WithRedoData(
            FlatResponseBuilder.Empty(),
            ["tok1", "tok2", "tok3"]);

        var redo = OpaccResponseParser.ParseRedoData(data);

        redo.Should().NotBeNull();
        redo.Should().Equal("tok1", "tok2", "tok3");
    }

    // ================================================================
    // GetLastRowValue
    // ================================================================

    [Fact]
    public void GetLastRowValue_NullData_ReturnsNull()
    {
        OpaccResponseParser.GetLastRowValue(null, "Addr.Number").Should().BeNull();
    }

    [Fact]
    public void GetLastRowValue_ZeroRows_ReturnsNull()
    {
        var data = FlatResponseBuilder.Empty();
        OpaccResponseParser.GetLastRowValue(data, "Addr.Number").Should().BeNull();
    }

    [Fact]
    public void GetLastRowValue_MissingColumn_ReturnsNull()
    {
        var data = FlatResponseBuilder.SingleRow(("Addr.City", "Bern"));
        OpaccResponseParser.GetLastRowValue(data, "Addr.Number").Should().BeNull();
    }

    [Fact]
    public void GetLastRowValue_SingleRow_ReturnsValue()
    {
        var data = FlatResponseBuilder.SingleRow(("Addr.Number", "1001"));
        OpaccResponseParser.GetLastRowValue(data, "Addr.Number").Should().Be("1001");
    }

    [Fact]
    public void GetLastRowValue_MultipleRows_ReturnsLastValue()
    {
        var data = FlatResponseBuilder.FromColumns(("Addr.Number", ["1001", "1002", "1003"]));
        OpaccResponseParser.GetLastRowValue(data, "Addr.Number").Should().Be("1003");
    }

    // ================================================================
    // ThrowIfError
    // ================================================================

    [Fact]
    public void ThrowIfError_NullResponseInfo_DoesNotThrow()
    {
        var act = () => OpaccResponseParser.ThrowIfError(null);
        act.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfError_SuccessfulResponse_DoesNotThrow()
    {
        var info = new ResponseInfo { Successful = true };
        var act = () => OpaccResponseParser.ThrowIfError(info);
        act.Should().NotThrow();
    }

    [Fact]
    public void ThrowIfError_FailedResponse_ThrowsOpaccRequestException()
    {
        var info = new ResponseInfo
        {
            Successful = false,
            MessageId = "E_ADDR_001",
            MlsMessageText = "Address not found",
        };

        var act = () => OpaccResponseParser.ThrowIfError(info);

        act.Should().Throw<OpaccRequestException>()
            .WithMessage("*Address not found*");
    }

    [Fact]
    public void ThrowIfError_FailedResponse_ExceptionHasMessageId()
    {
        var info = new ResponseInfo
        {
            Successful = false,
            MessageId = "E_ADDR_001",
            MlsMessageText = "Not found",
        };

        var act = () => OpaccResponseParser.ThrowIfError(info);

        act.Should().Throw<OpaccRequestException>()
            .Which.MessageId.Should().Be("E_ADDR_001");
    }

    // ================================================================
    // ParseCount
    // ================================================================

    [Fact]
    public void ParseCount_NullInput_ReturnsZero()
    {
        OpaccResponseParser.ParseCount(null).Should().Be(0);
    }

    [Fact]
    public void ParseCount_FlatResponseData_ReturnsRowCount()
    {
        var data = FlatResponseBuilder.CountOnly(77);
        OpaccResponseParser.ParseCount(data).Should().Be(77);
    }

    [Fact]
    public void ParseCount_FlatResponseDataZero_ReturnsZero()
    {
        var data = FlatResponseBuilder.CountOnly(0);
        OpaccResponseParser.ParseCount(data).Should().Be(0);
    }
}
