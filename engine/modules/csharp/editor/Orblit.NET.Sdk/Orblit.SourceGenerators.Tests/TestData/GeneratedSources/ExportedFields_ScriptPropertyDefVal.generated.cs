partial class ExportedFields
{
#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword
#if TOOLS
    /// <summary>
    /// Get the default values for all properties declared in this class.
    /// This method is used by Orblit to determine the value that will be
    /// used by the inspector when resetting properties.
    /// Do not call this method.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    internal new static global::System.Collections.Generic.Dictionary<global::Orblit.StringName, global::Orblit.Variant> GetOrblitPropertyDefaultValues()
    {
        var values = new global::System.Collections.Generic.Dictionary<global::Orblit.StringName, global::Orblit.Variant>(62);
        bool ___fieldBoolean_default_value = true;
        values.Add(PropertyName.@_fieldBoolean, global::Orblit.Variant.From<bool>(___fieldBoolean_default_value));
        char ___fieldChar_default_value = 'f';
        values.Add(PropertyName.@_fieldChar, global::Orblit.Variant.From<char>(___fieldChar_default_value));
        sbyte ___fieldSByte_default_value = 10;
        values.Add(PropertyName.@_fieldSByte, global::Orblit.Variant.From<sbyte>(___fieldSByte_default_value));
        short ___fieldInt16_default_value = 10;
        values.Add(PropertyName.@_fieldInt16, global::Orblit.Variant.From<short>(___fieldInt16_default_value));
        int ___fieldInt32_default_value = 10;
        values.Add(PropertyName.@_fieldInt32, global::Orblit.Variant.From<int>(___fieldInt32_default_value));
        long ___fieldInt64_default_value = -10_000;
        values.Add(PropertyName.@_fieldInt64, global::Orblit.Variant.From<long>(___fieldInt64_default_value));
        byte ___fieldByte_default_value = 10;
        values.Add(PropertyName.@_fieldByte, global::Orblit.Variant.From<byte>(___fieldByte_default_value));
        ushort ___fieldUInt16_default_value = 10;
        values.Add(PropertyName.@_fieldUInt16, global::Orblit.Variant.From<ushort>(___fieldUInt16_default_value));
        uint ___fieldUInt32_default_value = 10;
        values.Add(PropertyName.@_fieldUInt32, global::Orblit.Variant.From<uint>(___fieldUInt32_default_value));
        ulong ___fieldUInt64_default_value = 10;
        values.Add(PropertyName.@_fieldUInt64, global::Orblit.Variant.From<ulong>(___fieldUInt64_default_value));
        float ___fieldSingle_default_value = 10;
        values.Add(PropertyName.@_fieldSingle, global::Orblit.Variant.From<float>(___fieldSingle_default_value));
        double ___fieldDouble_default_value = 10;
        values.Add(PropertyName.@_fieldDouble, global::Orblit.Variant.From<double>(___fieldDouble_default_value));
        string ___fieldString_default_value = "foo";
        values.Add(PropertyName.@_fieldString, global::Orblit.Variant.From<string>(___fieldString_default_value));
        float ___fieldStaticImport_default_value = global::Orblit.Mathf.RadToDeg(2  * global::Orblit.Mathf.Pi);
        values.Add(PropertyName.@_fieldStaticImport, global::Orblit.Variant.From<float>(___fieldStaticImport_default_value));
        global::Orblit.Vector2 ___fieldVector2_default_value = new(10f, 10f);
        values.Add(PropertyName.@_fieldVector2, global::Orblit.Variant.From<global::Orblit.Vector2>(___fieldVector2_default_value));
        global::Orblit.Vector2I ___fieldVector2I_default_value = global::Orblit.Vector2I.Up;
        values.Add(PropertyName.@_fieldVector2I, global::Orblit.Variant.From<global::Orblit.Vector2I>(___fieldVector2I_default_value));
        global::Orblit.Rect2 ___fieldRect2_default_value = new(new global::Orblit.Vector2(10f, 10f), new global::Orblit.Vector2(10f, 10f));
        values.Add(PropertyName.@_fieldRect2, global::Orblit.Variant.From<global::Orblit.Rect2>(___fieldRect2_default_value));
        global::Orblit.Rect2I ___fieldRect2I_default_value = new(new global::Orblit.Vector2I(10, 10), new global::Orblit.Vector2I(10, 10));
        values.Add(PropertyName.@_fieldRect2I, global::Orblit.Variant.From<global::Orblit.Rect2I>(___fieldRect2I_default_value));
        global::Orblit.Transform2D ___fieldTransform2D_default_value = global::Orblit.Transform2D.Identity;
        values.Add(PropertyName.@_fieldTransform2D, global::Orblit.Variant.From<global::Orblit.Transform2D>(___fieldTransform2D_default_value));
        global::Orblit.Vector3 ___fieldVector3_default_value = new(10f, 10f, 10f);
        values.Add(PropertyName.@_fieldVector3, global::Orblit.Variant.From<global::Orblit.Vector3>(___fieldVector3_default_value));
        global::Orblit.Vector3I ___fieldVector3I_default_value = global::Orblit.Vector3I.Back;
        values.Add(PropertyName.@_fieldVector3I, global::Orblit.Variant.From<global::Orblit.Vector3I>(___fieldVector3I_default_value));
        global::Orblit.Basis ___fieldBasis_default_value = new global::Orblit.Basis(global::Orblit.Quaternion.Identity);
        values.Add(PropertyName.@_fieldBasis, global::Orblit.Variant.From<global::Orblit.Basis>(___fieldBasis_default_value));
        global::Orblit.Quaternion ___fieldQuaternion_default_value = new global::Orblit.Quaternion(global::Orblit.Basis.Identity);
        values.Add(PropertyName.@_fieldQuaternion, global::Orblit.Variant.From<global::Orblit.Quaternion>(___fieldQuaternion_default_value));
        global::Orblit.Transform3D ___fieldTransform3D_default_value = global::Orblit.Transform3D.Identity;
        values.Add(PropertyName.@_fieldTransform3D, global::Orblit.Variant.From<global::Orblit.Transform3D>(___fieldTransform3D_default_value));
        global::Orblit.Vector4 ___fieldVector4_default_value = new(10f, 10f, 10f, 10f);
        values.Add(PropertyName.@_fieldVector4, global::Orblit.Variant.From<global::Orblit.Vector4>(___fieldVector4_default_value));
        global::Orblit.Vector4I ___fieldVector4I_default_value = global::Orblit.Vector4I.One;
        values.Add(PropertyName.@_fieldVector4I, global::Orblit.Variant.From<global::Orblit.Vector4I>(___fieldVector4I_default_value));
        global::Orblit.Projection ___fieldProjection_default_value = global::Orblit.Projection.Identity;
        values.Add(PropertyName.@_fieldProjection, global::Orblit.Variant.From<global::Orblit.Projection>(___fieldProjection_default_value));
        global::Orblit.Aabb ___fieldAabb_default_value = new global::Orblit.Aabb(10f, 10f, 10f, new global::Orblit.Vector3(1f, 1f, 1f));
        values.Add(PropertyName.@_fieldAabb, global::Orblit.Variant.From<global::Orblit.Aabb>(___fieldAabb_default_value));
        global::Orblit.Color ___fieldColor_default_value = global::Orblit.Colors.Aquamarine;
        values.Add(PropertyName.@_fieldColor, global::Orblit.Variant.From<global::Orblit.Color>(___fieldColor_default_value));
        global::Orblit.Plane ___fieldPlane_default_value = global::Orblit.Plane.PlaneXZ;
        values.Add(PropertyName.@_fieldPlane, global::Orblit.Variant.From<global::Orblit.Plane>(___fieldPlane_default_value));
        global::Orblit.Callable ___fieldCallable_default_value = new global::Orblit.Callable(global::Orblit.Engine.GetMainLoop(), "_frame");
        values.Add(PropertyName.@_fieldCallable, global::Orblit.Variant.From<global::Orblit.Callable>(___fieldCallable_default_value));
        global::Orblit.Signal ___fieldSignal_default_value = new global::Orblit.Signal(global::Orblit.Engine.GetMainLoop(), "property_list_changed");
        values.Add(PropertyName.@_fieldSignal, global::Orblit.Variant.From<global::Orblit.Signal>(___fieldSignal_default_value));
        global::ExportedFields.MyEnum ___fieldEnum_default_value = global::ExportedFields.MyEnum.C;
        values.Add(PropertyName.@_fieldEnum, global::Orblit.Variant.From<global::ExportedFields.MyEnum>(___fieldEnum_default_value));
        global::ExportedFields.MyFlagsEnum ___fieldFlagsEnum_default_value = global::ExportedFields.MyFlagsEnum.C;
        values.Add(PropertyName.@_fieldFlagsEnum, global::Orblit.Variant.From<global::ExportedFields.MyFlagsEnum>(___fieldFlagsEnum_default_value));
        byte[] ___fieldByteArray_default_value = { 0, 1, 2, 3, 4, 5, 6  };
        values.Add(PropertyName.@_fieldByteArray, global::Orblit.Variant.From<byte[]>(___fieldByteArray_default_value));
        int[] ___fieldInt32Array_default_value = { 0, 1, 2, 3, 4, 5, 6  };
        values.Add(PropertyName.@_fieldInt32Array, global::Orblit.Variant.From<int[]>(___fieldInt32Array_default_value));
        long[] ___fieldInt64Array_default_value = { 0, 1, 2, 3, 4, 5, 6  };
        values.Add(PropertyName.@_fieldInt64Array, global::Orblit.Variant.From<long[]>(___fieldInt64Array_default_value));
        float[] ___fieldSingleArray_default_value = { 0f, 1f, 2f, 3f, 4f, 5f, 6f  };
        values.Add(PropertyName.@_fieldSingleArray, global::Orblit.Variant.From<float[]>(___fieldSingleArray_default_value));
        double[] ___fieldDoubleArray_default_value = { 0d, 1d, 2d, 3d, 4d, 5d, 6d  };
        values.Add(PropertyName.@_fieldDoubleArray, global::Orblit.Variant.From<double[]>(___fieldDoubleArray_default_value));
        string[] ___fieldStringArray_default_value = { "foo", "bar"  };
        values.Add(PropertyName.@_fieldStringArray, global::Orblit.Variant.From<string[]>(___fieldStringArray_default_value));
        string[] ___fieldStringArrayEnum_default_value = { "foo", "bar"  };
        values.Add(PropertyName.@_fieldStringArrayEnum, global::Orblit.Variant.From<string[]>(___fieldStringArrayEnum_default_value));
        global::Orblit.Vector2[] ___fieldVector2Array_default_value = { global::Orblit.Vector2.Up, global::Orblit.Vector2.Down, global::Orblit.Vector2.Left, global::Orblit.Vector2.Right   };
        values.Add(PropertyName.@_fieldVector2Array, global::Orblit.Variant.From<global::Orblit.Vector2[]>(___fieldVector2Array_default_value));
        global::Orblit.Vector3[] ___fieldVector3Array_default_value = { global::Orblit.Vector3.Up, global::Orblit.Vector3.Down, global::Orblit.Vector3.Left, global::Orblit.Vector3.Right   };
        values.Add(PropertyName.@_fieldVector3Array, global::Orblit.Variant.From<global::Orblit.Vector3[]>(___fieldVector3Array_default_value));
        global::Orblit.Color[] ___fieldColorArray_default_value = { global::Orblit.Colors.Aqua, global::Orblit.Colors.Aquamarine, global::Orblit.Colors.Azure, global::Orblit.Colors.Beige   };
        values.Add(PropertyName.@_fieldColorArray, global::Orblit.Variant.From<global::Orblit.Color[]>(___fieldColorArray_default_value));
        global::Orblit.OrblitObject[] ___fieldOrblitObjectOrDerivedArray_default_value = { null  };
        values.Add(PropertyName.@_fieldOrblitObjectOrDerivedArray, global::Orblit.Variant.CreateFrom(___fieldOrblitObjectOrDerivedArray_default_value));
        global::Orblit.StringName[] ___fieldStringNameArray_default_value = { "foo", "bar"  };
        values.Add(PropertyName.@_fieldStringNameArray, global::Orblit.Variant.From<global::Orblit.StringName[]>(___fieldStringNameArray_default_value));
        global::Orblit.EntityPath[] ___fieldEntityPathArray_default_value = { "foo", "bar"  };
        values.Add(PropertyName.@_fieldEntityPathArray, global::Orblit.Variant.From<global::Orblit.EntityPath[]>(___fieldEntityPathArray_default_value));
        global::Orblit.Rid[] ___fieldRidArray_default_value = { default, default, default  };
        values.Add(PropertyName.@_fieldRidArray, global::Orblit.Variant.From<global::Orblit.Rid[]>(___fieldRidArray_default_value));
        int[] ___fieldEmptyInt32Array_default_value = global::System.Array.Empty<int>();
        values.Add(PropertyName.@_fieldEmptyInt32Array, global::Orblit.Variant.From<int[]>(___fieldEmptyInt32Array_default_value));
        int[] ___fieldArrayFromList_default_value = new global::System.Collections.Generic.List<int>(global::System.Array.Empty<int>()).ToArray();
        values.Add(PropertyName.@_fieldArrayFromList, global::Orblit.Variant.From<int[]>(___fieldArrayFromList_default_value));
        global::Orblit.Variant ___fieldVariant_default_value = "foo";
        values.Add(PropertyName.@_fieldVariant, global::Orblit.Variant.From<global::Orblit.Variant>(___fieldVariant_default_value));
        global::Orblit.OrblitObject ___fieldOrblitObjectOrDerived_default_value = default;
        values.Add(PropertyName.@_fieldOrblitObjectOrDerived, global::Orblit.Variant.From<global::Orblit.OrblitObject>(___fieldOrblitObjectOrDerived_default_value));
        global::Orblit.Texture ___fieldOrblitResourceTexture_default_value = default;
        values.Add(PropertyName.@_fieldOrblitResourceTexture, global::Orblit.Variant.From<global::Orblit.Texture>(___fieldOrblitResourceTexture_default_value));
        global::Orblit.Texture ___fieldOrblitResourceTextureWithInitializer_default_value = new()  { ResourceName  = ""   };
        values.Add(PropertyName.@_fieldOrblitResourceTextureWithInitializer, global::Orblit.Variant.From<global::Orblit.Texture>(___fieldOrblitResourceTextureWithInitializer_default_value));
        global::Orblit.StringName ___fieldStringName_default_value = new global::Orblit.StringName("foo");
        values.Add(PropertyName.@_fieldStringName, global::Orblit.Variant.From<global::Orblit.StringName>(___fieldStringName_default_value));
        global::Orblit.EntityPath ___fieldEntityPath_default_value = new global::Orblit.EntityPath("foo");
        values.Add(PropertyName.@_fieldEntityPath, global::Orblit.Variant.From<global::Orblit.EntityPath>(___fieldEntityPath_default_value));
        global::Orblit.Rid ___fieldRid_default_value = default;
        values.Add(PropertyName.@_fieldRid, global::Orblit.Variant.From<global::Orblit.Rid>(___fieldRid_default_value));
        global::Orblit.Collections.Dictionary ___fieldOrblitDictionary_default_value = new()  { { "foo", 10  }, { global::Orblit.Vector2.Up, global::Orblit.Colors.Chocolate   }  };
        values.Add(PropertyName.@_fieldOrblitDictionary, global::Orblit.Variant.From<global::Orblit.Collections.Dictionary>(___fieldOrblitDictionary_default_value));
        global::Orblit.Collections.Array ___fieldOrblitArray_default_value = new()  { "foo", 10, global::Orblit.Vector2.Up, global::Orblit.Colors.Chocolate   };
        values.Add(PropertyName.@_fieldOrblitArray, global::Orblit.Variant.From<global::Orblit.Collections.Array>(___fieldOrblitArray_default_value));
        global::Orblit.Collections.Dictionary<string, bool> ___fieldOrblitGenericDictionary_default_value = new()  { { "foo", true  }, { "bar", false  }  };
        values.Add(PropertyName.@_fieldOrblitGenericDictionary, global::Orblit.Variant.CreateFrom(___fieldOrblitGenericDictionary_default_value));
        global::Orblit.Collections.Array<int> ___fieldOrblitGenericArray_default_value = new()  { 0, 1, 2, 3, 4, 5, 6  };
        values.Add(PropertyName.@_fieldOrblitGenericArray, global::Orblit.Variant.CreateFrom(___fieldOrblitGenericArray_default_value));
        long[] ___fieldEmptyInt64Array_default_value = global::System.Array.Empty<long>();
        values.Add(PropertyName.@_fieldEmptyInt64Array, global::Orblit.Variant.From<long[]>(___fieldEmptyInt64Array_default_value));
        return values;
    }
#endif // TOOLS
#pragma warning restore CS0109
}
