using System;

namespace Orblit.SourceGenerators.Sample;

public partial class NestedClass : OrblitObject
{
    public partial class NestedClass2 : OrblitObject
    {
        public partial class NestedClass3 : OrblitObject
        {
            [Signal]
            public delegate void MySignalEventHandler(string str, int num);

            [Export] private String _fieldString = "foo";
            [Export] private String PropertyString { get; set; } = "foo";

            private void Method()
            {
            }
        }
    }
}
