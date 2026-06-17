using Opacc.Client.Operations.DeleteBo;
using Opacc.Client.Operations.GetBo;
using Opacc.Client.Operations.Query;
using Opacc.Client.Operations.SaveBo;
using Opacc.Client.Session;
using OpaccWebservice;

namespace Opacc.Client.Transport;

public interface IOpaccTransport
{
    Task<FlatResponseData?> SendGetBoAsync(OpaccGetBoRequest request, SessionCredentials? credentials = null, CancellationToken ct = default);
    Task<FlatResponseData?> SendDeleteBoAsync(OpaccDeleteBoRequest request, SessionCredentials? credentials = null, CancellationToken ct = default);
    Task<FlatResponseData?> SendSaveBoAsync(OpaccSaveBoRequest request, SessionCredentials? credentials = null, CancellationToken ct = default);
    Task<FlatResponseData?> SendQueryAsync(OpaccQueryRequest request, SessionCredentials? credentials = null, CancellationToken ct = default);

    /// <summary>
    /// Sendet einen generischen FlatRequest an einen beliebigen Port/Operation.
    /// Wird für Info-Operationen wie GetInfoBo, GetInfoBoAttr verwendet.
    /// </summary>
    Task<FlatResponseData?> SendRawAsync(string portId, string operationId, string[] parameters, CancellationToken ct = default);
}
