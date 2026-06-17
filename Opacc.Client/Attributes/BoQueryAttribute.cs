using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Opacc.Client.Attributes;

/// <summary>Property ist nur für Query-Operationen relevant, nicht für GetBo.</summary>
[AttributeUsage(AttributeTargets.Property)]
public class BoQueryAttribute : Attribute { }
