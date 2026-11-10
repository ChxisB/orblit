using Orblit;

public partial class Generic<T> : OrblitObject
{
    private int _field;
}

// Generic again but different generic parameters
public partial class {|ORB0003:Generic|}<T, R> : OrblitObject
{
    private int _field;
}

// Generic again but without generic parameters
public partial class {|ORB0003:Generic|} : OrblitObject
{
    private int _field;
}
