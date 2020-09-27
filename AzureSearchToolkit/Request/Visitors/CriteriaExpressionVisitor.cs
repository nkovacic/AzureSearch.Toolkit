﻿using AzureSearchToolkit.Request.Criteria;
using AzureSearchToolkit.Request.Expressions;
using AzureSearchToolkit.Utilities;

using Microsoft.Spatial;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureSearchToolkit.Request.Visitors
{
    internal abstract class CriteriaExpressionVisitor<T> : ExpressionVisitor
    {
        private static Dictionary<Type, Dictionary<string, string>> mappedPropertiesCache = new Dictionary<Type, Dictionary<string, string>>();

        private readonly JsonSerializerOptions jsonOptions;

        protected CriteriaExpressionVisitor(JsonSerializerOptions jsonOptions)
        {
            this.jsonOptions = jsonOptions;
        }

        private Dictionary<string, string> GetMappedPropertiesForType(Type sourceType)
        {
            if (!mappedPropertiesCache.ContainsKey(sourceType))
            {
                mappedPropertiesCache.Add(sourceType, new Dictionary<string, string>());
                var camelCasePropertyAttribute = sourceType.GetCustomAttribute<JsonPropertyNameAttribute>(inherit: true);
                foreach (var property in sourceType.GetProperties())
                {
                    var propertyName = jsonOptions.PropertyNamingPolicy.ConvertName(property.Name);
                    mappedPropertiesCache[sourceType].Add(property.Name, propertyName);
                }
            }
            return mappedPropertiesCache[sourceType];
        }

        /// <summary>
        /// Get the AzureSearch field name for a given member.
        /// </summary>
        /// <param name="type">The prefix to put in front of this field name, if the field is
        /// an ongoing part of the document search.</param>
        /// <param name="memberInfo">The member whose field name is required.</param>
        /// <returns>The AzureSearch field name that matches the member.</returns>
        public virtual string GetFieldName(Type type, MemberInfo memberInfo)
        {
            Argument.EnsureNotNull(nameof(type), type);
            Argument.EnsureNotNull(nameof(memberInfo), memberInfo);

            var propertyName = memberInfo.Name;

            var mappedProperties = GetMappedPropertiesForType(type);

            var mappedProperty = mappedProperties[propertyName];

            if (!mappedProperties.TryGetValue(propertyName, out var name))
            {
                return name;
            }
            else
            {
                throw new KeyNotFoundException($"Property {propertyName} was not found on {type}.");
            }
        }

        /// <inheritdoc/>
        public string GetFieldName(Type type, MemberExpression memberExpression)
        {
            Argument.EnsureNotNull(nameof(memberExpression), memberExpression);
            switch (memberExpression.Expression.NodeType)
            {
                case ExpressionType.MemberAccess:
                case ExpressionType.Parameter:
                    return GetFieldName(type, memberExpression.Member);
                default:
                    throw new NotSupportedException($"Unknown expression type {memberExpression.Expression.NodeType} for left hand side of expression {memberExpression}");
            }
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(string))
            {
                return VisitStringMethodCall(node);
            }

            if (node.Method.DeclaringType == typeof(Enumerable))
            {
                return VisitEnumerableMethodCall(node);
            }


            if (node.Method.DeclaringType == typeof(AzureSearchMethods))
            {
                return VisitAzureSearchMethodsMethodCall(node);
            }

            return VisitDefaultMethodCall(node);
        }

        Expression VisitDefaultMethodCall(MethodCallExpression m)
        {
            switch (m.Method.Name)
            {
                case "Equals":
                    if (m.Arguments.Count == 1)
                    {
                        return VisitEquals(Visit(m.Object), Visit(m.Arguments[0]));
                    }

                    if (m.Arguments.Count == 2)
                    {
                        return VisitEquals(Visit(m.Arguments[0]), Visit(m.Arguments[1]));
                    }

                    break;

                case "Contains":
                    if (TypeHelper.FindIEnumerable(m.Method.DeclaringType) != null)
                    {
                        return VisitEnumerableContainsMethodCall(m.Object, m.Arguments[0]);
                    }

                    break;
            }

            return base.VisitMethodCall(m);
        }

        protected Expression VisitAzureSearchMethodsMethodCall(MethodCallExpression m)
        {
            switch (m.Method.Name)
            {
                case "ContainsAny":
                    if (m.Arguments.Count == 2)
                    {
                        return VisitContains("ContainsAny", m.Arguments[0], m.Arguments[1], TermsOperator.Any);
                    }

                    break;

                case "ContainsAll":
                    if (m.Arguments.Count == 2)
                    {
                        return VisitContains("ContainsAll", m.Arguments[0], m.Arguments[1], TermsOperator.All);
                    }

                    break;
                case "Distance":
                    if (m.Arguments.Count == 2)
                    {
                        return VisitDistance(m.Arguments[0], m.Arguments[1]);
                    }

                    break;
            }

            throw new NotSupportedException($"AzureSearch.{m.Method.Name} method is not supported");
        }

        protected Expression VisitEnumerableMethodCall(MethodCallExpression m)
        {
            switch (m.Method.Name)
            {
                case "Contains":
                    if (m.Arguments.Count == 2)
                        return VisitEnumerableContainsMethodCall(m.Arguments[0], m.Arguments[1]);
                    break;
            }

            throw new NotSupportedException($"Enumerable.{m.Method.Name} method is not supported");
        }

        protected Expression VisitStringMethodCall(MethodCallExpression m)
        {
            switch (m.Method.Name)
            {
                case "Contains":  // Where(x => x.StringProperty.Contains(value))
                    if (m.Arguments.Count == 1)
                    {
                        return VisitStringPatternCheckMethodCall(m.Object, m.Arguments[0], "/.*{0}.*/", m.Method.Name);
                    }

                    break;

                case "StartsWith": // Where(x => x.StringProperty.StartsWith(value))
                    if (m.Arguments.Count == 1)
                    {
                        return VisitStringPatternCheckMethodCall(m.Object, m.Arguments[0], "{0}*", m.Method.Name);
                    }

                    break;

                case "EndsWith": // Where(x => x.StringProperty.EndsWith(value))
                    if (m.Arguments.Count == 1)
                    {
                        return VisitStringPatternCheckMethodCall(m.Object, m.Arguments[0], "/.*{0}/", m.Method.Name);
                    }

                    break;
            }

            return VisitDefaultMethodCall(m);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            switch (node.NodeType)
            {
                case ExpressionType.Convert:
                    return node.Operand;

                case ExpressionType.Not:
                    var subExpression = Visit(node.Operand) as CriteriaExpression;

                    if (subExpression != null)
                    {
                        return new CriteriaExpression(NotCriteria.Create(subExpression.Criteria));
                    }

                    break;
            }

            return base.VisitUnary(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            switch (node.Expression.NodeType)
            {
                case ExpressionType.Parameter:
                case ExpressionType.MemberAccess:
                    return node;

                default:
                    var memberName = node.Member.Name;

                    if (node.Member.DeclaringType != null)
                    {
                        memberName = node.Member.DeclaringType.Name + "." + node.Member.Name;
                    }

                    throw new NotSupportedException($"{memberName} is of unsupported type {node.Expression.NodeType}");
            }
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            switch (node.NodeType)
            {
                case ExpressionType.OrElse:
                    return VisitOrElse(node);

                case ExpressionType.AndAlso:
                    return VisitAndAlso(node);

                case ExpressionType.Equal:
                    return VisitEquals(Visit(node.Left), Visit(node.Right));

                case ExpressionType.NotEqual:
                    return VisitNotEqual(Visit(node.Left), Visit(node.Right));

                case ExpressionType.GreaterThan:
                    return VisitRange(Comparison.GreaterThan, Visit(node.Left), Visit(node.Right));

                case ExpressionType.GreaterThanOrEqual:
                    return VisitRange(Comparison.GreaterThanOrEqual, Visit(node.Left), Visit(node.Right));

                case ExpressionType.LessThan:
                    return VisitRange(Comparison.LessThan, Visit(node.Left), Visit(node.Right));

                case ExpressionType.LessThanOrEqual:
                    return VisitRange(Comparison.LessThanOrEqual, Visit(node.Left), Visit(node.Right));

                default:
                    throw new NotSupportedException($"Binary expression '{node.NodeType}' is not supported");
            }
        }

        protected Expression BooleanMemberAccessBecomesEquals(Expression e)
        {
            e = Visit(e);

            var c = e as ConstantExpression;

            if (c?.Value != null)
            {
                if (c.Value.Equals(true))
                {
                    return new CriteriaExpression(ConstantCriteria.True);
                }

                if (c.Value.Equals(false))
                {
                    return new CriteriaExpression(ConstantCriteria.False);
                }
            }

            var wasNegative = e.NodeType == ExpressionType.Not;

            if (e is UnaryExpression)
                e = Visit(((UnaryExpression)e).Operand);

            if (e is MemberExpression && e.Type == typeof(bool))
                return Visit(Expression.Equal(e, Expression.Constant(!wasNegative)));

            return e;
        }

        Expression VisitEnumerableContainsMethodCall(Expression source, Expression match)
        {
            var matched = Visit(match);

            // Where(x => constantsList.Contains(x.Property))
            if (source is ConstantExpression && matched is MemberExpression)
            {
                var memberExpression = (MemberExpression)matched;
                var field = GetFieldName(typeof(T), memberExpression);
                var containsSource = ((IEnumerable)((ConstantExpression)source).Value);

                // If criteria contains a null create an Or criteria with Terms on one
                // side and Missing on the other.
                var values = containsSource.Cast<object>().Distinct().ToList();
                var nonNullValues = values.Where(v => v != null).ToList();

                ICriteria criteria = TermsCriteria.Build(field, memberExpression.Member, nonNullValues);

                if (values.Count != nonNullValues.Count)
                {
                    criteria = OrCriteria.Combine(criteria, new MissingCriteria(field));
                }

                return new CriteriaExpression(criteria);
            }

            // Where(x => x.SomeList.Contains(constantValue))
            if (source is MemberExpression && matched is ConstantExpression)
            {
                var memberExpression = (MemberExpression)source;
                var value = ((ConstantExpression)matched).Value;

                var field = GetFieldName(typeof(T), memberExpression);

                return new CriteriaExpression(TermsCriteria.Build(field, memberExpression.Member, value));
            }

            throw new NotSupportedException(source is MemberExpression
                ? $"Match '{match}' in Contains operation must be a constant"
                : $"Unknown source '{source}' for Contains operation");
        }

        protected virtual Expression VisitStringPatternCheckMethodCall(Expression source, Expression match, string pattern, string methodName)
        {
            var matched = Visit(match);

            if (source is MemberExpression && matched is ConstantExpression)
            {
                var field = GetFieldName(typeof(T), (MemberExpression)source);
                var value = ((ConstantExpression)matched).Value;

                return new CriteriaExpression(new QueryStringCriteria(string.Format(pattern, value), field));
            }

            throw new NotSupportedException(source is MemberExpression
                ? $"Match '{match}' in Contains operation must be a constant"
                : $"Unknown source '{source}' for Contains operation");
        }

        Expression VisitAndAlso(BinaryExpression b)
        {
            return new CriteriaExpression(
                AndCriteria.Combine(CombineExpressions<CriteriaExpression>(b.Left, b.Right).Select(f => f.Criteria).ToArray()));
        }

        Expression VisitOrElse(BinaryExpression b)
        {
            return new CriteriaExpression(
                OrCriteria.Combine(CombineExpressions<CriteriaExpression>(b.Left, b.Right).Select(f => f.Criteria).ToArray()));
        }

        IEnumerable<TExpr> CombineExpressions<TExpr>(params Expression[] expressions) where TExpr : Expression
        {
            foreach (var expression in expressions.Select(BooleanMemberAccessBecomesEquals))
            {
                if ((expression as TExpr) == null)
                    throw new NotSupportedException($"Unexpected binary expression '{expression}'");

                yield return (TExpr)expression;
            }
        }

        Expression VisitContains(string methodName, Expression left, Expression right, TermsOperator executionMode)
        {
            var cm = ConstantMemberPair.Create(left, right);

            if (cm != null)
            {
                var values = ((IEnumerable)cm.ConstantExpression.Value).Cast<object>().ToArray();

                return new CriteriaExpression(TermsCriteria.Build(executionMode, GetFieldName(typeof(T), cm.MemberExpression), cm.MemberExpression.Member, values));
            }

            throw new NotSupportedException(methodName + " must be between a Member and a Constant");
        }

        Expression VisitDistance(Expression left, Expression right)
        {
            var cm = ConstantMemberPair.Create(left, right);

            if (cm != null)
            {
                var value = ((GeographyPoint)cm.ConstantExpression.Value);

                return new CriteriaExpression(new DistanceCriteria(GetFieldName(typeof(T), cm.MemberExpression), cm.MemberExpression.Member, value, null));
            }

            throw new NotSupportedException("Distance must be between a Member and a Constant");
        }

        Expression CreateExists(ConstantMemberPair cm, bool positiveTest)
        {
            var fieldName = GetFieldName(typeof(T), UnwrapNullableMethodExpression(cm.MemberExpression));

            var value = cm.ConstantExpression.Value ?? false;

            if (value.Equals(positiveTest))
            {
                return new CriteriaExpression(new ExistsCriteria(fieldName));
            }

            if (value.Equals(!positiveTest))
            {
                return new CriteriaExpression(new MissingCriteria(fieldName));
            }

            throw new NotSupportedException("A null test Expression must have a member being compared to a bool or null");
        }

        Expression VisitEquals(Expression left, Expression right)
        {
            var booleanEquals = VisitCriteriaEquals(left, right, true);

            if (booleanEquals != null)
            {
                return booleanEquals;
            }

            var cm = ConstantMemberPair.Create(left, right);

            if (cm != null)
            {

                return cm.IsNullTest
                    ? CreateExists(cm, true)
                    : new CriteriaExpression(new ComparisonCriteria(GetFieldName(typeof(T), cm.MemberExpression),
                        cm.MemberExpression.Member, Comparison.Equal, cm.ConstantExpression.Value));
            }

            throw new NotSupportedException("Equality must be between a Member and a Constant");
        }

        static Expression VisitCriteriaEquals(Expression left, Expression right, bool positiveCondition)
        {
            var criteria = left as CriteriaExpression ?? right as CriteriaExpression;
            var constant = left as ConstantExpression ?? right as ConstantExpression;

            if (criteria == null || constant == null)
            {
                return null;
            }

            if (constant.Value.Equals(positiveCondition))
            {
                return criteria;
            }

            if (constant.Value.Equals(!positiveCondition))
            {
                return new CriteriaExpression(NotCriteria.Create(criteria.Criteria));
            }

            return null;
        }

        static MemberExpression UnwrapNullableMethodExpression(MemberExpression m)
        {
            var lhsMemberExpression = m.Expression as MemberExpression;

            if (lhsMemberExpression != null && m.Member.Name == "HasValue" && m.Member.DeclaringType.IsGenericOf(typeof(Nullable<>)))
            {
                return lhsMemberExpression;
            }

            return m;
        }

        Expression VisitNotEqual(Expression left, Expression right)
        {
            var booleanEquals = VisitCriteriaEquals(left, right, false);
            if (booleanEquals != null)
                return booleanEquals;

            var cm = ConstantMemberPair.Create(left, right);

            if (cm == null)
                throw new NotSupportedException("A not-equal expression must be between a constant and a member");

            return cm.IsNullTest
                ? CreateExists(cm, false)
                : new CriteriaExpression(new ComparisonCriteria(GetFieldName(typeof(T), cm.MemberExpression),
                        cm.MemberExpression.Member, Comparison.NotEqual, cm.ConstantExpression.Value));
        }

        Expression VisitRange(Comparison rangeComparison, Expression left, Expression right)
        {
            var existingCriteriaExpression = left as CriteriaExpression;

            if (existingCriteriaExpression == null)
            {
                var inverted = left is ConstantExpression;

                var cm = ConstantMemberPair.Create(left, right);

                if (cm == null)
                {
                    throw new NotSupportedException("A {0} must test a constant against a member");
                }

                var field = GetFieldName(typeof(T), cm.MemberExpression);
                var comparisonCriteria = new ComparisonCriteria(field, cm.MemberExpression.Member, rangeComparison, cm.ConstantExpression.Value);

                return new CriteriaExpression(inverted ? comparisonCriteria.Negate() : comparisonCriteria);
            }
            else
            {
                var distanceCriteria = existingCriteriaExpression.Criteria as DistanceCriteria;

                if (distanceCriteria != null)
                {
                    var constantExpression = right as ConstantExpression;

                    if (constantExpression == null)
                    {
                        throw new NotSupportedException($"A {right} must test a constant against a member");
                    }

                    distanceCriteria.ReplaceComparison(rangeComparison, constantExpression.Value);
                }

                return existingCriteriaExpression;
            }
        }
    }
}
