using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;

namespace Orblit.Ui.Avalonia.Tests;

public class AutomationTests
{
    private static Border Square() => new() { Width = 100, Height = 100 };

    private static Control[] TouchControls() =>
    [
        new TouchStick(Square(), Brushes.White),
        new TouchHold(Square()),
        new TouchSwipe(Square()),
    ];

    private static AutomationPeer PeerOf(Control control)
    {
        TestWindow.Show(control);
        return ControlAutomationPeer.CreatePeerForElement(control);
    }

    [Fact]
    public Task EveryTouchControlIsFoundAsAControl() => Headless.Run(() =>
    {
        foreach (var control in TouchControls())
        {
            var peer = PeerOf(control);

            Assert.IsNotType<NoneAutomationPeer>(peer);
            Assert.True(peer.IsControlElement(), control.GetType().Name);
            Assert.True(peer.IsContentElement(), control.GetType().Name);
        }
    });

    [Fact]
    public Task ATouchControlClaimsNoRoleItCannotKeep() => Headless.Run(() =>
    {
        // A button promises a press from the keyboard or the reader, and a touch control takes a finger only.
        foreach (var control in TouchControls())
        {
            Assert.Equal(AutomationControlType.Custom, PeerOf(control).GetAutomationControlType());
        }
    });

    [Fact]
    public Task TheNameTheGameGivesIsTheNameItIsFoundBy() => Headless.Run(() =>
    {
        foreach (var control in TouchControls())
        {
            AutomationProperties.SetName(control, "Move");

            Assert.Equal("Move", PeerOf(control).GetName());
        }
    });

    [Fact]
    public Task AFlexPanelAddsNothingAndItsChildrenStayReachable() => Headless.Run(() =>
    {
        var flex = new FlexPanel { Children = { new TextBlock { Text = "Score" }, new Button { Content = "Retry" } } };

        var peer = PeerOf(flex);

        Assert.False(peer.IsControlElement());
        Assert.Equal(["Score", "Retry"], peer.GetChildren().Select(child => child.GetName()));
    });
}
