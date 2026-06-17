using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Opacc.Client.Operations.Query;

public class QueryColumn
{
    /// <summary>Die OO-Expression (z.B. "Addr.Number" oder "Addr.Number, Titel")</summary>
    public required string Expression { get; init; }

    /// <summary>Ob die Column einen Alias enthält (Komma in der Expression)</summary>
    public bool HasAlias { get; init; }

    /// <summary>CLR Property Name für das Mapping zurück</summary>
    public required string ClrPropertyName { get; init; }
}
