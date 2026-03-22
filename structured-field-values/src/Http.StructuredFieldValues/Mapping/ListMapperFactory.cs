// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Builds parse and serialize delegates from a <see cref="ListBuilder{T}"/> configuration.
/// </summary>
internal static class ListMapperFactory
{
    /// <summary>
    /// Builds a parse delegate that converts a <see cref="StructuredFieldList"/> into a <typeparamref name="T"/>.
    /// </summary>
    internal static Func<StructuredFieldList, T> BuildParseDelegate<T>(ListBuilder<T> builder)
        where T : new()
    {
        var config = builder.ElementConfig
            ?? throw new InvalidOperationException(
                "No element mapping was registered on the ListBuilder. " +
                "Call Elements() or TokenElements() before creating the mapper.");

        return list =>
        {
            var instance = new T();
            var elements = new List<object>();

            foreach (var member in list.Members)
            {
                if (!member.IsItem)
                    throw new StructuredFieldParseException(
                        "List members that are inner lists are not supported by this mapper. " +
                        "Use a dictionary mapper for inner-list structures.");

                var item = member.Item;

                if (config.IsNestedItem)
                {
                    elements.Add(config.NestedItemParseDelegate!(item));
                }
                else
                {
                    elements.Add(ItemTypeResolver.ExtractValue(
                        config.ElementKind,
                        item,
                        config.ElementClrType,
                        "list element"));
                }
            }

            var typedList = CreateTypedReadOnlyList(config.ElementClrType, elements);
            config.Setter(instance, typedList);
            return instance;
        };
    }

    /// <summary>
    /// Builds a serialize delegate that converts a <typeparamref name="T"/> into a <see cref="StructuredFieldList"/>.
    /// </summary>
    internal static Func<T, StructuredFieldList> BuildSerializeDelegate<T>(ListBuilder<T> builder)
        where T : new()
    {
        var config = builder.ElementConfig
            ?? throw new InvalidOperationException(
                "No element mapping was registered on the ListBuilder.");

        return instance =>
        {
            var list = new StructuredFieldList();
            var rawValue = config.Getter(instance);

            if (rawValue == null)
                return list; // empty list for null collection

            var collection = (System.Collections.IEnumerable)rawValue;

            if (config.IsNestedItem)
            {
                foreach (var element in collection)
                    list.Add(config.NestedItemSerializeDelegate!(element));
            }
            else
            {
                foreach (var element in collection)
                {
                    var item = ItemTypeResolver.ToItem(config.ElementKind, element);
                    if (item != null)
                        list.Add(item);
                }
            }

            return list;
        };
    }

    private static object CreateTypedReadOnlyList(Type elementType, List<object> elements)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
        foreach (var e in elements)
            list.Add(e);
        return list;
    }
}
