using Opacc.Client;
using Opacc.Client.Attributes;

namespace Opacc.Client.Tests.TestModels;

/// <summary>
/// Minimal test model for the "SalDocItem" BO with a 2-segment default index (4):
/// SalDocInternalNo (segment 1) + InternalNo (segment 2). Used to exercise multi-segment
/// start-key derivation. No [BoProperty] is declared, so OoExpression == property name —
/// matching the real generated model (assignments read e.g. "SRebatePerc=@3").
/// </summary>
[Bo("SalDocItem")]
[BoDefaultIndex(4)]
public class FakeSalDocItem : IOpaccModel
{
    [BoId(4, 1)]
    public int SalDocInternalNo { get; set; }

    [BoId(4, 2)]
    public int InternalNo { get; set; }

    public string SRebatePerc { get; set; } = "";

    public string GrossSP { get; set; } = "";
}
