using Opacc.Client.Enums;
using Opacc.Client.Extensions;

namespace Opacc.Client.Operations.SaveBo;

public class OpaccSaveBoRequest
{
    public required string BoEntity { get; init; }

    /// <summary>Key to locate the record (for Update / CreateOrUpdate).</summary>
    public string StartKeys { get; init; } = "";

    public SearchOperator SearchOperator { get; init; } = SearchOperator.Equal;

    public int IndexNo { get; init; } = 1;

    /// <summary>1=Update, 2=Create, 3=CreateOrUpdate.</summary>
    public required SaveBoOperation Operation { get; init; }

    public int FixedSegsOfBoIndex { get; init; } = 0;

    /// <summary>Return per-record SaveBoStateCd and SaveBoInfo. Default: true.</summary>
    public bool WithReport { get; init; } = true;

    public string? Filter { get; init; }

    public string? ResultObject { get; init; }

    /// <summary>Field assignments in the format "OoExpression=@Value".</summary>
    public IReadOnlyList<string> Assignments { get; init; } = [];

    /// <summary>Append #NoScript as the last parameter to suppress F-Script execution.</summary>
    public bool NoScript { get; init; } = false;

    public bool UseBofScript { get; init; }

    public string[] BuildParameters()
    {
        var args = new List<string>
        {
            BoEntity,
            StartKeys,
            SearchOperator.ToOpaccCode(),
            IndexNo.ToString(),
            ((int)Operation).ToString(),
            FixedSegsOfBoIndex.ToString(),
            // Arg 7 (SaveBoModeCd / "Ausführungsart") — always 0 (normal). The Opacc docs label this
            // a test flag, but that text is a copy-paste artifact from DeleteBo; SaveBo has no dry-run.
            "0",
            WithReport ? "1" : "0",
            Filter ?? "",
            ResultObject ?? "",
        };

        args.AddRange(Assignments);

        if (NoScript)
            args.Add("#NoScript");

        return args.ToArray();
    }
}
