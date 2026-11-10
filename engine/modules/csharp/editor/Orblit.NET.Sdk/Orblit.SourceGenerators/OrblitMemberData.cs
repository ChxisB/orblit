using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Orblit.SourceGenerators
{
    public readonly struct OrblitMethodData
    {
        public OrblitMethodData(IMethodSymbol method, ImmutableArray<MarshalType> paramTypes,
            ImmutableArray<ITypeSymbol> paramTypeSymbols, (MarshalType MarshalType, ITypeSymbol TypeSymbol)? retType)
        {
            Method = method;
            ParamTypes = paramTypes;
            ParamTypeSymbols = paramTypeSymbols;
            RetType = retType;
        }

        public IMethodSymbol Method { get; }
        public ImmutableArray<MarshalType> ParamTypes { get; }
        public ImmutableArray<ITypeSymbol> ParamTypeSymbols { get; }
        public (MarshalType MarshalType, ITypeSymbol TypeSymbol)? RetType { get; }
    }

    public readonly struct OrblitSignalDelegateData
    {
        public OrblitSignalDelegateData(string name, INamedTypeSymbol delegateSymbol, OrblitMethodData invokeMethodData)
        {
            Name = name;
            DelegateSymbol = delegateSymbol;
            InvokeMethodData = invokeMethodData;
        }

        public string Name { get; }
        public INamedTypeSymbol DelegateSymbol { get; }
        public OrblitMethodData InvokeMethodData { get; }
    }

    public readonly struct OrblitPropertyData
    {
        public OrblitPropertyData(IPropertySymbol propertySymbol, MarshalType type)
        {
            PropertySymbol = propertySymbol;
            Type = type;
        }

        public IPropertySymbol PropertySymbol { get; }
        public MarshalType Type { get; }

        public bool IsReadOnly => PropertySymbol.IsReadOnly ||
                                  PropertySymbol.SetMethodOrBaseSetMethod() is { IsInitOnly: true };
        public bool IsWriteOnly => PropertySymbol.IsWriteOnly;
    }

    public readonly struct OrblitFieldData
    {
        public OrblitFieldData(IFieldSymbol fieldSymbol, MarshalType type)
        {
            FieldSymbol = fieldSymbol;
            Type = type;
        }

        public IFieldSymbol FieldSymbol { get; }
        public MarshalType Type { get; }

        public bool IsReadOnly => FieldSymbol.IsReadOnly;
    }

    public struct OrblitPropertyOrFieldData
    {
        public OrblitPropertyOrFieldData(ISymbol symbol, MarshalType type)
        {
            Symbol = symbol;
            Type = type;
        }

        public OrblitPropertyOrFieldData(OrblitPropertyData propertyData)
            : this(propertyData.PropertySymbol, propertyData.Type)
        {
        }

        public OrblitPropertyOrFieldData(OrblitFieldData fieldData)
            : this(fieldData.FieldSymbol, fieldData.Type)
        {
        }

        public ISymbol Symbol { get; }
        public MarshalType Type { get; }
    }
}
