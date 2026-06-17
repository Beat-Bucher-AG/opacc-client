using Opacc.Client.Enums;

namespace Opacc.Client.Metadata;

public record PropertyMeta
{
    public required string ClrName { get; init; }
    public required Type ClrType { get; init; }
    public required string OoExpression { get; init; }

    /// <summary>
    /// Alle Index-Memberships dieses Property, sortiert nach IndexNo dann SegmentNo.
    /// Leer wenn das Property in keinem Index ist.
    /// </summary>
    public IReadOnlyList<BoIdKey> IndexKeys { get; init; } = [];

    /// <summary>True wenn das Property in mindestens einem BO-Index ist.</summary>
    public bool IsId => IndexKeys.Count > 0;

    public bool IsVirtual { get; init; }
    public bool IsQueryOnly { get; init; }
    public bool IsNotAvailableInQuery { get; init; }
    public OpaccDataType DataType { get; init; }
    public string? VirtualExpression { get; init; }

    /// <summary>
    /// Prefix vor dem ersten Punkt im OoExpression.
    /// Wird verwendet um benötigte Relationen zu ermitteln.
    /// </summary>
    public string? RelationAlias { get; init; }
}

/// <summary>Beschreibt die Position eines Property in einem BO-Index.</summary>
public record BoIdKey(int IndexNo, int SegmentNo);
