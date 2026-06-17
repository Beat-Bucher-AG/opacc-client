namespace Opacc.Client.Operations.DeleteBo;

public sealed class DeleteBoRecord
{
    public string? BoId { get; }
    public string? BoNumber { get; }
    public string? BoName { get; }

    /// <summary>0 = deleted, 1 = error, 2 = not found.</summary>
    public int DeleteBoStateCd { get; }

    /// <summary>Error detail when <see cref="DeleteBoStateCd"/> is non-zero.</summary>
    public string? DeleteBoInfo { get; }

    public bool IsDeleted => DeleteBoStateCd == 0;
    public bool HasError => DeleteBoStateCd == 1;
    public bool WasNotFound => DeleteBoStateCd == 2;

    internal DeleteBoRecord(Dictionary<string, object?> row)
    {
        BoId = row.GetValueOrDefault("BoId")?.ToString();
        BoNumber = row.GetValueOrDefault("BoNumber")?.ToString();
        BoName = row.GetValueOrDefault("BoName")?.ToString();
        DeleteBoStateCd = int.TryParse(row.GetValueOrDefault("DeleteBoStateCd")?.ToString(), out var code) ? code : 0;
        DeleteBoInfo = row.GetValueOrDefault("DeleteBoInfo")?.ToString();
    }
}