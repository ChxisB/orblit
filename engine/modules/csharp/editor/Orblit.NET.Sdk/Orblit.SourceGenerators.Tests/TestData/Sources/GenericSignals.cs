using Orblit;

public partial class GenericSignals<T> : OrblitObject
{
    [Signal]
    public delegate void MySignalEventHandler(int num);
}
