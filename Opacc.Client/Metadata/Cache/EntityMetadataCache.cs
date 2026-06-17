using System.Collections.Concurrent;
using System.Reflection;
using Opacc.Client.Attributes;
using Opacc.Client.Enums;

namespace Opacc.Client.Metadata.Cache;

public static class EntityMetadataCache
{
    private static readonly ConcurrentDictionary<Type, EntityMetadata> _cache = new();

    public static EntityMetadata Get<T>() => _cache.GetOrAdd(typeof(T), Build);

    public static EntityMetadata Get(Type type) => _cache.GetOrAdd(type, Build);

    private static EntityMetadata Build(Type type)
    {
        // BO-Attribut (required)
        var boAttr =
            type.GetCustomAttribute<BoAttribute>()
            ?? throw new InvalidOperationException(
                $"Type {type.Name} is missing the [Bo] attribute. " +
                "All Opacc entity types must be decorated with [Bo(\"EntityName\")].");

        var boEntity     = boAttr.EntityName;
        var defaultIndex = type.GetCustomAttribute<BoDefaultIndexAttribute>()?.IndexNo ?? 1;

        // ----------------------------------------------------------------
        // Pass 1: collect known relation aliases cheaply
        // ----------------------------------------------------------------
        var knownAliases = CollectAliases(type);

        // ----------------------------------------------------------------
        // Pass 2: build property metadata
        // ----------------------------------------------------------------
        var properties      = new Dictionary<string, PropertyMeta>(StringComparer.Ordinal);
        // indexNo → (segmentNo → PropertyMeta)
        var indexBuckets    = new Dictionary<int, SortedDictionary<int, PropertyMeta>>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var ooPropertyAttr = prop.GetCustomAttribute<BoPropertyAttribute>();
            var virtualAttr    = prop.GetCustomAttribute<BoVirtualAttribute>();
            var isQueryOnly              = prop.GetCustomAttribute<BoQueryAttribute>() != null;
            var isNotAvailableInQuery    = prop.GetCustomAttribute<BoPropertyNotAvailableInQuery>() != null;

            string ooExpression;
            OpaccDataType dataType = OpaccDataType.None;

            if (ooPropertyAttr != null)
            {
                ooExpression = ooPropertyAttr.Expression;
                dataType     = ooPropertyAttr.DataType;
            }
            else
            {
                ooExpression = prop.Name;
            }

            // Collect all [BoId(indexNo, segmentNo)] on this property
            var boIdAttrs = prop.GetCustomAttributes<BoIdAttribute>().ToList();
            var indexKeys = boIdAttrs
                .Select(a => new BoIdKey(a.IndexNo, a.SegmentNo))
                .OrderBy(k => k.IndexNo).ThenBy(k => k.SegmentNo)
                .ToList();

            var meta = new PropertyMeta
            {
                ClrName                = prop.Name,
                ClrType                = prop.PropertyType,
                OoExpression           = ooExpression,
                IndexKeys              = indexKeys,
                IsVirtual              = virtualAttr != null,
                IsQueryOnly            = isQueryOnly,
                IsNotAvailableInQuery  = isNotAvailableInQuery,
                DataType               = dataType,
                VirtualExpression      = virtualAttr?.Expression,
                RelationAlias          = ExtractRelationAlias(ooExpression, boEntity, knownAliases),
            };

            properties[prop.Name] = meta;

            // Register in index buckets
            foreach (var key in indexKeys)
            {
                if (!indexBuckets.TryGetValue(key.IndexNo, out var bucket))
                    indexBuckets[key.IndexNo] = bucket = new SortedDictionary<int, PropertyMeta>();
                bucket[key.SegmentNo] = meta;
            }
        }

        // Build AllIndexProperties: indexNo → ordered list of PropertyMeta
        var allIndexProperties = indexBuckets
            .ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<PropertyMeta>)kvp.Value.Values.ToList());

        var idProperties = allIndexProperties.TryGetValue(defaultIndex, out var defProps)
            ? defProps
            : (IReadOnlyList<PropertyMeta>)[];

        // ----------------------------------------------------------------
        // Pass 3: fully resolve relations
        // ----------------------------------------------------------------
        var relations = ResolveRelations(type, boEntity, properties);

        return new EntityMetadata
        {
            BoEntity            = boEntity,
            DefaultIndex        = defaultIndex,
            Properties          = properties,
            IdProperties        = idProperties,
            AllIndexProperties  = allIndexProperties,
            DefaultProperties   = [],
            Relations           = relations,
        };
    }

    // ----------------------------------------------------------------
    // Pass 1 helper: cheap alias extraction
    // ----------------------------------------------------------------

    private static HashSet<string> CollectAliases(Type type)
    {
        var aliases = new HashSet<string>(StringComparer.Ordinal);

        foreach (var attr in type.GetCustomAttributes())
        {
            switch (attr)
            {
                case BoRelationAttribute raw:
                    aliases.Add(raw.Alias);
                    break;

                default:
                    var attrType = attr.GetType();
                    if (!attrType.IsGenericType) break;
                    if (attrType.GetGenericTypeDefinition() != typeof(BoRelationAttribute<>)) break;

                    var explicitAlias = (string?)attrType.GetProperty("Alias")!.GetValue(attr);
                    if (explicitAlias != null)
                    {
                        aliases.Add(explicitAlias);
                    }
                    else
                    {
                        var relatedType = attrType.GetGenericArguments()[0];
                        var boName = relatedType.GetCustomAttribute<BoAttribute>()?.EntityName
                                     ?? relatedType.Name;
                        aliases.Add(boName);
                    }
                    break;
            }
        }

        return aliases;
    }

    // ----------------------------------------------------------------
    // Pass 3 helper: full relation resolution
    // ----------------------------------------------------------------

    private static List<RelationMeta> ResolveRelations(
        Type type, string boEntity, Dictionary<string, PropertyMeta> properties)
    {
        var result = new List<RelationMeta>();

        foreach (var attr in type.GetCustomAttributes())
        {
            switch (attr)
            {
                case BoRelationAttribute raw:
                    result.Add(raw.ToRelationMeta());
                    break;

                default:
                    var attrType = attr.GetType();
                    if (!attrType.IsGenericType) break;
                    if (attrType.GetGenericTypeDefinition() != typeof(BoRelationAttribute<>)) break;

                    var toMeta = attrType.GetMethod("ToRelationMeta")!;
                    var meta   = (RelationMeta)toMeta.Invoke(attr, [type, boEntity, properties])!;
                    result.Add(meta);
                    break;
            }
        }

        return result;
    }

    // ----------------------------------------------------------------
    // Shared helper: extract relation alias from an OO expression
    // ----------------------------------------------------------------

    private static string? ExtractRelationAlias(string ooExpression, string boEntity,
        HashSet<string> knownAliases)
    {
        if (string.IsNullOrWhiteSpace(ooExpression)) return null;

        var dotIndex = ooExpression.IndexOf('.');
        if (dotIndex <= 0) return null;

        var prefix = ooExpression[..dotIndex];

        if (prefix == boEntity) return null;

        return prefix;
    }
}
