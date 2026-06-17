using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Opacc.Client.Enums;

public enum SearchOperator
{
    Equal,
    EqualIfFound,
    Next,
    NextEqual,
    NextPreviousEqual,
    NextEqualPrevious,
    Previous,
    PreviousEqual,
    PreviousNextEqual,
    PreviousEqualNext,
    First,
    Last,
    None,
}
