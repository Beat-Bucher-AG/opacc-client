namespace Opacc.Client.Attributes;

/// <summary>
/// Marks a property as not available via the Opacc Query service.
/// Such properties can only be loaded through GetBo.
/// Emitted automatically by the scaffold tool when GetInfoQuery reports
/// a <c>ViewItemStateCd</c> other than <c>AV_PUB</c> for the attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class BoPropertyNotAvailableInQuery : Attribute { }
