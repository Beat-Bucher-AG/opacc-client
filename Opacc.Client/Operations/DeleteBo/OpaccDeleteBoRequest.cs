using Opacc.Client.Enums;
using Opacc.Client.Extensions;

namespace Opacc.Client.Operations.DeleteBo;

public class OpaccDeleteBoRequest
{
    public required string BoEntity { get; init; }

    /// <summary>Primary key value(s). Composite keys are comma-separated.</summary>
    public required string StartKeys { get; init; }

    /// <summary>Default: Equal ("e").</summary>
    public SearchOperator SearchOperator { get; init; } = SearchOperator.Equal;

    public int IndexNo { get; init; } = 1;

    /// <summary>Number of fixed segments in the index (FixedSegsOfBoIndex). Default: 0.</summary>
    public int FixedSegsOfBoIndex { get; init; } = 0;

    /// <summary>Dry-run mode — validates without deleting. Default: false.</summary>
    public bool IsTest { get; init; } = false;

    /// <summary>Return a report of deleted records. Default: true.</summary>
    public bool WithReport { get; init; } = true;

    public string? Filter { get; init; }

    /// <summary>Name of a result BO to populate after deletion.</summary>
    public string? ResultObject { get; init; }

    /// <summary>Skip running BO scripts on deletion. Default: false.</summary>
    public bool NoScript { get; init; } = false;

    public bool UseBofScript { get; init; }

    public string[] BuildParameters() =>
    [
        BoEntity,
        StartKeys,
        SearchOperator.ToOpaccCode(),
        IndexNo.ToString(),
        FixedSegsOfBoIndex.ToString(),
        IsTest ? "1" : "0",
        WithReport ? "1" : "0",
        Filter ?? "",
        ResultObject ?? "",
        NoScript ? "1" : "0",
    ];
}
