using Orblit;
using Orblit.NativeInterop;

partial class GenericClass<T>
{
partial class NestedClass
{
#pragma warning disable CS0109 // Disable warning about redundant 'new' keyword
    /// <summary>
    /// Cached StringNames for the methods contained in this class, for fast lookup.
    /// </summary>
    public new class MethodName : global::Orblit.OrblitObject.MethodName {
    }
    protected internal new static partial class OrblitInternal
    {
        public static void GetOrblitMethodTrampolines(global::Orblit.Bridge.MethodTrampolineCollector collector)
        {
        }
    }
#pragma warning restore CS0109
}
}
