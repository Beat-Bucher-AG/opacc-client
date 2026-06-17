namespace Opacc.Client;

/// <summary>
/// Extension methods for use in Select-Expressions to apply Opacc language/currency suffixes per property.
///
/// At runtime these are identity functions (return the value unchanged).
/// In expression trees they are intercepted by the query builders to generate
/// suffixed OO expressions (e.g., Art.Name1@2 or Art.Price1$7).
///
/// Beispiele:
///   .Select(t => new { NameFR = t.Name1.WithLang(2) })           → Art.Name1@2
///   .Select(t => new { NameFR = t.Name1.WithLangStrict(2) })     → Art.Name1@@2
///   .Select(t => new { PriceEUR = t.Price1.WithCurrency(7) })    → Art.Price1$7
///   .Select(t => new { PriceEUR = t.Price1.WithCurrencyStrict(7) }) → Art.Price1$$7
/// </summary>
public static class OpaccSelectExtensions
{
    /// <summary>Sprache mit Fallback auf Sprache 1 (De). Erzeugt @langCode.</summary>
    public static T WithLang<T>(this T value, int langCode) => value;

    /// <summary>Sprache ohne Fallback. Erzeugt @@langCode.</summary>
    public static T WithLangStrict<T>(this T value, int langCode) => value;

    /// <summary>Währung mit Fallback auf Währung 1 (CHF). Erzeugt $currencyCode.</summary>
    public static T WithCurrency<T>(this T value, int currencyCode) => value;

    /// <summary>Währung ohne Fallback. Erzeugt $$currencyCode.</summary>
    public static T WithCurrencyStrict<T>(this T value, int currencyCode) => value;
}
