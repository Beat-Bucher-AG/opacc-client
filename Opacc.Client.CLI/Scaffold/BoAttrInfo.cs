namespace Opacc.Client.CLI.Scaffold;

/// <summary>
/// Repräsentiert ein Attribut eines Opacc Business Objects
/// (aus GetInfoBoAttr-Response).
/// </summary>
internal record BoAttrInfo(
    string AttrExpression,             // vollqualifiziert, z.B. "Addr.Number"
    string ShortName,                  // Kurzname, z.B. "Number"
    string DataTypeCd,                 // Opacc-Typcode: "A" (String), "N" (Numeric), "D" (Date), "B" (Bool)
    string? Format,                    // Format-String: "50" (Stringlänge), "8.0" (Int), "8.2" (Decimal)
    string? Description,
    bool IsNullOk,
    int BoIndex,                       // > 0 wenn Attribut Teil eines Index ist
    string ViewItemStateCd = "AV_PUB", // Status aus GetInfoBoAttr; nur AV_PUB wird scaffolded
    bool NotAvailableInQuery = false   // true wenn GetInfoQuery ViewItemStateCd != "AV_PUB"
);

/// <summary>
/// Repräsentiert ein Business Object (aus GetInfoBo-Response).
/// </summary>
internal record BoInfo(
    string Name,
    string? Description,
    int BoIdIndex   // Hauptindex laut BoIdIndex-Spalte, z.B. 7 für SalDoc
);

/// <summary>
/// Repräsentiert einen Opacc-Service (aus GetInfoService-Response).
/// </summary>
internal record ServiceInfo(
    string PortId,        // z.B. "Biz", "System"
    string OperationId    // z.B. "ArtSal_GetPrice", "SalDoc_Process"
);

/// <summary>
/// Repräsentiert ein Input-Attribut eines Opacc-Services (aus GetInfoServiceAoAttr-Response).
/// </summary>
internal record ServiceAoAttrInfo(
    string Name,             // z.B. "ArtNo", "CustNo"
    string DataTypeCd,       // A, N, D, B, X
    string? Format,          // z.B. "15", "8.0", "8.2"
    bool IsNullOk,
    string RelationshipCd    // "0" = normal, "2" = related, "R" = result/output → überspringen
);

/// <summary>
/// Ergebnis von GetInfoQuery: welche Attribute sind bekannt und welche sind AV_PUB.
/// </summary>
/// <param name="Available">Attribute mit ViewItemStateCd = "AV_PUB" (vollqualifiziert + Kurzname).</param>
/// <param name="AllKnown">Alle in GetInfoQuery enthaltenen Attribute, unabhängig vom Status.</param>
internal record QueryFieldAvailability(
    HashSet<string> Available,
    HashSet<string> AllKnown
)
{
    public bool IsAvailable(BoAttrInfo attr) =>
        Available.Contains(attr.AttrExpression) || Available.Contains(attr.ShortName);
};
