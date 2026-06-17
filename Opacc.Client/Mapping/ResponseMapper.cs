using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Opacc.Client.Enums;
using Opacc.Client.Helper;
using Opacc.Client.Metadata;

namespace Opacc.Client.Mapping;

internal static partial class ResponseMapper
{
    private static readonly string[] _dateFormats = ["dd.MM.yyyy", "yyyyMMdd", "yyyy-MM-dd"];

    private static readonly ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> _typePropertyCache = new();

    private static Dictionary<string, PropertyInfo> GetTargetProperties(Type type) =>
        _typePropertyCache.GetOrAdd(type, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToDictionary(p => p.Name));
    /// <summary>
    /// Mappt die Opacc-Response auf eine Liste von TResult-Objekten.
    ///
    /// Wenn TResult == TSource: direktes Mapping auf die Entity-Properties.
    /// Wenn TResult != TSource: Projection — nur Properties die in TResult existieren.
    /// </summary>
    public static List<TResult> MapToList<TSource, TResult>(object? responseData, EntityMetadata metadata, List<string> requestedPropertyNames)
        where TResult : class, new()
    {
        if (responseData == null)
            return new List<TResult>();

        // Das Response-Format von Opacc konvertieren
        var records = OpaccResponseParser.ParseRecords(responseData, metadata.BoEntity);

        var results = new List<TResult>();
        var targetProperties = GetTargetProperties(typeof(TResult));

        foreach (var record in records)
        {
            var obj = new TResult();

            foreach (var propName in requestedPropertyNames)
            {
                if (!metadata.Properties.TryGetValue(propName, out var propMeta))
                    continue;

                // Ziel-Property auf TResult finden
                if (!targetProperties.TryGetValue(propName, out var targetProp))
                    continue;

                // Wert aus Record lesen
                var ooKey = propMeta.OoExpression;
                if (!ooKey.Contains('.'))
                    ooKey = metadata.BoEntity + "." + ooKey;

                if (record.TryGetValue(ooKey, out var rawValue))
                {
                    var convertedValue = ConvertValue(rawValue, targetProp.PropertyType, propMeta.DataType);
                    if (convertedValue != null)
                        targetProp.SetValue(obj, convertedValue);
                }
            }

            results.Add(obj);
        }

        return results;
    }

    /// <summary>
    /// Mappt die Query-Response auf eine Liste von TResult-Objekten.
    ///
    /// Query-Responses haben Column-Namen wie "Addr.Number", "Addr.FullName" etc.
    /// Bei Relationen: "Cust.Remark", "SalDocPoolItem9001.Free1" etc.
    /// </summary>
    public static List<TResult> MapQueryToList<TSource, TResult>(object? responseData, EntityMetadata metadata, List<string> requestedPropertyNames)
        where TResult : class, new()
    {
        if (responseData == null)
            return new List<TResult>();

        var records = OpaccResponseParser.ParseRecords(responseData, metadata.BoEntity);
        if (records.Count == 0)
            return new List<TResult>();

        var results = new List<TResult>();

        // Property-Mapping vorbereiten: CLR-Name → (TargetPropertyInfo, OoKey)
        var mappings = PrepareMappings<TResult>(metadata, requestedPropertyNames);

        foreach (var record in records)
        {
            var obj = new TResult();

            foreach (var mapping in mappings)
            {
                // Wert im Record finden — versuche verschiedene Key-Varianten
                object? rawValue = null;
                if (
                    record.TryGetValue(mapping.OoKey, out rawValue)
                    || record.TryGetValue(mapping.OoKeyFull, out rawValue)
                    || record.TryGetValue(mapping.OoKeyShort, out rawValue)
                )
                {
                    var convertedValue = ConvertValue(rawValue, mapping.TargetProperty.PropertyType, mapping.DataType);
                    if (convertedValue != null)
                        mapping.TargetProperty.SetValue(obj, convertedValue);
                }
            }

            results.Add(obj);
        }

        return results;
    }

    /// <summary>
    /// Maps response columns directly to TResult properties using explicit OO expression → property mappings.
    /// Used by projected builders when language/currency modifiers are present (bypasses T).
    /// TResult must have a parameterless constructor and writable properties (DTOs, not anonymous types).
    /// </summary>
    public static List<TResult> MapDirectToList<TResult>(
        object? responseData,
        string boEntity,
        List<(string OoExpression, string ResultPropertyName, OpaccDataType DataType)> columnMappings)
    {
        if (responseData == null)
            return new List<TResult>();

        var records = OpaccResponseParser.ParseRecords(responseData, boEntity);
        if (records.Count == 0)
            return new List<TResult>();

        var targetType  = typeof(TResult);
        var targetProps = GetTargetProperties(targetType);
        var results     = new List<TResult>(records.Count);

        // Anonymous types have read-only properties → use their single all-args constructor.
        // DTOs with writable properties use Activator.CreateInstance + SetValue.
        var useCtorInit = targetProps.Values.All(p => !p.CanWrite);
        var ctor        = useCtorInit ? targetType.GetConstructors()[0] : null;
        var ctorParams  = ctor?.GetParameters();

        // Pre-resolve: key, shortKey, dataType, PropertyInfo, constructor parameter index
        var resolved = new List<(string Key, string KeyShort, OpaccDataType DataType, PropertyInfo? Prop, int CtorIndex)>(columnMappings.Count);

        foreach (var (ooExpr, resultPropName, dataType) in columnMappings)
        {
            var dotIdx = ooExpr.LastIndexOf('.');
            var shortKey = dotIdx >= 0 && dotIdx < ooExpr.Length - 1
                ? OpaccResponseParser.StripModifierSuffix(ooExpr[(dotIdx + 1)..])
                : ooExpr;

            targetProps.TryGetValue(resultPropName, out var prop);

            var ctorIndex = -1;
            if (ctorParams != null)
            {
                for (int i = 0; i < ctorParams.Length; i++)
                {
                    if (string.Equals(ctorParams[i].Name, resultPropName, StringComparison.Ordinal))
                    {
                        ctorIndex = i;
                        break;
                    }
                }
            }

            resolved.Add((ooExpr, shortKey, dataType, prop, ctorIndex));
        }

        foreach (var record in records)
        {
            if (useCtorInit && ctor != null && ctorParams != null)
            {
                // Anonymous type path: build constructor argument array
                var args = new object?[ctorParams.Length];
                for (int i = 0; i < ctorParams.Length; i++)
                    args[i] = ctorParams[i].ParameterType.IsValueType
                        ? Activator.CreateInstance(ctorParams[i].ParameterType)
                        : null;

                foreach (var (key, keyShort, dataType, _, ctorIndex) in resolved)
                {
                    if (ctorIndex < 0) continue;
                    if (record.TryGetValue(key, out var rawValue) || record.TryGetValue(keyShort, out rawValue))
                    {
                        var paramType = ctorParams[ctorIndex].ParameterType;
                        args[ctorIndex] = ConvertValue(rawValue, paramType, dataType)
                            ?? (paramType.IsValueType ? Activator.CreateInstance(paramType) : null);
                    }
                }

                results.Add((TResult)ctor.Invoke(args));
            }
            else
            {
                // DTO path: parameterless constructor + SetValue
                var obj = (TResult)Activator.CreateInstance(targetType)!;

                foreach (var (key, keyShort, dataType, prop, _) in resolved)
                {
                    if (prop is not { CanWrite: true }) continue;
                    if (record.TryGetValue(key, out var rawValue) || record.TryGetValue(keyShort, out rawValue))
                    {
                        var converted = ConvertValue(rawValue, prop.PropertyType, dataType);
                        if (converted != null)
                            prop.SetValue(obj, converted);
                    }
                }

                results.Add(obj);
            }
        }

        return results;
    }

    private static object? ConvertValue(object? raw, Type targetType, OpaccDataType dataType)
    {
        if (raw == null || raw == DBNull.Value)
            return null;

        var rawString = raw.ToString();
        if (string.IsNullOrWhiteSpace(rawString))
            return null;

        // Nullable<T> auspacken
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // Opacc-Datumsformat
        if (dataType == OpaccDataType.Date && underlyingType == typeof(DateTime))
        {
            if (
                DateTime.TryParseExact(
                    rawString,
                    _dateFormats,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var date
                )
            )
                return date;
            return null;
        }

        try
        {
            if (underlyingType == typeof(bool))
            {
                // Opacc: "0"/"1" oder "true"/"false"
                return rawString == "1" || rawString.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            if (underlyingType == typeof(int))
                return int.TryParse(rawString, out var i) ? i : 0;

            if (underlyingType == typeof(decimal))
                return decimal.TryParse(rawString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d)
                    ? d
                    : 0m;

            if (underlyingType == typeof(string))
                return rawString;

            return Convert.ChangeType(raw, underlyingType);
        }
        catch
        {
            return underlyingType.IsValueType ? Activator.CreateInstance(underlyingType) : null;
        }
    }

    /// <summary>
    /// Parst die Anzahl Datensätze aus einer Count-Response (MaxRows=0).
    /// </summary>
    public static int ParseCount(object? responseData)
    {
        if (responseData == null)
            return 0;

        // Bei MaxRows=0 gibt Opacc die Gesamtanzahl zurück
        // Das Format hängt von der Opacc-Backend-Implementierung ab
        if (responseData is System.Data.DataTable dt && dt.Rows.Count > 0)
        {
            var firstValue = dt.Rows[0][0];
            if (int.TryParse(firstValue?.ToString(), out var count))
                return count;
        }

        // Fallback: als String parsen
        if (int.TryParse(responseData.ToString(), out var result))
            return result;

        return 0;
    }

    private record PropertyMapping(
        PropertyInfo TargetProperty,
        string OoKey, // z.B. "Addr.Number"
        string OoKeyFull, // z.B. "Addr.CountrySc!!"
        string OoKeyShort, // z.B. "Number"
        OpaccDataType DataType
    );

    private static List<PropertyMapping> PrepareMappings<TResult>(EntityMetadata metadata, List<string> propertyNames)
    {
        var mappings = new List<PropertyMapping>(propertyNames.Count);
        var targetProps = GetTargetProperties(typeof(TResult));

        foreach (var propName in propertyNames)
        {
            if (!metadata.Properties.TryGetValue(propName, out var propMeta))
                continue;
            if (!targetProps.TryGetValue(propName, out var targetProp))
                continue;

            var ooExpr = propMeta.OoExpression;
            var ooExprFull = ooExpr.Contains('.') ? ooExpr : metadata.BoEntity + "." + ooExpr;

            // Kurzform: letzter Teil nach dem Punkt (ohne Modifikatoren)
            var shortName = ooExprFull.Split('.').Last().Split('(').First().TrimEnd('!', '@');

            mappings.Add(new PropertyMapping(targetProp, ooExprFull, ooExpr, shortName, propMeta.DataType));
        }

        return mappings;
    }
}
