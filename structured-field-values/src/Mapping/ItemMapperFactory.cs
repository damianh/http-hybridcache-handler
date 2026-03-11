// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Builds parse and serialize delegates from an <see cref="ItemBuilder{T}"/> configuration.
/// </summary>
internal static class ItemMapperFactory
{
    /// <summary>
    /// Builds a parse delegate that converts a <see cref="StructuredFieldItem"/> into a <typeparamref name="T"/>.
    /// </summary>
    internal static Func<StructuredFieldItem, T> BuildParseDelegate<T>(ItemBuilder<T> builder)
        where T : new()
    {
        var valueMapping = builder.ValueMapping;
        var parameters = builder.Parameters;

        return item =>
        {
            var instance = new T();

            // Map bare item value
            if (valueMapping != null)
            {
                var extracted = ItemTypeResolver.ExtractValue(
                    valueMapping.Kind,
                    item,
                    valueMapping.ClrType,
                    "item value");
                valueMapping.Setter(instance, extracted);
            }

            // Map parameters
            foreach (var param in parameters)
            {
                if (item.Parameters.TryGetValue(param.Key, out var paramItem))
                {
                    if (paramItem == null)
                    {
                        // Boolean shorthand: key present with null value means true
                        if (param.Kind == ValueKind.Boolean)
                            param.Setter(instance, true);
                        else
                            throw new StructuredFieldParseException(
                                $"Parameter '{param.Key}' has no value but is not a Boolean.");
                    }
                    else
                    {
                        var extracted = ItemTypeResolver.ExtractValue(
                            param.Kind,
                            paramItem,
                            param.ClrType,
                            $"parameter '{param.Key}'");
                        param.Setter(instance, extracted);
                    }
                }
                else if (param.IsRequired)
                {
                    throw new StructuredFieldParseException(
                        $"Missing required parameter '{param.Key}'.");
                }
            }

            return instance;
        };
    }

    /// <summary>
    /// Builds a serialize delegate that converts a <typeparamref name="T"/> into a <see cref="StructuredFieldItem"/>.
    /// </summary>
    internal static Func<T, StructuredFieldItem> BuildSerializeDelegate<T>(ItemBuilder<T> builder)
        where T : new()
    {
        var valueMapping = builder.ValueMapping;
        var parameters = builder.Parameters;

        return instance =>
        {
            // Build the bare item
            StructuredFieldItem item;
            if (valueMapping != null)
            {
                var rawValue = valueMapping.Getter(instance);
                if (rawValue == null)
                {
                    if (valueMapping.IsRequired)
                        throw new InvalidOperationException(
                            "Item value is required but the property is null.");
                    // Fallback: emit a new BooleanItem(false) as placeholder — callers should avoid null required.
                    // Must not use BooleanItem.False singleton because parameters are added below.
                    item = new BooleanItem(false);
                }
                else
                {
                    item = ItemTypeResolver.ToItem(valueMapping.Kind, rawValue)!;
                }
            }
            else
            {
                // No value mapping — emit a bare Boolean true (like a flag).
                // Must not use BooleanItem.True singleton because parameters may be added below.
                item = new BooleanItem(true);
            }

            // Attach parameters
            foreach (var param in parameters)
            {
                var rawValue = param.Getter(instance);
                if (rawValue == null)
                {
                    if (param.IsRequired)
                        throw new InvalidOperationException(
                            $"Parameter '{param.Key}' is required but the property is null.");
                    continue; // skip optional null parameters
                }

                var paramItem = ItemTypeResolver.ToItem(param.Kind, rawValue);
                item.Parameters.Add(param.Key, paramItem);
            }

            return item;
        };
    }
}
