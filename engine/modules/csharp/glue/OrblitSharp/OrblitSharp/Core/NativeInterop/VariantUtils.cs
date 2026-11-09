#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable IDE1006 // Naming rule violation
// ReSharper disable InconsistentNaming

using System;
using System.Runtime.CompilerServices;
using Orblit.Collections;


#nullable enable

namespace Orblit.NativeInterop
{
    public static partial class VariantUtils
    {
        public static orblit_variant CreateFromRid(Rid from)
            => new() { Type = Variant.Type.Rid, Rid = from };

        public static orblit_variant CreateFromBool(bool from)
            => new() { Type = Variant.Type.Bool, Bool = from.ToOrblitBool() };

        public static orblit_variant CreateFromInt(long from)
            => new() { Type = Variant.Type.Int, Int = from };

        public static orblit_variant CreateFromInt(ulong from)
            => new() { Type = Variant.Type.Int, Int = (long)from };

        public static orblit_variant CreateFromFloat(double from)
            => new() { Type = Variant.Type.Float, Float = from };

        public static orblit_variant CreateFromVector2(Vector2 from)
            => new() { Type = Variant.Type.Vector2, Vector2 = from };

        public static orblit_variant CreateFromVector2I(Vector2I from)
            => new() { Type = Variant.Type.Vector2I, Vector2I = from };

        public static orblit_variant CreateFromVector3(Vector3 from)
            => new() { Type = Variant.Type.Vector3, Vector3 = from };

        public static orblit_variant CreateFromVector3I(Vector3I from)
            => new() { Type = Variant.Type.Vector3I, Vector3I = from };

        public static orblit_variant CreateFromVector4(Vector4 from)
            => new() { Type = Variant.Type.Vector4, Vector4 = from };

        public static orblit_variant CreateFromVector4I(Vector4I from)
            => new() { Type = Variant.Type.Vector4I, Vector4I = from };

        public static orblit_variant CreateFromRect2(Rect2 from)
            => new() { Type = Variant.Type.Rect2, Rect2 = from };

        public static orblit_variant CreateFromRect2I(Rect2I from)
            => new() { Type = Variant.Type.Rect2I, Rect2I = from };

        public static orblit_variant CreateFromQuaternion(Quaternion from)
            => new() { Type = Variant.Type.Quaternion, Quaternion = from };

        public static orblit_variant CreateFromColor(Color from)
            => new() { Type = Variant.Type.Color, Color = from };

        public static orblit_variant CreateFromPlane(Plane from)
            => new() { Type = Variant.Type.Plane, Plane = from };

        public static orblit_variant CreateFromTransform2D(Transform2D from)
        {
            NativeFuncs.orblitsharp_variant_new_transform2d(out orblit_variant ret, from);
            return ret;
        }

        public static orblit_variant CreateFromBasis(Basis from)
        {
            NativeFuncs.orblitsharp_variant_new_basis(out orblit_variant ret, from);
            return ret;
        }

        public static orblit_variant CreateFromTransform3D(Transform3D from)
        {
            NativeFuncs.orblitsharp_variant_new_transform3d(out orblit_variant ret, from);
            return ret;
        }

        public static orblit_variant CreateFromProjection(Projection from)
        {
            NativeFuncs.orblitsharp_variant_new_projection(out orblit_variant ret, from);
            return ret;
        }

        public static orblit_variant CreateFromAabb(Aabb from)
        {
            NativeFuncs.orblitsharp_variant_new_aabb(out orblit_variant ret, from);
            return ret;
        }

        // Explicit name to make it very clear
        public static orblit_variant CreateFromCallableTakingOwnershipOfDisposableValue(orblit_callable from)
            => new() { Type = Variant.Type.Callable, Callable = from };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromCallable(Callable from)
            => CreateFromCallableTakingOwnershipOfDisposableValue(
                Marshaling.ConvertCallableToNative(from));

        // Explicit name to make it very clear
        public static orblit_variant CreateFromSignalTakingOwnershipOfDisposableValue(orblit_signal from)
            => new() { Type = Variant.Type.Signal, Signal = from };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromSignal(Signal from)
            => CreateFromSignalTakingOwnershipOfDisposableValue(
                Marshaling.ConvertSignalToNative(from));

        // Explicit name to make it very clear
        public static orblit_variant CreateFromStringTakingOwnershipOfDisposableValue(orblit_string from)
            => new() { Type = Variant.Type.String, String = from };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromString(string? from)
            => CreateFromStringTakingOwnershipOfDisposableValue(Marshaling.ConvertStringToNative(from));

        public static orblit_variant CreateFromPackedByteArray(scoped in orblit_packed_byte_array from)
        {
            NativeFuncs.orblitsharp_variant_new_packed_byte_array(out orblit_variant ret, from);
            return ret;
        }

        public static orblit_variant CreateFromPackedInt32Array(scoped in orblit_packed_int32_array from)
        {
            NativeFuncs.orblitsharp_variant_new_packed_int32_array(out orblit_variant ret, from);
            return ret;
        }

        public static orblit_variant CreateFromPackedInt64Array(scoped in orblit_packed_int64_array from)
        {
            NativeFuncs.orblitsharp_variant_new_packed_int64_array(out orblit_variant ret, from);
            return ret;
        }

        public static orblit_variant CreateFromPackedFloat32Array(scoped in orblit_packed_float32_array from)
        {
            NativeFuncs.orblitsharp_variant_new_packed_float32_array(out orblit_variant ret, from);
            return ret;
        }

        public static orblit_variant CreateFromPackedFloat64Array(scoped in orblit_packed_float64_array from)
        {
            NativeFuncs.orblitsharp_variant_new_packed_float64_array(out orblit_variant ret, from);
            return ret;
        }

        public static orblit_variant CreateFromPackedStringArray(scoped in orblit_packed_string_array from)
        {
            NativeFuncs.orblitsharp_variant_new_packed_string_array(out orblit_variant ret, from);
            return ret;
        }

        public static orblit_variant CreateFromPackedVector2Array(scoped in orblit_packed_vector2_array from)
        {
            NativeFuncs.orblitsharp_variant_new_packed_vector2_array(out orblit_variant ret, from);
            return ret;
        }

        public static orblit_variant CreateFromPackedVector3Array(scoped in orblit_packed_vector3_array from)
        {
            NativeFuncs.orblitsharp_variant_new_packed_vector3_array(out orblit_variant ret, from);
            return ret;
        }

        public static orblit_variant CreateFromPackedVector4Array(scoped in orblit_packed_vector4_array from)
        {
            NativeFuncs.orblitsharp_variant_new_packed_vector4_array(out orblit_variant ret, from);
            return ret;
        }

        public static orblit_variant CreateFromPackedColorArray(scoped in orblit_packed_color_array from)
        {
            NativeFuncs.orblitsharp_variant_new_packed_color_array(out orblit_variant ret, from);
            return ret;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromPackedByteArray(scoped Span<byte> from)
        {
            using var nativePackedArray = Marshaling.ConvertSystemArrayToNativePackedByteArray(from);
            return CreateFromPackedByteArray(nativePackedArray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromPackedInt32Array(scoped Span<int> from)
        {
            using var nativePackedArray = Marshaling.ConvertSystemArrayToNativePackedInt32Array(from);
            return CreateFromPackedInt32Array(nativePackedArray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromPackedInt64Array(scoped Span<long> from)
        {
            using var nativePackedArray = Marshaling.ConvertSystemArrayToNativePackedInt64Array(from);
            return CreateFromPackedInt64Array(nativePackedArray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromPackedFloat32Array(scoped Span<float> from)
        {
            using var nativePackedArray = Marshaling.ConvertSystemArrayToNativePackedFloat32Array(from);
            return CreateFromPackedFloat32Array(nativePackedArray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromPackedFloat64Array(scoped Span<double> from)
        {
            using var nativePackedArray = Marshaling.ConvertSystemArrayToNativePackedFloat64Array(from);
            return CreateFromPackedFloat64Array(nativePackedArray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromPackedStringArray(scoped Span<string> from)
        {
            using var nativePackedArray = Marshaling.ConvertSystemArrayToNativePackedStringArray(from);
            return CreateFromPackedStringArray(nativePackedArray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromPackedVector2Array(scoped Span<Vector2> from)
        {
            using var nativePackedArray = Marshaling.ConvertSystemArrayToNativePackedVector2Array(from);
            return CreateFromPackedVector2Array(nativePackedArray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromPackedVector3Array(scoped Span<Vector3> from)
        {
            using var nativePackedArray = Marshaling.ConvertSystemArrayToNativePackedVector3Array(from);
            return CreateFromPackedVector3Array(nativePackedArray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromPackedVector4Array(scoped Span<Vector4> from)
        {
            using var nativePackedArray = Marshaling.ConvertSystemArrayToNativePackedVector4Array(from);
            return CreateFromPackedVector4Array(nativePackedArray);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromPackedColorArray(scoped Span<Color> from)
        {
            using var nativePackedArray = Marshaling.ConvertSystemArrayToNativePackedColorArray(from);
            return CreateFromPackedColorArray(nativePackedArray);
        }

        public static orblit_variant CreateFromSystemArrayOfStringName(scoped Span<StringName> from)
        {
#pragma warning disable CA2265 // Comparing a span to 'null' might be redundant.
            if (from == null)
                return default;
#pragma warning restore CA2265
            using var fromOrblit = new Collections.Array(from);
            return CreateFromArray((orblit_array)fromOrblit.NativeValue);
        }

        public static orblit_variant CreateFromSystemArrayOfEntityPath(scoped Span<EntityPath> from)
        {
#pragma warning disable CA2265 // Comparing a span to 'null' might be redundant.
            if (from == null)
                return default;
#pragma warning restore CA2265
            using var fromOrblit = new Collections.Array(from);
            return CreateFromArray((orblit_array)fromOrblit.NativeValue);
        }

        public static orblit_variant CreateFromSystemArrayOfRid(scoped Span<Rid> from)
        {
#pragma warning disable CA2265 // Comparing a span to 'null' might be redundant.
            if (from == null)
                return default;
#pragma warning restore CA2265
            using var fromOrblit = new Collections.Array(from);
            return CreateFromArray((orblit_array)fromOrblit.NativeValue);
        }

        public static orblit_variant CreateFromSystemArrayOfOrblitObject(OrblitObject[]? from)
        {
            if (from == null)
                return default; // Nil
            using var fromOrblit = new Collections.Array(from);
            return CreateFromArray((orblit_array)fromOrblit.NativeValue);
        }

        public static orblit_variant CreateFromArray(scoped in orblit_array from)
        {
            NativeFuncs.orblitsharp_variant_new_array(out orblit_variant ret, from);
            return ret;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromArray(Collections.Array? from)
            => from != null ? CreateFromArray((orblit_array)from.NativeValue) : default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromArray<[MustBeVariant] T>(Array<T>? from)
            => from != null ? CreateFromArray((orblit_array)((Collections.Array)from).NativeValue) : default;

        public static orblit_variant CreateFromDictionary(scoped in orblit_dictionary from)
        {
            NativeFuncs.orblitsharp_variant_new_dictionary(out orblit_variant ret, from);
            return ret;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromDictionary(Dictionary? from)
            => from != null ? CreateFromDictionary((orblit_dictionary)from.NativeValue) : default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromDictionary<[MustBeVariant] TKey, [MustBeVariant] TValue>(Dictionary<TKey, TValue>? from)
            => from != null ? CreateFromDictionary((orblit_dictionary)((Dictionary)from).NativeValue) : default;

        public static orblit_variant CreateFromStringName(scoped in orblit_string_name from)
        {
            NativeFuncs.orblitsharp_variant_new_string_name(out orblit_variant ret, from);
            return ret;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromStringName(StringName? from)
            => from != null ? CreateFromStringName((orblit_string_name)from.NativeValue) : default;

        public static orblit_variant CreateFromEntityPath(scoped in orblit_node_path from)
        {
            NativeFuncs.orblitsharp_variant_new_node_path(out orblit_variant ret, from);
            return ret;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromEntityPath(EntityPath? from)
            => from != null ? CreateFromEntityPath((orblit_node_path)from.NativeValue) : default;

        public static orblit_variant CreateFromOrblitObjectPtr(IntPtr from)
        {
            if (from == IntPtr.Zero)
                return new orblit_variant();
            NativeFuncs.orblitsharp_variant_new_object(out orblit_variant ret, from);
            return ret;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_variant CreateFromOrblitObject(OrblitObject? from)
            => from != null ? CreateFromOrblitObjectPtr(OrblitObject.GetPtr(from)) : default;

        // We avoid the internal call if the stored type is the same we want.

        public static bool ConvertToBool(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Bool ?
                p_var.Bool.ToBool() :
                NativeFuncs.orblitsharp_variant_as_bool(p_var).ToBool();

        public static char ConvertToChar(in orblit_variant p_var)
            => (char)(p_var.Type == Variant.Type.Int ?
                p_var.Int :
                NativeFuncs.orblitsharp_variant_as_int(p_var));

        public static sbyte ConvertToInt8(in orblit_variant p_var)
            => (sbyte)(p_var.Type == Variant.Type.Int ?
                p_var.Int :
                NativeFuncs.orblitsharp_variant_as_int(p_var));

        public static short ConvertToInt16(in orblit_variant p_var)
            => (short)(p_var.Type == Variant.Type.Int ?
                p_var.Int :
                NativeFuncs.orblitsharp_variant_as_int(p_var));

        public static int ConvertToInt32(in orblit_variant p_var)
            => (int)(p_var.Type == Variant.Type.Int ?
                p_var.Int :
                NativeFuncs.orblitsharp_variant_as_int(p_var));

        public static long ConvertToInt64(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Int ? p_var.Int : NativeFuncs.orblitsharp_variant_as_int(p_var);

        public static byte ConvertToUInt8(in orblit_variant p_var)
            => (byte)(p_var.Type == Variant.Type.Int ?
                p_var.Int :
                NativeFuncs.orblitsharp_variant_as_int(p_var));

        public static ushort ConvertToUInt16(in orblit_variant p_var)
            => (ushort)(p_var.Type == Variant.Type.Int ?
                p_var.Int :
                NativeFuncs.orblitsharp_variant_as_int(p_var));

        public static uint ConvertToUInt32(in orblit_variant p_var)
            => (uint)(p_var.Type == Variant.Type.Int ?
                p_var.Int :
                NativeFuncs.orblitsharp_variant_as_int(p_var));

        public static ulong ConvertToUInt64(in orblit_variant p_var)
            => (ulong)(p_var.Type == Variant.Type.Int ?
                p_var.Int :
                NativeFuncs.orblitsharp_variant_as_int(p_var));

        public static float ConvertToFloat32(in orblit_variant p_var)
            => (float)(p_var.Type == Variant.Type.Float ?
                p_var.Float :
                NativeFuncs.orblitsharp_variant_as_float(p_var));

        public static double ConvertToFloat64(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Float ?
                p_var.Float :
                NativeFuncs.orblitsharp_variant_as_float(p_var);

        public static Vector2 ConvertToVector2(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Vector2 ?
                p_var.Vector2 :
                NativeFuncs.orblitsharp_variant_as_vector2(p_var);

        public static Vector2I ConvertToVector2I(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Vector2I ?
                p_var.Vector2I :
                NativeFuncs.orblitsharp_variant_as_vector2i(p_var);

        public static Rect2 ConvertToRect2(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Rect2 ?
                p_var.Rect2 :
                NativeFuncs.orblitsharp_variant_as_rect2(p_var);

        public static Rect2I ConvertToRect2I(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Rect2I ?
                p_var.Rect2I :
                NativeFuncs.orblitsharp_variant_as_rect2i(p_var);

        public static unsafe Transform2D ConvertToTransform2D(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Transform2D ?
                *p_var.Transform2D :
                NativeFuncs.orblitsharp_variant_as_transform2d(p_var);

        public static Vector3 ConvertToVector3(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Vector3 ?
                p_var.Vector3 :
                NativeFuncs.orblitsharp_variant_as_vector3(p_var);

        public static Vector3I ConvertToVector3I(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Vector3I ?
                p_var.Vector3I :
                NativeFuncs.orblitsharp_variant_as_vector3i(p_var);

        public static unsafe Vector4 ConvertToVector4(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Vector4 ?
                p_var.Vector4 :
                NativeFuncs.orblitsharp_variant_as_vector4(p_var);

        public static unsafe Vector4I ConvertToVector4I(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Vector4I ?
                p_var.Vector4I :
                NativeFuncs.orblitsharp_variant_as_vector4i(p_var);

        public static unsafe Basis ConvertToBasis(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Basis ?
                *p_var.Basis :
                NativeFuncs.orblitsharp_variant_as_basis(p_var);

        public static Quaternion ConvertToQuaternion(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Quaternion ?
                p_var.Quaternion :
                NativeFuncs.orblitsharp_variant_as_quaternion(p_var);

        public static unsafe Transform3D ConvertToTransform3D(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Transform3D ?
                *p_var.Transform3D :
                NativeFuncs.orblitsharp_variant_as_transform3d(p_var);

        public static unsafe Projection ConvertToProjection(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Projection ?
                *p_var.Projection :
                NativeFuncs.orblitsharp_variant_as_projection(p_var);

        public static unsafe Aabb ConvertToAabb(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Aabb ?
                *p_var.Aabb :
                NativeFuncs.orblitsharp_variant_as_aabb(p_var);

        public static Color ConvertToColor(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Color ?
                p_var.Color :
                NativeFuncs.orblitsharp_variant_as_color(p_var);

        public static Plane ConvertToPlane(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Plane ?
                p_var.Plane :
                NativeFuncs.orblitsharp_variant_as_plane(p_var);

        public static Rid ConvertToRid(in orblit_variant p_var)
            => p_var.Type == Variant.Type.Rid ?
                p_var.Rid :
                NativeFuncs.orblitsharp_variant_as_rid(p_var);

        public static IntPtr ConvertToOrblitObjectPtr(in orblit_variant p_var)
        {
            if (p_var.Type != Variant.Type.Object || p_var.ObjectId == 0)
            {
                return IntPtr.Zero;
            }

            return NativeFuncs.orblitsharp_instance_from_id(p_var.ObjectId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static OrblitObject ConvertToOrblitObject(in orblit_variant p_var)
            => InteropUtils.UnmanagedGetManaged(ConvertToOrblitObjectPtr(p_var));

        public static string ConvertToString(in orblit_variant p_var)
        {
            switch (p_var.Type)
            {
                case Variant.Type.Nil:
                    return ""; // Otherwise, Variant -> String would return the string "Null"
                case Variant.Type.String:
                {
                    // We avoid the internal call if the stored type is the same we want.
                    return Marshaling.ConvertStringToManaged(p_var.String);
                }
                default:
                {
                    using orblit_string orblitString = NativeFuncs.orblitsharp_variant_as_string(p_var);
                    return Marshaling.ConvertStringToManaged(orblitString);
                }
            }
        }

        public static orblit_string_name ConvertToNativeStringName(scoped in orblit_variant p_var)
            => p_var.Type == Variant.Type.StringName ?
                NativeFuncs.orblitsharp_string_name_new_copy(p_var.StringName) :
                NativeFuncs.orblitsharp_variant_as_string_name(p_var);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StringName ConvertToStringName(in orblit_variant p_var)
            => StringName.CreateTakingOwnershipOfDisposableValue(ConvertToNativeStringName(p_var));

        public static orblit_node_path ConvertToNativeEntityPath(scoped in orblit_variant p_var)
            => p_var.Type == Variant.Type.EntityPath ?
                NativeFuncs.orblitsharp_node_path_new_copy(p_var.EntityPath) :
                NativeFuncs.orblitsharp_variant_as_node_path(p_var);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static EntityPath ConvertToEntityPath(in orblit_variant p_var)
            => EntityPath.CreateTakingOwnershipOfDisposableValue(ConvertToNativeEntityPath(p_var));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_callable ConvertToNativeCallable(scoped in orblit_variant p_var)
            => NativeFuncs.orblitsharp_variant_as_callable(p_var);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Callable ConvertToCallable(in orblit_variant p_var)
        {
            using var callable = ConvertToNativeCallable(p_var);
            return Marshaling.ConvertCallableToManaged(callable);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static orblit_signal ConvertToNativeSignal(scoped in orblit_variant p_var)
            => NativeFuncs.orblitsharp_variant_as_signal(p_var);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Signal ConvertToSignal(in orblit_variant p_var)
        {
            using var signal = ConvertToNativeSignal(p_var);
            return Marshaling.ConvertSignalToManaged(signal);
        }

        public static orblit_array ConvertToNativeArray(scoped in orblit_variant p_var)
            => p_var.Type == Variant.Type.Array ?
                NativeFuncs.orblitsharp_array_new_copy(p_var.Array) :
                NativeFuncs.orblitsharp_variant_as_array(p_var);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Collections.Array ConvertToArray(in orblit_variant p_var)
            => Collections.Array.CreateTakingOwnershipOfDisposableValue(ConvertToNativeArray(p_var));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Array<T> ConvertToArray<[MustBeVariant] T>(in orblit_variant p_var)
            => Array<T>.CreateTakingOwnershipOfDisposableValue(ConvertToNativeArray(p_var));

        public static orblit_dictionary ConvertToNativeDictionary(scoped in orblit_variant p_var)
            => p_var.Type == Variant.Type.Dictionary ?
                NativeFuncs.orblitsharp_dictionary_new_copy(p_var.Dictionary) :
                NativeFuncs.orblitsharp_variant_as_dictionary(p_var);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dictionary ConvertToDictionary(in orblit_variant p_var)
            => Dictionary.CreateTakingOwnershipOfDisposableValue(ConvertToNativeDictionary(p_var));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Dictionary<TKey, TValue> ConvertToDictionary<[MustBeVariant] TKey, [MustBeVariant] TValue>(in orblit_variant p_var)
            => Dictionary<TKey, TValue>.CreateTakingOwnershipOfDisposableValue(ConvertToNativeDictionary(p_var));

        public static byte[] ConvertAsPackedByteArrayToSystemArray(in orblit_variant p_var)
        {
            using var packedArray = NativeFuncs.orblitsharp_variant_as_packed_byte_array(p_var);
            return Marshaling.ConvertNativePackedByteArrayToSystemArray(packedArray);
        }

        public static int[] ConvertAsPackedInt32ArrayToSystemArray(in orblit_variant p_var)
        {
            using var packedArray = NativeFuncs.orblitsharp_variant_as_packed_int32_array(p_var);
            return Marshaling.ConvertNativePackedInt32ArrayToSystemArray(packedArray);
        }

        public static long[] ConvertAsPackedInt64ArrayToSystemArray(in orblit_variant p_var)
        {
            using var packedArray = NativeFuncs.orblitsharp_variant_as_packed_int64_array(p_var);
            return Marshaling.ConvertNativePackedInt64ArrayToSystemArray(packedArray);
        }

        public static float[] ConvertAsPackedFloat32ArrayToSystemArray(in orblit_variant p_var)
        {
            using var packedArray = NativeFuncs.orblitsharp_variant_as_packed_float32_array(p_var);
            return Marshaling.ConvertNativePackedFloat32ArrayToSystemArray(packedArray);
        }

        public static double[] ConvertAsPackedFloat64ArrayToSystemArray(in orblit_variant p_var)
        {
            using var packedArray = NativeFuncs.orblitsharp_variant_as_packed_float64_array(p_var);
            return Marshaling.ConvertNativePackedFloat64ArrayToSystemArray(packedArray);
        }

        public static string[] ConvertAsPackedStringArrayToSystemArray(in orblit_variant p_var)
        {
            using var packedArray = NativeFuncs.orblitsharp_variant_as_packed_string_array(p_var);
            return Marshaling.ConvertNativePackedStringArrayToSystemArray(packedArray);
        }

        public static Vector2[] ConvertAsPackedVector2ArrayToSystemArray(in orblit_variant p_var)
        {
            using var packedArray = NativeFuncs.orblitsharp_variant_as_packed_vector2_array(p_var);
            return Marshaling.ConvertNativePackedVector2ArrayToSystemArray(packedArray);
        }

        public static Vector3[] ConvertAsPackedVector3ArrayToSystemArray(in orblit_variant p_var)
        {
            using var packedArray = NativeFuncs.orblitsharp_variant_as_packed_vector3_array(p_var);
            return Marshaling.ConvertNativePackedVector3ArrayToSystemArray(packedArray);
        }

        public static Vector4[] ConvertAsPackedVector4ArrayToSystemArray(in orblit_variant p_var)
        {
            using var packedArray = NativeFuncs.orblitsharp_variant_as_packed_vector4_array(p_var);
            return Marshaling.ConvertNativePackedVector4ArrayToSystemArray(packedArray);
        }

        public static Color[] ConvertAsPackedColorArrayToSystemArray(in orblit_variant p_var)
        {
            using var packedArray = NativeFuncs.orblitsharp_variant_as_packed_color_array(p_var);
            return Marshaling.ConvertNativePackedColorArrayToSystemArray(packedArray);
        }

        public static StringName[] ConvertToSystemArrayOfStringName(in orblit_variant p_var)
        {
            using var orblitArray = NativeFuncs.orblitsharp_variant_as_array(p_var);
            return Marshaling.ConvertNativeOrblitArrayToSystemArrayOfStringName(orblitArray);
        }

        public static EntityPath[] ConvertToSystemArrayOfEntityPath(in orblit_variant p_var)
        {
            using var orblitArray = NativeFuncs.orblitsharp_variant_as_array(p_var);
            return Marshaling.ConvertNativeOrblitArrayToSystemArrayOfEntityPath(orblitArray);
        }

        public static Rid[] ConvertToSystemArrayOfRid(in orblit_variant p_var)
        {
            using var orblitArray = NativeFuncs.orblitsharp_variant_as_array(p_var);
            return Marshaling.ConvertNativeOrblitArrayToSystemArrayOfRid(orblitArray);
        }

        public static T[] ConvertToSystemArrayOfOrblitObject<T>(in orblit_variant p_var)
            where T : OrblitObject
        {
            using var orblitArray = NativeFuncs.orblitsharp_variant_as_array(p_var);
            return Marshaling.ConvertNativeOrblitArrayToSystemArrayOfOrblitObjectType<T>(orblitArray);
        }
    }
}
