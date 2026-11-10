#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable IDE1006 // Naming rule violation
// ReSharper disable InconsistentNaming

using System;
using System.Runtime.CompilerServices;
using Orblit.SourceGenerators.Internal;


namespace Orblit.NativeInterop
{
    /*
     * IMPORTANT:
     * The order of the methods defined in NativeFuncs must match the order
     * in the array defined at the bottom of 'glue/runtime_interop.cpp'.
     */

    [GenerateUnmanagedCallbacks(typeof(UnmanagedCallbacks))]
    public static unsafe partial class NativeFuncs
    {
        private static bool initialized;

        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Global
        public static void Initialize(IntPtr unmanagedCallbacks, int unmanagedCallbacksSize)
        {
            if (initialized)
                throw new InvalidOperationException("Already initialized.");
            initialized = true;

            if (unmanagedCallbacksSize != sizeof(UnmanagedCallbacks))
                throw new ArgumentException("Unmanaged callbacks size mismatch.", nameof(unmanagedCallbacksSize));

            _unmanagedCallbacks = Unsafe.AsRef<UnmanagedCallbacks>((void*)unmanagedCallbacks);
        }

        private partial struct UnmanagedCallbacks
        {
        }

        // Custom functions

        internal static partial orblit_bool orblitsharp_dotnet_module_is_initialized();

        internal static partial void orblitsharp_initialize_marshaling_information(out ulong r_ref_count_offset, out ulong r_capacity_offset, out ulong r_size_offset, out ulong r_data_offset);

        public static partial IntPtr orblitsharp_method_bind_get_method(in orblit_string_name p_classname,
            in orblit_string_name p_methodname);

        public static partial IntPtr orblitsharp_method_bind_get_method_by_hash(
            in orblit_string_name p_classname, in orblit_string_name p_methodname, ulong p_hash);

        public static partial delegate* unmanaged<orblit_bool, IntPtr> orblitsharp_get_class_constructor(
            in orblit_string_name p_classname);

        public static partial IntPtr orblitsharp_engine_get_singleton(in orblit_string p_name);


        internal static partial Error orblitsharp_stack_info_vector_resize(
            ref DebuggingUtils.orblit_stack_info_vector p_stack_info_vector, int p_size);

        internal static partial void orblitsharp_stack_info_vector_destroy(
            ref DebuggingUtils.orblit_stack_info_vector p_stack_info_vector);

        internal static partial void orblitsharp_internal_editor_file_system_update_files(in orblit_packed_string_array p_script_paths);

        internal static partial void orblitsharp_internal_script_debugger_send_error(in orblit_string p_func,
            in orblit_string p_file, int p_line, in orblit_string p_err, in orblit_string p_descr,
            orblit_error_handler_type p_type, in DebuggingUtils.orblit_stack_info_vector p_stack_info_vector);

        internal static partial orblit_bool orblitsharp_internal_script_debugger_is_active();

        internal static partial IntPtr orblitsharp_internal_object_get_associated_gchandle(IntPtr ptr);

        internal static partial void orblitsharp_internal_object_disposed(IntPtr ptr, IntPtr gcHandleToFree);

        internal static partial void orblitsharp_internal_refcounted_disposed(IntPtr ptr, IntPtr gcHandleToFree,
            orblit_bool isFinalizer);

        internal static partial Error orblitsharp_internal_signal_awaiter_connect(IntPtr source,
            in orblit_string_name signal,
            IntPtr target, IntPtr awaiterHandlePtr);

        internal static partial void orblitsharp_internal_tie_native_managed_to_unmanaged(IntPtr gcHandleIntPtr,
            IntPtr unmanaged, in orblit_string_name nativeName, orblit_bool refCounted);

        internal static partial void orblitsharp_internal_tie_user_managed_to_unmanaged(IntPtr gcHandleIntPtr,
            IntPtr unmanaged, orblit_ref* scriptPtr, orblit_bool refCounted);

        internal static partial void orblitsharp_internal_tie_managed_to_unmanaged_with_pre_setup(
            IntPtr gcHandleIntPtr, IntPtr unmanaged);

        internal static partial IntPtr orblitsharp_internal_unmanaged_get_script_instance_managed(IntPtr p_unmanaged,
            out orblit_bool r_has_cs_script_instance);

        internal static partial IntPtr orblitsharp_internal_unmanaged_get_instance_binding_managed(IntPtr p_unmanaged);

        internal static partial IntPtr orblitsharp_internal_unmanaged_instance_binding_create_managed(IntPtr p_unmanaged,
            IntPtr oldGCHandlePtr);

        internal static partial void orblitsharp_internal_new_csharp_script(orblit_ref* r_dest);

        internal static partial orblit_bool orblitsharp_internal_script_load(in orblit_string p_path, orblit_ref* r_dest);

        internal static partial void orblitsharp_internal_reload_registered_script(IntPtr scriptPtr);

        internal static partial void orblitsharp_array_filter_objects_by_native(scoped in orblit_string_name p_native_name,
            scoped in orblit_array p_input, out orblit_array r_output);

        internal static partial void orblitsharp_array_filter_objects_by_non_native(scoped in orblit_array p_input,
            out orblit_array r_output);

        public static partial void orblitsharp_ref_new_from_ref_counted_ptr(out orblit_ref r_dest,
            IntPtr p_ref_counted_ptr);

        public static partial void orblitsharp_ref_destroy(ref orblit_ref p_instance);

        public static partial void orblitsharp_string_name_new_from_string(out orblit_string_name r_dest,
            scoped in orblit_string p_name);

        public static partial void orblitsharp_node_path_new_from_string(out orblit_node_path r_dest,
            scoped in orblit_string p_name);

        public static partial void
            orblitsharp_string_name_as_string(out orblit_string r_dest, scoped in orblit_string_name p_name);

        public static partial void orblitsharp_node_path_as_string(out orblit_string r_dest, scoped in orblit_node_path p_np);

        public static partial orblit_packed_byte_array orblitsharp_packed_byte_array_new_mem_copy(byte* p_src,
            int p_length);

        public static partial orblit_packed_int32_array orblitsharp_packed_int32_array_new_mem_copy(int* p_src,
            int p_length);

        public static partial orblit_packed_int64_array orblitsharp_packed_int64_array_new_mem_copy(long* p_src,
            int p_length);

        public static partial orblit_packed_float32_array orblitsharp_packed_float32_array_new_mem_copy(float* p_src,
            int p_length);

        public static partial orblit_packed_float64_array orblitsharp_packed_float64_array_new_mem_copy(double* p_src,
            int p_length);

        public static partial orblit_packed_vector2_array orblitsharp_packed_vector2_array_new_mem_copy(Vector2* p_src,
            int p_length);

        public static partial orblit_packed_vector3_array orblitsharp_packed_vector3_array_new_mem_copy(Vector3* p_src,
            int p_length);

        public static partial orblit_packed_vector4_array orblitsharp_packed_vector4_array_new_mem_copy(Vector4* p_src,
            int p_length);

        public static partial orblit_packed_color_array orblitsharp_packed_color_array_new_mem_copy(Color* p_src,
            int p_length);

        public static partial void orblitsharp_packed_string_array_add(ref orblit_packed_string_array r_dest,
            in orblit_string p_element);

        public static partial void orblitsharp_callable_new_with_delegate(IntPtr p_delegate_handle, IntPtr p_trampoline,
            IntPtr p_object, out orblit_callable r_callable);

        internal static partial orblit_bool orblitsharp_callable_get_data_for_marshalling(scoped in orblit_callable p_callable,
            out IntPtr r_delegate_handle, out IntPtr r_trampoline, out IntPtr r_object, out orblit_string_name r_name);

        internal static partial orblit_variant orblitsharp_callable_call(scoped in orblit_callable p_callable,
            orblit_variant** p_args, int p_arg_count, out orblit_variant_call_error p_call_error);

        internal static partial void orblitsharp_callable_call_deferred(in orblit_callable p_callable,
            orblit_variant** p_args, int p_arg_count);

        internal static partial Color orblitsharp_color_from_ok_hsl(float p_h, float p_s, float p_l, float p_alpha);

        internal static partial float orblitsharp_color_get_ok_hsl_h(in Color p_self);

        internal static partial float orblitsharp_color_get_ok_hsl_s(in Color p_self);

        internal static partial float orblitsharp_color_get_ok_hsl_l(in Color p_self);

        // GDNative functions

        // gdnative.h

        public static partial void orblitsharp_method_bind_ptrcall(IntPtr p_method_bind, IntPtr p_instance, void** p_args,
            void* p_ret);

        public static partial orblit_variant orblitsharp_method_bind_call(IntPtr p_method_bind, IntPtr p_instance,
            orblit_variant** p_args, int p_arg_count, out orblit_variant_call_error p_call_error);

        // variant.h

        public static partial void
            orblitsharp_variant_new_string_name(out orblit_variant r_dest, scoped in orblit_string_name p_s);

        public static partial void orblitsharp_variant_new_copy(out orblit_variant r_dest, scoped in orblit_variant p_src);

        public static partial void orblitsharp_variant_new_node_path(out orblit_variant r_dest, scoped in orblit_node_path p_np);

        public static partial void orblitsharp_variant_new_object(out orblit_variant r_dest, IntPtr p_obj);

        public static partial void orblitsharp_variant_new_transform2d(out orblit_variant r_dest, scoped in Transform2D p_t2d);

        public static partial void orblitsharp_variant_new_basis(out orblit_variant r_dest, scoped in Basis p_basis);

        public static partial void orblitsharp_variant_new_transform3d(out orblit_variant r_dest, scoped in Transform3D p_trans);

        public static partial void orblitsharp_variant_new_projection(out orblit_variant r_dest, scoped in Projection p_proj);

        public static partial void orblitsharp_variant_new_aabb(out orblit_variant r_dest, scoped in Aabb p_aabb);

        public static partial void orblitsharp_variant_new_dictionary(out orblit_variant r_dest,
            scoped in orblit_dictionary p_dict);

        public static partial void orblitsharp_variant_new_array(out orblit_variant r_dest, scoped in orblit_array p_arr);

        public static partial void orblitsharp_variant_new_packed_byte_array(out orblit_variant r_dest,
            scoped in orblit_packed_byte_array p_pba);

        public static partial void orblitsharp_variant_new_packed_int32_array(out orblit_variant r_dest,
            scoped in orblit_packed_int32_array p_pia);

        public static partial void orblitsharp_variant_new_packed_int64_array(out orblit_variant r_dest,
            scoped in orblit_packed_int64_array p_pia);

        public static partial void orblitsharp_variant_new_packed_float32_array(out orblit_variant r_dest,
            scoped in orblit_packed_float32_array p_pra);

        public static partial void orblitsharp_variant_new_packed_float64_array(out orblit_variant r_dest,
            scoped in orblit_packed_float64_array p_pra);

        public static partial void orblitsharp_variant_new_packed_string_array(out orblit_variant r_dest,
            scoped in orblit_packed_string_array p_psa);

        public static partial void orblitsharp_variant_new_packed_vector2_array(out orblit_variant r_dest,
            scoped in orblit_packed_vector2_array p_pv2a);

        public static partial void orblitsharp_variant_new_packed_vector3_array(out orblit_variant r_dest,
            scoped in orblit_packed_vector3_array p_pv3a);

        public static partial void orblitsharp_variant_new_packed_vector4_array(out orblit_variant r_dest,
            scoped in orblit_packed_vector4_array p_pv4a);

        public static partial void orblitsharp_variant_new_packed_color_array(out orblit_variant r_dest,
            scoped in orblit_packed_color_array p_pca);

        public static partial orblit_bool orblitsharp_variant_as_bool(scoped in orblit_variant p_self);

        public static partial Int64 orblitsharp_variant_as_int(scoped in orblit_variant p_self);

        public static partial double orblitsharp_variant_as_float(scoped in orblit_variant p_self);

        public static partial orblit_string orblitsharp_variant_as_string(scoped in orblit_variant p_self);

        public static partial Vector2 orblitsharp_variant_as_vector2(scoped in orblit_variant p_self);

        public static partial Vector2I orblitsharp_variant_as_vector2i(scoped in orblit_variant p_self);

        public static partial Rect2 orblitsharp_variant_as_rect2(scoped in orblit_variant p_self);

        public static partial Rect2I orblitsharp_variant_as_rect2i(scoped in orblit_variant p_self);

        public static partial Vector3 orblitsharp_variant_as_vector3(scoped in orblit_variant p_self);

        public static partial Vector3I orblitsharp_variant_as_vector3i(scoped in orblit_variant p_self);

        public static partial Transform2D orblitsharp_variant_as_transform2d(scoped in orblit_variant p_self);

        public static partial Vector4 orblitsharp_variant_as_vector4(scoped in orblit_variant p_self);

        public static partial Vector4I orblitsharp_variant_as_vector4i(scoped in orblit_variant p_self);

        public static partial Plane orblitsharp_variant_as_plane(scoped in orblit_variant p_self);

        public static partial Quaternion orblitsharp_variant_as_quaternion(scoped in orblit_variant p_self);

        public static partial Aabb orblitsharp_variant_as_aabb(scoped in orblit_variant p_self);

        public static partial Basis orblitsharp_variant_as_basis(scoped in orblit_variant p_self);

        public static partial Transform3D orblitsharp_variant_as_transform3d(scoped in orblit_variant p_self);

        public static partial Projection orblitsharp_variant_as_projection(scoped in orblit_variant p_self);

        public static partial Color orblitsharp_variant_as_color(scoped in orblit_variant p_self);

        public static partial orblit_string_name orblitsharp_variant_as_string_name(scoped in orblit_variant p_self);

        public static partial orblit_node_path orblitsharp_variant_as_node_path(scoped in orblit_variant p_self);

        public static partial Rid orblitsharp_variant_as_rid(scoped in orblit_variant p_self);

        public static partial orblit_callable orblitsharp_variant_as_callable(scoped in orblit_variant p_self);

        public static partial orblit_signal orblitsharp_variant_as_signal(scoped in orblit_variant p_self);

        public static partial orblit_dictionary orblitsharp_variant_as_dictionary(scoped in orblit_variant p_self);

        public static partial orblit_array orblitsharp_variant_as_array(scoped in orblit_variant p_self);

        public static partial orblit_packed_byte_array orblitsharp_variant_as_packed_byte_array(scoped in orblit_variant p_self);

        public static partial orblit_packed_int32_array orblitsharp_variant_as_packed_int32_array(scoped in orblit_variant p_self);

        public static partial orblit_packed_int64_array orblitsharp_variant_as_packed_int64_array(scoped in orblit_variant p_self);

        public static partial orblit_packed_float32_array orblitsharp_variant_as_packed_float32_array(scoped in orblit_variant p_self);

        public static partial orblit_packed_float64_array orblitsharp_variant_as_packed_float64_array(scoped in orblit_variant p_self);

        public static partial orblit_packed_string_array orblitsharp_variant_as_packed_string_array(scoped in orblit_variant p_self);

        public static partial orblit_packed_vector2_array orblitsharp_variant_as_packed_vector2_array(scoped in orblit_variant p_self);

        public static partial orblit_packed_vector3_array orblitsharp_variant_as_packed_vector3_array(scoped in orblit_variant p_self);

        public static partial orblit_packed_vector4_array orblitsharp_variant_as_packed_vector4_array(
            in orblit_variant p_self);

        public static partial orblit_packed_color_array orblitsharp_variant_as_packed_color_array(scoped in orblit_variant p_self);

        public static partial orblit_bool orblitsharp_variant_equals(scoped in orblit_variant p_a, scoped in orblit_variant p_b);

        // string.h

        public static partial void orblitsharp_string_new_with_utf16_chars(out orblit_string r_dest, char* p_contents);

        // string_name.h

        public static partial void orblitsharp_string_name_new_copy(out orblit_string_name r_dest,
            scoped in orblit_string_name p_src);

        // entity_path.h

        public static partial void orblitsharp_node_path_new_copy(out orblit_node_path r_dest, scoped in orblit_node_path p_src);

        // array.h

        public static partial void orblitsharp_array_new(out orblit_array r_dest);

        public static partial void orblitsharp_array_new_copy(out orblit_array r_dest, scoped in orblit_array p_src);

        public static partial orblit_variant* orblitsharp_array_ptrw(ref orblit_array p_self);

        // dictionary.h

        public static partial void orblitsharp_dictionary_new(out orblit_dictionary r_dest);

        public static partial void orblitsharp_dictionary_new_copy(out orblit_dictionary r_dest,
            scoped in orblit_dictionary p_src);

        // destroy functions

        public static partial void orblitsharp_packed_byte_array_destroy(ref orblit_packed_byte_array p_self);

        public static partial void orblitsharp_packed_int32_array_destroy(ref orblit_packed_int32_array p_self);

        public static partial void orblitsharp_packed_int64_array_destroy(ref orblit_packed_int64_array p_self);

        public static partial void orblitsharp_packed_float32_array_destroy(ref orblit_packed_float32_array p_self);

        public static partial void orblitsharp_packed_float64_array_destroy(ref orblit_packed_float64_array p_self);

        public static partial void orblitsharp_packed_string_array_destroy(ref orblit_packed_string_array p_self);

        public static partial void orblitsharp_packed_vector2_array_destroy(ref orblit_packed_vector2_array p_self);

        public static partial void orblitsharp_packed_vector3_array_destroy(ref orblit_packed_vector3_array p_self);

        public static partial void orblitsharp_packed_vector4_array_destroy(ref orblit_packed_vector4_array p_self);

        public static partial void orblitsharp_packed_color_array_destroy(ref orblit_packed_color_array p_self);

        public static partial void orblitsharp_variant_destroy(ref orblit_variant p_self);

        public static partial void orblitsharp_string_destroy(ref orblit_string p_self);

        public static partial void orblitsharp_string_name_destroy(ref orblit_string_name p_self);

        public static partial void orblitsharp_node_path_destroy(ref orblit_node_path p_self);

        public static partial void orblitsharp_signal_destroy(ref orblit_signal p_self);

        public static partial void orblitsharp_callable_destroy(ref orblit_callable p_self);

        public static partial void orblitsharp_array_destroy(ref orblit_array p_self);

        public static partial void orblitsharp_dictionary_destroy(ref orblit_dictionary p_self);

        // Array

        public static partial int orblitsharp_array_add(ref orblit_array p_self, in orblit_variant p_item);

        public static partial int orblitsharp_array_add_range(ref orblit_array p_self, in orblit_array p_collection);

        public static partial int orblitsharp_array_binary_search(ref orblit_array p_self, int p_index, int p_count, in orblit_variant p_value);

        public static partial void orblitsharp_array_duplicate(scoped ref orblit_array p_self, orblit_bool p_deep, out orblit_array r_dest);

        public static partial void orblitsharp_array_fill(ref orblit_array p_self, in orblit_variant p_value);

        public static partial int orblitsharp_array_index_of(ref orblit_array p_self, in orblit_variant p_item, int p_index = 0);

        public static partial void orblitsharp_array_insert(ref orblit_array p_self, int p_index, in orblit_variant p_item);

        public static partial int orblitsharp_array_last_index_of(ref orblit_array p_self, in orblit_variant p_item, int p_index);

        public static partial void orblitsharp_array_make_read_only(ref orblit_array p_self);

        public static partial void orblitsharp_array_set_typed(
            ref orblit_array p_self,
            uint p_elem_type,
            in orblit_string_name p_elem_class_name,
            in orblit_ref p_elem_script);

        public static partial orblit_bool orblitsharp_array_is_typed(ref orblit_array p_self);

        public static partial void orblitsharp_array_max(scoped ref orblit_array p_self, out orblit_variant r_value);

        public static partial void orblitsharp_array_min(scoped ref orblit_array p_self, out orblit_variant r_value);

        public static partial void orblitsharp_array_pick_random(scoped ref orblit_array p_self, out orblit_variant r_value);

        public static partial orblit_bool orblitsharp_array_recursive_equal(ref orblit_array p_self, in orblit_array p_other);

        public static partial void orblitsharp_array_remove_at(ref orblit_array p_self, int p_index);

        public static partial Error orblitsharp_array_resize(ref orblit_array p_self, int p_new_size);

        public static partial void orblitsharp_array_reverse(ref orblit_array p_self);

        public static partial void orblitsharp_array_shuffle(ref orblit_array p_self);

        public static partial void orblitsharp_array_slice(scoped ref orblit_array p_self, int p_start, int p_end,
            int p_step, orblit_bool p_deep, out orblit_array r_dest);

        public static partial void orblitsharp_array_sort(ref orblit_array p_self);

        public static partial void orblitsharp_array_to_string(ref orblit_array p_self, out orblit_string r_str);

        public static partial void orblitsharp_packed_byte_array_compress(scoped in orblit_packed_byte_array p_src, int p_mode, out orblit_packed_byte_array r_dst);

        public static partial void orblitsharp_packed_byte_array_decompress(scoped in orblit_packed_byte_array p_src, long p_buffer_size, int p_mode, out orblit_packed_byte_array r_dst);

        public static partial void orblitsharp_packed_byte_array_decompress_dynamic(scoped in orblit_packed_byte_array p_src, long p_buffer_size, int p_mode, out orblit_packed_byte_array r_dst);

        // Dictionary

        public static partial orblit_bool orblitsharp_dictionary_try_get_value(scoped ref orblit_dictionary p_self,
            scoped in orblit_variant p_key,
            out orblit_variant r_value);

        public static partial void orblitsharp_dictionary_set_value(ref orblit_dictionary p_self, in orblit_variant p_key,
            in orblit_variant p_value);

        public static partial void orblitsharp_dictionary_keys(scoped ref orblit_dictionary p_self, out orblit_array r_dest);

        public static partial void orblitsharp_dictionary_values(scoped ref orblit_dictionary p_self, out orblit_array r_dest);

        public static partial int orblitsharp_dictionary_count(ref orblit_dictionary p_self);

        public static partial void orblitsharp_dictionary_key_value_pair_at(scoped ref orblit_dictionary p_self, int p_index,
            out orblit_variant r_key, out orblit_variant r_value);

        public static partial void orblitsharp_dictionary_add(ref orblit_dictionary p_self, in orblit_variant p_key,
            in orblit_variant p_value);

        public static partial void orblitsharp_dictionary_clear(ref orblit_dictionary p_self);

        public static partial orblit_bool orblitsharp_dictionary_contains_key(ref orblit_dictionary p_self,
            in orblit_variant p_key);

        public static partial void orblitsharp_dictionary_duplicate(scoped ref orblit_dictionary p_self, orblit_bool p_deep,
            out orblit_dictionary r_dest);

        public static partial void orblitsharp_dictionary_merge(ref orblit_dictionary p_self, in orblit_dictionary p_dictionary, orblit_bool p_overwrite);

        public static partial orblit_bool orblitsharp_dictionary_recursive_equal(ref orblit_dictionary p_self, in orblit_dictionary p_other);

        public static partial orblit_bool orblitsharp_dictionary_remove_key(ref orblit_dictionary p_self,
            in orblit_variant p_key);

        public static partial void orblitsharp_dictionary_make_read_only(ref orblit_dictionary p_self);

        public static partial void orblitsharp_dictionary_set_typed(
            ref orblit_dictionary p_self,
            uint p_key_type,
            in orblit_string_name p_key_class_name,
            in orblit_ref p_key_script,
            uint p_value_type,
            in orblit_string_name p_value_class_name,
            in orblit_ref p_value_script);

        public static partial orblit_bool orblitsharp_dictionary_is_typed_key(ref orblit_dictionary p_self);

        public static partial orblit_bool orblitsharp_dictionary_is_typed_value(ref orblit_dictionary p_self);

        public static partial uint orblitsharp_dictionary_get_typed_key_builtin(ref orblit_dictionary p_self);

        public static partial uint orblitsharp_dictionary_get_typed_value_builtin(ref orblit_dictionary p_self);

        public static partial void orblitsharp_dictionary_get_typed_key_class_name(ref orblit_dictionary p_self, out orblit_string_name r_dest);

        public static partial void orblitsharp_dictionary_get_typed_value_class_name(ref orblit_dictionary p_self, out orblit_string_name r_dest);

        public static partial void orblitsharp_dictionary_get_typed_key_script(ref orblit_dictionary p_self, out orblit_variant r_dest);

        public static partial void orblitsharp_dictionary_get_typed_value_script(ref orblit_dictionary p_self, out orblit_variant r_dest);

        public static partial void orblitsharp_dictionary_to_string(scoped ref orblit_dictionary p_self, out orblit_string r_str);

        // StringExtensions

        public static partial void orblitsharp_string_simplify_path(scoped in orblit_string p_self,
            out orblit_string r_simplified_path);

        public static partial void orblitsharp_string_capitalize(scoped in orblit_string p_self,
            out orblit_string r_capitalized);

        public static partial void orblitsharp_string_to_camel_case(scoped in orblit_string p_self,
            out orblit_string r_camel_case);

        public static partial void orblitsharp_string_to_pascal_case(scoped in orblit_string p_self,
            out orblit_string r_pascal_case);

        public static partial void orblitsharp_string_to_snake_case(scoped in orblit_string p_self,
            out orblit_string r_snake_case);

        public static partial void orblitsharp_string_to_kebab_case(scoped in orblit_string p_self,
            out orblit_string r_kebab_case);

        // EntityPath

        public static partial void orblitsharp_node_path_get_as_property_path(in orblit_node_path p_self,
            ref orblit_node_path r_dest);

        public static partial void orblitsharp_node_path_get_concatenated_names(scoped in orblit_node_path p_self,
            out orblit_string r_names);

        public static partial void orblitsharp_node_path_get_concatenated_subnames(scoped in orblit_node_path p_self,
            out orblit_string r_subnames);

        public static partial void orblitsharp_node_path_get_name(scoped in orblit_node_path p_self, int p_idx,
            out orblit_string r_name);

        public static partial int orblitsharp_node_path_get_name_count(in orblit_node_path p_self);

        public static partial void orblitsharp_node_path_get_subname(scoped in orblit_node_path p_self, int p_idx,
            out orblit_string r_subname);

        public static partial int orblitsharp_node_path_get_subname_count(in orblit_node_path p_self);

        public static partial orblit_bool orblitsharp_node_path_is_absolute(in orblit_node_path p_self);

        public static partial orblit_bool orblitsharp_node_path_equals(in orblit_node_path p_self, in orblit_node_path p_other);

        public static partial int orblitsharp_node_path_hash(in orblit_node_path p_self);

        // Orb, etc

        internal static partial void orblitsharp_bytes_to_var(scoped in orblit_packed_byte_array p_bytes,
            orblit_bool p_allow_objects,
            out orblit_variant r_ret);

        internal static partial void orblitsharp_convert(scoped in orblit_variant p_what, int p_type,
            out orblit_variant r_ret);

        internal static partial int orblitsharp_hash(in orblit_variant p_var);

        internal static partial IntPtr orblitsharp_instance_from_id(ulong p_instance_id);

        internal static partial void orblitsharp_print(in orblit_string p_what);

        public static partial void orblitsharp_print_rich(in orblit_string p_what);

        internal static partial void orblitsharp_printerr(in orblit_string p_what);

        internal static partial void orblitsharp_printraw(in orblit_string p_what);

        internal static partial void orblitsharp_prints(in orblit_string p_what);

        internal static partial void orblitsharp_printt(in orblit_string p_what);

        internal static partial float orblitsharp_randf();

        internal static partial uint orblitsharp_randi();

        internal static partial void orblitsharp_randomize();

        internal static partial double orblitsharp_randf_range(double from, double to);

        internal static partial double orblitsharp_randfn(double mean, double deviation);

        internal static partial int orblitsharp_randi_range(int from, int to);

        internal static partial uint orblitsharp_rand_from_seed(ulong seed, out ulong newSeed);

        internal static partial void orblitsharp_seed(ulong seed);

        internal static partial void orblitsharp_weakref(IntPtr p_obj, out orblit_ref r_weak_ref);

        internal static partial void orblitsharp_str_to_var(scoped in orblit_string p_str, out orblit_variant r_ret);

        internal static partial void orblitsharp_var_to_bytes(scoped in orblit_variant p_what, orblit_bool p_full_objects,
            out orblit_packed_byte_array r_bytes);

        internal static partial void orblitsharp_var_to_str(scoped in orblit_variant p_var, out orblit_string r_ret);

        internal static partial void orblitsharp_err_print_error(in orblit_string p_function, in orblit_string p_file, int p_line, in orblit_string p_error, in orblit_string p_message = default, orblit_bool p_editor_notify = orblit_bool.False, orblit_error_handler_type p_type = orblit_error_handler_type.ERR_HANDLER_ERROR);

        // Object

        public static partial void orblitsharp_object_to_string(IntPtr ptr, out orblit_string r_str);
    }
}
