using FluentAssertions;
using NSubstitute;
using Opacc.Client.Enums;
using Opacc.Client.Operations.SaveBo;
using Opacc.Client.Session;
using Opacc.Client.Tests.Helpers;
using Opacc.Client.Tests.TestModels;
using OpaccWebservice;

namespace Opacc.Client.Tests.SaveBo;

/// <summary>
/// Tests for the SaveBo builder, exercised through the public <see cref="OpaccClient"/> API.
/// The transport is mocked; the captured <see cref="OpaccSaveBoRequest"/> is used to assert
/// that the correct parameters are sent to Opacc.
///
/// Note: Update / CreateOrUpdate now require a start key (auto-derived from the index key
/// segments set via .Set(...), or supplied via .Start(...)/.Where()/.Filter()). Tests that only
/// exercise unrelated facets add a bare <c>.Start(1001)</c> to satisfy that contract.
/// </summary>
public class SaveBoBuilderTests
{
    private readonly IOpaccTransport _transport;
    private readonly OpaccClient _client;
    private OpaccSaveBoRequest? _captured;
    private SessionCredentials? _capturedCredentials;

    public SaveBoBuilderTests()
    {
        _transport = Substitute.For<IOpaccTransport>();
        _client    = new OpaccClient(_transport);
    }

    private void Setup(FlatResponseData? response = null)
    {
        _transport.SendSaveBoAsync(
            Arg.Do<OpaccSaveBoRequest>(r => _captured = r),
            Arg.Do<SessionCredentials?>(c => _capturedCredentials = c),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));
    }

    // ================================================================
    // BoEntity
    // ================================================================

    [Fact]
    public async Task SaveBo_UsesBoEntityFromMetadata()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>().Start(1001).ExecuteAsync();
        _captured!.BoEntity.Should().Be("Addr");
    }

    // ================================================================
    // Operation (default: CreateOrUpdate)
    // ================================================================

    [Fact]
    public async Task SaveBo_DefaultOperation_IsCreateOrUpdate()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>().Start(1001).ExecuteAsync();
        _captured!.Operation.Should().Be(SaveBoOperation.CreateOrUpdate);
    }

    [Fact]
    public async Task SaveBo_Create_SetsOperation()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>().Create().ExecuteAsync();
        _captured!.Operation.Should().Be(SaveBoOperation.Create);
    }

    [Fact]
    public async Task SaveBo_Update_SetsOperation()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>().Update().Start(1001).ExecuteAsync();
        _captured!.Operation.Should().Be(SaveBoOperation.Update);
    }

    [Fact]
    public async Task SaveBo_CreateOrUpdate_SetsOperation()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>().CreateOrUpdate().Start(1001).ExecuteAsync();
        _captured!.Operation.Should().Be(SaveBoOperation.CreateOrUpdate);
    }

    // ================================================================
    // StartKeys
    // ================================================================

    [Fact]
    public async Task SaveBo_Start_StringifiesValue()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>().Start(1001).ExecuteAsync();
        _captured!.StartKeys.Should().Be("1001");
    }

    [Fact]
    public async Task SaveBo_Create_HasEmptyStartKeys()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>().Create().ExecuteAsync();
        _captured!.StartKeys.Should().Be("");
        _captured.FixedSegsOfBoIndex.Should().Be(0);
    }

    // ================================================================
    // Index
    // ================================================================

    [Fact]
    public async Task SaveBo_DefaultIndex_UsesMetadataDefault()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>().Start(1001).ExecuteAsync();
        _captured!.IndexNo.Should().Be(1);
    }

    [Fact]
    public async Task SaveBo_Index_WithFixedSegments()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>().Index(3, 1).Start(1001).ExecuteAsync();
        _captured!.IndexNo.Should().Be(3);
        _captured.FixedSegsOfBoIndex.Should().Be(1);
    }

    // ================================================================
    // NoScript
    // ================================================================

    [Fact]
    public async Task SaveBo_DefaultNoScript_IsFalse()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>().Start(1001).ExecuteAsync();
        _captured!.NoScript.Should().BeFalse();
    }

    [Fact]
    public async Task SaveBo_NoScript_SetsFlag()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>().Start(1001).NoScript().ExecuteAsync();
        _captured!.NoScript.Should().BeTrue();
    }

    // ================================================================
    // Set (typed lambda)
    // ================================================================

    [Fact]
    public async Task SaveBo_Set_StringProperty_BuildsAtAssignment()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>()
            .Start(1001)
            .Set(a => a.City, "Bern")
            .ExecuteAsync();
        _captured!.Assignments.Should().Contain("Addr.City=@Bern");
    }

    [Fact]
    public async Task SaveBo_Set_IntProperty_BuildsAtAssignment()
    {
        Setup();
        // Number is the index-1 key segment → auto-derives the start key and (for CreateOrUpdate)
        // is also emitted as an assignment.
        await _client.SaveBoAsync<FakeAddr>()
            .Set(a => a.Number, 1001)
            .ExecuteAsync();
        _captured!.Assignments.Should().Contain("Addr.Number=@1001");
        _captured.StartKeys.Should().Be("1001");
    }

    [Fact]
    public async Task SaveBo_Set_BoolTrue_SerializesAsOne()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>()
            .Start(1001)
            .Set(a => a.IsPassive, true)
            .ExecuteAsync();
        _captured!.Assignments.Should().Contain("Addr.IsPassive=@1");
    }

    [Fact]
    public async Task SaveBo_Set_BoolFalse_SerializesAsZero()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>()
            .Start(1001)
            .Set(a => a.IsPassive, false)
            .ExecuteAsync();
        _captured!.Assignments.Should().Contain("Addr.IsPassive=@0");
    }

    [Fact]
    public async Task SaveBo_Set_NullableString_NullValue_SerializesAsEmpty()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>()
            .Start(1001)
            .Set(a => a.City, null!)
            .ExecuteAsync();
        _captured!.Assignments.Should().Contain("Addr.City=@");
    }

    [Fact]
    public async Task SaveBo_Set_Date_UsesOpaccDateFormat()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>()
            .Start(1001)
            .Set(a => a.DateOfEntry, new DateTime(2024, 3, 15))
            .ExecuteAsync();
        _captured!.Assignments.Should().Contain("Addr.DateOfEntry=@15.03.2024");
    }

    [Fact]
    public async Task SaveBo_MultipleSet_AllAssignmentsPresent()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>()
            .Set(a => a.City, "Zürich")
            .Set(a => a.Number, 42)
            .Set(a => a.IsPassive, false)
            .ExecuteAsync();

        _captured!.Assignments.Should().HaveCount(3);
        _captured.Assignments.Should().Contain("Addr.City=@Zürich");
        _captured.Assignments.Should().Contain("Addr.Number=@42");
        _captured.Assignments.Should().Contain("Addr.IsPassive=@0");
    }

    // ================================================================
    // SetRaw
    // ================================================================

    [Fact]
    public async Task SaveBo_SetRaw_BuildsAssignment()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>()
            .Start(1001)
            .SetRaw("Addr.Keyword", "FirstName + ' ' + LastName")
            .ExecuteAsync();
        _captured!.Assignments.Should().Contain("Addr.Keyword=@FirstName + ' ' + LastName");
    }

    [Fact]
    public async Task SaveBo_SetRaw_NullValue_SerializesAsEmpty()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>()
            .Start(1001)
            .SetRaw("Addr.City", null)
            .ExecuteAsync();
        _captured!.Assignments.Should().Contain("Addr.City=@");
    }

    // ================================================================
    // SetFrom (model)
    // ================================================================

    [Fact]
    public async Task SaveBo_SetFrom_IncludesAllWritableProperties()
    {
        Setup();
        var model = new FakeAddr { Number = 1001, FullName = "Hans Müller", City = "Bern", IsPassive = false };

        await _client.SaveBoAsync<FakeAddr>()
            .Create()
            .SetFrom(model)
            .ExecuteAsync();

        _captured!.Assignments.Should().Contain("Addr.Number=@1001");
        _captured.Assignments.Should().Contain("Addr.FullName=@Hans Müller");
        _captured.Assignments.Should().Contain("Addr.City=@Bern");
        _captured.Assignments.Should().Contain("Addr.IsPassive=@0");
    }

    [Fact]
    public async Task SaveBo_SetFrom_WithExplicitProperties_OnlyIncludesThose()
    {
        Setup();
        var model = new FakeAddr { Number = 1001, FullName = "Hans", City = "Bern" };

        await _client.SaveBoAsync<FakeAddr>()
            .Update()
            .Start(model.Number)
            .SetFrom(model, a => a.City, a => a.FullName)
            .ExecuteAsync();

        _captured!.Assignments.Should().HaveCount(2);
        _captured.Assignments.Should().Contain("Addr.City=@Bern");
        _captured.Assignments.Should().Contain("Addr.FullName=@Hans");
        _captured.Assignments.Should().NotContain(a => a.StartsWith("Addr.Number="));
    }

    // ================================================================
    // Filter
    // ================================================================

    [Fact]
    public async Task SaveBo_Filter_PassedToRequest()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>()
            .Filter("Addr.CountrySc = 'CH'")
            .ExecuteAsync();
        _captured!.Filter.Should().Be("Addr.CountrySc = 'CH'");
    }

    [Fact]
    public async Task SaveBo_MultipleFilters_CombinedWithAnd()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>()
            .Filter("Addr.CountrySc = 'CH'")
            .Filter("Addr.IsPassive = 0")
            .ExecuteAsync();
        _captured!.Filter.Should().Be("(Addr.CountrySc = 'CH') and (Addr.IsPassive = 0)");
    }

    [Fact]
    public async Task SaveBo_Where_Lambda_TranslatesFilter()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>()
            .Update()
            .Where(a => a.City == "Bern")
            .Set(a => a.IsPassive, true)
            .ExecuteAsync();
        _captured!.Filter.Should().Be("Addr.City = 'Bern'");
    }

    // ================================================================
    // ResultObject
    // ================================================================

    [Fact]
    public async Task SaveBo_ResultObject_PassedToRequest()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>()
            .Start(1001)
            .ResultObject("Addr.Number,Addr.FullName")
            .ExecuteAsync();
        _captured!.ResultObject.Should().Be("Addr.Number,Addr.FullName");
    }

    // ================================================================
    // WithCredentials
    // ================================================================

    [Fact]
    public async Task SaveBo_WithCredentials_PassesUserIdToTransport()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>().Start(1001).WithCredentials(42).ExecuteAsync();
        _capturedCredentials!.UserId.Should().Be(42);
    }

    [Fact]
    public async Task SaveBo_NoCredentials_PassesNullToTransport()
    {
        Setup();
        await _client.SaveBoAsync<FakeAddr>().Start(1001).ExecuteAsync();
        _capturedCredentials.Should().BeNull();
    }

    // ================================================================
    // Auto-derivation of the start key (multi-segment index)
    // ================================================================

    [Fact]
    public async Task SaveBo_Update_DerivesStartKeyFromSetSegments_ExcludesKeysFromAssignments()
    {
        Setup();
        await _client.SaveBoAsync<FakeSalDocItem>()
            .Update()
            .Set(i => i.SalDocInternalNo, 10)
            .Set(i => i.InternalNo, 5)
            .Set(i => i.SRebatePerc, "3")
            .ExecuteAsync();

        _captured!.StartKeys.Should().Be("10,5");
        _captured.FixedSegsOfBoIndex.Should().Be(2);
        _captured.IndexNo.Should().Be(4);
        _captured.Operation.Should().Be(SaveBoOperation.Update);
        _captured.Assignments.Should().ContainSingle().Which.Should().Be("SRebatePerc=@3");
    }

    [Fact]
    public async Task SaveBo_CreateOrUpdate_IncludesKeysInAssignments()
    {
        Setup();
        await _client.SaveBoAsync<FakeSalDocItem>()
            .CreateOrUpdate()
            .Set(i => i.SalDocInternalNo, 10)
            .Set(i => i.InternalNo, 5)
            .Set(i => i.SRebatePerc, "3")
            .ExecuteAsync();

        _captured!.StartKeys.Should().Be("10,5");
        _captured.FixedSegsOfBoIndex.Should().Be(2);
        _captured.Assignments.Should().BeEquivalentTo("SalDocInternalNo=@10", "InternalNo=@5", "SRebatePerc=@3");
    }

    [Fact]
    public async Task SaveBo_Create_EmptyStartKeys_KeysGoIntoAssignments()
    {
        Setup();
        await _client.SaveBoAsync<FakeSalDocItem>()
            .Create()
            .Set(i => i.SalDocInternalNo, 10)
            .Set(i => i.InternalNo, 5)
            .Set(i => i.SRebatePerc, "3")
            .ExecuteAsync();

        _captured!.StartKeys.Should().Be("");
        _captured.FixedSegsOfBoIndex.Should().Be(0);
        _captured.Assignments.Should().BeEquivalentTo("SalDocInternalNo=@10", "InternalNo=@5", "SRebatePerc=@3");
    }

    [Fact]
    public async Task SaveBo_PartialLeadingKey_FixesOnlyProvidedSegments()
    {
        Setup();
        await _client.SaveBoAsync<FakeSalDocItem>()
            .Update()
            .Set(i => i.SalDocInternalNo, 10)
            .Set(i => i.SRebatePerc, "3")
            .ExecuteAsync();

        _captured!.StartKeys.Should().Be("10");
        _captured.FixedSegsOfBoIndex.Should().Be(1);
        _captured.Assignments.Should().ContainSingle().Which.Should().Be("SRebatePerc=@3");
    }

    [Fact]
    public async Task SaveBo_FixedSegments_OverridesDerivedCount()
    {
        Setup();
        await _client.SaveBoAsync<FakeSalDocItem>()
            .Update()
            .Set(i => i.SalDocInternalNo, 10)
            .Set(i => i.InternalNo, 5)
            .Set(i => i.SRebatePerc, "3")
            .FixedSegments(1)
            .ExecuteAsync();

        _captured!.StartKeys.Should().Be("10,5");
        _captured.FixedSegsOfBoIndex.Should().Be(1);
    }

    [Fact]
    public async Task SaveBo_ExplicitStart_OverridesDerivation()
    {
        Setup();
        await _client.SaveBoAsync<FakeSalDocItem>()
            .Update()
            .Start("99,99")
            .Set(i => i.SalDocInternalNo, 10)
            .Set(i => i.InternalNo, 5)
            .Set(i => i.SRebatePerc, "3")
            .ExecuteAsync();

        _captured!.StartKeys.Should().Be("99,99");
        _captured.Assignments.Should().ContainSingle().Which.Should().Be("SRebatePerc=@3");
    }

    [Fact]
    public async Task SaveBo_StartParams_ComposesCompositeKey()
    {
        Setup();
        // Composite-key pattern (e.g. SalDocItemPoolItem): leading segments via .Start(...),
        // field values via .Set(...). The individual key-segment fields are not set directly.
        await _client.SaveBoAsync<FakeSalDocItem>()
            .CreateOrUpdate()
            .Start(10, 5)
            .Set(i => i.SRebatePerc, "3")
            .ExecuteAsync();

        _captured!.StartKeys.Should().Be("10,5");
        _captured.FixedSegsOfBoIndex.Should().Be(2);
        _captured.Assignments.Should().BeEquivalentTo("SRebatePerc=@3");
    }

    [Fact]
    public async Task SaveBo_ExcludeFromAssignments_OmitsKeyButKeepsStartKey()
    {
        Setup();
        await _client.SaveBoAsync<FakeSalDocItem>()
            .CreateOrUpdate()
            .Set(i => i.SalDocInternalNo, 10)
            .Set(i => i.InternalNo, 5)
            .Set(i => i.SRebatePerc, "3")
            .ExcludeFromAssignments(i => i.SalDocInternalNo)
            .ExecuteAsync();

        _captured!.StartKeys.Should().Be("10,5");
        _captured.Assignments.Should().NotContain(a => a.StartsWith("SalDocInternalNo="));
        _captured.Assignments.Should().BeEquivalentTo("InternalNo=@5", "SRebatePerc=@3");
    }

    [Fact]
    public async Task SaveBo_Update_MissingKey_ButWhereSupplied_DoesNotThrow()
    {
        Setup();
        await _client.SaveBoAsync<FakeSalDocItem>()
            .Update()
            .Where(i => i.SRebatePerc == "0")
            .Set(i => i.GrossSP, "1")
            .ExecuteAsync();

        _captured!.StartKeys.Should().Be("");
        _captured.Filter.Should().NotBeNullOrEmpty();
    }

    // ================================================================
    // Block Set — object initializer (multiple fields in one call)
    // ================================================================

    [Fact]
    public async Task SaveBo_BlockSet_Update_DerivesStartKeyAndAssigns()
    {
        Setup();
        await _client.SaveBoAsync<FakeSalDocItem>()
            .Update()
            .Set(i => new FakeSalDocItem { SalDocInternalNo = 10, InternalNo = 5, SRebatePerc = "3" })
            .ExecuteAsync();

        _captured!.StartKeys.Should().Be("10,5");
        _captured.FixedSegsOfBoIndex.Should().Be(2);
        _captured.Operation.Should().Be(SaveBoOperation.Update);
        _captured.Assignments.Should().ContainSingle().Which.Should().Be("SRebatePerc=@3");
    }

    [Fact]
    public async Task SaveBo_BlockSet_CreateOrUpdate_WithStart()
    {
        Setup();
        await _client.SaveBoAsync<FakeSalDocItem>()
            .CreateOrUpdate()
            .Start(10, 5)
            .Set(i => new FakeSalDocItem { SRebatePerc = "3" })
            .ExecuteAsync();

        _captured!.StartKeys.Should().Be("10,5");
        _captured.Assignments.Should().ContainSingle().Which.Should().Be("SRebatePerc=@3");
    }

    [Fact]
    public async Task SaveBo_BlockSet_MixedWithSingleSet()
    {
        Setup();
        await _client.SaveBoAsync<FakeSalDocItem>()
            .Update()
            .Set(i => i.SalDocInternalNo, 10)
            .Set(i => new FakeSalDocItem { InternalNo = 5, SRebatePerc = "3" })
            .ExecuteAsync();

        _captured!.StartKeys.Should().Be("10,5");
        _captured.Assignments.Should().ContainSingle().Which.Should().Be("SRebatePerc=@3");
    }

    [Fact]
    public async Task SaveBo_BlockSet_EvaluatesValueExpressions()
    {
        Setup();
        var city = "Bern";
        await _client.SaveBoAsync<FakeAddr>()
            .Update()
            .Set(a => new FakeAddr { Number = 1001, City = city.ToUpper(), IsPassive = true })
            .ExecuteAsync();

        _captured!.StartKeys.Should().Be("1001");
        _captured.Assignments.Should().Contain("Addr.City=@BERN");
        _captured.Assignments.Should().Contain("Addr.IsPassive=@1");
        _captured.Assignments.Should().NotContain(a => a.StartsWith("Addr.Number="));
    }

    [Fact]
    public void SaveBo_BlockSet_NonInitializerBody_Throws()
    {
        Setup();
        Action act = () => _client.SaveBoAsync<FakeSalDocItem>().Update().Set(i => i);
        act.Should().Throw<ArgumentException>();
    }

    // ================================================================
    // Runtime guard
    // ================================================================

    [Fact]
    public async Task SaveBo_Update_WithoutAnyKey_Throws()
    {
        Setup();
        Func<Task> act = () =>
        {
            var builder = _client.SaveBoAsync<FakeSalDocItem>()
                .Update()
                .Set(i => i.SRebatePerc, "3");
            return builder.ExecuteAsync();
        };
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveBo_Update_NonLeadingKeyOnly_Throws()
    {
        Setup();
        Func<Task> act = () =>
        {
            // Only the second segment set → no contiguous leading prefix → empty start key on Update.
            var builder = _client.SaveBoAsync<FakeSalDocItem>()
                .Update()
                .Set(i => i.InternalNo, 5)
                .Set(i => i.SRebatePerc, "3");
            return builder.ExecuteAsync();
        };
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveBo_CreateOrUpdate_WithoutStartKey_Throws()
    {
        Setup();
        Func<Task> act = () =>
        {
            // No key segment, no .Start, no .Where/.Filter → cannot locate the record.
            var builder = _client.SaveBoAsync<FakeSalDocItem>()
                .CreateOrUpdate()
                .Set(i => i.SRebatePerc, "3");
            return builder.ExecuteAsync();
        };
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveBo_Create_WithExplicitStart_Throws()
    {
        Setup();
        Func<Task> act = () =>
        {
            var builder = _client.SaveBoAsync<FakeSalDocItem>()
                .Create()
                .Start("10,5")
                .Set(i => i.SRebatePerc, "3");
            return builder.ExecuteAsync();
        };
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ================================================================
    // Response mapping — SaveBoRecord
    // ================================================================

    [Fact]
    public async Task SaveBo_Response_ParsesSavedRecords()
    {
        Setup(FlatResponseBuilder.FromColumns(
            ("BoId",          ["101", "102"]),
            ("BoNumber",      ["1001", "1002"]),
            ("BoName",        ["Müller AG", "Schmidt GmbH"]),
            ("SaveBoStateCd", ["0", "0"]),
            ("SaveBoInfo",    ["", ""])));

        var result = await _client.SaveBoAsync<FakeAddr>().Start(1001).ExecuteAsync();

        result.Records.Should().HaveCount(2);
        result.SavedCount.Should().Be(2);
        result.HasErrors.Should().BeFalse();

        result.Records[0].BoId.Should().Be("101");
        result.Records[0].BoNumber.Should().Be("1001");
        result.Records[0].IsOk.Should().BeTrue();
    }

    [Fact]
    public async Task SaveBo_Response_ParsesErrorRecord()
    {
        Setup(FlatResponseBuilder.FromColumns(
            ("BoId",          ["201"]),
            ("BoNumber",      ["2001"]),
            ("BoName",        ["Locked Corp"]),
            ("SaveBoStateCd", ["1"]),
            ("SaveBoInfo",    ["Cannot assign number manually"])));

        var result = await _client.SaveBoAsync<FakeAddr>().Start(1001).ExecuteAsync();

        result.SavedCount.Should().Be(0);
        result.HasErrors.Should().BeTrue();
        result.Records[0].HasError.Should().BeTrue();
        result.Records[0].SaveBoInfo.Should().Be("Cannot assign number manually");
    }

    [Fact]
    public async Task SaveBo_Response_ParsesCreatedOnlyRecord()
    {
        Setup(FlatResponseBuilder.FromColumns(
            ("BoId",          ["301"]),
            ("BoNumber",      ["3001"]),
            ("BoName",        ["Partial Corp"]),
            ("SaveBoStateCd", ["2"]),
            ("SaveBoInfo",    ["Created but mutation failed"])));

        var result = await _client.SaveBoAsync<FakeAddr>().Start(1001).ExecuteAsync();

        result.Records[0].SaveBoStateCd.Should().Be(2);
        result.Records[0].IsCreatedOnly.Should().BeTrue();
        result.Records[0].IsOk.Should().BeFalse();
        result.Records[0].HasError.Should().BeFalse();
    }

    [Fact]
    public async Task SaveBo_WithReport_False_ReturnsEmptyRecordsList()
    {
        Setup(FlatResponseBuilder.Empty());
        var result = await _client.SaveBoAsync<FakeAddr>().Start(1001).WithReport(false).ExecuteAsync();
        result.Records.Should().BeEmpty();
        result.SavedCount.Should().Be(0);
    }

    [Fact]
    public async Task SaveBo_NullResponse_ReturnsEmptyRecordsList()
    {
        Setup(null);
        var result = await _client.SaveBoAsync<FakeAddr>().Start(1001).ExecuteAsync();
        result.Records.Should().BeEmpty();
    }
}
