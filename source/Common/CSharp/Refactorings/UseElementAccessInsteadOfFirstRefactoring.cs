﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslynator.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Roslynator.CSharp.CSharpFactory;

namespace Roslynator.CSharp.Refactorings
{
    internal static class UseElementAccessInsteadOfFirstRefactoring
    {
        public static bool CanRefactor(MemberInvocationExpressionInfo memberInvocation, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            if (memberInvocation.Expression?.IsMissing == false)
            {
                MethodInfo methodInfo;
                if (semanticModel.TryGetExtensionMethodInfo(memberInvocation.InvocationExpression, out methodInfo, ExtensionMethodKind.Reduced, cancellationToken)
                    && methodInfo.IsLinqExtensionOfIEnumerableOfTWithoutParameters("First", allowImmutableArrayExtension: true))
                {
                    ITypeSymbol typeSymbol = semanticModel.GetTypeSymbol(memberInvocation.Expression, cancellationToken);

                    if (typeSymbol?.IsErrorType() == false
                        && (typeSymbol.IsArrayType() || ExistsApplicableIndexer(memberInvocation.InvocationExpression, typeSymbol, semanticModel)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ExistsApplicableIndexer(
            ExpressionSyntax expression,
            ITypeSymbol containingType,
            SemanticModel semanticModel)
        {
            foreach (ISymbol member in containingType.GetMembers("this[]"))
            {
                var propertySymbol = (IPropertySymbol)member;

                if (!propertySymbol.IsWriteOnly
                    && semanticModel.IsAccessible(expression.SpanStart, propertySymbol.GetMethod)
                    && semanticModel.IsImplicitConversion(expression, propertySymbol.Type))
                {
                    switch (propertySymbol.SingleParameterOrDefault()?.Type.SpecialType)
                    {
                        case SpecialType.System_SByte:
                        case SpecialType.System_Byte:
                        case SpecialType.System_Int16:
                        case SpecialType.System_UInt16:
                        case SpecialType.System_Int32:
                        case SpecialType.System_UInt32:
                        case SpecialType.System_Int64:
                        case SpecialType.System_UInt64:
                            return true;
                    }
                }
            }

            return false;
        }

        public static Task<Document> RefactorAsync(
            Document document,
            InvocationExpressionSyntax invocation,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ArgumentListSyntax argumentList = invocation.ArgumentList;

            var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;

            ExpressionSyntax expression = memberAccess.Expression;

            IEnumerable<SyntaxTrivia> trivia = memberAccess.DescendantTrivia(TextSpan.FromBounds(memberAccess.Expression.Span.End, memberAccess.Name.FullSpan.End));

            if (trivia.All(f => f.IsWhitespaceOrEndOfLineTrivia()))
            {
                expression = expression.WithoutTrailingTrivia();
            }
            else
            {
                expression = expression.WithTrailingTrivia(trivia);
            }

            ExpressionSyntax argumentExpression = NumericLiteralExpression(0);

            ElementAccessExpressionSyntax elementAccess = ElementAccessExpression(
                expression,
                BracketedArgumentList(
                    OpenBracketToken().WithTriviaFrom(argumentList.OpenParenToken),
                    SingletonSeparatedList(Argument(argumentExpression)),
                    CloseBracketToken().WithTriviaFrom(argumentList.CloseParenToken)));

            return document.ReplaceNodeAsync(invocation, elementAccess, cancellationToken);
        }
    }
}