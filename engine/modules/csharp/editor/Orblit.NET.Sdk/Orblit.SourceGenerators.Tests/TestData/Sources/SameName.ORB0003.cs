using Orblit;

namespace NamespaceA
{
    partial class SameName : OrblitObject
    {
        private int _field;
    }
}

// SameName again but different namespace
namespace NamespaceB
{
    partial class {|ORB0003:SameName|} : OrblitObject
    {
        private int _field;
    }
}
