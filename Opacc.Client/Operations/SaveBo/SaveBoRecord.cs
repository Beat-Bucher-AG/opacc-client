namespace Opacc.Client.Operations.SaveBo;

public sealed class SaveBoRecord
{
    public string? BoId     { get; }
    public string? BoNumber { get; }
    public string? BoName   { get; }

    /// <summary>0 = ok, 1 = error (not saved), 2 = createdOnly (created but mutation failed).</summary>
    public int SaveBoStateCd { get; }

    /// <summary>Error or warning detail when <see cref="SaveBoStateCd"/> is non-zero.</summary>
    public string? SaveBoInfo { get; }

    public bool IsOk          => SaveBoStateCd == 0;
    public bool HasError      => SaveBoStateCd == 1;
    public bool IsCreatedOnly => SaveBoStateCd == 2;

    internal SaveBoRecord(Dictionary<string, object?> row)
    {
        BoId         = row.GetValueOrDefault("BoId")?.ToString();
        BoNumber     = row.GetValueOrDefault("BoNumber")?.ToString();
        BoName       = row.GetValueOrDefault("BoName")?.ToString();
        SaveBoStateCd = int.TryParse(row.GetValueOrDefault("SaveBoStateCd")?.ToString(), out var code) ? code : 0;
        SaveBoInfo   = row.GetValueOrDefault("SaveBoInfo")?.ToString();
    }
}
