using Opacc.Client.Attributes;
using Opacc.Client.Enums;

namespace Opacc.Client.Tests.TestModels;

/// <summary>
/// Minimal test model for the "Addr" BO. Keeps property count small so
/// expected select strings are easy to reason about in assertions.
/// </summary>
[Bo("Addr")]
[BoDefaultIndex(1)]
public class FakeAddr : IOpaccModel
{
    [BoId(1, 1)]
    [BoProperty("Addr.Number")]
    public int Number { get; set; }

    [BoProperty("Addr.FullName")]
    public string FullName { get; set; } = "";

    [BoProperty("Addr.City")]
    public string City { get; set; } = "";

    [BoProperty("Addr.IsPassive")]
    public bool IsPassive { get; set; }

    [BoProperty("Addr.DateOfEntry", OpaccDataType.Date)]
    public DateTime? DateOfEntry { get; set; }
}
