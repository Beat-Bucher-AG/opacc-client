namespace Opacc.Client.Attributes;

/// <summary>
/// Markiert den Default-Index des BO, der für GetBo/SaveBo/DeleteBo-Requests
/// ohne expliziten Index verwendet wird.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class BoDefaultIndexAttribute : Attribute
{
    public int IndexNo { get; }

    public BoDefaultIndexAttribute(int indexNo)
    {
        IndexNo = indexNo;
    }
}
