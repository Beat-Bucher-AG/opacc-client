using FluentAssertions;
using Opacc.Client.Enums;
using Opacc.Client.Operations.SaveBo;

namespace Opacc.Client.Tests.SaveBo;

/// <summary>
/// Tests for <see cref="OpaccSaveBoRequest.BuildParameters"/>.
/// Verifies the fixed positional layout expected by the Opacc WCF service.
/// </summary>
public class SaveBoRequestTests
{
    [Fact]
    public void BuildParameters_DefaultRequest_HasCorrectLayout()
    {
        var request = new OpaccSaveBoRequest
        {
            BoEntity  = "Addr",
            Operation = SaveBoOperation.CreateOrUpdate,
        };

        var parameters = request.BuildParameters();

        parameters.Should().HaveCount(10); // 10 fixed, no assignments
        parameters[0].Should().Be("Addr");  // Bo
        parameters[1].Should().Be("");      // StartKeys (empty)
        parameters[2].Should().Be("e");     // SearchOperationCd = Equal
        parameters[3].Should().Be("1");     // BoIndex
        parameters[4].Should().Be("3");     // SaveBoProcessingCd = CreateOrUpdate
        parameters[5].Should().Be("0");     // FixedSegsOfBoIndex
        parameters[6].Should().Be("0");     // SaveBoModeCd = normal
        parameters[7].Should().Be("1");     // WithReport = true
        parameters[8].Should().Be("");      // Filter (empty)
        parameters[9].Should().Be("");      // ResultObject (empty)
    }

    [Theory]
    [InlineData(SaveBoOperation.Update,        "1")]
    [InlineData(SaveBoOperation.Create,        "2")]
    [InlineData(SaveBoOperation.CreateOrUpdate,"3")]
    public void BuildParameters_Operation_PlacedAtIndex4(SaveBoOperation op, string expected)
    {
        var request = new OpaccSaveBoRequest { BoEntity = "Addr", Operation = op };
        request.BuildParameters()[4].Should().Be(expected);
    }

    [Fact]
    public void BuildParameters_SaveBoModeCd_AlwaysNormal()
    {
        // SaveBo has no dry-run; arg 6 (SaveBoModeCd / "Ausführungsart") is always 0 (normal).
        var request = new OpaccSaveBoRequest { BoEntity = "Addr", Operation = SaveBoOperation.Create };
        request.BuildParameters()[6].Should().Be("0");
    }

    [Fact]
    public void BuildParameters_WithReport_False_PlacesZeroAtIndex7()
    {
        var request = new OpaccSaveBoRequest { BoEntity = "Addr", Operation = SaveBoOperation.Create, WithReport = false };
        request.BuildParameters()[7].Should().Be("0");
    }

    [Fact]
    public void BuildParameters_WithFilter_PlacedAtIndex8()
    {
        var request = new OpaccSaveBoRequest
        {
            BoEntity  = "Addr",
            Operation = SaveBoOperation.Update,
            Filter    = "Addr.CountrySc = 'CH'",
        };
        request.BuildParameters()[8].Should().Be("Addr.CountrySc = 'CH'");
    }

    [Fact]
    public void BuildParameters_WithResultObject_PlacedAtIndex9()
    {
        var request = new OpaccSaveBoRequest
        {
            BoEntity     = "Addr",
            Operation    = SaveBoOperation.Create,
            ResultObject = "Addr.Number,Addr.FullName",
        };
        request.BuildParameters()[9].Should().Be("Addr.Number,Addr.FullName");
    }

    [Fact]
    public void BuildParameters_Assignments_AppendedAfterIndex9()
    {
        var request = new OpaccSaveBoRequest
        {
            BoEntity    = "Addr",
            Operation   = SaveBoOperation.Create,
            Assignments = ["Addr.LastName=@Smith", "Addr.City=@Bern"],
        };

        var parameters = request.BuildParameters();

        parameters.Should().HaveCount(12);
        parameters[10].Should().Be("Addr.LastName=@Smith");
        parameters[11].Should().Be("Addr.City=@Bern");
    }

    [Fact]
    public void BuildParameters_NoScript_AppendsLiteralString()
    {
        var request = new OpaccSaveBoRequest
        {
            BoEntity    = "Addr",
            Operation   = SaveBoOperation.Create,
            Assignments = ["Addr.City=@Bern"],
            NoScript    = true,
        };

        var parameters = request.BuildParameters();

        parameters.Last().Should().Be("#NoScript");
    }

    [Fact]
    public void BuildParameters_NoScript_False_DoesNotAppend()
    {
        var request = new OpaccSaveBoRequest
        {
            BoEntity    = "Addr",
            Operation   = SaveBoOperation.Create,
            Assignments = ["Addr.City=@Bern"],
            NoScript    = false,
        };

        request.BuildParameters().Should().NotContain("#NoScript");
    }

    [Fact]
    public void BuildParameters_StartKeys_PlacedAtIndex1()
    {
        var request = new OpaccSaveBoRequest
        {
            BoEntity  = "Addr",
            Operation = SaveBoOperation.Update,
            StartKeys = "1001",
        };
        request.BuildParameters()[1].Should().Be("1001");
    }

    [Fact]
    public void BuildParameters_CustomIndex_PlacedAtIndex3()
    {
        var request = new OpaccSaveBoRequest
        {
            BoEntity           = "Addr",
            Operation          = SaveBoOperation.Update,
            IndexNo            = 5,
            FixedSegsOfBoIndex = 2,
        };
        var parameters = request.BuildParameters();
        parameters[3].Should().Be("5");
        parameters[5].Should().Be("2");
    }
}
