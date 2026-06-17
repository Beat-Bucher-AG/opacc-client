namespace Opacc.Client.Indices;

/// <summary>
/// Typed descriptor for a BO index, usable in place of a raw index number.
///
/// Use the generated <c>Indices</c> nested class on your model instead of magic ints:
///   .Index(SalDoc.Indices.ByCustNo)
///   .Index(SalDoc.Indices.Default)
///
/// The implicit int conversion keeps backward-compatibility with the raw
/// <c>Index(int indexNo)</c> overload.
///
/// Future: an analyzer can use this to verify that all required index segments
/// are populated before executing a SaveBo or DeleteBo.
/// </summary>
public readonly struct BoIndexSpec<T>
    where T : class, IOpaccModel, new()
{
    /// <summary>The Opacc index number (corresponds to GetInfoBoIndex).</summary>
    public int IndexNo { get; }

    /// <summary>Human-readable name for diagnostics and error messages.</summary>
    public string Name { get; }

    public BoIndexSpec(int indexNo, string name)
    {
        IndexNo = indexNo;
        Name    = name;
    }

    /// <summary>Allows a BoIndexSpec to be passed wherever a raw int index number is expected.</summary>
    public static implicit operator int(BoIndexSpec<T> spec) => spec.IndexNo;

    public override string ToString() => Name;
}
