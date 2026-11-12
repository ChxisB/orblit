using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Orblit.SourceGenerators
{
    [Generator]
    public class ScriptSignalsGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.IsOrblitSourceGeneratorDisabled("ScriptSignals"))
                return;

            INamedTypeSymbol[] orblitClasses = context
                .Compilation.SyntaxTrees
                .SelectMany(tree =>
                    tree.GetRoot().DescendantNodes()
                        .OfType<ClassDeclarationSyntax>()
                        .SelectOrblitScriptClasses(context.Compilation)
                        // Report and skip non-partial classes
                        .Where(x =>
                        {
                            if (x.cds.IsPartial())
                            {
                                if (x.cds.IsNested() && !x.cds.AreAllOuterTypesPartial(out _))
                                {
                                    return false;
                                }

                                return true;
                            }

                            return false;
                        })
                        .Select(x => x.symbol)
                )
                .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
                .ToArray();

            if (orblitClasses.Length > 0)
            {
                var typeCache = new MarshalUtils.TypeCache(context.Compilation);

                foreach (var orblitClass in orblitClasses)
                {
                    VisitOrblitScriptClass(context, typeCache, orblitClass);
                }
            }
        }

        internal static string SignalDelegateSuffix = "EventHandler";

        private static void VisitOrblitScriptClass(
            GeneratorExecutionContext context,
            MarshalUtils.TypeCache typeCache,
            INamedTypeSymbol symbol
        )
        {
            INamespaceSymbol namespaceSymbol = symbol.ContainingNamespace;
            string classNs = namespaceSymbol != null && !namespaceSymbol.IsGlobalNamespace
                ? namespaceSymbol.FullQualifiedNameOmitGlobal()
                : string.Empty;
            bool hasNamespace = classNs.Length != 0;

            bool isInnerClass = symbol.ContainingType != null;

            string uniqueHint = symbol.FullQualifiedNameOmitGlobal().SanitizeQualifiedNameForUniqueHint()
                                + "_ScriptSignals.generated";

            var source = new StringBuilder();

            source.Append("using Orblit;\n");
            source.Append("using Orblit.NativeInterop;\n");
            source.Append("\n");

            if (hasNamespace)
            {
                source.Append("namespace ");
                source.Append(classNs);
                source.Append(" {\n\n");
            }

            if (isInnerClass)
            {
                var containingType = symbol.ContainingType;
                AppendPartialContainingTypeDeclarations(containingType);

                void AppendPartialContainingTypeDeclarations(INamedTypeSymbol? containingType)
                {
                    if (containingType == null)
                        return;

                    AppendPartialContainingTypeDeclarations(containingType.ContainingType);

                    source.Append("partial ");
                    source.Append(containingType.GetDeclarationKeyword());
                    source.Append(" ");
                    source.Append(containingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                    source.Append("\n{\n");
                }
            }

            source.Append("partial class ");
            source.Append(symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            source.Append("\n{\n");

            var members = symbol.GetMembers();

            var signalDelegateSymbols = members
                .Where(s => s.Kind == SymbolKind.NamedType)
                .Cast<INamedTypeSymbol>()
                .Where(namedTypeSymbol => namedTypeSymbol.TypeKind == TypeKind.Delegate)
                .Where(s => s.GetAttributes()
                    .Any(a => a.AttributeClass?.IsOrblitSignalAttribute() ?? false));

            List<OrblitSignalDelegateData> orblitSignalDelegates = new();

            foreach (var signalDelegateSymbol in signalDelegateSymbols)
            {
                if (!signalDelegateSymbol.Name.EndsWith(SignalDelegateSuffix))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Common.SignalDelegateMissingSuffixRule,
                        signalDelegateSymbol.Locations.FirstLocationWithSourceTreeOrDefault(),
                        signalDelegateSymbol.ToDisplayString()
                    ));
                    continue;
                }

                string signalName = signalDelegateSymbol.Name;
                signalName = signalName.Substring(0, signalName.Length - SignalDelegateSuffix.Length);

                var invokeMethodData = signalDelegateSymbol
                    .DelegateInvokeMethod?.HasOrblitCompatibleSignature(typeCache);

                if (invokeMethodData == null)
                {
                    var methodSymbol = signalDelegateSymbol.DelegateInvokeMethod;

                    if (methodSymbol != null)
                    {
                        foreach (var parameter in methodSymbol.Parameters)
                        {
                            if (parameter.RefKind != RefKind.None)
                            {
                                context.ReportDiagnostic(Diagnostic.Create(
                                    Common.SignalParameterTypeNotSupportedRule,
                                    parameter.Locations.FirstLocationWithSourceTreeOrDefault(),
                                    parameter.ToDisplayString()
                                ));
                                continue;
                            }

                            var marshalType = MarshalUtils.ConvertManagedTypeToMarshalType(parameter.Type, typeCache);
                            if (marshalType == null)
                            {
                                context.ReportDiagnostic(Diagnostic.Create(
                                    Common.SignalParameterTypeNotSupportedRule,
                                    parameter.Locations.FirstLocationWithSourceTreeOrDefault(),
                                    parameter.ToDisplayString()
                                ));
                            }
                        }

                        if (!methodSymbol.ReturnsVoid)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(
                                Common.SignalDelegateSignatureMustReturnVoidRule,
                                signalDelegateSymbol.Locations.FirstLocationWithSourceTreeOrDefault(),
                                signalDelegateSymbol.ToDisplayString()
                            ));
                        }
                    }

                    continue;
                }

                orblitSignalDelegates.Add(new(signalName, signalDelegateSymbol, invokeMethodData.Value));
            }

            source.Append("#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword\n");

            source.Append("    /// <summary>\n")
                .Append("    /// Cached StringNames for the signals contained in this class, for fast lookup.\n")
                .Append("    /// </summary>\n");

            source.Append("    public new class SignalName : ")
                .Append(symbol.BaseType!.FullQualifiedNameIncludeGlobal())
                .Append(".SignalName {\n");

            // Generate cached StringNames for methods and properties, for fast lookup

            foreach (var signalDelegate in orblitSignalDelegates)
            {
                string signalName = signalDelegate.Name;

                source.Append("        /// <summary>\n")
                    .Append("        /// Cached name for the '")
                    .Append(signalName)
                    .Append("' signal.\n")
                    .Append("        /// </summary>\n");

                source.Append("        public new static readonly global::Orblit.StringName @");
                source.Append(signalName);
                source.Append(" = \"");
                source.Append(signalName);
                source.Append("\";\n");
            }

            source.Append("    }\n"); // end of class SignalName

            // Generate GetOrblitSignalList

            if (orblitSignalDelegates.Count > 0)
            {
                const string ListType = "global::System.Collections.Generic.List<global::Orblit.Bridge.MethodInfo>";

                source.Append("    /// <summary>\n")
                    .Append("    /// Get the signal information for all the signals declared in this class.\n")
                    .Append("    /// This method is used by Orblit to register the available signals in the editor.\n")
                    .Append("    /// Do not call this method.\n")
                    .Append("    /// </summary>\n");

                source.Append(
                    "    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]\n");

                source.Append("    internal new static ")
                    .Append(ListType)
                    .Append(" GetOrblitSignalList()\n    {\n");

                source.Append("        var signals = new ")
                    .Append(ListType)
                    .Append("(")
                    .Append(orblitSignalDelegates.Count)
                    .Append(");\n");

                foreach (var signalDelegateData in orblitSignalDelegates)
                {
                    var methodInfo = DetermineMethodInfo(signalDelegateData);
                    AppendMethodInfo(source, methodInfo);
                }

                source.Append("        return signals;\n");
                source.Append("    }\n");
            }

            // Generate GetOrblitRaiseSignalTrampolines

            {
                source.Append("    ").Append(symbol.IsSealed ? "" : "protected ")
                    .Append("internal new static partial class OrblitInternal\n    {\n");

                const string CollectorType = "global::Orblit.Bridge.RaiseSignalTrampolineCollector";

                source.Append("        public static void GetOrblitRaiseSignalTrampolines(")
                    .Append(CollectorType).Append(" collector)\n        {\n");

                foreach (var signal in orblitSignalDelegates)
                {
                    GenerateRaiseSignalTrampoline(symbol, signal, source);
                    AppendRaiseSignalTrampoline(source, signal);
                }

                source.Append("        }\n");

                source.Append("    }\n"); // partial class OrblitInternal
            }

            source.Append("#pragma warning restore CS0109\n");

            // Generate signal event

            foreach (var signalDelegate in orblitSignalDelegates)
            {
                string signalName = signalDelegate.Name;

                // TODO: Hide backing event from code-completion and debugger
                // The reason we have a backing field is to hide the invoke method from the event,
                // as it doesn't emit the signal, only the event delegates. This can confuse users.
                // Maybe we should directly connect the delegates, as we do with native signals?
                source.Append("    private ")
                    .Append(signalDelegate.DelegateSymbol.FullQualifiedNameIncludeGlobal())
                    .Append(" backing_")
                    .Append(signalName)
                    .Append(";\n");

                // A doc reference spells generic arguments with braces. Angle brackets break the XML.
                string docReference = signalDelegate.DelegateSymbol.FullQualifiedNameIncludeGlobal()
                    .Replace('<', '{').Replace('>', '}');
                source.Append($"    /// <inheritdoc cref=\"{docReference}\"/>\n");

                source.Append($"    {signalDelegate.DelegateSymbol.GetAccessibilityKeyword()} event ")
                    .Append(signalDelegate.DelegateSymbol.FullQualifiedNameIncludeGlobal())
                    .Append(" @")
                    .Append(signalName)
                    .Append(" {\n")
                    .Append("        add => backing_")
                    .Append(signalName)
                    .Append(" += value;\n")
                    .Append("        remove => backing_")
                    .Append(signalName)
                    .Append(" -= value;\n")
                    .Append("    }\n");

                // Generate EmitSignal{EventName} method to raise the event

                var invokeMethodSymbol = signalDelegate.InvokeMethodData.Method;
                int paramCount = invokeMethodSymbol.Parameters.Length;

                string raiseMethodModifiers =
                    signalDelegate.DelegateSymbol.ContainingType.IsSealed ? "private" : "protected";

                source.Append($"    {raiseMethodModifiers} void EmitSignal{signalName}(");
                for (int i = 0; i < paramCount; i++)
                {
                    var paramSymbol = invokeMethodSymbol.Parameters[i];
                    source.Append($"{paramSymbol.Type.FullQualifiedNameIncludeGlobal()} @{paramSymbol.Name}");
                    if (i < paramCount - 1)
                    {
                        source.Append(", ");
                    }
                }

                source.Append(")\n");
                source.Append("    {\n");
                source.Append($"        EmitSignal(SignalName.{signalName}, [");
                foreach (var paramSymbol in invokeMethodSymbol.Parameters)
                {
                    if (paramSymbol.Ordinal != 0)
                    {
                        source.Append(", ");
                    }

                    // Enums must be converted to the underlying type before they can be implicitly converted to Variant
                    if (paramSymbol.Type.TypeKind == TypeKind.Enum)
                    {
                        var underlyingType = ((INamedTypeSymbol)paramSymbol.Type).EnumUnderlyingType!;
                        source.Append($"({underlyingType.FullQualifiedNameIncludeGlobal()})@{paramSymbol.Name}");
                        continue;
                    }

                    source.Append($"@{paramSymbol.Name}");
                }

                source.Append("]);\n");
                source.Append("    }\n");
            }

            source.Append("}\n"); // partial class

            if (isInnerClass)
            {
                var containingType = symbol.ContainingType;

                while (containingType != null)
                {
                    source.Append("}\n"); // outer class

                    containingType = containingType.ContainingType;
                }
            }

            if (hasNamespace)
            {
                source.Append("\n}\n");
            }

            context.AddSource(uniqueHint, SourceText.From(source.ToString(), Encoding.UTF8));
        }

        private static void AppendMethodInfo(StringBuilder source, MethodInfo methodInfo)
        {
            source.Append("        signals.Add(new(name: SignalName.@")
                .Append(methodInfo.Name)
                .Append(", returnVal: ");

            AppendPropertyInfo(source, methodInfo.ReturnVal);

            source.Append(", flags: (global::Orblit.MethodFlags)")
                .Append((int)methodInfo.Flags)
                .Append(", arguments: ");

            if (methodInfo.Arguments is { Count: > 0 })
            {
                source.Append("new() { ");

                foreach (var param in methodInfo.Arguments)
                {
                    AppendPropertyInfo(source, param);

                    // C# allows colon after the last element
                    source.Append(", ");
                }

                source.Append(" }");
            }
            else
            {
                source.Append("null");
            }

            source.Append(", defaultArguments: null));\n");
        }

        private static void AppendPropertyInfo(StringBuilder source, PropertyInfo propertyInfo)
        {
            source.Append("new(type: (global::Orblit.Variant.Type)")
                .Append((int)propertyInfo.Type)
                .Append(", name: \"")
                .Append(propertyInfo.Name)
                .Append("\", hint: (global::Orblit.PropertyHint)")
                .Append((int)propertyInfo.Hint)
                .Append(", hintString: \"")
                .Append(propertyInfo.HintString)
                .Append("\", usage: (global::Orblit.PropertyUsageFlags)")
                .Append((int)propertyInfo.Usage)
                .Append(", exported: ")
                .Append(propertyInfo.Exported ? "true" : "false");
            if (propertyInfo.ClassName != null)
            {
                source.Append(", className: new global::Orblit.StringName(\"")
                    .Append(propertyInfo.ClassName)
                    .Append("\")");
            }

            source.Append(")");
        }

        private static MethodInfo DetermineMethodInfo(OrblitSignalDelegateData signalDelegateData)
        {
            var invokeMethodData = signalDelegateData.InvokeMethodData;

            PropertyInfo returnVal;

            if (invokeMethodData.RetType != null)
            {
                returnVal = DeterminePropertyInfo(invokeMethodData.RetType.Value.MarshalType,
                    invokeMethodData.RetType.Value.TypeSymbol,
                    name: string.Empty);
            }
            else
            {
                returnVal = new PropertyInfo(VariantType.Nil, string.Empty, PropertyHint.None,
                    hintString: null, PropertyUsageFlags.Default, exported: false);
            }

            int paramCount = invokeMethodData.ParamTypes.Length;

            List<PropertyInfo>? arguments;

            if (paramCount > 0)
            {
                arguments = new(capacity: paramCount);

                for (int i = 0; i < paramCount; i++)
                {
                    arguments.Add(DeterminePropertyInfo(invokeMethodData.ParamTypes[i],
                        invokeMethodData.Method.Parameters[i].Type,
                        name: invokeMethodData.Method.Parameters[i].Name));
                }
            }
            else
            {
                arguments = null;
            }

            return new MethodInfo(signalDelegateData.Name, returnVal, MethodFlags.Default, arguments,
                defaultArguments: null);
        }

        private static PropertyInfo DeterminePropertyInfo(MarshalType marshalType, ITypeSymbol typeSymbol, string name)
        {
            var memberVariantType = MarshalUtils.ConvertMarshalTypeToVariantType(marshalType)!.Value;

            var propUsage = PropertyUsageFlags.Default;

            if (memberVariantType == VariantType.Nil)
                propUsage |= PropertyUsageFlags.NilIsVariant;

            string? className = null;
            if (memberVariantType == VariantType.Object && typeSymbol is INamedTypeSymbol namedTypeSymbol)
            {
                className = namedTypeSymbol.GetPropertyInfoClassName();
            }

            return new PropertyInfo(memberVariantType, name,
                PropertyHint.None, string.Empty, propUsage, className, exported: false);
        }

        private static void AppendRaiseSignalTrampoline(StringBuilder source, OrblitSignalDelegateData signal)
        {
            string signalName = signal.Name;
            int parameterCount = signal.InvokeMethodData.ParamTypes.Length;

            source.Append("            var aux_delegate_")
                .Append(parameterCount).Append("_").Append(signalName)
                .Append(" = ")
                .Append("trampoline_")
                .Append(parameterCount).Append("_").Append(signalName)
                .Append(";\n");

            source.Append("            collector.TryAdd(new(SignalName.@")
                .Append(signalName).Append(", ").Append(parameterCount)
                .Append("), new(");

            source
                .Append("aux_delegate_")
                .Append(parameterCount).Append("_").Append(signalName)
                .Append(".Method.MethodHandle.GetFunctionPointer()");

            source.Append("));\n");
        }

        private static void GenerateRaiseSignalTrampoline(
            INamedTypeSymbol classSymbol,
            OrblitSignalDelegateData signal,
            StringBuilder source
        )
        {
            string signalName = signal.Name;
            var invokeMethodData = signal.InvokeMethodData;
            int parameterCount = invokeMethodData.ParamTypes.Length;

            source
                .Append("            static void trampoline_")
                .Append(parameterCount).Append("_").Append(signalName)
                .Append("(object orblitObject, NativeVariantPtrArgs args, ")
                .Append("ref orblit_variant_call_error callError)\n            {\n");

            source.Append("                if (args.Count != ");
            source.Append(parameterCount);
            source.Append(") {\n");
            source.Append("                    callError = ")
                .Append("orblit_variant_call_error.CreateInvalidArgumentCountError(expected: ")
                .Append(parameterCount).Append(", provided: args.Count);\n");
            source.Append("                    return;\n");
            source.Append("                }\n");

            source
                .Append("                ((").Append(classSymbol.FullQualifiedNameIncludeGlobal())
                .Append(")orblitObject).backing_")
                .Append(signalName)
                .Append("?.Invoke(");

            for (int i = 0; i < invokeMethodData.ParamTypes.Length; i++)
            {
                if (i != 0)
                    source.Append(", ");

                source.AppendNativeVariantToManagedExpr(("args[", i, "]"),
                    invokeMethodData.ParamTypeSymbols[i], invokeMethodData.ParamTypes[i]);
            }

            source.Append(");\n");

            source.Append("            }\n");
        }
    }
}
