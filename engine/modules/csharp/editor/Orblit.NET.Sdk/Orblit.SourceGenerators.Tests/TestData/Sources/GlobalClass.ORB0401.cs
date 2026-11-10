using Orblit;

// This works because it inherits from OrblitObject.
[GlobalClass]
public partial class CustomGlobalClass1 : OrblitObject
{

}

// This works because it inherits from an object that inherits from OrblitObject
[GlobalClass]
public partial class CustomGlobalClass2 : Entity
{

}

// This raises a ORB0401 diagnostic error: global classes must inherit from OrblitObject
[GlobalClass]
public partial class {|ORB0401:CustomGlobalClass3|}
{

}
