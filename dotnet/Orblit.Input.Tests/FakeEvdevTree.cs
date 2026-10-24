namespace Orblit.Input.Tests;

/// <summary>
/// A sysfs tree and a device directory in a temporary folder. Ordinary files stand in for event
/// nodes, so the scan can be checked without a kernel that will invent a pad. Reading one yields
/// nothing and its ioctls fail, which is all the scan itself needs.
/// </summary>
internal sealed class FakeEvdevTree : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("orblit-evdev-").FullName;

    public FakeEvdevTree()
    {
        Directory.CreateDirectory(Devices);
        Directory.CreateDirectory(Sysfs);
    }

    public string Devices => Path.Combine(_root, "dev");

    public string Sysfs => Path.Combine(_root, "sys");

    public void AddPad(string node, string name)
    {
        AddPadWithoutNode(node, name);
        File.WriteAllBytes(Path.Combine(Devices, node), []);
    }

    /// <summary>A description whose device node cannot be opened, as when a user is not in the <c>input</c> group.</summary>
    public void AddPadWithoutNode(string node, string name) => Describe(node, name, isPad: true);

    public void AddKeyboard(string node)
    {
        Describe(node, "A Keyboard", isPad: false);
        File.WriteAllBytes(Path.Combine(Devices, node), []);
    }

    /// <summary>An entry that is not an event node, such as <c>js0</c> or <c>mouse0</c>.</summary>
    public void AddOther(string entry) => Directory.CreateDirectory(Path.Combine(Sysfs, entry));

    public void Remove(string node) => Directory.Delete(Path.Combine(Sysfs, node), recursive: true);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Describe(string node, string name, bool isPad)
    {
        var device = Path.Combine(Sysfs, node, "device");
        Directory.CreateDirectory(Path.Combine(device, "capabilities"));
        Directory.CreateDirectory(Path.Combine(device, "id"));

        // A pad has the gamepad buttons and two axes. A keyboard has keys and no axes.
        File.WriteAllText(Path.Combine(device, "capabilities", "ev"), isPad ? "b" : "120013");
        File.WriteAllText(Path.Combine(device, "capabilities", "key"), isPad ? "3000000000000 0 0 0 0" : "10000000000 0 0 0");
        File.WriteAllText(Path.Combine(device, "capabilities", "abs"), isPad ? "3" : "");
        File.WriteAllText(Path.Combine(device, "name"), name);
        File.WriteAllText(Path.Combine(device, "id", "vendor"), "28de");
        File.WriteAllText(Path.Combine(device, "id", "product"), "1205");
        File.WriteAllText(Path.Combine(device, "id", "version"), "0111");
        File.WriteAllText(Path.Combine(device, "id", "bustype"), "0003");
    }
}
