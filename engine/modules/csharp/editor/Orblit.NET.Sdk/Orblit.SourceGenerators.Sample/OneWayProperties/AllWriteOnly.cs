using System;

namespace Orblit.SourceGenerators.Sample
{
    public partial class AllWriteOnly : OrblitObject
    {
        private bool _writeOnlyBackingField = false;
        public bool WriteOnlyProperty { set => _writeOnlyBackingField = value; }
    }
}
