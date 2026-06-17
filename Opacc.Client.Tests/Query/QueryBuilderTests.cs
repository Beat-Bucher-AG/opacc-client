using FluentAssertions;
using NSubstitute;
using Opacc.Client.Operations.Query;
using Opacc.Client.Session;
using Opacc.Client.Tests.Helpers;
using Opacc.Client.Tests.TestModels;
using OpaccWebservice;

namespace Opacc.Client.Tests.Query;

/// <summary>
/// Tests for the Query builder, exercised through the public <see cref="OpaccClient"/> API.
/// The transport is mocked; the captured <see cref="OpaccQueryRequest"/> is used to assert
/// that the correct parameters are sent to Opacc.
/// </summary>
public class QueryBuilderTests
{
    private readonly IOpaccTransport _transport;
    private readonly OpaccClient _client;
    private OpaccQueryRequest? _captured;
    private SessionCredentials? _capturedCredentials;

    public QueryBuilderTests()
    {
        _transport = Substitute.For<IOpaccTransport>();
        _client = new OpaccClient(_transport);
    }

    private void Setup(FlatResponseData? response = null)
    {
        _transport.SendQueryAsync(
            Arg.Do<OpaccQueryRequest>(r => _captured = r),
            Arg.Do<SessionCredentials?>(c => _capturedCredentials = c),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));
    }

    // ================================================================
    // BoEntity
    // ================================================================

    [Fact]
    public async Task Query_UsesBoEntityFromMetadata()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>().ToListAsync();
        _captured!.BoEntity.Should().Be("Addr");
    }

    // ================================================================
    // Where (raw filter)
    // ================================================================

    [Fact]
    public async Task Query_Where_RawFilter_PassedToRequest()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .Where("Addr.City = 'Bern'")
            .ToListAsync();
        _captured!.Filter.Should().Be("Addr.City = 'Bern'");
    }

    [Fact]
    public async Task Query_Where_EmptyFilter_Ignored()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .Where("   ")
            .ToListAsync();
        _captured!.Filter.Should().BeNull();
    }

    // ================================================================
    // Where (lambda)
    // ================================================================

    [Fact]
    public async Task Query_Where_Lambda_StringEquality_TranslatesFilter()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .Where(a => a.City == "Bern")
            .ToListAsync();
        _captured!.Filter.Should().Be("Addr.City = 'Bern'");
    }

    [Fact]
    public async Task Query_Where_Lambda_IntComparison_TranslatesFilter()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .Where(a => a.Number >= 1000)
            .ToListAsync();
        _captured!.Filter.Should().Be("Addr.Number >= 1000");
    }

    [Fact]
    public async Task Query_Where_Lambda_NotBool_TranslatesFilter()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .Where(a => !a.IsPassive)
            .ToListAsync();
        _captured!.Filter.Should().Be("not (Addr.IsPassive = 1)");
    }

    // ================================================================
    // Where (3-param)
    // ================================================================

    [Fact]
    public async Task Query_Where_ThreeParam_StringEquality()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .Where(a => a.City, "=", "Bern")
            .ToListAsync();
        _captured!.Filter.Should().Be("Addr.City = 'Bern'");
    }

    [Fact]
    public async Task Query_Where_ThreeParam_IntGreaterThan()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .Where(a => a.Number, ">", 500)
            .ToListAsync();
        _captured!.Filter.Should().Be("Addr.Number > 500");
    }

    [Fact]
    public async Task Query_Where_ThreeParam_BoolValue()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .Where(a => a.IsPassive, "=", false)
            .ToListAsync();
        _captured!.Filter.Should().Be("Addr.IsPassive = 0");
    }

    // ================================================================
    // Multiple Where (AND combination)
    // ================================================================

    [Fact]
    public async Task Query_MultipleWhere_CombinedWithAnd()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .Where("Addr.City = 'Bern'")
            .Where(a => a.IsPassive == false)
            .ToListAsync();
        _captured!.Filter.Should().Be("(Addr.City = 'Bern') and (Addr.IsPassive = 0)");
    }

    // ================================================================
    // OrderBy
    // ================================================================

    [Fact]
    public async Task Query_OrderBy_Ascending_AddsOoExpression()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .OrderBy(a => a.FullName)
            .ToListAsync();
        _captured!.OrderBy.Should().Contain("Addr.FullName");
    }

    [Fact]
    public async Task Query_OrderBy_Descending_AddsPrefixedExpression()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .OrderBy(a => a.FullName, descending: true)
            .ToListAsync();
        _captured!.OrderBy.Should().Contain("-Addr.FullName");
    }

    [Fact]
    public async Task Query_OrderBy_RawString_PassedThrough()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .OrderBy("Addr.City")
            .ToListAsync();
        _captured!.OrderBy.Should().Contain("Addr.City");
    }

    [Fact]
    public async Task Query_OrderByAsDate_AddsToDateList()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .OrderByAsDate(a => a.DateOfEntry!)
            .ToListAsync();
        _captured!.OrderByAsDate.Should().Contain("Addr.DateOfEntry");
        _captured.OrderBy.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Query_OrderByAsNmb_AddsToNmbList()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .OrderByAsNmb(a => a.Number)
            .ToListAsync();
        _captured!.OrderByAsNmb.Should().Contain("Addr.Number");
        _captured.OrderBy.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Query_MultipleOrderBy_AllPresent()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .OrderBy(a => a.City)
            .OrderBy(a => a.FullName, descending: true)
            .ToListAsync();
        _captured!.OrderBy.Should().HaveCount(2);
        _captured.OrderBy.Should().Contain("Addr.City");
        _captured.OrderBy.Should().Contain("-Addr.FullName");
    }

    // ================================================================
    // Distinct
    // ================================================================

    [Fact]
    public async Task Query_Distinct_SetsFlagToTrue()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>().Distinct().ToListAsync();
        _captured!.Distinct.Should().BeTrue();
    }

    [Fact]
    public async Task Query_NoDistinct_FlagIsNull()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>().ToListAsync();
        _captured!.Distinct.Should().BeNull();
    }

    // ================================================================
    // Define
    // ================================================================

    [Fact]
    public async Task Query_Define_NormalizesAtSign()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .Define("MyVar", "Addr.Number * 2")
            .ToListAsync();
        _captured!.Defines.Should().Contain("@MyVar,Addr.Number * 2");
    }

    [Fact]
    public async Task Query_Define_AlreadyHasAtSign_NotDoubled()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .Define("@MyVar", "Addr.Number * 2")
            .ToListAsync();
        _captured!.Defines.Should().Contain("@MyVar,Addr.Number * 2");
        _captured.Defines!.Any(d => d.StartsWith("@@")).Should().BeFalse();
    }

    // ================================================================
    // Take / Skip
    // ================================================================

    [Fact]
    public async Task Query_Take_SetsMaxRows()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>().Take(50).ToListAsync();
        _captured!.MaxRows.Should().Be(50);
    }

    [Fact]
    public async Task Query_FirstAsync_SetsMaxRowsToOne()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>().FirstAsync();
        _captured!.MaxRows.Should().Be(1);
    }

    [Fact]
    public async Task Query_NoTake_MaxRowsIsNull()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>().ToListAsync();
        _captured!.MaxRows.Should().BeNull();
    }

    [Fact]
    public async Task Query_SkipAndTake_SendsSumAsMaxRows()
    {
        var rows = Enumerable.Range(1, 15).Select(i => i.ToString()).ToArray();
        Setup(FlatResponseBuilder.FromColumns(
            ("Addr.Number", rows),
            ("Addr.FullName", Enumerable.Repeat("Name", 15).ToArray())));

        var result = await _client.QueryAsync<FakeAddr>().Skip(5).Take(10).ToListAsync();

        _captured!.MaxRows.Should().Be(15);  // 5 + 10 sent to Opacc
        result.Should().HaveCount(10);       // first 5 discarded client-side
    }

    [Fact]
    public async Task Query_Skip_WithoutTake_ThrowsInvalidOperation()
    {
        Setup();
        var act = async () => await _client.QueryAsync<FakeAddr>().Skip(5).ToListAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Skip*requires*Take*");
    }

    // ================================================================
    // CountAsync
    // ================================================================

    [Fact]
    public async Task Query_CountAsync_SendsMaxRowsZero()
    {
        _transport.SendQueryAsync(
            Arg.Do<OpaccQueryRequest>(r => _captured = r),
            Arg.Any<SessionCredentials?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<FlatResponseData?>(FlatResponseBuilder.CountOnly(42)));

        await _client.QueryAsync<FakeAddr>().CountAsync();

        _captured!.MaxRows.Should().Be(0);
    }

    [Fact]
    public async Task Query_CountAsync_WithFilter_FilterIsPreserved()
    {
        _transport.SendQueryAsync(
            Arg.Do<OpaccQueryRequest>(r => _captured = r),
            Arg.Any<SessionCredentials?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<FlatResponseData?>(FlatResponseBuilder.CountOnly(0)));

        await _client.QueryAsync<FakeAddr>()
            .Where(a => a.City == "Bern")
            .CountAsync();

        _captured!.Filter.Should().Be("Addr.City = 'Bern'");
    }

    // ================================================================
    // Credentials
    // ================================================================

    [Fact]
    public async Task Query_WithCredentials_PassesUserIdToTransport()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>().WithCredentials(99).ToListAsync();
        _capturedCredentials!.UserId.Should().Be(99);
    }

    [Fact]
    public async Task Query_NoCredentials_PassesNullToTransport()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>().ToListAsync();
        _capturedCredentials.Should().BeNull();
    }

    // ================================================================
    // Response mapping
    // ================================================================

    [Fact]
    public async Task Query_Response_MappedToTypedObjects()
    {
        Setup(FlatResponseBuilder.FromColumns(
            ("Addr.Number", ["1001", "1002"]),
            ("Addr.FullName", ["Hans Müller", "Anna Schmidt"]),
            ("Addr.City", ["Bern", "Zürich"])));

        var result = await _client.QueryAsync<FakeAddr>()
            .Select(a => a.Number, a => a.FullName, a => a.City)
            .ToListAsync();

        result.Should().HaveCount(2);
        result[0].Number.Should().Be(1001);
        result[0].FullName.Should().Be("Hans Müller");
        result[0].City.Should().Be("Bern");
        result[1].Number.Should().Be(1002);
        result[1].FullName.Should().Be("Anna Schmidt");
    }

    [Fact]
    public async Task Query_Response_BooleanMapping()
    {
        Setup(FlatResponseBuilder.FromColumns(
            ("Addr.Number", ["1"]),
            ("Addr.IsPassive", ["1"])));

        var result = await _client.QueryAsync<FakeAddr>()
            .Select(a => a.Number, a => a.IsPassive)
            .FirstAsync();

        result!.IsPassive.Should().BeTrue();
    }

    [Fact]
    public async Task Query_Response_DateMapping()
    {
        Setup(FlatResponseBuilder.FromColumns(
            ("Addr.Number", ["1"]),
            ("Addr.DateOfEntry", ["20230615"])));

        var result = await _client.QueryAsync<FakeAddr>()
            .Select(a => a.Number, a => a.DateOfEntry)
            .FirstAsync();

        result!.DateOfEntry.Should().Be(new DateTime(2023, 6, 15));
    }

    [Fact]
    public async Task Query_NullResponse_ReturnsEmptyList()
    {
        Setup(null);
        var result = await _client.QueryAsync<FakeAddr>().ToListAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Query_FirstAsync_NullResponse_ReturnsNull()
    {
        Setup(null);
        var result = await _client.QueryAsync<FakeAddr>().FirstAsync();
        result.Should().BeNull();
    }

    // ================================================================
    // Columns selection
    // ================================================================

    [Fact]
    public async Task Query_Select_SingleProperty_AppearsInColumns()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .Select(a => a.FullName)
            .ToListAsync();

        _captured!.Columns.Should().ContainSingle(c => c.Expression == "Addr.FullName");
    }

    [Fact]
    public async Task Query_Select_MultipleProperties_AllAppearInColumns()
    {
        Setup();
        await _client.QueryAsync<FakeAddr>()
            .Select(a => a.FullName, a => a.City)
            .ToListAsync();

        _captured!.Columns.Should().Contain(c => c.Expression == "Addr.FullName");
        _captured.Columns.Should().Contain(c => c.Expression == "Addr.City");
    }

    // ================================================================
    // ToPageAsync
    // ================================================================

    [Fact]
    public async Task Query_ToPageAsync_RequiresOrderBy_ThrowsWithoutIt()
    {
        Setup();
        var act = async () => await _client.QueryAsync<FakeAddr>().ToPageAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*OrderBy*");
    }

    [Fact]
    public async Task Query_ToPageAsync_SetsScrollingToNe()
    {
        Setup(FlatResponseBuilder.Empty());
        await _client.QueryAsync<FakeAddr>().OrderBy(a => a.FullName).ToPageAsync();
        _captured!.Scrolling.Should().Be("ne");
    }

    [Fact]
    public async Task Query_ToPageAsync_DefaultPageSize_Is25()
    {
        Setup(FlatResponseBuilder.Empty());
        await _client.QueryAsync<FakeAddr>().OrderBy(a => a.FullName).ToPageAsync();
        _captured!.MaxRows.Should().Be(25);
    }

    [Fact]
    public async Task Query_ToPageAsync_Take_ControlsPageSize()
    {
        Setup(FlatResponseBuilder.Empty());
        await _client.QueryAsync<FakeAddr>().OrderBy(a => a.FullName).Take(10).ToPageAsync();
        _captured!.MaxRows.Should().Be(10);
    }

    [Fact]
    public async Task Query_ToPageAsync_PartialPage_HasNoNextCursor()
    {
        Setup(FlatResponseBuilder.FromColumns(
            ("Addr.Number", ["1", "2", "3"]),
            ("Addr.FullName", ["A", "B", "C"])));

        var page = await _client.QueryAsync<FakeAddr>()
            .OrderBy(a => a.FullName)
            .Take(10)
            .ToPageAsync();

        page.HasNextPage.Should().BeFalse();
        page.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Query_ToPageAsync_FullPageWithRedoData_HasNextCursor()
    {
        var data = FlatResponseBuilder.WithRedoData(
            FlatResponseBuilder.FromColumns(
                ("Addr.Number", ["1", "2", "3", "4", "5"]),
                ("Addr.FullName", Enumerable.Repeat("Name", 5).ToArray())),
            redoRows: ["tok1", "tok2", "tok3", "tok4", "tok5"]);

        Setup(data);

        var page = await _client.QueryAsync<FakeAddr>()
            .OrderBy(a => a.FullName)
            .Take(5)
            .ToPageAsync();

        page.HasNextPage.Should().BeTrue();
        page.NextCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Query_ToPageAsync_WithCursor_SetsRedoDataAndRedoArgs()
    {
        // First page — get cursor
        var data = FlatResponseBuilder.WithRedoData(
            FlatResponseBuilder.FromColumns(
                ("Addr.Number", ["1", "2", "3", "4", "5"]),
                ("Addr.FullName", Enumerable.Repeat("Name", 5).ToArray())),
            redoRows: ["v1", "v2", "v3", "v4", "v5"]);

        Setup(data);

        var firstPage = await _client.QueryAsync<FakeAddr>()
            .OrderBy(a => a.FullName)
            .Take(5)
            .ToPageAsync();

        firstPage.NextCursor.Should().NotBeNull();

        // Second page — verify RedoData/RedoArgs
        _captured = null;
        Setup(FlatResponseBuilder.Empty());

        await _client.QueryAsync<FakeAddr>()
            .OrderBy(a => a.FullName)
            .Take(5)
            .ToPageAsync(firstPage.NextCursor);

        _captured!.RedoData.Should().BeEquivalentTo(["v1", "v2", "v3", "v4", "v5"]);
        _captured.RedoArgs.Should().Be("ne,,5");
    }
}
