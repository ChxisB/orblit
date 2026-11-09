using Orblit;

public partial class EventSignals : OrblitObject
{
    [Signal]
    public delegate void MySignalEventHandler(string str, int num);

    private struct MyStruct { }

    [Signal]
    private delegate void {|ORB0201:MyInvalidSignal|}();

    [Signal]
    private delegate void MyInvalidParameterTypeSignalEventHandler(MyStruct {|ORB0202:myStruct|});

    [Signal]
    private delegate MyStruct {|ORB0203:MyInvalidReturnTypeSignalEventHandler|}();
}
