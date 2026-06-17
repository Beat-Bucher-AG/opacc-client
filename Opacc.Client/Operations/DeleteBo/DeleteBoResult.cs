namespace Opacc.Client.Operations.DeleteBo;

public sealed class DeleteBoResult
{
    /// <summary>
    /// Per-record results. Only populated when <c>WithReport</c> is enabled (the default).
    /// Each record carries <see cref="DeleteBoRecord.DeleteBoStateCd"/> and
    /// <see cref="DeleteBoRecord.DeleteBoInfo"/> as returned by the service.
    /// </summary>
    public IReadOnlyList<DeleteBoRecord> Records { get; }

    /// <summary>True when the delete was executed in test mode (IsTest=1) — nothing was actually deleted.</summary>
    public bool IsTest { get; }

    /// <summary>Number of successfully deleted records (DeleteBoStateCd == 0).</summary>
    public int DeletedCount => Records.Count(r => r.IsDeleted);

    /// <summary>True if any record reported a deletion error (DeleteBoStateCd == 1).</summary>
    public bool HasErrors => Records.Any(r => r.HasError);

    public DeleteBoResult(IReadOnlyList<DeleteBoRecord> records, bool isTest)
    {
        Records = records;
        IsTest  = isTest;
    }
}
