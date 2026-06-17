using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Opacc.Client.Metadata;
using Opacc.Client.Enums;
using Opacc.Client.Operations.DeleteBo;
using Opacc.Client.Operations.GetBo;
using Opacc.Client.Operations.Query;
using Opacc.Client.Operations.SaveBo;

namespace Opacc.Client;

public partial interface IOpaccClient
{
    IOpaccGetBo<T> GetBoAsync<T>()
        where T : class, IOpaccModel, new();
    IOpaccQuery<T> QueryAsync<T>()
        where T : class, IOpaccModel, new();
    IOpaccDeleteBo<T> DeleteBoAsync<T>()
        where T : class, IOpaccModel, new();

    IOpaccSaveBo<T> SaveBoAsync<T>()
        where T : class, IOpaccModel, new();

    /// <summary>
    /// Ruft einen beliebigen Opacc-Service auf.
    /// Parameter werden als anonymes Objekt übergeben: <c>new { ArtNo = "10100", CustNo = 1001 }</c>
    /// → wird zu <c>["ArtNo=10100", "CustNo=1001"]</c> serialisiert.
    ///
    /// Beispiel:
    ///   var r = await _opaccClient.SendRawAsync(OpaccService.ArtSal_GetPrice, new { ArtNo = "10100", CustNo = 1001 });
    ///   var price = r.Scalar;
    /// </summary>
    Task<RawServiceResponse> SendRawAsync(
        OpaccServiceId service,
        object? parameters = null,
        CancellationToken ct = default);

    /// <summary>
    /// Typed variant of <see cref="SendRawAsync"/>. Uses the <see cref="IOpaccServiceRequest.ServiceId"/>
    /// on the request object to determine the target service.
    ///
    /// Example:
    ///   var r = await _client.SendAsync(new ArtSal_GetPriceRequest { ArtNo = "10100", CustNo = 1001 });
    ///   var price = r.Scalar;
    /// </summary>
    Task<RawServiceResponse> SendAsync<TRequest>(
        TRequest request,
        CancellationToken ct = default)
        where TRequest : IOpaccServiceRequest;

    /// <summary>Non-generic delete used internally by model extension methods.</summary>
    Task<DeleteBoResult> DeleteBoRawAsync(string boEntity, string startKeys, int indexNo = 1, int fixedSegsOfBoIndex = 0, CancellationToken ct = default);

    /// <summary>Non-generic save used internally by model extension methods.</summary>
    Task<SaveBoResult> SaveBoRawAsync(string boEntity, string startKeys, int indexNo, SaveBoOperation operation, IReadOnlyList<string> assignments, int fixedSegsOfBoIndex = 0, CancellationToken ct = default);
}
