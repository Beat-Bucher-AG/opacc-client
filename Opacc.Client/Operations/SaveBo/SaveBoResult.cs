namespace Opacc.Client.Operations.SaveBo;

public sealed class SaveBoResult
{
    /// <summary>
    /// Per-record results. Only populated when <c>WithReport</c> is enabled (the default).
    /// </summary>
    public IReadOnlyList<SaveBoRecord> Records { get; }

    /// <summary>Number of successfully saved records (SaveBoStateCd == 0).</summary>
    public int SavedCount => Records.Count(r => r.IsOk);

    /// <summary>True if any record reported a save error (SaveBoStateCd == 1).</summary>
    public bool HasErrors => Records.Any(r => r.HasError);

    public SaveBoResult(IReadOnlyList<SaveBoRecord> records)
    {
        Records = records;
    }
}
