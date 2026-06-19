using FluentAssertions;
using NSubstitute;
using Opacc.Client.Enums;
using Opacc.Client.Operations.DeleteBo;
using Opacc.Client.Session;
using Opacc.Client.Tests.Helpers;
using Opacc.Client.Tests.TestModels;
using OpaccWebservice;

namespace Opacc.Client.Tests.DeleteBo;

/// <summary>
/// Tests for the DeleteBo builder, exercised through the public <see cref="OpaccClient"/> API.
/// The transport is mocked; the captured <see cref="OpaccDeleteBoRequest"/> is used to assert
/// that the correct parameters are sent to Opacc.
/// </summary>
public class DeleteBoBuilderTests
{
    private readonly IOpaccTransport _transport;
    private readonly OpaccClient _client;
    private OpaccDeleteBoRequest? _captured;
    private SessionCredentials? _capturedCredentials;

    public DeleteBoBuilderTests()
    {
        _transport = Substitute.For<IOpaccTransport>();
        _client    = new OpaccClient(_transport);
    }

    private void Setup(FlatResponseData? response = null)
    {
        _transport.SendDeleteBoAsync(
            Arg.Do<OpaccDeleteBoRequest>(r => _captured = r),
            Arg.Do<SessionCredentials?>(c => _capturedCredentials = c),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));
    }

    // ================================================================
    // BoEntity
    // ================================================================

    [Fact]
    public async Task DeleteBo_UsesBoEntityFromMetadata()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>().Start(1001).ExecuteAsync();
        _captured!.BoEntity.Should().Be("Addr");
    }

    // ================================================================
    // StartKeys
    // ================================================================

    [Fact]
    public async Task DeleteBo_Start_StringifiesValue()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>().Start(1001).ExecuteAsync();
        _captured!.StartKeys.Should().Be("1001");
    }

    [Fact]
    public async Task DeleteBo_Start_StringValue_PassedThrough()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>().Start("1001,2").ExecuteAsync();
        _captured!.StartKeys.Should().Be("1001,2");
    }

    [Fact]
    public async Task DeleteBo_NoStart_DefaultsToEmptyString()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>().ExecuteAsync();
        _captured!.StartKeys.Should().Be("");
    }

    [Fact]
    public async Task DeleteBo_StartModel_ComposesMultiSegmentKey()
    {
        Setup();
        await _client.DeleteBoAsync<FakeSalDocItem>()
            .Start(new FakeSalDocItem { SalDocInternalNo = 10, InternalNo = 5 })
            .ExecuteAsync();
        _captured!.StartKeys.Should().Be("10,5");
    }

    [Fact]
    public async Task DeleteBo_StartParams_ComposesMultiSegmentKey()
    {
        Setup();
        await _client.DeleteBoAsync<FakeSalDocItem>()
            .Start(10, 5)
            .ExecuteAsync();
        _captured!.StartKeys.Should().Be("10,5");
    }

    // ================================================================
    // SearchOperator
    // ================================================================

    [Fact]
    public async Task DeleteBo_DefaultSearchOperator_IsEqual()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>().ExecuteAsync();
        _captured!.SearchOperator.Should().Be(SearchOperator.Equal);
    }

    [Fact]
    public async Task DeleteBo_SearchOperator_CanBeOverridden()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>()
            .SearchOperator(SearchOperator.Next)
            .ExecuteAsync();
        _captured!.SearchOperator.Should().Be(SearchOperator.Next);
    }

    // ================================================================
    // Index
    // ================================================================

    [Fact]
    public async Task DeleteBo_DefaultIndex_UsesMetadataDefault()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>().ExecuteAsync();
        _captured!.IndexNo.Should().Be(1); // FakeAddr has [BoDefaultIndex(1)]
    }

    [Fact]
    public async Task DeleteBo_Index_OverridesDefault()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>().Index(3).ExecuteAsync();
        _captured!.IndexNo.Should().Be(3);
    }

    [Fact]
    public async Task DeleteBo_Index_WithFixedSegments_SetsFixedSegments()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>().Index(2, 1).ExecuteAsync();
        _captured!.IndexNo.Should().Be(2);
        _captured.FixedSegsOfBoIndex.Should().Be(1);
    }

    // ================================================================
    // IsTest
    // ================================================================

    [Fact]
    public async Task DeleteBo_DefaultIsTest_IsFalse()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>().ExecuteAsync();
        _captured!.IsTest.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBo_Test_SetsIsTestTrue()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>().Test().ExecuteAsync();
        _captured!.IsTest.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteBo_Test_ResultReflectsTestMode()
    {
        Setup(FlatResponseBuilder.Empty());
        var result = await _client.DeleteBoAsync<FakeAddr>().Test().ExecuteAsync();
        result.IsTest.Should().BeTrue();
    }

    // ================================================================
    // WithReport
    // ================================================================

    [Fact]
    public async Task DeleteBo_DefaultWithReport_IsTrue()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>().ExecuteAsync();
        _captured!.WithReport.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteBo_WithReport_False_SetsFlag()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>().WithReport(false).ExecuteAsync();
        _captured!.WithReport.Should().BeFalse();
    }

    // ================================================================
    // NoScript
    // ================================================================

    [Fact]
    public async Task DeleteBo_DefaultNoScript_IsFalse()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>().ExecuteAsync();
        _captured!.NoScript.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBo_NoScript_SetsFlag()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>().NoScript().ExecuteAsync();
        _captured!.NoScript.Should().BeTrue();
    }

    // ================================================================
    // Filter
    // ================================================================

    [Fact]
    public async Task DeleteBo_Filter_RawString_PassedToRequest()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>()
            .Filter("Addr.City = 'CH'")
            .ExecuteAsync();
        _captured!.Filter.Should().Be("Addr.City = 'CH'");
    }

    [Fact]
    public async Task DeleteBo_Filter_EmptyString_Ignored()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>()
            .Filter("")
            .ExecuteAsync();
        _captured!.Filter.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBo_MultipleFilters_CombinedWithAnd()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>()
            .Filter("Addr.City = 'Bern'")
            .Filter("Addr.IsPassive = 0")
            .ExecuteAsync();
        _captured!.Filter.Should().Be("(Addr.City = 'Bern') and (Addr.IsPassive = 0)");
    }

    [Fact]
    public async Task DeleteBo_Where_Lambda_TranslatesFilter()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>()
            .Where(a => a.City == "Zürich")
            .ExecuteAsync();
        _captured!.Filter.Should().Be("Addr.City = 'Zürich'");
    }

    // ================================================================
    // ResultObject
    // ================================================================

    [Fact]
    public async Task DeleteBo_NoResultObject_DefaultsToEmptyString()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>().ExecuteAsync();
        _captured!.ResultObject.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task DeleteBo_ResultObject_PassedToRequest()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>()
            .ResultObject("Addr.Number,Addr.FullName")
            .ExecuteAsync();
        _captured!.ResultObject.Should().Be("Addr.Number,Addr.FullName");
    }

    // ================================================================
    // WithCredentials
    // ================================================================

    [Fact]
    public async Task DeleteBo_WithCredentials_PassesUserIdToTransport()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>().WithCredentials(42).ExecuteAsync();
        _capturedCredentials!.UserId.Should().Be(42);
        _capturedCredentials.Password.Should().BeNull();
    }

    [Fact]
    public async Task DeleteBo_NoCredentials_PassesNullToTransport()
    {
        Setup();
        await _client.DeleteBoAsync<FakeAddr>().ExecuteAsync();
        _capturedCredentials.Should().BeNull();
    }

    // ================================================================
    // Response mapping — DeleteBoRecord
    // ================================================================

    [Fact]
    public async Task DeleteBo_Response_ParsesDeletedRecords()
    {
        Setup(FlatResponseBuilder.FromColumns(
            ("BoId",            ["101", "102"]),
            ("BoNumber",        ["1001", "1002"]),
            ("BoName",          ["Müller AG", "Schmidt GmbH"]),
            ("DeleteBoStateCd", ["0", "0"]),
            ("DeleteBoInfo",    ["", ""])));

        var result = await _client.DeleteBoAsync<FakeAddr>().ExecuteAsync();

        result.Records.Should().HaveCount(2);
        result.DeletedCount.Should().Be(2);
        result.HasErrors.Should().BeFalse();

        result.Records[0].BoId.Should().Be("101");
        result.Records[0].BoNumber.Should().Be("1001");
        result.Records[0].BoName.Should().Be("Müller AG");
        result.Records[0].DeleteBoStateCd.Should().Be(0);
        result.Records[0].IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteBo_Response_ParsesErrorRecord()
    {
        Setup(FlatResponseBuilder.FromColumns(
            ("BoId",            ["201"]),
            ("BoNumber",        ["2001"]),
            ("BoName",          ["Locked Corp"]),
            ("DeleteBoStateCd", ["1"]),
            ("DeleteBoInfo",    ["Referenced by open order"])));

        var result = await _client.DeleteBoAsync<FakeAddr>().ExecuteAsync();

        result.Records.Should().HaveCount(1);
        result.DeletedCount.Should().Be(0);
        result.HasErrors.Should().BeTrue();

        result.Records[0].DeleteBoStateCd.Should().Be(1);
        result.Records[0].HasError.Should().BeTrue();
        result.Records[0].IsDeleted.Should().BeFalse();
        result.Records[0].DeleteBoInfo.Should().Be("Referenced by open order");
    }

    [Fact]
    public async Task DeleteBo_Response_ParsesNotFoundRecord()
    {
        Setup(FlatResponseBuilder.FromColumns(
            ("BoId",            ["999"]),
            ("BoNumber",        ["9999"]),
            ("BoName",          [""]),
            ("DeleteBoStateCd", ["2"]),
            ("DeleteBoInfo",    ["BO not found"])));

        var result = await _client.DeleteBoAsync<FakeAddr>().ExecuteAsync();

        result.Records[0].DeleteBoStateCd.Should().Be(2);
        result.Records[0].WasNotFound.Should().BeTrue();
        result.Records[0].IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBo_Response_MixedOutcomes_CountsOnlyDeleted()
    {
        Setup(FlatResponseBuilder.FromColumns(
            ("BoId",            ["1", "2", "3"]),
            ("BoNumber",        ["100", "200", "300"]),
            ("BoName",          ["A", "B", "C"]),
            ("DeleteBoStateCd", ["0", "1", "2"]),
            ("DeleteBoInfo",    ["", "Locked", "Not found"])));

        var result = await _client.DeleteBoAsync<FakeAddr>().ExecuteAsync();

        result.Records.Should().HaveCount(3);
        result.DeletedCount.Should().Be(1);
        result.HasErrors.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteBo_WithReport_False_ReturnsEmptyRecordsList()
    {
        Setup(FlatResponseBuilder.Empty());
        var result = await _client.DeleteBoAsync<FakeAddr>().WithReport(false).ExecuteAsync();
        result.Records.Should().BeEmpty();
        result.DeletedCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteBo_NullResponse_ReturnsEmptyRecordsList()
    {
        Setup(null);
        var result = await _client.DeleteBoAsync<FakeAddr>().ExecuteAsync();
        result.Records.Should().BeEmpty();
        result.DeletedCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteBo_EmptyResponse_ReturnsEmptyRecordsList()
    {
        Setup(FlatResponseBuilder.Empty());
        var result = await _client.DeleteBoAsync<FakeAddr>().ExecuteAsync();
        result.Records.Should().BeEmpty();
    }
}
