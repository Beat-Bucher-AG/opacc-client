using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Opacc.Client.Attributes;

/// <summary>Virtuelles Attribut — wird als separate VirtualAttribute-Parameter gesendet.</summary>
[AttributeUsage(AttributeTargets.Property)]
public class BoVirtualAttribute : Attribute
{
    public string Expression { get; }

    public BoVirtualAttribute(string expression) => Expression = expression;
}
