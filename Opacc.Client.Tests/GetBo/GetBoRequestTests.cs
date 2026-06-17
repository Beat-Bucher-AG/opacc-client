using FluentAssertions;
using Opacc.Client.Enums;
using Opacc.Client.Operations.GetBo;

namespace Opacc.Client.Tests.GetBo;

/// <summary>
/// Tests for <see cref="OpaccGetBoRequest.BuildParameters"/>.
/// Verifies the fixed positional layout expected by the Opacc WCF service.
/// </summary>
public class GetBoRequestTests
{
    [Fact]
    public void BuildParameters_DefaultRequest_HasCorrectLayout()
    {
        var request = new OpaccGetBoRequest
        {
            BoEntity = "Addr",
            Start = "1001",
        };

        var parameters = request.BuildParameters();

        parameters[0].Should().Be("Addr");      // BoEntity
        parameters[1].Should().Be("1001");      // Start
        parameters[2].Should().Be("e");         // SearchOperator Equal
        parameters[3].Should().Be("1");         // IndexNo
        parameters[4].Should().Be("1");         // Count
        parameters[5].Should().Be("1");         // Segment
        parameters[6].Should().Be("");          // Filter (empty)
        parameters[7].Should().Be("");          // SelectAttributes (empty)
    }

    [Theory]
    [InlineData(SearchOperator.Equal, "e")]
    [InlineData(SearchOperator.Next, "n")]
    [InlineData(SearchOperator.Previous, "p")]
    [InlineData(SearchOperator.First, "f")]
    [InlineData(SearchOperator.Last, "l")]
    public void BuildParameters_SearchOperator_IsTranslatedToOpaccCode(SearchOperator op, string expectedCode)
    {
        var request = new OpaccGetBoRequest { BoEntity = "Addr", Start = "", SearchOperator = op };

        request.BuildParameters()[2].Should().Be(expectedCode);
    }

    [Fact]
    public void BuildParameters_WithFilter_PlacedAtIndex6()
    {
        var request = new OpaccGetBoRequest
        {
            BoEntity = "Addr",
            Start = "",
            Filter = "Addr.City = 'Bern'",
        };

        request.BuildParameters()[6].Should().Be("Addr.City = 'Bern'");
    }

    [Fact]
    public void BuildParameters_WithSelectAttributes_PlacedAtIndex7()
    {
        var request = new OpaccGetBoRequest
        {
            BoEntity = "Addr",
            Start = "",
            SelectAttributes = "Addr.Number, Addr.FullName",
        };

        request.BuildParameters()[7].Should().Be("Addr.Number, Addr.FullName");
    }

    [Fact]
    public void BuildParameters_WithVirtualAttributes_AppendedAfterIndex7()
    {
        var request = new OpaccGetBoRequest
        {
            BoEntity = "Addr",
            Start = "",
            VirtualAttributes = ["VAttr1=expr1", "VAttr2=expr2"],
        };

        var parameters = request.BuildParameters();

        parameters[8].Should().Be("VAttr1=expr1");
        parameters[9].Should().Be("VAttr2=expr2");
    }

    [Fact]
    public void BuildParameters_EmptyVirtualAttributes_NotAppended()
    {
        var request = new OpaccGetBoRequest
        {
            BoEntity = "Addr",
            Start = "",
            VirtualAttributes = ["", "   "],
        };

        // Only fixed 8 params — blank virtual attributes are skipped
        request.BuildParameters().Should().HaveCount(8);
    }

    [Fact]
    public void BuildParameters_CustomIndexAndSegment_ReflectedInOutput()
    {
        var request = new OpaccGetBoRequest
        {
            BoEntity = "Addr",
            Start = "",
            IndexNo = 3,
            Segment = 2,
            Count = 50,
        };

        var parameters = request.BuildParameters();

        parameters[3].Should().Be("3"); // IndexNo
        parameters[4].Should().Be("50"); // Count
        parameters[5].Should().Be("2"); // Segment
    }
}
