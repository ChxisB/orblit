namespace Orblit.SourceGenerators.Sample;

public partial class EventSignals : OrblitObject
{
    [Signal]
    public delegate void MySignalEventHandler(string str, int num);
}
