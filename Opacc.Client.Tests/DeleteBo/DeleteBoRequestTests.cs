using FluentAssertions;
using Opacc.Client.Enums;
using Opacc.Client.Operations.DeleteBo;

namespace Opacc.Client.Tests.DeleteBo;

/// <summary>
/// Tests for <see cref="OpaccDeleteBoRequest.BuildParameters"/>.
/// Verifies the fixed positional layout expected by the Opacc WCF service.
/// </summary>
public class DeleteBoRequestTests
{
    [Fact]
    public void BuildParameters_DefaultRequest_HasCorrectLayout()
    {
        var request = new OpaccDeleteBoRequest
        {
            BoEntity  = "Addr",
            StartKeys = "1001",
        };

        var parameters = request.BuildParameters();

        parameters.Should().HaveCount(10);
        parameters[0].Should().Be("Addr");  // Bo
        parameters[1].Should().Be("1001");  // StartKeys
        parameters[2].Should().Be("e");     // SearchOperationCd = Equal
        parameters[3].Should().Be("1");     // BoIndex
        parameters[4].Should().Be("0");     // FixedSegsOfBoIndex
        parameters[5].Should().Be("0");     // IsTest = false
        parameters[6].Should().Be("1");     // WithReport = true
        parameters[7].Should().Be("");      // Filter (empty)
        parameters[8].Should().Be("");      // ResultObject (empty)
        parameters[9].Should().Be("0");     // NoScript = false
    }

    [Theory]
    [InlineData(SearchOperator.Equal,    "e")]
    [InlineData(SearchOperator.Next,     "n")]
    [InlineData(SearchOperator.Previous, "p")]
    [InlineData(SearchOperator.First,    "f")]
    [InlineData(SearchOperator.Last,     "l")]
    public void BuildParameters_SearchOperator_IsTranslatedToOpaccCode(SearchOperator op, string expectedCode)
    {
        var request = new OpaccDeleteBoRequest { BoEntity = "Addr", StartKeys = "", SearchOperator = op };
        request.BuildParameters()[2].Should().Be(expectedCode);
    }

    [Fact]
    public void BuildParameters_IsTest_True_PlacesOneAtIndex5()
    {
        var request = new OpaccDeleteBoRequest { BoEntity = "Addr", StartKeys = "", IsTest = true };
        request.BuildParameters()[5].Should().Be("1");
    }

    [Fact]
    public void BuildParameters_WithReport_False_PlacesZeroAtIndex6()
    {
        var request = new OpaccDeleteBoRequest { BoEntity = "Addr", StartKeys = "", WithReport = false };
        request.BuildParameters()[6].Should().Be("0");
    }

    [Fact]
    public void BuildParameters_WithFilter_PlacedAtIndex7()
    {
        var request = new OpaccDeleteBoRequest
        {
            BoEntity  = "Addr",
            StartKeys = "",
            Filter    = "Addr.City = 'CH'",
        };
        request.BuildParameters()[7].Should().Be("Addr.City = 'CH'");
    }

    [Fact]
    public void BuildParameters_WithResultObject_PlacedAtIndex8()
    {
        var request = new OpaccDeleteBoRequest
        {
            BoEntity     = "Addr",
            StartKeys    = "",
            ResultObject = "Addr.Number,Addr.FullName",
        };
        request.BuildParameters()[8].Should().Be("Addr.Number,Addr.FullName");
    }

    [Fact]
    public void BuildParameters_NoScript_True_PlacesOneAtIndex9()
    {
        var request = new OpaccDeleteBoRequest { BoEntity = "Addr", StartKeys = "", NoScript = true };
        request.BuildParameters()[9].Should().Be("1");
    }

    [Fact]
    public void BuildParameters_CustomIndexAndFixedSegments_ReflectedInOutput()
    {
        var request = new OpaccDeleteBoRequest
        {
            BoEntity           = "Addr",
            StartKeys          = "",
            IndexNo            = 3,
            FixedSegsOfBoIndex = 2,
        };

        var parameters = request.BuildParameters();
        parameters[3].Should().Be("3"); // BoIndex
        parameters[4].Should().Be("2"); // FixedSegsOfBoIndex
    }
}
