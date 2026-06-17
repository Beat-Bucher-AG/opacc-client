using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Opacc.Client.Extensions;
using Opacc.Client.Helper;
using Opacc.Client.Operations.Exceptions;
using Opacc.Client.Operations.DeleteBo;
using Opacc.Client.Operations.GetBo;
using Opacc.Client.Operations.Query;
using Opacc.Client.Operations.SaveBo;
using Opacc.Client.Session;
using OpaccWebservice;

namespace Opacc.Client.Transport;

public class OpaccTransport : IOpaccTransport
{
    private readonly IOpaccSessionManager _session;
    private readonly ILogger<OpaccTransport> _logger;

    public OpaccTransport(IOpaccSessionManager session, ILogger<OpaccTransport> logger)
    {
        _session = session;
        _logger = logger;
    }

    // ================================================================
    // GetBo
    // ================================================================

    public async Task<FlatResponseData?> SendGetBoAsync(OpaccGetBoRequest request, SessionCredentials? credentials = null, CancellationToken ct = default)
    {
        var parameters = request.BuildParameters();

        _logger.LogDebug("GetBo {Bo} Start={Start} Index={Index} Count={Count}", request.BoEntity, request.Start, request.IndexNo, request.Count);

        return await SendFlatRequestAsync("Biz", "GetBo", parameters, credentials, ct);
    }

    // ================================================================
    // DeleteBo
    // ================================================================

    public async Task<FlatResponseData?> SendDeleteBoAsync(OpaccDeleteBoRequest request, SessionCredentials? credentials = null, CancellationToken ct = default)
    {
        var parameters = request.BuildParameters();

        _logger.LogDebug("DeleteBo {Bo} StartKeys={StartKeys} Index={Index} IsTest={IsTest}", request.BoEntity, request.StartKeys, request.IndexNo, request.IsTest);

        return await SendFlatRequestAsync("Biz", "DeleteBo", parameters, credentials, ct);
    }

    // ================================================================
    // SaveBo
    // ================================================================

    public async Task<FlatResponseData?> SendSaveBoAsync(OpaccSaveBoRequest request, SessionCredentials? credentials = null, CancellationToken ct = default)
    {
        var parameters = request.BuildParameters();

        _logger.LogDebug("SaveBo {Bo} Operation={Operation} Assignments={Count}",
            request.BoEntity, request.Operation, request.Assignments.Count);

        return await SendFlatRequestAsync("Biz", "SaveBo", parameters, credentials, ct);
    }

    // ================================================================
    // Query
    // ================================================================

    public async Task<FlatResponseData?> SendQueryAsync(OpaccQueryRequest request, SessionCredentials? credentials = null, CancellationToken ct = default)
    {
        var parameters = request.BuildParameters();

        _logger.LogDebug("Query {Bo} MaxRows={MaxRows} Filter={Filter}", request.BoEntity, request.MaxRows?.ToString() ?? "All", request.Filter ?? "(none)");
        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("Query parameters:{NewLine}{Parameters}", Environment.NewLine, string.Join(Environment.NewLine, parameters.Select(p => "  " + p)));

        var response = await SendFlatRequestAsync("Biz", "Query", parameters, credentials, ct);

        if (_logger.IsEnabled(LogLevel.Trace) && response != null)
            _logger.LogTrace("Query response: RowCount={RowCount} Columns=[{Columns}]",
                response.RowCount,
                string.Join(", ", response.Columns?.Select(c => c?.Name) ?? []));

        return response;
    }

    // ================================================================
    // Raw / Info
    // ================================================================

    public Task<FlatResponseData?> SendRawAsync(string portId, string operationId, string[] parameters, CancellationToken ct = default)
    {
        return SendFlatRequestAsync(portId, operationId, parameters, null, ct);
    }

    // ================================================================
    // Core: FlatRequest mit Retry
    // ================================================================

    private async Task<FlatResponseData?> SendFlatRequestAsync(
        string portId,
        string operationId,
        string[] parameters,
        SessionCredentials? credentials,
        CancellationToken ct
    )
    {
        var session = await _session.GetSessionAsync(credentials, ct);

        var requestData = new FlatRequestData { Parameters = parameters };

        try
        {
            var response = await session.Client.FlatRequestAsync(new FlatRequestRequest(portId, operationId, session.Context, requestData));

            OpaccResponseParser.ThrowIfError(response?.ResponseInfo);
            return response?.ResponseData;
        }
        catch (OpaccRequestException)
        {
            // Business-Fehler → nicht retrien, direkt weiterwerfen
            throw;
        }
        catch (Exception ex) when (IsSessionError(ex))
        {
            _logger.LogWarning(ex, "Session error on {Operation} {Port}, retrying with fresh session", operationId, portId);

            await _session.InvalidateAsync(credentials);
            session = await _session.GetSessionAsync(credentials, ct);

            var response = await session.Client.FlatRequestAsync(new FlatRequestRequest(portId, operationId, session.Context, requestData));

            OpaccResponseParser.ThrowIfError(response?.ResponseInfo);
            return response?.ResponseData;
        }
    }

    private static bool IsSessionError(Exception ex)
    {
        // Business-Fehler nie retrien
        if (ex.Message.Contains("Storno gesperrt"))
            return false;

        return ex.Message.Contains("session", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || ex is TimeoutException
            || ex is System.ServiceModel.CommunicationException;
    }
}
