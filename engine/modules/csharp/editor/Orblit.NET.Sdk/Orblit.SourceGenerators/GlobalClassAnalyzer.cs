using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Orblit.SourceGenerators
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class GlobalClassAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(
                Common.GlobalClassMustDeriveFromOrblitObjectRule,
                Common.GlobalClassMustNotBeGenericRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ClassDeclaration);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            // Return if not a type symbol or the type is not a global class.
            if (context.ContainingSymbol is not INamedTypeSymbol typeSymbol ||
                !typeSymbol.GetAttributes().Any(a => a.AttributeClass?.IsOrblitGlobalClassAttribute() ?? false))
                return;

            if (typeSymbol.IsGenericType)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Common.GlobalClassMustNotBeGenericRule,
                    typeSymbol.Locations.FirstLocationWithSourceTreeOrDefault(),
                    typeSymbol.ToDisplayString()
                ));
            }

            if (!typeSymbol.InheritsFrom("OrblitSharp", OrblitClasses.OrblitObject))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Common.GlobalClassMustDeriveFromOrblitObjectRule,
                    typeSymbol.Locations.FirstLocationWithSourceTreeOrDefault(),
                    typeSymbol.ToDisplayString()
                ));
            }
        }
    }
}
