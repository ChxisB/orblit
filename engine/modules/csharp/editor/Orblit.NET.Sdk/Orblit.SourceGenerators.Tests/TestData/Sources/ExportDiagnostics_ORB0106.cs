using Orblit;

public interface MyInterface
{
    public int MyProperty { get; set; }
}

public partial class ExportDiagnostics_ORB0106_OK : Entity, MyInterface
{
    [Export]
    public int MyProperty { get; set; }
}

public partial class ExportDiagnostics_ORB0106_KO : Entity, MyInterface
{
    [Export]
    int MyInterface.{|ORB0106:MyProperty|} { get; set; }
}
