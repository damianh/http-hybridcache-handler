// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Compiles property getters and setters from expression trees for efficient reuse.
/// </summary>
internal static class PropertyAccessor
{
    /// <summary>
    /// Compiles a typed getter and a boxed setter from a property-access expression.
    /// </summary>
    /// <typeparam name="T">The declaring type.</typeparam>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="expression">A lambda that accesses the property (e.g. <c>x => x.Urgency</c>).</param>
    /// <returns>A compiled getter and a setter that accepts a boxed value.</returns>
    internal static (Func<T, TValue> getter, Action<T, TValue> setter) Compile<T, TValue>(
        Expression<Func<T, TValue>> expression)
    {
        var memberExpr = expression.Body as MemberExpression
            ?? throw new ArgumentException(
                $"Expression must be a direct property access (e.g. x => x.Property). Got: {expression.Body.NodeType}",
                nameof(expression));

        var property = memberExpr.Member as PropertyInfo
            ?? throw new ArgumentException(
                $"Expression member must be a property. Got: {memberExpr.Member.MemberType}",
                nameof(expression));

        if (!property.CanRead)
            throw new ArgumentException($"Property '{property.Name}' does not have a getter.", nameof(expression));

        if (!property.CanWrite)
            throw new ArgumentException($"Property '{property.Name}' does not have a setter.", nameof(expression));

        var getter = expression.Compile();

        // Build setter: (T instance, TValue value) => instance.Property = value
        var instanceParam = Expression.Parameter(typeof(T), "instance");
        var valueParam = Expression.Parameter(typeof(TValue), "value");
        var setterLambda = Expression.Lambda<Action<T, TValue>>(
            Expression.Assign(
                Expression.Property(instanceParam, property),
                valueParam),
            instanceParam, valueParam);

        var setter = setterLambda.Compile();
        return (getter, setter);
    }

    /// <summary>
    /// Returns the <see cref="PropertyInfo"/> for a property-access expression.
    /// </summary>
    internal static PropertyInfo GetProperty<T, TValue>(Expression<Func<T, TValue>> expression)
    {
        var memberExpr = expression.Body as MemberExpression
            ?? throw new ArgumentException(
                $"Expression must be a direct property access. Got: {expression.Body.NodeType}",
                nameof(expression));

        return memberExpr.Member as PropertyInfo
            ?? throw new ArgumentException(
                $"Expression member must be a property. Got: {memberExpr.Member.MemberType}",
                nameof(expression));
    }
}
