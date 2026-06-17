using Opacc.Client.Operations.DeleteBo;
using Opacc.Client.Operations.GetBo;
using Opacc.Client.Operations.Query;
using Opacc.Client.Operations.SaveBo;
using Opacc.Client.Session;
using Opacc.Client.Transport;
using OpaccWebservice;
using Sentry;

namespace Opacc.Client.Sentry;

/// <summary>
/// Decorator around <see cref="IOpaccTransport"/> that adds Sentry performance spans
/// for every Opacc operation. Drop-in replacement — register via
/// <c>services.AddOpaccSentry()</c> after <c>services.AddOpaccClient()</c>.
/// </summary>
internal sealed class SentryOpaccTransport(IOpaccTransport inner) : IOpaccTransport
{
    public async Task<FlatResponseData?> SendGetBoAsync(
        OpaccGetBoRequest request,
        SessionCredentials? credentials = null,
        CancellationToken ct = default)
    {
        var span = SentrySdk.GetSpan()?.StartChild("opacc.getbo", $"GetBo {request.BoEntity}");

        if (span is not null)
        {
            span.SetData("opacc.bo_entity", request.BoEntity);
            span.SetData("opacc.start", request.Start);
            span.SetData("opacc.index_no", request.IndexNo);
            span.SetData("opacc.count", request.Count);
            span.SetData("opacc.search_operator", request.SearchOperator.ToString());

            if (request.Filter is not null)
                span.SetData("opacc.filter", request.Filter);

            if (credentials?.UserId is int userId)
                span.SetData("opacc.user_id", userId);
        }

        try
        {
            var result = await inner.SendGetBoAsync(request, credentials, ct);
            span?.Finish(SpanStatus.Ok);
            return result;
        }
        catch (Exception ex)
        {
            span?.Finish(ex);
            throw;
        }
    }

    public async Task<FlatResponseData?> SendDeleteBoAsync(
        OpaccDeleteBoRequest request,
        SessionCredentials? credentials = null,
        CancellationToken ct = default)
    {
        var span = SentrySdk.GetSpan()?.StartChild("opacc.deletebo", $"DeleteBo {request.BoEntity}");

        if (span is not null)
        {
            span.SetData("opacc.bo_entity", request.BoEntity);
            span.SetData("opacc.start_keys", request.StartKeys);
            span.SetData("opacc.index_no", request.IndexNo);
            span.SetData("opacc.search_operator", request.SearchOperator.ToString());
            span.SetData("opacc.is_test", request.IsTest);

            if (request.Filter is not null)
                span.SetData("opacc.filter", request.Filter);

            if (credentials?.UserId is int userId)
                span.SetData("opacc.user_id", userId);
        }

        try
        {
            var result = await inner.SendDeleteBoAsync(request, credentials, ct);
            span?.Finish(SpanStatus.Ok);
            return result;
        }
        catch (Exception ex)
        {
            span?.Finish(ex);
            throw;
        }
    }

    public async Task<FlatResponseData?> SendSaveBoAsync(
        OpaccSaveBoRequest request,
        SessionCredentials? credentials = null,
        CancellationToken ct = default)
    {
        var span = SentrySdk.GetSpan()?.StartChild("opacc.savebo", $"SaveBo {request.BoEntity}");

        if (span is not null)
        {
            span.SetData("opacc.bo_entity",  request.BoEntity);
            span.SetData("opacc.operation",  request.Operation.ToString());
            span.SetData("opacc.assignments", request.Assignments.Count);

            if (request.Filter is not null)
                span.SetData("opacc.filter", request.Filter);

            if (credentials?.UserId is int userId)
                span.SetData("opacc.user_id", userId);
        }

        try
        {
            var result = await inner.SendSaveBoAsync(request, credentials, ct);
            span?.Finish(SpanStatus.Ok);
            return result;
        }
        catch (Exception ex)
        {
            span?.Finish(ex);
            throw;
        }
    }

    public async Task<FlatResponseData?> SendQueryAsync(
        OpaccQueryRequest request,
        SessionCredentials? credentials = null,
        CancellationToken ct = default)
    {
        var span = SentrySdk.GetSpan()?.StartChild("opacc.query", $"Query {request.BoEntity}");

        if (span is not null)
        {
            span.SetData("opacc.bo_entity", request.BoEntity);
            span.SetData("opacc.max_rows", request.MaxRows?.ToString() ?? "All");

            if (request.Filter is not null)
                span.SetData("opacc.filter", request.Filter);

            if (request.Scrolling is not null)
                span.SetData("opacc.scrolling", true);

            if (request.RedoData is not null)
                span.SetData("opacc.is_continuation", true);

            if (request.Distinct is true)
                span.SetData("opacc.distinct", true);

            if (credentials?.UserId is int userId)
                span.SetData("opacc.user_id", userId);
        }

        try
        {
            var result = await inner.SendQueryAsync(request, credentials, ct);
            span?.Finish(SpanStatus.Ok);
            return result;
        }
        catch (Exception ex)
        {
            span?.Finish(ex);
            throw;
        }
    }

    public async Task<FlatResponseData?> SendRawAsync(
        string portId,
        string operationId,
        string[] parameters,
        CancellationToken ct = default)
    {
        var span = SentrySdk.GetSpan()?.StartChild("opacc.raw", $"{portId}/{operationId}");

        if (span is not null)
        {
            span.SetData("opacc.port_id", portId);
            span.SetData("opacc.operation_id", operationId);
        }

        try
        {
            var result = await inner.SendRawAsync(portId, operationId, parameters, ct);
            span?.Finish(SpanStatus.Ok);
            return result;
        }
        catch (Exception ex)
        {
            span?.Finish(ex);
            throw;
        }
    }
}
