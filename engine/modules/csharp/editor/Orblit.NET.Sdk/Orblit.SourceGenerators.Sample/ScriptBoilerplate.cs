#pragma warning disable CS0169

namespace Orblit.SourceGenerators.Sample
{
    public partial class ScriptBoilerplate : Entity
    {
        private EntityPath _nodePath;
        private int _velocity;

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
        public partial class NesterClass : RefCounted
        {
            public override Variant _Get(StringName property) => default;
        }
    }
}
