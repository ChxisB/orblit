using Orblit;

public partial class ExportDiagnostics_ORB0102 : Entity
{
    public struct MyStruct { }

    [Export]
    public MyStruct {|ORB0102:StructField|};

    [Export]
    public MyStruct {|ORB0102:StructProperty|} { get; set; }
}
