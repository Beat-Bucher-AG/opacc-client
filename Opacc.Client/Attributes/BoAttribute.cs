using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Opacc.Client.Attributes;

/// <summary>
/// Markiert eine Klasse als Opacc Business Object.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class BoAttribute : Attribute
{
    public string EntityName { get; }

    public BoAttribute(string entityName) => EntityName = entityName;
}
