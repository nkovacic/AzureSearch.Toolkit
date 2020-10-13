﻿using Azure.Search.Documents.Models;
using AzureSearchToolkit.Utilities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace AzureSearchToolkit.Request.Visitors
{
    /// <summary>
    /// Expression visitor that substitutes references to <see cref="Document"/>
    /// with desired type.
    /// </summary>
    class AzureSearchFieldsExpressionVisitor : ExpressionVisitor
    {
        protected readonly ParameterExpression BindingParameter;
        protected readonly Type SourceType;

        public AzureSearchFieldsExpressionVisitor(Type sourcetype, ParameterExpression bindingParameter)
        {
            Argument.EnsureNotNull(nameof(bindingParameter), bindingParameter);

            SourceType = sourcetype;
            BindingParameter = bindingParameter;
        }

        internal static Tuple<Expression, ParameterExpression> Rebind(Type sourceType, Expression selector)
        {
            var parameter = Expression.Parameter(typeof(SearchDocument), "h");
            var visitor = new AzureSearchFieldsExpressionVisitor(sourceType, parameter);

            Argument.EnsureNotNull(nameof(selector), selector);

            return Tuple.Create(visitor.Visit(selector), parameter);
        }

        protected virtual Expression VisitAzureSearchField(MemberExpression m)
        {
            return Expression.Convert(Expression.Property(BindingParameter, "Item", Expression.Constant(m.Member.Name.ToLowerInvariant())), m.Type);
        }
    }
}
