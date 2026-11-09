namespace Orblit.SourceGenerators.Sample;

[GlobalClass]
public partial class CustomGlobalClass : OrblitObject
{
}

// This doesn't works because global classes can't have any generic type parameter.
/*
[GlobalClass]
public partial class CustomGlobalClass<T> : Entity
{
}
*/
