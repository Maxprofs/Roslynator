﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.CSharp.Syntax;

namespace Roslynator.CSharp.Analysis.UsePatternMatching
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UsePatternMatchingInsteadOfAsAndNullCheckAnalyzer : BaseDiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(DiagnosticDescriptors.UsePatternMatchingInsteadOfAsAndNullCheck); }
        }

        public override void Initialize(AnalysisContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            base.Initialize(context);

            context.RegisterSyntaxNodeAction(AnalyzeAsExpression, SyntaxKind.AsExpression);
        }

        public static void AnalyzeAsExpression(SyntaxNodeAnalysisContext context)
        {
            var asExpression = (BinaryExpressionSyntax)context.Node;

            AsExpressionInfo asExpressionInfo = SyntaxInfo.AsExpressionInfo(asExpression);

            if (!asExpressionInfo.Success)
                return;

            SingleLocalDeclarationStatementInfo localInfo = SyntaxInfo.SingleLocalDeclarationStatementInfo(asExpression);

            if (!localInfo.Success)
                return;

            if (localInfo.Statement.SpanOrTrailingTriviaContainsDirectives())
                return;

            if (!(localInfo.Statement.NextStatement() is IfStatementSyntax ifStatement))
                return;

            if (!ifStatement.IsSimpleIf())
                return;

            if (ifStatement.SpanOrLeadingTriviaContainsDirectives())
                return;

            StatementSyntax statement = ifStatement.SingleNonBlockStatementOrDefault();

            if (statement == null)
                return;

            if (!CSharpFacts.IsJumpStatement(statement.Kind()))
                return;

            NullCheckExpressionInfo nullCheck = SyntaxInfo.NullCheckExpressionInfo(ifStatement.Condition, NullCheckStyles.EqualsToNull | NullCheckStyles.IsNull);

            if (!nullCheck.Success)
                return;

            if (!string.Equals(localInfo.IdentifierText, (nullCheck.Expression as IdentifierNameSyntax)?.Identifier.ValueText, StringComparison.Ordinal))
                return;

            if (!localInfo.Type.IsVar)
            {
                SemanticModel semanticModel = context.SemanticModel;
                CancellationToken cancellationToken = context.CancellationToken;

                ITypeSymbol typeSymbol = semanticModel.GetTypeSymbol(localInfo.Type, cancellationToken);
                ITypeSymbol typeSymbol2 = semanticModel.GetTypeSymbol(asExpressionInfo.Type, cancellationToken);

                if (!typeSymbol.Equals(typeSymbol2))
                    return;
            }

            context.ReportDiagnostic(DiagnosticDescriptors.UsePatternMatchingInsteadOfAsAndNullCheck, localInfo.Statement);
        }
    }
}