using Avalonia;
using Orblit.Stage.Avalonia;

namespace Orblit.Stage.Avalonia.Tests;

// Stands in for an interface panel: a size, a picture that changes on demand, and a record of every pointer
// it was sent.
internal sealed class FakePanel(Size? size = null) : IPanel
{
    private byte _picture;

    public Size Size { get; } = size ?? new Size(200, 100);

    public bool IsDirty { get; private set; } = true;

    public List<PanelPointer> Sent { get; } = [];

    public void Change() => IsDirty = true;

    public byte[] Paint()
    {
        IsDirty = false;
        return [_picture++];
    }

    public void Send(PanelPointer input) => Sent.Add(input);
}
