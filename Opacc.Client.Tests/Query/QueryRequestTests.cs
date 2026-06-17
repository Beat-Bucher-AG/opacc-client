using FluentAssertions;
using Opacc.Client.Operations.Query;

namespace Opacc.Client.Tests.Query;

/// <summary>
/// Tests for <see cref="OpaccQueryRequest.BuildParameters"/>.
/// Verifies the Opacc Query parameter format ("Key=Value" strings).
/// </summary>
public class QueryRequestTests
{
    private static OpaccQueryRequest Minimal(string boEntity = "Addr") => new()
    {
        BoEntity = boEntity,
        Columns = [new QueryColumn { Expression = "Addr.Number", ClrPropertyName = "Number" }],
    };

    [Fact]
    public void BuildParameters_AlwaysStartsWithMain()
    {
        var parameters = Minimal().BuildParameters();
        parameters[0].Should().Be("Main=Addr");
    }

    [Fact]
    public void BuildParameters_SimpleColumns_CombinedIntoOneParameter()
    {
        var request = new OpaccQueryRequest
        {
            BoEntity = "Addr",
            Columns =
            [
                new QueryColumn { Expression = "Addr.Number", ClrPropertyName = "Number" },
                new QueryColumn { Expression = "Addr.FullName", ClrPropertyName = "FullName" },
            ],
        };

        var parameters = request.BuildParameters();

        parameters.Should().Contain("Columns=Addr.Number,Addr.FullName");
        parameters.Should().NotContain(p => p.StartsWith("Column="));
    }

    [Fact]
    public void BuildParameters_AliasedColumn_UsesIndividualColumnParameter()
    {
        var request = new OpaccQueryRequest
        {
            BoEntity = "Addr",
            Columns =
            [
                new QueryColumn { Expression = "Addr.Number, Nummer", HasAlias = true, ClrPropertyName = "Number" },
            ],
        };

        var parameters = request.BuildParameters();

        parameters.Should().Contain("Column=Addr.Number, Nummer");
        parameters.Should().NotContain(p => p.StartsWith("Columns="));
    }

    [Fact]
    public void BuildParameters_MaxRowsNull_UsesAll()
    {
        var parameters = Minimal().BuildParameters();
        parameters.Should().Contain("MaxRows=All");
    }

    [Fact]
    public void BuildParameters_MaxRowsSet_UsesValue()
    {
        var request = Minimal();
        var withMax = new OpaccQueryRequest
        {
            BoEntity = request.BoEntity,
            Columns = request.Columns,
            MaxRows = 50,
        };

        withMax.BuildParameters().Should().Contain("MaxRows=50");
    }

    [Fact]
    public void BuildParameters_MaxRowsZero_UsesZero()
    {
        var request = new OpaccQueryRequest
        {
            BoEntity = "Addr",
            Columns = Minimal().Columns,
            MaxRows = 0,
        };

        request.BuildParameters().Should().Contain("MaxRows=0");
    }

    [Fact]
    public void BuildParameters_Filter_AddedAsParameter()
    {
        var request = new OpaccQueryRequest
        {
            BoEntity = "Addr",
            Columns = Minimal().Columns,
            Filter = "Addr.City = 'Bern'",
        };

        request.BuildParameters().Should().Contain("Filter=Addr.City = 'Bern'");
    }

    [Fact]
    public void BuildParameters_NoFilter_NotIncluded()
    {
        Minimal().BuildParameters().Should().NotContain(p => p.StartsWith("Filter="));
    }

    [Fact]
    public void BuildParameters_Relations_AddedAsRelatedParameters()
    {
        var request = new OpaccQueryRequest
        {
            BoEntity = "Addr",
            Columns = Minimal().Columns,
            Relations = ["Country,Country,,,Addr.CountrySc = Country.ShortCut"],
        };

        request.BuildParameters().Should().Contain("Related=Country,Country,,,Addr.CountrySc = Country.ShortCut");
    }

    [Fact]
    public void BuildParameters_OrderBy_AddedAsOrderByParameters()
    {
        var request = new OpaccQueryRequest
        {
            BoEntity = "Addr",
            Columns = Minimal().Columns,
            OrderBy = ["Addr.FullName", "-Addr.City"],
        };

        var parameters = request.BuildParameters();
        parameters.Should().Contain("OrderBy=Addr.FullName");
        parameters.Should().Contain("OrderBy=-Addr.City");
    }

    [Fact]
    public void BuildParameters_OrderByAsDate_AddedAsOrderByAsDateParameters()
    {
        var request = new OpaccQueryRequest
        {
            BoEntity = "Addr",
            Columns = Minimal().Columns,
            OrderByAsDate = ["Addr.DateOfEntry"],
        };

        request.BuildParameters().Should().Contain("OrderByAsDate=Addr.DateOfEntry");
    }

    [Fact]
    public void BuildParameters_OrderByAsNmb_AddedAsOrderByAsNmbParameters()
    {
        var request = new OpaccQueryRequest
        {
            BoEntity = "Addr",
            Columns = Minimal().Columns,
            OrderByAsNmb = ["Addr.Number"],
        };

        request.BuildParameters().Should().Contain("OrderByAsNmb=Addr.Number");
    }

    [Fact]
    public void BuildParameters_Distinct_AddedAsParameter()
    {
        var request = new OpaccQueryRequest
        {
            BoEntity = "Addr",
            Columns = Minimal().Columns,
            Distinct = true,
        };

        request.BuildParameters().Should().Contain("Distinct=1");
    }

    [Fact]
    public void BuildParameters_DistinctFalse_IsZero()
    {
        var request = new OpaccQueryRequest
        {
            BoEntity = "Addr",
            Columns = Minimal().Columns,
            Distinct = false,
        };

        request.BuildParameters().Should().Contain("Distinct=0");
    }

    [Fact]
    public void BuildParameters_Defines_AddedAsDefineParameters()
    {
        var request = new OpaccQueryRequest
        {
            BoEntity = "Addr",
            Columns = Minimal().Columns,
            Defines = ["@MyVar,Addr.Number * 2"],
        };

        request.BuildParameters().Should().Contain("Define=@MyVar,Addr.Number * 2");
    }

    [Fact]
    public void BuildParameters_ScrollingWithoutOrderBy_NotIncluded()
    {
        var request = new OpaccQueryRequest
        {
            BoEntity = "Addr",
            Columns = Minimal().Columns,
            Scrolling = "ne",
            // No OrderBy — Scrolling should be suppressed
        };

        request.BuildParameters().Should().NotContain(p => p.StartsWith("Scrolling="));
    }

    [Fact]
    public void BuildParameters_ScrollingWithOrderBy_Included()
    {
        var request = new OpaccQueryRequest
        {
            BoEntity = "Addr",
            Columns = Minimal().Columns,
            OrderBy = ["Addr.FullName"],
            Scrolling = "ne",
        };

        request.BuildParameters().Should().Contain("Scrolling=ne");
    }

    [Fact]
    public void BuildParameters_RedoData_AddedWithHash()
    {
        var request = new OpaccQueryRequest
        {
            BoEntity = "Addr",
            Columns = Minimal().Columns,
            RedoData = ["sometoken"],
        };

        request.BuildParameters().Should().Contain("#RedoData=sometoken");
    }

    [Fact]
    public void BuildParameters_RedoArgs_AddedWithHash()
    {
        var request = new OpaccQueryRequest
        {
            BoEntity = "Addr",
            Columns = Minimal().Columns,
            RedoArgs = "ne,,25",
        };

        request.BuildParameters().Should().Contain("#RedoArgs=ne,,25");
    }
}
