// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Interop
{
    /// <summary>
    /// Type used to pass on default marshalling details.
    /// </summary>
    /// <remarks>
    /// This type used to pass default marshalling details to the various marshalling info parsers.
    /// Since it contains a <see cref="INamedTypeSymbol"/>, it should not be used as a field on any types
    /// derived from <see cref="MarshallingInfo"/>. See remarks on <see cref="MarshallingInfo"/>.
    /// </remarks>
    public sealed record DefaultMarshallingInfo(
        CharEncoding CharEncoding,
        INamedTypeSymbol? StringMarshallingCustomType
    );

    // The following types are modeled to fit with the current prospective spec
    // for C# vNext discriminated unions. Once discriminated unions are released,
    // these should be updated to be implemented as a discriminated union.

    /// <summary>
    /// Base type for marshalling information
    /// </summary>
    /// <remarks>
    /// Types derived from this are used to represent the stub information calculated from the semantic model.
    /// To support incremental generation, they must not include any types derived from <see cref="ISymbol"/>.
    /// </remarks>
    public abstract record MarshallingInfo
    {
        protected MarshallingInfo()
        { }

        public virtual IEnumerable<TypePositionInfo> ElementDependencies => [];
    }

    /// <summary>
    /// No marshalling information exists for the type.
    /// </summary>
    public sealed record NoMarshallingInfo : MarshallingInfo
    {
        public static readonly MarshallingInfo Instance = new NoMarshallingInfo();

        private NoMarshallingInfo() { }
    }

    /// <summary>
    /// Character encoding enumeration.
    /// </summary>
    public enum CharEncoding
    {
        Undefined,
        Utf8,
        Utf16,
        Custom
    }

    /// <summary>
    /// Details that are required when scenario supports strings.
    /// </summary>
    public record MarshallingInfoStringSupport(
        CharEncoding CharEncoding
    ) : MarshallingInfo;

    /// <summary>
    /// The provided type was determined to be an "unmanaged" type that can be passed as-is to native code.
    /// </summary>
    /// <param name="IsStrictlyBlittable">Indicates if the type is blittable as defined by the built-in .NET marshallers.</param>
    public sealed record UnmanagedBlittableMarshallingInfo(
        bool IsStrictlyBlittable
    ) : MarshallingInfo;

    public abstract record CountInfo
    {
        private protected CountInfo() { }
    }

    public sealed record NoCountInfo : CountInfo
    {
        public static readonly NoCountInfo Instance = new NoCountInfo();

        private NoCountInfo() { }
    }

    public sealed record ConstSizeCountInfo(int Size) : CountInfo;

    public sealed record CountElementCountInfo(TypePositionInfo ElementInfo) : CountInfo
    {
        public const string ReturnValueElementName = "return-value";
    }

    public sealed record SizeAndParamIndexInfo(int ConstSize, TypePositionInfo? ParamAtIndex) : CountInfo
    {
        public const int UnspecifiedConstSize = -1;

        public const TypePositionInfo UnspecifiedParam = null;

        public static readonly SizeAndParamIndexInfo Unspecified = new(UnspecifiedConstSize, UnspecifiedParam);
    }

    /// <summary>
    /// Custom type marshalling via MarshalUsingAttribute or NativeMarshallingAttribute
    /// </summary>
    public record NativeMarshallingAttributeInfo(
        ManagedTypeInfo EntryPointType,
        CustomTypeMarshallers Marshallers) : MarshallingInfo;

    /// <summary>
    /// Custom type marshalling via MarshalUsingAttribute or NativeMarshallingAttribute for a linear collection
    /// </summary>
    public sealed record NativeLinearCollectionMarshallingInfo(
        ManagedTypeInfo EntryPointType,
        CustomTypeMarshallers Marshallers,
        CountInfo ElementCountInfo,
        ManagedTypeInfo PlaceholderTypeParameter) : NativeMarshallingAttributeInfo(
            EntryPointType,
            Marshallers)
    {
        public override IEnumerable<TypePositionInfo> ElementDependencies
        {
            get
            {
                return field ??= GetElementDependencies().ToImmutableArray();

                IEnumerable<TypePositionInfo> GetElementDependencies()
                {
                    if (ElementCountInfo is CountElementCountInfo { ElementInfo: TypePositionInfo nestedCountElement })
                    {
                        // Do not include dependent elements with no managed or native index.
                        // These values are dummy values that are inserted earlier to avoid emitting extra diagnostics.
                        if (nestedCountElement.ManagedIndex != TypePositionInfo.UnsetIndex || nestedCountElement.NativeIndex != TypePositionInfo.UnsetIndex)
                        {
                            yield return nestedCountElement;
                        }
                    }

                    foreach (KeyValuePair<MarshalMode, CustomTypeMarshallerData> mode in Marshallers.Modes)
                    {
                        foreach (TypePositionInfo nestedElement in mode.Value.CollectionElementMarshallingInfo.ElementDependencies)
                        {
                            if (nestedElement.ManagedIndex != TypePositionInfo.UnsetIndex || nestedElement.NativeIndex != TypePositionInfo.UnsetIndex)
                            {
                                yield return nestedElement;
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Marshal an exception based on the same rules as the built-in COM system based on the unmanaged type of the native return marshaller.
    /// </summary>
    public sealed record ComExceptionMarshalling : MarshallingInfo
    {
        internal static MarshallingInfo CreateSpecificMarshallingInfo(ManagedTypeInfo unmanagedReturnType)
        {
            return (unmanagedReturnType as SpecialTypeInfo)?.SpecialType switch
            {
                SpecialType.System_Void => CreateWellKnownComExceptionMarshallingData(TypeNames.ExceptionAsVoidMarshaller, unmanagedReturnType),
                SpecialType.System_Int32 => CreateWellKnownComExceptionMarshallingData($"{TypeNames.ExceptionAsHResultMarshaller}<int>", unmanagedReturnType),
                SpecialType.System_UInt32 => CreateWellKnownComExceptionMarshallingData($"{TypeNames.ExceptionAsHResultMarshaller}<uint>", unmanagedReturnType),
                SpecialType.System_Single => CreateWellKnownComExceptionMarshallingData($"{TypeNames.ExceptionAsNaNMarshaller}<float>", unmanagedReturnType),
                SpecialType.System_Double => CreateWellKnownComExceptionMarshallingData($"{TypeNames.ExceptionAsNaNMarshaller}<double>", unmanagedReturnType),
                _ => CreateWellKnownComExceptionMarshallingData($"{TypeNames.ExceptionAsDefaultMarshaller}<{MarshallerHelpers.GetCompatibleGenericTypeParameterSyntax(SyntaxFactory.ParseTypeName(unmanagedReturnType.FullTypeName))}>", unmanagedReturnType),
            };

            static NativeMarshallingAttributeInfo CreateWellKnownComExceptionMarshallingData(string marshallerName, ManagedTypeInfo unmanagedType)
            {
                ManagedTypeInfo marshallerTypeInfo = new ReferenceTypeInfo(TypeNames.GlobalAlias + marshallerName, marshallerName);
                return new NativeMarshallingAttributeInfo(marshallerTypeInfo,
                    new CustomTypeMarshallers(ImmutableDictionary<MarshalMode, CustomTypeMarshallerData>.Empty.Add(
                        MarshalMode.UnmanagedToManagedOut,
                        new CustomTypeMarshallerData(
                            marshallerTypeInfo,
                            unmanagedType,
                            HasState: false,
                            MarshallerShape.ToUnmanaged,
                            IsStrictlyBlittable: true,
                            BufferElementType: null,
                            CollectionElementType: null,
                            CollectionElementMarshallingInfo: null
                            ))));
            }
        }
    }
}
