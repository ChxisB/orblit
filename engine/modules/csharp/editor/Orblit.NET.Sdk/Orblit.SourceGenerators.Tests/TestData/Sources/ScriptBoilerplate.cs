using Orblit;

public partial class ScriptBoilerplate : Entity
{
    private EntityPath _nodePath;
    private int _velocity;

    [IgnoreMember]
    private int _ignoredField;

    [IgnoreMember]
    private int IgnoredProperty { get; set; }

    public override void OnFrame(double delta)
    {
        _ = delta;

        base.OnFrame(delta);
    }

    public int Bazz(StringName name)
    {
        _ = name;
        return 1;
    }

    public void IgnoreThisMethodWithByRefParams(ref int a)
    {
        _ = a;
    }
}

public partial struct OuterClass
{
    public partial class NestedClass : RefCounted
    {
        public override Variant _Get(StringName property) => default;
    }
}
