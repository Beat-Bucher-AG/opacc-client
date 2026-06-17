using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Opacc.Client.Enums;
using Opacc.Client.Extensions;

namespace Opacc.Client.Operations.GetBo;

public class OpaccGetBoRequest
{
    public required string BoEntity { get; init; }
    public required string Start { get; init; }
    public SearchOperator SearchOperator { get; init; } = SearchOperator.Equal;
    public int IndexNo { get; init; } = 1;
    public int Segment { get; init; } = 1;
    public int Count { get; init; } = 1;
    public string? SelectAttributes { get; init; }
    public List<string>? VirtualAttributes { get; init; }
    public string? Filter { get; init; }
    public bool Cache { get; init; }
    public bool UseBofScript { get; init; }

    public string[] BuildParameters()
    {
        var args = new List<string>
        {
            BoEntity,
            Start,
            SearchOperator.ToOpaccCode(),
            IndexNo.ToString(),
            Count.ToString(),
            Segment.ToString(),
            Filter ?? "",
            SelectAttributes ?? "",
        };

        // Virtual Attributes als einzelne Parameter anfügen
        if (VirtualAttributes != null)
        {
            foreach (var va in VirtualAttributes)
            {
                if (!string.IsNullOrWhiteSpace(va))
                    args.Add(va);
            }
        }

        return args.ToArray();
    }
}
