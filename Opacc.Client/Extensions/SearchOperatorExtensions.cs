using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Opacc.Client.Enums;

namespace Opacc.Client.Extensions;

public static class SearchOperatorExtensions
{
    public static string ToOpaccCode(this SearchOperator op) =>
        op switch
        {
            SearchOperator.Equal => "e",
            SearchOperator.EqualIfFound => "eif",
            SearchOperator.Next => "n",
            SearchOperator.NextEqual => "ne",
            SearchOperator.NextPreviousEqual => "npe",
            SearchOperator.NextEqualPrevious => "nep",
            SearchOperator.Previous => "p",
            SearchOperator.PreviousEqual => "pe",
            SearchOperator.PreviousNextEqual => "pne",
            SearchOperator.PreviousEqualNext => "pen",
            SearchOperator.First => "f",
            SearchOperator.Last => "l",
            SearchOperator.None => "",
            _ => "e",
        };

    public static SearchOperator Parse(string? code) =>
        code?.Trim().ToLower() switch
        {
            "e" => SearchOperator.Equal,
            "eif" => SearchOperator.EqualIfFound,
            "n" => SearchOperator.Next,
            "ne" => SearchOperator.NextEqual,
            "npe" => SearchOperator.NextPreviousEqual,
            "nep" => SearchOperator.NextEqualPrevious,
            "p" => SearchOperator.Previous,
            "pe" => SearchOperator.PreviousEqual,
            "pne" => SearchOperator.PreviousNextEqual,
            "pen" => SearchOperator.PreviousEqualNext,
            "f" => SearchOperator.First,
            "l" => SearchOperator.Last,
            _ => SearchOperator.Equal,
        };
}
