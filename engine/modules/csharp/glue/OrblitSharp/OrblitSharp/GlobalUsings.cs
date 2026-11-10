#if REAL_T_IS_DOUBLE
global using real_t = System.Double;
#else
global using real_t = System.Single;
#endif

global using unsafe MethodTrampolineDelegate = delegate* managed<
    /* orblitObject: */ object,
    /* args: */ Orblit.NativeInterop.NativeVariantPtrArgs,
    /* callError: */ ref Orblit.NativeInterop.orblit_variant_call_error,
    /* return (value): */ Orblit.NativeInterop.orblit_variant>;
global using unsafe PropertyGetterTrampolineDelegate = delegate* managed<
    /* orblitObject: */ object,
    /* return (value): */ Orblit.NativeInterop.orblit_variant>;
global using unsafe PropertySetterTrampolineDelegate = delegate* managed<
    /* orblitObject: */ object,
    /* value: */ in Orblit.NativeInterop.orblit_variant,
    /* return: */ void>;
global using unsafe RaiseSignalTrampolineDelegate = delegate* managed<
    /* orblitObject: */ object,
    /* args: */ Orblit.NativeInterop.NativeVariantPtrArgs,
    /* callError: */ ref Orblit.NativeInterop.orblit_variant_call_error,
    /* return */ void>;

global using unsafe TryAddMethodTrampolineDelegate = delegate* unmanaged<
    /* scriptPtr: */ System.IntPtr,
    /* name: */ Orblit.NativeInterop.orblit_string_name*,
    /* argCount: */ int,
    /* trampolineDelegate: */ void*,
    /* isStatic: */ Orblit.NativeInterop.orblit_bool,
    /* return */ void>;
global using unsafe TryAddPropertyTrampolineDelegate = delegate* unmanaged<
    /* scriptPtr: */ System.IntPtr,
    /* name: */ Orblit.NativeInterop.orblit_string_name*,
    /* getterTrampolineDelegate: */ void*,
    /* setterTrampolineDelegate: */ void*,
    /* return */ void>;
global using unsafe TryAddRaiseSignalTrampolineDelegate = delegate* unmanaged<
    /* scriptPtr: */ System.IntPtr,
    /* name: */ Orblit.NativeInterop.orblit_string_name*,
    /* argCount: */ int,
    /* trampolineDelegate: */ void*,
    /* return */ void>;
