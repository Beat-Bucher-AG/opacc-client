using FluentAssertions;
using NSubstitute;
using Opacc.Client.Enums;
using Opacc.Client.Operations.GetBo;
using Opacc.Client.Session;
using Opacc.Client.Tests.Helpers;
using Opacc.Client.Tests.TestModels;
using OpaccWebservice;

namespace Opacc.Client.Tests.GetBo;

/// <summary>
/// Tests for the GetBo builder, exercised through the public <see cref="OpaccClient"/> API.
/// The transport is mocked; the captured <see cref="OpaccGetBoRequest"/> is used to assert
/// that the correct parameters are sent to Opacc.
/// </summary>
public class GetBoBuilderTests
{
    private readonly IOpaccTransport _transport;
    private readonly OpaccClient _client;
    private OpaccGetBoRequest? _captured;
    private SessionCredentials? _capturedCredentials;

    public GetBoBuilderTests()
    {
        _transport = Substitute.For<IOpaccTransport>();
        _client = new OpaccClient(_transport);
    }

    /// Sets up the transport mock to capture the request and return the given response.
    private void Setup(FlatResponseData? response = null)
    {
        _transport.SendGetBoAsync(
            Arg.Do<OpaccGetBoRequest>(r => _captured = r),
            Arg.Do<SessionCredentials?>(c => _capturedCredentials = c),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));
    }

    // ================================================================
    // BoEntity
    // ================================================================

    [Fact]
    public async Task GetBo_UsesBoEntityFromMetadata()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>().FirstAsync();
        _captured!.BoEntity.Should().Be("Addr");
    }

    // ================================================================
    // Start
    // ================================================================

    [Fact]
    public async Task GetBo_DefaultStart_IsEmpty()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>().FirstAsync();
        _captured!.Start.Should().Be("");
    }

    [Fact]
    public async Task GetBo_Start_StringifiesValue()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>().Start(1001).FirstAsync();
        _captured!.Start.Should().Be("1001");
    }

    [Fact]
    public async Task GetBo_Start_String_PassedThrough()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>().Start("ABC").FirstAsync();
        _captured!.Start.Should().Be("ABC");
    }

    [Fact]
    public async Task GetBo_StartModel_ComposesMultiSegmentKey()
    {
        Setup();
        await _client.GetBoAsync<FakeSalDocItem>()
            .Start(new FakeSalDocItem { SalDocInternalNo = 10, InternalNo = 5 })
            .FirstAsync();
        _captured!.Start.Should().Be("10,5");
        _captured.Segment.Should().Be(2);
    }

    [Fact]
    public async Task GetBo_StartParams_ComposesMultiSegmentKey()
    {
        Setup();
        await _client.GetBoAsync<FakeSalDocItem>()
            .Start(10, 5)
            .FirstAsync();
        _captured!.Start.Should().Be("10,5");
    }

    // ================================================================
    // SearchOperator
    // ================================================================

    [Fact]
    public async Task GetBo_DefaultSearchOperator_IsEqual()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>().FirstAsync();
        _captured!.SearchOperator.Should().Be(SearchOperator.Equal);
    }

    [Fact]
    public async Task GetBo_SearchOperator_Next_IsSet()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>()
            .SearchOperator(SearchOperator.Next)
            .FirstAsync();
        _captured!.SearchOperator.Should().Be(SearchOperator.Next);
    }

    // ================================================================
    // Index
    // ================================================================

    [Fact]
    public async Task GetBo_DefaultIndex_UsesMetadataDefault()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>().FirstAsync();
        _captured!.IndexNo.Should().Be(1); // FakeAddr has [BoDefaultIndex(1)]
    }

    [Fact]
    public async Task GetBo_Index_OverridesDefault()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>().Index(3).FirstAsync();
        _captured!.IndexNo.Should().Be(3);
    }

    [Fact]
    public async Task GetBo_Index_WithSegment_SetsSegment()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>().Index(2, 5).FirstAsync();
        _captured!.IndexNo.Should().Be(2);
        _captured.Segment.Should().Be(5);
    }

    // ================================================================
    // Count / FirstAsync / Take
    // ================================================================

    [Fact]
    public async Task GetBo_FirstAsync_SetsCountToOne()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>().FirstAsync();
        _captured!.Count.Should().Be(1);
    }

    [Fact]
    public async Task GetBo_Take_SetsCount()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>().Take(25).ToListAsync();
        _captured!.Count.Should().Be(25);
    }

    [Fact]
    public async Task GetBo_DefaultCount_IsOne()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>().ToListAsync();
        _captured!.Count.Should().Be(1);
    }

    // ================================================================
    // Skip + Take
    // ================================================================

    [Fact]
    public async Task GetBo_SkipAndTake_SendsSumAsCount()
    {
        Setup(FlatResponseBuilder.FromColumns(
            ("Addr.Number", ["1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15"]),
            ("Addr.FullName", Enumerable.Range(1, 15).Select(i => $"Name {i}").ToArray())));

        var result = await _client.GetBoAsync<FakeAddr>().Skip(5).Take(10).ToListAsync();

        _captured!.Count.Should().Be(15);   // 5 + 10 sent to Opacc
        result.Should().HaveCount(10);      // first 5 discarded client-side
    }

    [Fact]
    public async Task GetBo_Skip_WithoutTake_ThrowsInvalidOperation()
    {
        Setup();
        var act = async () => await _client.GetBoAsync<FakeAddr>().Skip(5).ToListAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Skip*requires*Take*");
    }

    [Fact]
    public async Task GetBo_SkipPlusTakeExceedsLimit_ThrowsInvalidOperation()
    {
        Setup();
        var act = async () => await _client.GetBoAsync<FakeAddr>().Skip(99_990).Take(10).ToListAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*99999*");
    }

    // ================================================================
    // Filter
    // ================================================================

    [Fact]
    public async Task GetBo_Filter_RawString_PassedToRequest()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>()
            .Filter("Addr.City = 'Bern'")
            .FirstAsync();
        _captured!.Filter.Should().Be("Addr.City = 'Bern'");
    }

    [Fact]
    public async Task GetBo_Filter_EmptyString_Ignored()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>()
            .Filter("")
            .FirstAsync();
        _captured!.Filter.Should().BeNull();
    }

    [Fact]
    public async Task GetBo_MultipleFilters_CombinedWithAnd()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>()
            .Filter("Addr.City = 'Bern'")
            .Filter("Addr.IsPassive = 0")
            .FirstAsync();
        _captured!.Filter.Should().Be("(Addr.City = 'Bern') and (Addr.IsPassive = 0)");
    }

    // ================================================================
    // Where (lambda predicate)
    // ================================================================

    [Fact]
    public async Task GetBo_Where_StringEquality_TranslatesFilter()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>()
            .Where(a => a.City == "Zürich")
            .FirstAsync();
        _captured!.Filter.Should().Be("Addr.City = 'Zürich'");
    }

    [Fact]
    public async Task GetBo_Where_IntGreaterThan_TranslatesFilter()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>()
            .Where(a => a.Number > 1000)
            .FirstAsync();
        _captured!.Filter.Should().Be("Addr.Number > 1000");
    }

    [Fact]
    public async Task GetBo_Where_BooleanProperty_TranslatesFilter()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>()
            .Where(a => a.IsPassive)
            .FirstAsync();
        _captured!.Filter.Should().Be("Addr.IsPassive = 1");
    }

    [Fact]
    public async Task GetBo_Where_BooleanFalse_TranslatesFilter()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>()
            .Where(a => a.IsPassive == false)
            .FirstAsync();
        _captured!.Filter.Should().Be("Addr.IsPassive = 0");
    }

    [Fact]
    public async Task GetBo_Where_NullComparison_TranslatesFilter()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>()
            .Where(a => a.City == null)
            .FirstAsync();
        _captured!.Filter.Should().Be("Addr.City = ''");
    }

    [Fact]
    public async Task GetBo_Where_AndCombination_TranslatesFilter()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>()
            .Where(a => a.City == "Bern" && a.IsPassive == false)
            .FirstAsync();
        _captured!.Filter.Should().Be("(Addr.City = 'Bern') and (Addr.IsPassive = 0)");
    }

    [Fact]
    public async Task GetBo_Where_OrCombination_TranslatesFilter()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>()
            .Where(a => a.City == "Bern" || a.City == "Zürich")
            .FirstAsync();
        _captured!.Filter.Should().Be("(Addr.City = 'Bern') or (Addr.City = 'Zürich')");
    }

    [Fact]
    public async Task GetBo_Where_StringWithSingleQuote_IsEscaped()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>()
            .Where(a => a.City == "O'Brien")
            .FirstAsync();
        _captured!.Filter.Should().Be("Addr.City = 'O''Brien'");
    }

    [Fact]
    public async Task GetBo_MultipleWhere_CombinedWithAnd()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>()
            .Where(a => a.City == "Bern")
            .Where(a => a.Number > 1000)
            .FirstAsync();
        _captured!.Filter.Should().Be("(Addr.City = 'Bern') and (Addr.Number > 1000)");
    }

    // ================================================================
    // Select
    // ================================================================

    [Fact]
    public async Task GetBo_Select_SingleProperty_BuildsSelectString()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>()
            .Select(a => a.FullName)
            .FirstAsync();
        _captured!.SelectAttributes.Should().Be("Addr.FullName");
    }

    [Fact]
    public async Task GetBo_Select_MultipleProperties_BuildsSelectString()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>()
            .Select(a => a.FullName, a => a.City)
            .FirstAsync();
        _captured!.SelectAttributes.Should().Be("Addr.FullName, Addr.City");
    }

    [Fact]
    public async Task GetBo_NoSelect_LoadsAllProperties()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>().FirstAsync();

        // Without explicit Select, all non-virtual properties are loaded
        var selectParts = _captured!.SelectAttributes!.Split(", ");
        selectParts.Should().Contain("Addr.Number");
        selectParts.Should().Contain("Addr.FullName");
        selectParts.Should().Contain("Addr.City");
        selectParts.Should().Contain("Addr.IsPassive");
        selectParts.Should().Contain("Addr.DateOfEntry");
    }

    // ================================================================
    // WithCredentials
    // ================================================================

    [Fact]
    public async Task GetBo_WithCredentials_PassesUserIdToTransport()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>()
            .WithCredentials(42)
            .FirstAsync();
        _capturedCredentials!.UserId.Should().Be(42);
        _capturedCredentials.Password.Should().BeNull();
    }

    [Fact]
    public async Task GetBo_WithCredentials_WithPassword_PassesBoth()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>()
            .WithCredentials(42, "secret")
            .FirstAsync();
        _capturedCredentials!.UserId.Should().Be(42);
        _capturedCredentials.Password.Should().Be("secret");
    }

    [Fact]
    public async Task GetBo_NoCredentials_PassesNullToTransport()
    {
        Setup();
        await _client.GetBoAsync<FakeAddr>().FirstAsync();
        _capturedCredentials.Should().BeNull();
    }

    // ================================================================
    // Response mapping
    // ================================================================

    [Fact]
    public async Task GetBo_Response_MappedToTypedObjects()
    {
        Setup(FlatResponseBuilder.FromColumns(
            ("Addr.Number", ["1001", "1002"]),
            ("Addr.FullName", ["Hans Müller", "Anna Schmidt"]),
            ("Addr.City", ["Bern", "Zürich"])));

        var result = await _client.GetBoAsync<FakeAddr>().Take(2).ToListAsync();

        result.Should().HaveCount(2);
        result[0].Number.Should().Be(1001);
        result[0].FullName.Should().Be("Hans Müller");
        result[0].City.Should().Be("Bern");
        result[1].Number.Should().Be(1002);
        result[1].FullName.Should().Be("Anna Schmidt");
    }

    [Fact]
    public async Task GetBo_Response_BooleanMapping()
    {
        Setup(FlatResponseBuilder.FromColumns(
            ("Addr.Number", ["1"]),
            ("Addr.IsPassive", ["1"])));

        var result = await _client.GetBoAsync<FakeAddr>().Select(a => a.Number, a => a.IsPassive).FirstAsync();

        result!.IsPassive.Should().BeTrue();
    }

    [Fact]
    public async Task GetBo_Response_DateMapping()
    {
        Setup(FlatResponseBuilder.FromColumns(
            ("Addr.Number", ["1"]),
            ("Addr.DateOfEntry", ["15.06.2023"])));

        var result = await _client.GetBoAsync<FakeAddr>()
            .Select(a => a.Number, a => a.DateOfEntry)
            .FirstAsync();

        result!.DateOfEntry.Should().Be(new DateTime(2023, 6, 15));
    }

    [Fact]
    public async Task GetBo_Response_EmptyData_ReturnsEmptyList()
    {
        Setup(FlatResponseBuilder.Empty());
        var result = await _client.GetBoAsync<FakeAddr>().Take(10).ToListAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBo_NullResponse_ReturnsEmptyList()
    {
        Setup(null);
        var result = await _client.GetBoAsync<FakeAddr>().Take(10).ToListAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBo_FirstAsync_NullResponse_ReturnsNull()
    {
        Setup(null);
        var result = await _client.GetBoAsync<FakeAddr>().FirstAsync();
        result.Should().BeNull();
    }

    // ================================================================
    // Pagination (ToPageAsync)
    // ================================================================

    [Fact]
    public async Task GetBo_ToPageAsync_DefaultPageSize_Is25()
    {
        Setup(FlatResponseBuilder.Empty());
        await _client.GetBoAsync<FakeAddr>().ToPageAsync();
        _captured!.Count.Should().Be(25);
    }

    [Fact]
    public async Task GetBo_ToPageAsync_Take_ControlsPageSize()
    {
        Setup(FlatResponseBuilder.Empty());
        await _client.GetBoAsync<FakeAddr>().Take(10).ToPageAsync();
        _captured!.Count.Should().Be(10);
    }

    [Fact]
    public async Task GetBo_ToPageAsync_PartialPage_HasNoNextCursor()
    {
        // 3 rows returned when page size is 10 → no next page
        Setup(FlatResponseBuilder.FromColumns(
            ("Addr.Number", ["1", "2", "3"]),
            ("Addr.FullName", ["A", "B", "C"])));

        var page = await _client.GetBoAsync<FakeAddr>().Take(10).ToPageAsync();

        page.HasNextPage.Should().BeFalse();
        page.NextCursor.Should().BeNull();
        page.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetBo_ToPageAsync_FullPage_HasNextCursor()
    {
        // Exactly page size rows → Opacc might have more
        var rows = Enumerable.Range(1, 5).Select(i => i.ToString()).ToArray();
        Setup(FlatResponseBuilder.FromColumns(
            ("Addr.Number", rows),
            ("Addr.FullName", Enumerable.Repeat("Name", 5).ToArray())));

        var page = await _client.GetBoAsync<FakeAddr>().Take(5).ToPageAsync();

        page.HasNextPage.Should().BeTrue();
        page.NextCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetBo_ToPageAsync_WithCursor_UsesNextOperatorAndDecodedStart()
    {
        // First page returns a cursor
        var rows = Enumerable.Range(1, 5).Select(i => i.ToString()).ToArray();
        Setup(FlatResponseBuilder.FromColumns(
            ("Addr.Number", rows),
            ("Addr.FullName", Enumerable.Repeat("Name", 5).ToArray())));

        var firstPage = await _client.GetBoAsync<FakeAddr>().Take(5).ToPageAsync();
        firstPage.NextCursor.Should().NotBeNull();

        // Second page — reset capture
        _captured = null;
        Setup(FlatResponseBuilder.Empty());

        await _client.GetBoAsync<FakeAddr>().Take(5).ToPageAsync(firstPage.NextCursor);

        // Builder must use Next operator with the last ID as start
        _captured!.SearchOperator.Should().Be(SearchOperator.Next);
        _captured.Start.Should().Be("5"); // last row's Addr.Number value
    }
}
