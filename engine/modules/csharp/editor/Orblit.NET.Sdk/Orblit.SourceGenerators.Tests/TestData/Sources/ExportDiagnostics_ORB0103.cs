using Orblit;

public partial class ExportDiagnostics_ORB0103 : Entity
{
    [Export]
    public readonly string {|ORB0103:ReadOnlyField|};

    [Export]
    public string {|ORB0103:ReadOnlyProperty|} { get; }
}
