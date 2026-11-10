using Orblit;

public class {|ORB0002:OuterOuterClassPartialModifierAnalyzer|}
{
    public class {|ORB0002:OuterClassPartialModifierAnalyzer|}
    {
        // MyNode is contained in a non-partial type so the source generators
        // can't enhance this type to work with Orblit.
        public partial class MyNode : Entity { }
    }
}
