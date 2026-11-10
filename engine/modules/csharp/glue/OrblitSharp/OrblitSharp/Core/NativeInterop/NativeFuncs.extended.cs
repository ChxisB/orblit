#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable IDE1006 // Naming rule violation
// ReSharper disable InconsistentNaming

namespace Orblit.NativeInterop
{
    public static partial class NativeFuncs
    {
        public static orblit_variant orblitsharp_variant_new_copy(scoped in orblit_variant src)
        {
            switch (src.Type)
            {
                case Variant.Type.Nil:
                    return default;
                case Variant.Type.Bool:
                    return new orblit_variant() { Bool = src.Bool, Type = Variant.Type.Bool };
                case Variant.Type.Int:
                    return new orblit_variant() { Int = src.Int, Type = Variant.Type.Int };
                case Variant.Type.Float:
                    return new orblit_variant() { Float = src.Float, Type = Variant.Type.Float };
                case Variant.Type.Vector2:
                    return new orblit_variant() { Vector2 = src.Vector2, Type = Variant.Type.Vector2 };
                case Variant.Type.Vector2I:
                    return new orblit_variant() { Vector2I = src.Vector2I, Type = Variant.Type.Vector2I };
                case Variant.Type.Rect2:
                    return new orblit_variant() { Rect2 = src.Rect2, Type = Variant.Type.Rect2 };
                case Variant.Type.Rect2I:
                    return new orblit_variant() { Rect2I = src.Rect2I, Type = Variant.Type.Rect2I };
                case Variant.Type.Vector3:
                    return new orblit_variant() { Vector3 = src.Vector3, Type = Variant.Type.Vector3 };
                case Variant.Type.Vector3I:
                    return new orblit_variant() { Vector3I = src.Vector3I, Type = Variant.Type.Vector3I };
                case Variant.Type.Vector4:
                    return new orblit_variant() { Vector4 = src.Vector4, Type = Variant.Type.Vector4 };
                case Variant.Type.Vector4I:
                    return new orblit_variant() { Vector4I = src.Vector4I, Type = Variant.Type.Vector4I };
                case Variant.Type.Plane:
                    return new orblit_variant() { Plane = src.Plane, Type = Variant.Type.Plane };
                case Variant.Type.Quaternion:
                    return new orblit_variant() { Quaternion = src.Quaternion, Type = Variant.Type.Quaternion };
                case Variant.Type.Color:
                    return new orblit_variant() { Color = src.Color, Type = Variant.Type.Color };
                case Variant.Type.Rid:
                    return new orblit_variant() { Rid = src.Rid, Type = Variant.Type.Rid };
            }

            orblitsharp_variant_new_copy(out orblit_variant ret, src);
            return ret;
        }

        public static orblit_string_name orblitsharp_string_name_new_copy(scoped in orblit_string_name src)
        {
            if (src.IsEmpty)
                return default;
            orblitsharp_string_name_new_copy(out orblit_string_name ret, src);
            return ret;
        }

        public static orblit_node_path orblitsharp_node_path_new_copy(scoped in orblit_node_path src)
        {
            if (src.IsEmpty)
                return default;
            orblitsharp_node_path_new_copy(out orblit_node_path ret, src);
            return ret;
        }

        public static orblit_array orblitsharp_array_new()
        {
            orblitsharp_array_new(out orblit_array ret);
            return ret;
        }

        public static orblit_array orblitsharp_array_new_copy(scoped in orblit_array src)
        {
            orblitsharp_array_new_copy(out orblit_array ret, src);
            return ret;
        }

        public static orblit_dictionary orblitsharp_dictionary_new()
        {
            orblitsharp_dictionary_new(out orblit_dictionary ret);
            return ret;
        }

        public static orblit_dictionary orblitsharp_dictionary_new_copy(scoped in orblit_dictionary src)
        {
            orblitsharp_dictionary_new_copy(out orblit_dictionary ret, src);
            return ret;
        }

        public static orblit_string_name orblitsharp_string_name_new_from_string(string name)
        {
            using orblit_string src = Marshaling.ConvertStringToNative(name);
            orblitsharp_string_name_new_from_string(out orblit_string_name ret, src);
            return ret;
        }

        public static orblit_node_path orblitsharp_node_path_new_from_string(string name)
        {
            using orblit_string src = Marshaling.ConvertStringToNative(name);
            orblitsharp_node_path_new_from_string(out orblit_node_path ret, src);
            return ret;
        }
    }
}
