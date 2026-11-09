using Orblit;

// This works because it inherits from OrblitObject and it doesn't have any generic type parameter.
[GlobalClass]
public partial class CustomGlobalClass : OrblitObject
{

}

// This raises a ORB0402 diagnostic error: global classes can't have any generic type parameter
[GlobalClass]
public partial class {|ORB0402:CustomGlobalClass|}<T> : OrblitObject
{

}
