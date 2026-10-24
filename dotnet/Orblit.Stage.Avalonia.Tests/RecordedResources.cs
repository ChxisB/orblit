namespace Orblit.Stage.Avalonia.Tests;

// Stands in for the renderer's resource store, so a test sees what was handed over and in what order.
internal sealed class RecordedResources
{
    public List<(string Method, string Name)> Calls { get; } = [];

    public IEnumerable<string> Provided => Named("provide");

    public IEnumerable<string> Released => Named("release");

    public void Provide(string name, byte[] bytes) => Calls.Add(("provide", name));

    public bool Release(string name)
    {
        Calls.Add(("release", name));
        return true;
    }

    private IEnumerable<string> Named(string method) => Calls.Where(call => call.Method == method).Select(call => call.Name);
}
