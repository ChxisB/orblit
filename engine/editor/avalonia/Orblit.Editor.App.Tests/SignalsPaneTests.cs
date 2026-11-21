using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class SignalsPaneTests
{
    static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static string[] Texts(SignalsPane pane) =>
        [.. pane.List.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text ?? "").Where(text => text.Length > 0)];

    static Button ButtonOf(SignalsPane pane, string tip) =>
        pane.List.GetLogicalDescendants().OfType<Button>().Single(button => (string?)ToolTip.GetTip(button) == tip);

    static (EditorWindow Window, FakeScene Scene) Lamp()
    {
        var scene = new FakeScene();
        var window = OpenAtReferenceSize(scene);
        window.Outline.SelectedItem = FakeScene.Lamp;
        return (window, scene);
    }

    [Fact]
    public void Each_class_bar_holds_the_signals_it_declares_and_a_script_comes_first()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.Scripted[FakeScene.Lamp.Handle] = "res://spin.cs";
            var window = OpenAtReferenceSize(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;

            Assert.Equal(["spin.cs", "hit(damage)", "Entity3D", "visibility_changed()", "Entity", "child_entered_stage(entity)", "renamed()"], Texts(window.Signals));
        });
    }

    [Fact]
    public void A_class_with_no_signals_of_its_own_says_so()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            window.Outline.SelectedItem = FakeScene.Sound;

            Assert.Equal(["Audio", "No signals of its own.", "Entity", "child_entered_stage(entity)", "renamed()"], Texts(window.Signals));
        });
    }

    [Fact]
    public void Connect_offers_every_entity_and_a_method_named_for_the_signal_and_saves_the_link()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = Lamp();
            var pane = window.Signals;

            Click(ButtonOf(pane, "Connect visibility_changed to a method"));
            Assert.Equal(["Field", "Lamp", "Sound"], pane.TargetList.Shown.Select(choice => choice.Name));
            Assert.Equal("OnVisibilityChanged", pane.MethodBox.Text);
            pane.TargetList.SearchBox.Text = "Sound";
            Click(pane.Dialog.MainButton);

            Assert.Equal([(FakeScene.Lamp.Handle, "visibility_changed", FakeScene.Sound.Handle, "OnVisibilityChanged")], scene.Links);
            Assert.False(pane.Dialog.IsVisible);
            Assert.Equal("Connected visibility_changed to Sound.OnVisibilityChanged.", window.Status);
            Assert.Contains("Sound · OnVisibilityChanged", Texts(pane));
        });
    }

    [Fact]
    public void Connect_waits_for_a_method_name_that_is_an_identifier()
    {
        UiThread.Run(() =>
        {
            var (window, _) = Lamp();
            var pane = window.Signals;
            Click(ButtonOf(pane, "Connect renamed to a method"));
            Assert.True(pane.Dialog.MainButton.IsEnabled);

            pane.MethodBox.Text = "On Renamed";
            Assert.False(pane.Dialog.MainButton.IsEnabled);
        });
    }

    [Fact]
    public void A_connection_the_scene_refuses_keeps_the_dialog_and_says_why()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = Lamp();
            var pane = window.Signals;
            scene.Links.Add((FakeScene.Lamp.Handle, "renamed", FakeScene.Field.Handle, "OnRenamed"));

            Click(ButtonOf(pane, "Connect renamed to a method"));
            Click(pane.Dialog.MainButton);

            Assert.True(pane.Dialog.IsVisible);
            Assert.Equal("The engine could not connect renamed to OnRenamed, error 22.", window.Status);
        });
    }

    [Fact]
    public void The_cross_on_a_link_disconnects_it()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.Links.Add((FakeScene.Lamp.Handle, "renamed", FakeScene.Sound.Handle, "OnRenamed"));
            var window = OpenAtReferenceSize(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;

            Click(ButtonOf(window.Signals, "Disconnect renamed from Sound.OnRenamed"));

            Assert.Empty(scene.Links);
            Assert.DoesNotContain("Sound · OnRenamed", Texts(window.Signals));
            Assert.Equal("Disconnected renamed from Sound.OnRenamed.", window.Status);
        });
    }

    [Fact]
    public void A_filter_keeps_the_matching_signals_and_their_bars()
    {
        UiThread.Run(() =>
        {
            var (window, _) = Lamp();

            window.Signals.FilterBox.Text = "VISIB";

            Assert.Equal(["Entity3D", "visibility_changed()"], Texts(window.Signals));
        });
    }

    [Theory]
    [InlineData("visibility_changed", "OnVisibilityChanged")]
    [InlineData("renamed", "OnRenamed")]
    [InlineData("_ready", "OnReady")]
    public void The_offered_method_is_On_and_the_signal_in_Pascal_case(string signal, string method) =>
        Assert.Equal(method, SignalsPane.MethodFor(signal));
}
