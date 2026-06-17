namespace Opacc.Client.Attributes;

/// <summary>
/// Markiert das Property als Segment eines BO-Index.
/// Ein Property kann in mehreren Indices vorkommen → mehrere Attribute erlaubt.
/// </summary>
/// <param name="indexNo">Nummer des Index (entspricht BoIndex in GetInfoBoIndex).</param>
/// <param name="segmentNo">Position innerhalb des Index, beginnend bei 1.</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class BoIdAttribute(int indexNo, int segmentNo) : Attribute
{
    public int IndexNo   { get; } = indexNo;
    public int SegmentNo { get; } = segmentNo;
}
