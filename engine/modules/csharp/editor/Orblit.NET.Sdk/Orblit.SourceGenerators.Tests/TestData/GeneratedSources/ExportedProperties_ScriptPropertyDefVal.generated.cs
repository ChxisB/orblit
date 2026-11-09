partial class ExportedProperties
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
        var values = new global::System.Collections.Generic.Dictionary<global::Orblit.StringName, global::Orblit.Variant>(71);
        string __NotGenerateComplexLamdaProperty_default_value = default;
        values.Add(PropertyName.@NotGenerateComplexLamdaProperty, global::Orblit.Variant.From<string>(__NotGenerateComplexLamdaProperty_default_value));
        string __NotGenerateLamdaNoFieldProperty_default_value = default;
        values.Add(PropertyName.@NotGenerateLamdaNoFieldProperty, global::Orblit.Variant.From<string>(__NotGenerateLamdaNoFieldProperty_default_value));
        string __NotGenerateComplexReturnProperty_default_value = default;
        values.Add(PropertyName.@NotGenerateComplexReturnProperty, global::Orblit.Variant.From<string>(__NotGenerateComplexReturnProperty_default_value));
        string __NotGenerateReturnsProperty_default_value = default;
        values.Add(PropertyName.@NotGenerateReturnsProperty, global::Orblit.Variant.From<string>(__NotGenerateReturnsProperty_default_value));
        string __FullPropertyString_default_value = "FullPropertyString";
        values.Add(PropertyName.@FullPropertyString, global::Orblit.Variant.From<string>(__FullPropertyString_default_value));
        string __FullPropertyString_Complex_default_value = new string("FullPropertyString_Complex")   + global::System.Convert.ToInt32("1");
        values.Add(PropertyName.@FullPropertyString_Complex, global::Orblit.Variant.From<string>(__FullPropertyString_Complex_default_value));
        float __FullPropertyStaticImport_default_value = global::Orblit.Mathf.Pi;
        values.Add(PropertyName.@FullPropertyStaticImport, global::Orblit.Variant.From<float>(__FullPropertyStaticImport_default_value));
        string __LamdaPropertyString_default_value = "LamdaPropertyString";
        values.Add(PropertyName.@LamdaPropertyString, global::Orblit.Variant.From<string>(__LamdaPropertyString_default_value));
        float __LambdaPropertyStaticImport_default_value = global::Orblit.Mathf.Tau;
        values.Add(PropertyName.@LambdaPropertyStaticImport, global::Orblit.Variant.From<float>(__LambdaPropertyStaticImport_default_value));
        string __PrimaryCtorParameter_default_value = default;
        values.Add(PropertyName.@PrimaryCtorParameter, global::Orblit.Variant.From<string>(__PrimaryCtorParameter_default_value));
        float __ConstantMath_default_value = 2  * global::Orblit.Mathf.Pi;
        values.Add(PropertyName.@ConstantMath, global::Orblit.Variant.From<float>(__ConstantMath_default_value));
        float __ConstantMathStaticImport_default_value = global::Orblit.Mathf.RadToDeg(2  * global::Orblit.Mathf.Pi);
        values.Add(PropertyName.@ConstantMathStaticImport, global::Orblit.Variant.From<float>(__ConstantMathStaticImport_default_value));
        string __StaticStringAddition_default_value = string.Empty   + string.Empty;
        values.Add(PropertyName.@StaticStringAddition, global::Orblit.Variant.From<string>(__StaticStringAddition_default_value));
        bool __PropertyBoolean_default_value = true;
        values.Add(PropertyName.@PropertyBoolean, global::Orblit.Variant.From<bool>(__PropertyBoolean_default_value));
        char __PropertyChar_default_value = 'f';
        values.Add(PropertyName.@PropertyChar, global::Orblit.Variant.From<char>(__PropertyChar_default_value));
        sbyte __PropertySByte_default_value = 10;
        values.Add(PropertyName.@PropertySByte, global::Orblit.Variant.From<sbyte>(__PropertySByte_default_value));
        short __PropertyInt16_default_value = 10;
        values.Add(PropertyName.@PropertyInt16, global::Orblit.Variant.From<short>(__PropertyInt16_default_value));
        int __PropertyInt32_default_value = 10;
        values.Add(PropertyName.@PropertyInt32, global::Orblit.Variant.From<int>(__PropertyInt32_default_value));
        long __PropertyInt64_default_value = -10_000;
        values.Add(PropertyName.@PropertyInt64, global::Orblit.Variant.From<long>(__PropertyInt64_default_value));
        byte __PropertyByte_default_value = 10;
        values.Add(PropertyName.@PropertyByte, global::Orblit.Variant.From<byte>(__PropertyByte_default_value));
        ushort __PropertyUInt16_default_value = 10;
        values.Add(PropertyName.@PropertyUInt16, global::Orblit.Variant.From<ushort>(__PropertyUInt16_default_value));
        uint __PropertyUInt32_default_value = 10;
        values.Add(PropertyName.@PropertyUInt32, global::Orblit.Variant.From<uint>(__PropertyUInt32_default_value));
        ulong __PropertyUInt64_default_value = 10;
        values.Add(PropertyName.@PropertyUInt64, global::Orblit.Variant.From<ulong>(__PropertyUInt64_default_value));
        float __PropertySingle_default_value = 10;
        values.Add(PropertyName.@PropertySingle, global::Orblit.Variant.From<float>(__PropertySingle_default_value));
        double __PropertyDouble_default_value = 10;
        values.Add(PropertyName.@PropertyDouble, global::Orblit.Variant.From<double>(__PropertyDouble_default_value));
        string __PropertyString_default_value = "foo";
        values.Add(PropertyName.@PropertyString, global::Orblit.Variant.From<string>(__PropertyString_default_value));
        global::Orblit.Vector2 __PropertyVector2_default_value = new(10f, 10f);
        values.Add(PropertyName.@PropertyVector2, global::Orblit.Variant.From<global::Orblit.Vector2>(__PropertyVector2_default_value));
        global::Orblit.Vector2I __PropertyVector2I_default_value = global::Orblit.Vector2I.Up;
        values.Add(PropertyName.@PropertyVector2I, global::Orblit.Variant.From<global::Orblit.Vector2I>(__PropertyVector2I_default_value));
        global::Orblit.Rect2 __PropertyRect2_default_value = new(new global::Orblit.Vector2(10f, 10f), new global::Orblit.Vector2(10f, 10f));
        values.Add(PropertyName.@PropertyRect2, global::Orblit.Variant.From<global::Orblit.Rect2>(__PropertyRect2_default_value));
        global::Orblit.Rect2I __PropertyRect2I_default_value = new(new global::Orblit.Vector2I(10, 10), new global::Orblit.Vector2I(10, 10));
        values.Add(PropertyName.@PropertyRect2I, global::Orblit.Variant.From<global::Orblit.Rect2I>(__PropertyRect2I_default_value));
        global::Orblit.Transform2D __PropertyTransform2D_default_value = global::Orblit.Transform2D.Identity;
        values.Add(PropertyName.@PropertyTransform2D, global::Orblit.Variant.From<global::Orblit.Transform2D>(__PropertyTransform2D_default_value));
        global::Orblit.Vector3 __PropertyVector3_default_value = new(10f, 10f, 10f);
        values.Add(PropertyName.@PropertyVector3, global::Orblit.Variant.From<global::Orblit.Vector3>(__PropertyVector3_default_value));
        global::Orblit.Vector3I __PropertyVector3I_default_value = global::Orblit.Vector3I.Back;
        values.Add(PropertyName.@PropertyVector3I, global::Orblit.Variant.From<global::Orblit.Vector3I>(__PropertyVector3I_default_value));
        global::Orblit.Basis __PropertyBasis_default_value = new global::Orblit.Basis(global::Orblit.Quaternion.Identity);
        values.Add(PropertyName.@PropertyBasis, global::Orblit.Variant.From<global::Orblit.Basis>(__PropertyBasis_default_value));
        global::Orblit.Quaternion __PropertyQuaternion_default_value = new global::Orblit.Quaternion(global::Orblit.Basis.Identity);
        values.Add(PropertyName.@PropertyQuaternion, global::Orblit.Variant.From<global::Orblit.Quaternion>(__PropertyQuaternion_default_value));
        global::Orblit.Transform3D __PropertyTransform3D_default_value = global::Orblit.Transform3D.Identity;
        values.Add(PropertyName.@PropertyTransform3D, global::Orblit.Variant.From<global::Orblit.Transform3D>(__PropertyTransform3D_default_value));
        global::Orblit.Vector4 __PropertyVector4_default_value = new(10f, 10f, 10f, 10f);
        values.Add(PropertyName.@PropertyVector4, global::Orblit.Variant.From<global::Orblit.Vector4>(__PropertyVector4_default_value));
        global::Orblit.Vector4I __PropertyVector4I_default_value = global::Orblit.Vector4I.One;
        values.Add(PropertyName.@PropertyVector4I, global::Orblit.Variant.From<global::Orblit.Vector4I>(__PropertyVector4I_default_value));
        global::Orblit.Projection __PropertyProjection_default_value = global::Orblit.Projection.Identity;
        values.Add(PropertyName.@PropertyProjection, global::Orblit.Variant.From<global::Orblit.Projection>(__PropertyProjection_default_value));
        global::Orblit.Aabb __PropertyAabb_default_value = new global::Orblit.Aabb(10f, 10f, 10f, new global::Orblit.Vector3(1f, 1f, 1f));
        values.Add(PropertyName.@PropertyAabb, global::Orblit.Variant.From<global::Orblit.Aabb>(__PropertyAabb_default_value));
        global::Orblit.Color __PropertyColor_default_value = global::Orblit.Colors.Aquamarine;
        values.Add(PropertyName.@PropertyColor, global::Orblit.Variant.From<global::Orblit.Color>(__PropertyColor_default_value));
        global::Orblit.Plane __PropertyPlane_default_value = global::Orblit.Plane.PlaneXZ;
        values.Add(PropertyName.@PropertyPlane, global::Orblit.Variant.From<global::Orblit.Plane>(__PropertyPlane_default_value));
        global::Orblit.Callable __PropertyCallable_default_value = new global::Orblit.Callable(global::Orblit.Engine.GetMainLoop(), "_frame");
        values.Add(PropertyName.@PropertyCallable, global::Orblit.Variant.From<global::Orblit.Callable>(__PropertyCallable_default_value));
        global::Orblit.Signal __PropertySignal_default_value = new global::Orblit.Signal(global::Orblit.Engine.GetMainLoop(), "Propertylist_changed");
        values.Add(PropertyName.@PropertySignal, global::Orblit.Variant.From<global::Orblit.Signal>(__PropertySignal_default_value));
        global::ExportedProperties.MyEnum __PropertyEnum_default_value = global::ExportedProperties.MyEnum.C;
        values.Add(PropertyName.@PropertyEnum, global::Orblit.Variant.From<global::ExportedProperties.MyEnum>(__PropertyEnum_default_value));
        global::ExportedProperties.MyFlagsEnum __PropertyFlagsEnum_default_value = global::ExportedProperties.MyFlagsEnum.C;
        values.Add(PropertyName.@PropertyFlagsEnum, global::Orblit.Variant.From<global::ExportedProperties.MyFlagsEnum>(__PropertyFlagsEnum_default_value));
        byte[] __PropertyByteArray_default_value = { 0, 1, 2, 3, 4, 5, 6  };
        values.Add(PropertyName.@PropertyByteArray, global::Orblit.Variant.From<byte[]>(__PropertyByteArray_default_value));
        int[] __PropertyInt32Array_default_value = { 0, 1, 2, 3, 4, 5, 6  };
        values.Add(PropertyName.@PropertyInt32Array, global::Orblit.Variant.From<int[]>(__PropertyInt32Array_default_value));
        long[] __PropertyInt64Array_default_value = { 0, 1, 2, 3, 4, 5, 6  };
        values.Add(PropertyName.@PropertyInt64Array, global::Orblit.Variant.From<long[]>(__PropertyInt64Array_default_value));
        float[] __PropertySingleArray_default_value = { 0f, 1f, 2f, 3f, 4f, 5f, 6f  };
        values.Add(PropertyName.@PropertySingleArray, global::Orblit.Variant.From<float[]>(__PropertySingleArray_default_value));
        double[] __PropertyDoubleArray_default_value = { 0d, 1d, 2d, 3d, 4d, 5d, 6d  };
        values.Add(PropertyName.@PropertyDoubleArray, global::Orblit.Variant.From<double[]>(__PropertyDoubleArray_default_value));
        string[] __PropertyStringArray_default_value = { "foo", "bar"  };
        values.Add(PropertyName.@PropertyStringArray, global::Orblit.Variant.From<string[]>(__PropertyStringArray_default_value));
        string[] __PropertyStringArrayEnum_default_value = { "foo", "bar"  };
        values.Add(PropertyName.@PropertyStringArrayEnum, global::Orblit.Variant.From<string[]>(__PropertyStringArrayEnum_default_value));
        global::Orblit.Vector2[] __PropertyVector2Array_default_value = { global::Orblit.Vector2.Up, global::Orblit.Vector2.Down, global::Orblit.Vector2.Left, global::Orblit.Vector2.Right   };
        values.Add(PropertyName.@PropertyVector2Array, global::Orblit.Variant.From<global::Orblit.Vector2[]>(__PropertyVector2Array_default_value));
        global::Orblit.Vector3[] __PropertyVector3Array_default_value = { global::Orblit.Vector3.Up, global::Orblit.Vector3.Down, global::Orblit.Vector3.Left, global::Orblit.Vector3.Right   };
        values.Add(PropertyName.@PropertyVector3Array, global::Orblit.Variant.From<global::Orblit.Vector3[]>(__PropertyVector3Array_default_value));
        global::Orblit.Color[] __PropertyColorArray_default_value = { global::Orblit.Colors.Aqua, global::Orblit.Colors.Aquamarine, global::Orblit.Colors.Azure, global::Orblit.Colors.Beige   };
        values.Add(PropertyName.@PropertyColorArray, global::Orblit.Variant.From<global::Orblit.Color[]>(__PropertyColorArray_default_value));
        global::Orblit.OrblitObject[] __PropertyOrblitObjectOrDerivedArray_default_value = { null  };
        values.Add(PropertyName.@PropertyOrblitObjectOrDerivedArray, global::Orblit.Variant.CreateFrom(__PropertyOrblitObjectOrDerivedArray_default_value));
        global::Orblit.StringName[] __field_StringNameArray_default_value = { "foo", "bar"  };
        values.Add(PropertyName.@field_StringNameArray, global::Orblit.Variant.From<global::Orblit.StringName[]>(__field_StringNameArray_default_value));
        global::Orblit.EntityPath[] __field_EntityPathArray_default_value = { "foo", "bar"  };
        values.Add(PropertyName.@field_EntityPathArray, global::Orblit.Variant.From<global::Orblit.EntityPath[]>(__field_EntityPathArray_default_value));
        global::Orblit.Rid[] __field_RidArray_default_value = { default, default, default  };
        values.Add(PropertyName.@field_RidArray, global::Orblit.Variant.From<global::Orblit.Rid[]>(__field_RidArray_default_value));
        global::Orblit.Variant __PropertyVariant_default_value = "foo";
        values.Add(PropertyName.@PropertyVariant, global::Orblit.Variant.From<global::Orblit.Variant>(__PropertyVariant_default_value));
        global::Orblit.OrblitObject __PropertyOrblitObjectOrDerived_default_value = default;
        values.Add(PropertyName.@PropertyOrblitObjectOrDerived, global::Orblit.Variant.From<global::Orblit.OrblitObject>(__PropertyOrblitObjectOrDerived_default_value));
        global::Orblit.Texture __PropertyOrblitResourceTexture_default_value = default;
        values.Add(PropertyName.@PropertyOrblitResourceTexture, global::Orblit.Variant.From<global::Orblit.Texture>(__PropertyOrblitResourceTexture_default_value));
        global::Orblit.Texture __PropertyOrblitResourceTextureWithInitializer_default_value = new()  { ResourceName  = ""   };
        values.Add(PropertyName.@PropertyOrblitResourceTextureWithInitializer, global::Orblit.Variant.From<global::Orblit.Texture>(__PropertyOrblitResourceTextureWithInitializer_default_value));
        global::Orblit.StringName __PropertyStringName_default_value = new global::Orblit.StringName("foo");
        values.Add(PropertyName.@PropertyStringName, global::Orblit.Variant.From<global::Orblit.StringName>(__PropertyStringName_default_value));
        global::Orblit.EntityPath __PropertyEntityPath_default_value = new global::Orblit.EntityPath("foo");
        values.Add(PropertyName.@PropertyEntityPath, global::Orblit.Variant.From<global::Orblit.EntityPath>(__PropertyEntityPath_default_value));
        global::Orblit.Rid __PropertyRid_default_value = default;
        values.Add(PropertyName.@PropertyRid, global::Orblit.Variant.From<global::Orblit.Rid>(__PropertyRid_default_value));
        global::Orblit.Collections.Dictionary __PropertyOrblitDictionary_default_value = new()  { { "foo", 10  }, { global::Orblit.Vector2.Up, global::Orblit.Colors.Chocolate   }  };
        values.Add(PropertyName.@PropertyOrblitDictionary, global::Orblit.Variant.From<global::Orblit.Collections.Dictionary>(__PropertyOrblitDictionary_default_value));
        global::Orblit.Collections.Array __PropertyOrblitArray_default_value = new()  { "foo", 10, global::Orblit.Vector2.Up, global::Orblit.Colors.Chocolate   };
        values.Add(PropertyName.@PropertyOrblitArray, global::Orblit.Variant.From<global::Orblit.Collections.Array>(__PropertyOrblitArray_default_value));
        global::Orblit.Collections.Dictionary<string, bool> __PropertyOrblitGenericDictionary_default_value = new()  { { "foo", true  }, { "bar", false  }  };
        values.Add(PropertyName.@PropertyOrblitGenericDictionary, global::Orblit.Variant.CreateFrom(__PropertyOrblitGenericDictionary_default_value));
        global::Orblit.Collections.Array<int> __PropertyOrblitGenericArray_default_value = new()  { 0, 1, 2, 3, 4, 5, 6  };
        values.Add(PropertyName.@PropertyOrblitGenericArray, global::Orblit.Variant.CreateFrom(__PropertyOrblitGenericArray_default_value));
        return values;
    }
#endif // TOOLS
#pragma warning restore CS0109
}
