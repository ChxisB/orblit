using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Orblit.SourceGenerators
{
    internal static class MarshalUtils
    {
        public class TypeCache
        {
            public INamedTypeSymbol OrblitObjectType { get; }

            public TypeCache(Compilation compilation)
            {
                INamedTypeSymbol GetTypeByMetadataNameOrThrow(string fullyQualifiedMetadataName)
                {
                    return compilation.GetTypeByMetadataName(fullyQualifiedMetadataName) ??
                           throw new InvalidOperationException($"Type not found: '{fullyQualifiedMetadataName}'.");
                }

                OrblitObjectType = GetTypeByMetadataNameOrThrow(OrblitClasses.OrblitObject);
            }
        }

        public static VariantType? ConvertMarshalTypeToVariantType(MarshalType marshalType)
            => marshalType switch
            {
                MarshalType.Boolean => VariantType.Bool,
                MarshalType.Char => VariantType.Int,
                MarshalType.SByte => VariantType.Int,
                MarshalType.Int16 => VariantType.Int,
                MarshalType.Int32 => VariantType.Int,
                MarshalType.Int64 => VariantType.Int,
                MarshalType.Byte => VariantType.Int,
                MarshalType.UInt16 => VariantType.Int,
                MarshalType.UInt32 => VariantType.Int,
                MarshalType.UInt64 => VariantType.Int,
                MarshalType.Single => VariantType.Float,
                MarshalType.Double => VariantType.Float,
                MarshalType.String => VariantType.String,
                MarshalType.Vector2 => VariantType.Vector2,
                MarshalType.Vector2I => VariantType.Vector2I,
                MarshalType.Rect2 => VariantType.Rect2,
                MarshalType.Rect2I => VariantType.Rect2I,
                MarshalType.Transform2D => VariantType.Transform2D,
                MarshalType.Vector3 => VariantType.Vector3,
                MarshalType.Vector3I => VariantType.Vector3I,
                MarshalType.Basis => VariantType.Basis,
                MarshalType.Quaternion => VariantType.Quaternion,
                MarshalType.Transform3D => VariantType.Transform3D,
                MarshalType.Vector4 => VariantType.Vector4,
                MarshalType.Vector4I => VariantType.Vector4I,
                MarshalType.Projection => VariantType.Projection,
                MarshalType.Aabb => VariantType.Aabb,
                MarshalType.Color => VariantType.Color,
                MarshalType.Plane => VariantType.Plane,
                MarshalType.Callable => VariantType.Callable,
                MarshalType.Signal => VariantType.Signal,
                MarshalType.Enum => VariantType.Int,
                MarshalType.ByteArray => VariantType.PackedByteArray,
                MarshalType.Int32Array => VariantType.PackedInt32Array,
                MarshalType.Int64Array => VariantType.PackedInt64Array,
                MarshalType.Float32Array => VariantType.PackedFloat32Array,
                MarshalType.Float64Array => VariantType.PackedFloat64Array,
                MarshalType.StringArray => VariantType.PackedStringArray,
                MarshalType.Vector2Array => VariantType.PackedVector2Array,
                MarshalType.Vector3Array => VariantType.PackedVector3Array,
                MarshalType.Vector4Array => VariantType.PackedVector4Array,
                MarshalType.ColorArray => VariantType.PackedColorArray,
                MarshalType.OrblitObjectOrDerivedArray => VariantType.Array,
                MarshalType.SystemArrayOfStringName => VariantType.Array,
                MarshalType.SystemArrayOfEntityPath => VariantType.Array,
                MarshalType.SystemArrayOfRid => VariantType.Array,
                MarshalType.Variant => VariantType.Nil,
                MarshalType.OrblitObjectOrDerived => VariantType.Object,
                MarshalType.StringName => VariantType.StringName,
                MarshalType.EntityPath => VariantType.EntityPath,
                MarshalType.Rid => VariantType.Rid,
                MarshalType.OrblitDictionary => VariantType.Dictionary,
                MarshalType.OrblitArray => VariantType.Array,
                MarshalType.OrblitGenericDictionary => VariantType.Dictionary,
                MarshalType.OrblitGenericArray => VariantType.Array,
                _ => null
            };

        public static MarshalType? ConvertManagedTypeToMarshalType(ITypeSymbol type, TypeCache typeCache)
        {
            var specialType = type.SpecialType;

            switch (specialType)
            {
                case SpecialType.System_Boolean:
                    return MarshalType.Boolean;
                case SpecialType.System_Char:
                    return MarshalType.Char;
                case SpecialType.System_SByte:
                    return MarshalType.SByte;
                case SpecialType.System_Int16:
                    return MarshalType.Int16;
                case SpecialType.System_Int32:
                    return MarshalType.Int32;
                case SpecialType.System_Int64:
                    return MarshalType.Int64;
                case SpecialType.System_Byte:
                    return MarshalType.Byte;
                case SpecialType.System_UInt16:
                    return MarshalType.UInt16;
                case SpecialType.System_UInt32:
                    return MarshalType.UInt32;
                case SpecialType.System_UInt64:
                    return MarshalType.UInt64;
                case SpecialType.System_Single:
                    return MarshalType.Single;
                case SpecialType.System_Double:
                    return MarshalType.Double;
                case SpecialType.System_String:
                    return MarshalType.String;
                default:
                {
                    var typeKind = type.TypeKind;

                    if (typeKind == TypeKind.Enum)
                        return MarshalType.Enum;

                    if (typeKind == TypeKind.Struct)
                    {
                        if (type.ContainingAssembly?.Name == "OrblitSharp" &&
                            type.ContainingNamespace?.Name == "Orblit")
                        {
                            return type switch
                            {
                                { Name: "Vector2" } => MarshalType.Vector2,
                                { Name: "Vector2I" } => MarshalType.Vector2I,
                                { Name: "Rect2" } => MarshalType.Rect2,
                                { Name: "Rect2I" } => MarshalType.Rect2I,
                                { Name: "Transform2D" } => MarshalType.Transform2D,
                                { Name: "Vector3" } => MarshalType.Vector3,
                                { Name: "Vector3I" } => MarshalType.Vector3I,
                                { Name: "Basis" } => MarshalType.Basis,
                                { Name: "Quaternion" } => MarshalType.Quaternion,
                                { Name: "Transform3D" } => MarshalType.Transform3D,
                                { Name: "Vector4" } => MarshalType.Vector4,
                                { Name: "Vector4I" } => MarshalType.Vector4I,
                                { Name: "Projection" } => MarshalType.Projection,
                                { Name: "Aabb" } => MarshalType.Aabb,
                                { Name: "Color" } => MarshalType.Color,
                                { Name: "Plane" } => MarshalType.Plane,
                                { Name: "Rid" } => MarshalType.Rid,
                                { Name: "Callable" } => MarshalType.Callable,
                                { Name: "Signal" } => MarshalType.Signal,
                                { Name: "Variant" } => MarshalType.Variant,
                                _ => null
                            };
                        }
                    }
                    else if (typeKind == TypeKind.Array)
                    {
                        var arrayType = (IArrayTypeSymbol)type;

                        if (arrayType.Rank != 1)
                            return null;

                        var elementType = arrayType.ElementType;

                        switch (elementType.SpecialType)
                        {
                            case SpecialType.System_Byte:
                                return MarshalType.ByteArray;
                            case SpecialType.System_Int32:
                                return MarshalType.Int32Array;
                            case SpecialType.System_Int64:
                                return MarshalType.Int64Array;
                            case SpecialType.System_Single:
                                return MarshalType.Float32Array;
                            case SpecialType.System_Double:
                                return MarshalType.Float64Array;
                            case SpecialType.System_String:
                                return MarshalType.StringArray;
                        }

                        if (elementType.SimpleDerivesFrom(typeCache.OrblitObjectType))
                            return MarshalType.OrblitObjectOrDerivedArray;

                        if (elementType.ContainingAssembly?.Name == "OrblitSharp" &&
                            elementType.ContainingNamespace?.Name == "Orblit")
                        {
                            switch (elementType)
                            {
                                case { Name: "Vector2" }:
                                    return MarshalType.Vector2Array;
                                case { Name: "Vector3" }:
                                    return MarshalType.Vector3Array;
                                case { Name: "Vector4" }:
                                    return MarshalType.Vector4Array;
                                case { Name: "Color" }:
                                    return MarshalType.ColorArray;
                                case { Name: "StringName" }:
                                    return MarshalType.SystemArrayOfStringName;
                                case { Name: "EntityPath" }:
                                    return MarshalType.SystemArrayOfEntityPath;
                                case { Name: "Rid" }:
                                    return MarshalType.SystemArrayOfRid;
                            }
                        }

                        return null;
                    }
                    else
                    {
                        if (type.SimpleDerivesFrom(typeCache.OrblitObjectType))
                            return MarshalType.OrblitObjectOrDerived;

                        if (type.ContainingAssembly?.Name == "OrblitSharp")
                        {
                            switch (type.ContainingNamespace?.Name)
                            {
                                case "Orblit":
                                    return type switch
                                    {
                                        { Name: "StringName" } => MarshalType.StringName,
                                        { Name: "EntityPath" } => MarshalType.EntityPath,
                                        _ => null
                                    };
                                case "Collections"
                                    when type.ContainingNamespace?.FullQualifiedNameOmitGlobal() == "Orblit.Collections":
                                    return type switch
                                    {
                                        { Name: "Dictionary" } =>
                                            type is INamedTypeSymbol { IsGenericType: false }
                                                ? MarshalType.OrblitDictionary
                                                : MarshalType.OrblitGenericDictionary,
                                        { Name: "Array" } =>
                                            type is INamedTypeSymbol { IsGenericType: false }
                                                ? MarshalType.OrblitArray
                                                : MarshalType.OrblitGenericArray,
                                        _ => null
                                    };
                            }
                        }
                    }

                    break;
                }
            }

            return null;
        }

        private static bool SimpleDerivesFrom(this ITypeSymbol? type, ITypeSymbol candidateBaseType)
        {
            while (type != null)
            {
                if (SymbolEqualityComparer.Default.Equals(type, candidateBaseType))
                    return true;

                type = type.BaseType;
            }

            return false;
        }

        public static ITypeSymbol? GetArrayElementType(ITypeSymbol typeSymbol)
        {
            if (typeSymbol.TypeKind == TypeKind.Array)
            {
                var arrayType = (IArrayTypeSymbol)typeSymbol;
                return arrayType.ElementType;
            }

            if (typeSymbol is INamedTypeSymbol { IsGenericType: true } genericType)
                return genericType.TypeArguments.FirstOrDefault();

            return null;
        }

        public static ITypeSymbol[]? GetGenericElementTypes(ITypeSymbol typeSymbol)
        {
            if (typeSymbol is INamedTypeSymbol { IsGenericType: true } genericType)
                return genericType.TypeArguments.ToArray();

            return null;
        }

        private static StringBuilder Append(this StringBuilder source, string a, string b)
            => source.Append(a).Append(b);

        private static StringBuilder Append(this StringBuilder source, string a, string b, string c)
            => source.Append(a).Append(b).Append(c);

        private static StringBuilder Append(this StringBuilder source, string a, string b,
            string c, string d)
            => source.Append(a).Append(b).Append(c).Append(d);

        private static StringBuilder Append(this StringBuilder source, string a, string b,
            string c, string d, string e)
            => source.Append(a).Append(b).Append(c).Append(d).Append(e);

        private static StringBuilder Append(this StringBuilder source, string a, string b,
            string c, string d, string e, string f)
            => source.Append(a).Append(b).Append(c).Append(d).Append(e).Append(f);

        private const string VariantUtils = "global::Orblit.NativeInterop.VariantUtils";

        private static StringBuilder AppendNativeVariantToManagedExprPrePart(this StringBuilder source,
            ITypeSymbol typeSymbol, MarshalType marshalType)
        {
            return marshalType switch
            {
                // We need a special case for OrblitObjectOrDerived[], because it's not supported by VariantUtils.ConvertTo<T>
                MarshalType.OrblitObjectOrDerivedArray =>
                    source.Append(VariantUtils, ".ConvertToSystemArrayOfOrblitObject<",
                        ((IArrayTypeSymbol)typeSymbol).ElementType.FullQualifiedNameIncludeGlobal(), ">("),
                // We need a special case for generic Orblit collections and OrblitObjectOrDerived[], because VariantUtils.ConvertTo<T> is slower
                MarshalType.OrblitGenericDictionary =>
                    source.Append(VariantUtils, ".ConvertToDictionary<",
                        ((INamedTypeSymbol)typeSymbol).TypeArguments[0].FullQualifiedNameIncludeGlobal(), ", ",
                        ((INamedTypeSymbol)typeSymbol).TypeArguments[1].FullQualifiedNameIncludeGlobal(), ">("),
                MarshalType.OrblitGenericArray =>
                    source.Append(VariantUtils, ".ConvertToArray<",
                        ((INamedTypeSymbol)typeSymbol).TypeArguments[0].FullQualifiedNameIncludeGlobal(), ">("),
                _ => source.Append(VariantUtils, ".ConvertTo<",
                    typeSymbol.FullQualifiedNameIncludeGlobal(), ">("),
            };
        }

        private static StringBuilder AppendNativeVariantToManagedExprPostPart(this StringBuilder source)
            => source.Append(")");

        public static StringBuilder AppendNativeVariantToManagedExpr(this StringBuilder source,
            (string firstPart, int secondPart, string thirdPart) inputExpr,
            ITypeSymbol typeSymbol, MarshalType marshalType)
        {
            return source
                .AppendNativeVariantToManagedExprPrePart(typeSymbol, marshalType)
                .Append(inputExpr.firstPart).Append(inputExpr.secondPart).Append(inputExpr.thirdPart)
                .AppendNativeVariantToManagedExprPostPart();
        }

        public static StringBuilder AppendNativeVariantToManagedExpr(this StringBuilder source,
            string inputExpr, ITypeSymbol typeSymbol, MarshalType marshalType)
        {
            return source
                .AppendNativeVariantToManagedExprPrePart(typeSymbol, marshalType)
                .Append(inputExpr)
                .AppendNativeVariantToManagedExprPostPart();
        }

        private static StringBuilder AppendManagedToNativeVariantExprPrePart(this StringBuilder source,
            ITypeSymbol typeSymbol, MarshalType marshalType)
        {
            return marshalType switch
            {
                // We need a special case for OrblitObjectOrDerived[], because it's not supported by VariantUtils.CreateFrom<T>
                MarshalType.OrblitObjectOrDerivedArray =>
                    source.Append(VariantUtils, ".CreateFromSystemArrayOfOrblitObject("),
                // We need a special case for generic Orblit collections and OrblitObjectOrDerived[], because VariantUtils.CreateFrom<T> is slower
                MarshalType.OrblitGenericDictionary =>
                    source.Append(VariantUtils, ".CreateFromDictionary("),
                MarshalType.OrblitGenericArray =>
                    source.Append(VariantUtils, ".CreateFromArray("),
                _ => source.Append(VariantUtils, ".CreateFrom<", typeSymbol.FullQualifiedNameIncludeGlobal(), ">("),
            };
        }

        private static StringBuilder AppendManagedToNativeVariantExprPostPart(this StringBuilder source)
            => source.Append(")");

        public static StringBuilder AppendManagedToNativeVariantExpr(this StringBuilder source,
            string inputExpr, ITypeSymbol typeSymbol, MarshalType marshalType)
        {
            return source
                .AppendManagedToNativeVariantExprPrePart(typeSymbol, marshalType)
                .Append(inputExpr)
                .AppendManagedToNativeVariantExprPostPart();
        }

        private static StringBuilder AppendVariantToManagedExprPostPart(this StringBuilder source,
            ITypeSymbol typeSymbol, MarshalType marshalType)
        {
            return marshalType switch
            {
                // We need a special case for OrblitObjectOrDerived[], because it's not supported by Variant.As<T>
                MarshalType.OrblitObjectOrDerivedArray =>
                    source.Append(".AsOrblitObjectArray<",
                        ((IArrayTypeSymbol)typeSymbol).ElementType.FullQualifiedNameIncludeGlobal(), ">()"),
                // We need a special case for generic Orblit collections and OrblitObjectOrDerived[], because Variant.As<T> is slower
                MarshalType.OrblitGenericDictionary =>
                    source.Append(".AsOrblitDictionary<",
                        ((INamedTypeSymbol)typeSymbol).TypeArguments[0].FullQualifiedNameIncludeGlobal(), ", ",
                        ((INamedTypeSymbol)typeSymbol).TypeArguments[1].FullQualifiedNameIncludeGlobal(), ">()"),
                MarshalType.OrblitGenericArray =>
                    source.Append(".AsOrblitArray<",
                        ((INamedTypeSymbol)typeSymbol).TypeArguments[0].FullQualifiedNameIncludeGlobal(), ">()"),
                _ => source.Append(".As<", typeSymbol.FullQualifiedNameIncludeGlobal(), ">()")
            };
        }

        public static StringBuilder AppendVariantToManagedExpr(this StringBuilder source,
            (string firstPart, string secondPart) inputExpr, ITypeSymbol typeSymbol, MarshalType marshalType)
        {
            return source
                .Append(inputExpr.firstPart, inputExpr.secondPart)
                .AppendVariantToManagedExprPostPart(typeSymbol, marshalType);
        }

        private static StringBuilder AppendManagedToVariantExprPrePart(this StringBuilder source,
            ITypeSymbol typeSymbol, MarshalType marshalType)
        {
            return marshalType switch
            {
                // We need a special case for OrblitObjectOrDerived[], because it's not supported by Variant.From<T>
                MarshalType.OrblitObjectOrDerivedArray =>
                    source.Append("global::Orblit.Variant.CreateFrom("),
                // We need a special case for generic Orblit collections, because Variant.From<T> is slower
                MarshalType.OrblitGenericDictionary or MarshalType.OrblitGenericArray =>
                    source.Append("global::Orblit.Variant.CreateFrom("),
                _ => source.Append("global::Orblit.Variant.From<",
                    typeSymbol.FullQualifiedNameIncludeGlobal(), ">(")
            };
        }

        private static StringBuilder AppendManagedToVariantExprPostPart(this StringBuilder source)
            => source.Append(")");

        public static StringBuilder AppendManagedToVariantExpr(this StringBuilder source,
            string inputExpr, ITypeSymbol typeSymbol, MarshalType marshalType)
        {
            return source
                .AppendManagedToVariantExprPrePart(typeSymbol, marshalType)
                .Append(inputExpr)
                .AppendManagedToVariantExprPostPart();
        }

        public static StringBuilder AppendManagedToVariantExpr(this StringBuilder source,
            (string firstPart, string secondPart) inputExpr, ITypeSymbol typeSymbol, MarshalType marshalType)
        {
            return source
                .AppendManagedToVariantExprPrePart(typeSymbol, marshalType)
                .Append(inputExpr.firstPart, inputExpr.secondPart)
                .AppendManagedToVariantExprPostPart();
        }
    }
}
