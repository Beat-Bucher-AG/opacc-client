using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Opacc.Client.Enums;

namespace Opacc.Client.Attributes;

/// <summary>
/// Mappt ein C#-Property auf ein Opacc-Attribut.
/// Der Expression-String wird 1:1 an Opacc gesendet.
///
/// Beispiele:
///   [OOProperty("Free2")]                                          → Einfaches Rename
///   [OOProperty("Addr.CountrySc!!")]                               → Dereferenzierung (Long)
///   [OOProperty("Salut.Name@@2(Addr.SalutNo)")]                    → Sprach-Lookup
///   [OOProperty("SalDocCondText.Text(\"[SalDoc.BoId],350\")")]     → Parametrisierter Lookup
///   [OOProperty("SalDoc.EarliestDelivTerm", OpaccDataType.Date)]   → Mit Typ-Konvertierung
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class BoPropertyAttribute : Attribute
{
    public string Expression { get; }
    public OpaccDataType DataType { get; }

    public BoPropertyAttribute(string expression, OpaccDataType dataType = OpaccDataType.None)
    {
        Expression = expression;
        DataType = dataType;
    }
}
