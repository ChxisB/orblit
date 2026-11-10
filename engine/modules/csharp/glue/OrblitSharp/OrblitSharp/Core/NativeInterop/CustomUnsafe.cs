using System.Runtime.CompilerServices;

namespace Orblit.NativeInterop;

// Ref structs are not allowed as generic type parameters, so we can't use Unsafe.AsPointer<T>/AsRef<T>.
// As a workaround we create our own overloads for our structs with some tricks under the hood.

public static class CustomUnsafe
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_ref* AsPointer(ref orblit_ref value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_ref* ReadOnlyRefAsPointer(in orblit_ref value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_ref AsRef(orblit_ref* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_ref AsRef(in orblit_ref source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_variant_call_error* AsPointer(ref orblit_variant_call_error value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_variant_call_error* ReadOnlyRefAsPointer(in orblit_variant_call_error value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_variant_call_error AsRef(orblit_variant_call_error* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_variant_call_error AsRef(in orblit_variant_call_error source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_variant* AsPointer(ref orblit_variant value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_variant* ReadOnlyRefAsPointer(in orblit_variant value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_variant AsRef(orblit_variant* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_variant AsRef(in orblit_variant source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_string* AsPointer(ref orblit_string value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_string* ReadOnlyRefAsPointer(in orblit_string value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_string AsRef(orblit_string* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_string AsRef(in orblit_string source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_string_name* AsPointer(ref orblit_string_name value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_string_name* ReadOnlyRefAsPointer(in orblit_string_name value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_string_name AsRef(orblit_string_name* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_string_name AsRef(in orblit_string_name source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_node_path* AsPointer(ref orblit_node_path value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_node_path* ReadOnlyRefAsPointer(in orblit_node_path value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_node_path AsRef(orblit_node_path* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_node_path AsRef(in orblit_node_path source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_signal* AsPointer(ref orblit_signal value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_signal* ReadOnlyRefAsPointer(in orblit_signal value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_signal AsRef(orblit_signal* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_signal AsRef(in orblit_signal source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_callable* AsPointer(ref orblit_callable value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_callable* ReadOnlyRefAsPointer(in orblit_callable value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_callable AsRef(orblit_callable* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_callable AsRef(in orblit_callable source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_array* AsPointer(ref orblit_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_array* ReadOnlyRefAsPointer(in orblit_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_array AsRef(orblit_array* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_array AsRef(in orblit_array source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_dictionary* AsPointer(ref orblit_dictionary value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_dictionary* ReadOnlyRefAsPointer(in orblit_dictionary value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_dictionary AsRef(orblit_dictionary* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_dictionary AsRef(in orblit_dictionary source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_byte_array* AsPointer(ref orblit_packed_byte_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_byte_array* ReadOnlyRefAsPointer(in orblit_packed_byte_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_byte_array AsRef(orblit_packed_byte_array* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_byte_array AsRef(in orblit_packed_byte_array source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_int32_array* AsPointer(ref orblit_packed_int32_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_int32_array* ReadOnlyRefAsPointer(in orblit_packed_int32_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_int32_array AsRef(orblit_packed_int32_array* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_int32_array AsRef(in orblit_packed_int32_array source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_int64_array* AsPointer(ref orblit_packed_int64_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_int64_array* ReadOnlyRefAsPointer(in orblit_packed_int64_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_int64_array AsRef(orblit_packed_int64_array* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_int64_array AsRef(in orblit_packed_int64_array source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_float32_array* AsPointer(ref orblit_packed_float32_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_float32_array* ReadOnlyRefAsPointer(in orblit_packed_float32_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_float32_array AsRef(orblit_packed_float32_array* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_float32_array AsRef(in orblit_packed_float32_array source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_float64_array* AsPointer(ref orblit_packed_float64_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_float64_array* ReadOnlyRefAsPointer(in orblit_packed_float64_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_float64_array AsRef(orblit_packed_float64_array* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_float64_array AsRef(in orblit_packed_float64_array source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_string_array* AsPointer(ref orblit_packed_string_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_string_array* ReadOnlyRefAsPointer(in orblit_packed_string_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_string_array AsRef(orblit_packed_string_array* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_string_array AsRef(in orblit_packed_string_array source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_vector2_array* AsPointer(ref orblit_packed_vector2_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_vector2_array* ReadOnlyRefAsPointer(in orblit_packed_vector2_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_vector2_array AsRef(orblit_packed_vector2_array* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_vector2_array AsRef(in orblit_packed_vector2_array source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_vector3_array* AsPointer(ref orblit_packed_vector3_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_vector3_array* ReadOnlyRefAsPointer(in orblit_packed_vector3_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_vector3_array AsRef(orblit_packed_vector3_array* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_vector3_array AsRef(in orblit_packed_vector3_array source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_vector4_array* AsPointer(ref orblit_packed_vector4_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_vector4_array* ReadOnlyRefAsPointer(in orblit_packed_vector4_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_vector4_array AsRef(orblit_packed_vector4_array* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_vector4_array AsRef(in orblit_packed_vector4_array source)
        => ref *ReadOnlyRefAsPointer(in source);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_color_array* AsPointer(ref orblit_packed_color_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe orblit_packed_color_array* ReadOnlyRefAsPointer(in orblit_packed_color_array value)
        => value.GetUnsafeAddress();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_color_array AsRef(orblit_packed_color_array* source)
        => ref *source;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe ref orblit_packed_color_array AsRef(in orblit_packed_color_array source)
        => ref *ReadOnlyRefAsPointer(in source);
}
