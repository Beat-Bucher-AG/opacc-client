using System.Reflection;
using Opacc.Client.Enums;
using Opacc.Client.Helper;
using Opacc.Client.Operations.DeleteBo;
using Opacc.Client.Operations.DeleteBo.Internal;
using Opacc.Client.Operations.GetBo;
using Opacc.Client.Operations.GetBo.Internal;
using Opacc.Client.Operations.Query;
using Opacc.Client.Operations.Query.Internal;
using Opacc.Client.Operations.SaveBo;
using Opacc.Client.Operations.SaveBo.Internal;
using Opacc.Client.Transport;

namespace Opacc.Client;

public partial class OpaccClient(IOpaccTransport transport) : IOpaccClient
{


    public IOpaccGetBo<T> GetBoAsync<T>()
        where T : class, IOpaccModel, new() => new OpaccGetBoBuilder<T>(transport, this);

    public IOpaccQuery<T> QueryAsync<T>()
        where T : class, IOpaccModel, new() => new OpaccQueryBuilder<T>(transport, this);

    public IOpaccDeleteBo<T> DeleteBoAsync<T>()
        where T : class, IOpaccModel, new() => new OpaccDeleteBoBuilder<T>(transport);

    public IOpaccSaveBo<T> SaveBoAsync<T>()
        where T : class, IOpaccModel, new() => new OpaccSaveBoBuilder<T>(transport);

    public async Task<DeleteBoResult> DeleteBoRawAsync(string boEntity, string startKeys, int indexNo = 1, int fixedSegsOfBoIndex = 0, CancellationToken ct = default)
    {
        var request = new OpaccDeleteBoRequest
        {
            BoEntity           = boEntity,
            StartKeys          = startKeys,
            IndexNo            = indexNo,
            FixedSegsOfBoIndex = fixedSegsOfBoIndex,
            WithReport         = true,
        };
        var response = await transport.SendDeleteBoAsync(request, null, ct);
        var records = OpaccResponseParser.ParseRecords(response, boEntity)
            .Select(r => new DeleteBoRecord(r))
            .ToList();
        return new DeleteBoResult(records, false);
    }

    public async Task<RawServiceResponse> SendRawAsync(
        OpaccServiceId service,
        object? parameters = null,
        CancellationToken ct = default)
    {
        var paramArray = ObjectToParameters(parameters);
        return await SendCoreAsync(service, paramArray, ct);
    }

    public async Task<RawServiceResponse> SendAsync<TRequest>(TRequest request, CancellationToken ct = default)
        where TRequest : IOpaccServiceRequest
    {
        var paramArray = ServiceRequestToParameters(request);
        return await SendCoreAsync(request.ServiceId, paramArray, ct);
    }

    private async Task<RawServiceResponse> SendCoreAsync(OpaccServiceId service, string[] paramArray, CancellationToken ct)
    {
        var response = await transport.SendRawAsync(service.PortId, service.OperationId, paramArray, ct);
        var records = OpaccResponseParser.ParseRecordsAsStrings(response);
        return new RawServiceResponse(records);
    }

    /// <summary>
    /// Typed service request → positional parameter array.
    /// Null properties become "" to preserve argument positions.
    /// Trailing empty strings are trimmed (no need to send them).
    /// </summary>
    private static string[] ServiceRequestToParameters(IOpaccServiceRequest request)
    {
        var props = request.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType != typeof(OpaccServiceId))
            .ToArray();

        var values = props.Select(p => p.GetValue(request)?.ToString() ?? "").ToArray();

        // trim trailing empty strings — no point sending them
        var lastNonEmpty = Array.FindLastIndex(values, v => v.Length > 0);
        return lastNonEmpty < 0 ? [] : values[..(lastNonEmpty + 1)];
    }

    /// <summary>
    /// Anonymous object → named "Key=Value" parameters (for SendRawAsync).
    /// Null properties are omitted.
    /// </summary>
    private static string[] ObjectToParameters(object? parameters)
    {
        if (parameters == null) return [];
        return parameters.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetValue(parameters) != null)
            .Select(p => $"{p.Name}={p.GetValue(parameters)}")
            .ToArray();
    }

    public async Task<SaveBoResult> SaveBoRawAsync(
        string boEntity,
        string startKeys,
        int indexNo,
        SaveBoOperation operation,
        IReadOnlyList<string> assignments,
        int fixedSegsOfBoIndex = 0,
        CancellationToken ct = default)
    {
        var request = new OpaccSaveBoRequest
        {
            BoEntity           = boEntity,
            StartKeys          = startKeys,
            IndexNo            = indexNo,
            Operation          = operation,
            FixedSegsOfBoIndex = fixedSegsOfBoIndex,
            Assignments        = assignments,
            WithReport         = true,
        };
        var response = await transport.SendSaveBoAsync(request, null, ct);
        var records = OpaccResponseParser.ParseRecords(response, boEntity)
            .Select(r => new SaveBoRecord(r))
            .ToList();
        return new SaveBoResult(records);
    }
}