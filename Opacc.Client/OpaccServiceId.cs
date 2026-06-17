namespace Opacc.Client;

/// <summary>
/// Identifies an Opacc service by port and operation ID.
/// Use the scaffolded <c>OpaccService</c> class for pre-defined constants,
/// or construct directly for custom/unlisted services.
///
/// Example:
///   await _opaccClient.SendRawAsync(OpaccService.ArtSal_GetPrice, new { ArtNo = "10100", CustNo = 1001 });
///   await _opaccClient.SendRawAsync(new OpaccServiceId("Biz", "MyCustomService"), new { Param1 = "X" });
/// </summary>
public readonly record struct OpaccServiceId(string PortId, string OperationId)
{
    public override string ToString() => $"{PortId}.{OperationId}";
}
