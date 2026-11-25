using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class SignalsPaneTests
{
    static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    // A bar titled with its class name keeps the engine's line hidden, so only the shown text counts.
    static string[] Texts(SignalsPane pane) =>
        [.. pane.List.GetLogicalDescendants().OfType<TextBlock>().Where(text => text.IsVisible).Select(text => text.Text ?? "").Where(text => text.Length > 0)];

    // The row of a signal holds its engine name and its Connect… button side by side.
    static Button ConnectOf(SignalsPane pane, string signal) =>
        pane.List.GetLogicalDescendants().OfType<DockPanel>()
            .Single(row => row.Children.OfType<TextBlock>().Any(text => text.Text?.StartsWith(signal + "(", StringComparison.Ordinal) == true))
            .Children.OfType<Button>().First();

    static Button ButtonOf(SignalsPane pane, string line) =>
        pane.List.GetLogicalDescendants().OfType<Button>().Single(button => ToolTip.GetTip(button) is HoverCard card && card.Line == line);

    static HoverCard Card(Control control) => (HoverCard)ToolTip.GetTip(control)!;

    static (EditorWindow Window, FakeScene Scene) Lamp()
    {
        var scene = new FakeScene();
        var window = OpenAtReferenceSize(scene);
        window.Outline.SelectedItem = FakeScene.Lamp;
        return (window, scene);
    }

    [Fact]
    public void Each_bar_holds_the_signals_it_declares_in_plain_words_and_a_script_comes_first()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.Scripted[FakeScene.Lamp.Handle] = "res://spin.cs";
            var window = OpenAtReferenceSize(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;

            Assert.Equal(
                ["spin.cs", "hit(damage)", "From Lamp itself", "Entity3D", "Shown or hidden", "visibility_changed()", "From any thing", "Entity", "A child came in", "child_entered_stage(entity)", "Renamed", "renamed()"],
                Texts(window.Signals));
            Assert.Equal("Things Lamp can tell others when they happen. Connect one to run a method on another thing.", window.Signals.Lead.Text);
        });
    }

    [Fact]
    public void Nothing_selected_hides_the_lead_line()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene());

            Assert.False(window.Signals.Lead.IsVisible);
        });
    }

    [Fact]
    public void A_thing_with_no_signals_of_its_own_says_so_and_skips_empty_classes_under_it()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            window.Outline.SelectedItem = FakeScene.Sound;

            Assert.Equal(["From Sound itself", "Audio", "Sound has no signals of its own.", "From any thing", "Entity", "A child came in", "child_entered_stage(entity)", "Renamed", "renamed()"], Texts(window.Signals));
        });
    }

    [Fact]
    public void Connect_offers_every_entity_and_a_method_named_for_the_signal_and_saves_the_link()
    {
        UiThread.Run(() =>
        {
            var (window, scene) = Lamp();
            var pane = window.Signals;

            Click(ConnectOf(pane, "visibility_changed"));
            Assert.Equal(["Field", "Lamp", "Sound"], pane.TargetList.Shown.Select(choice => choice.Name));
            Assert.Equal("OnVisibilityChanged", pane.MethodBox.Text);
            pane.TargetList.SearchBox.Text = "Sound";
            Click(pane.Dialog.MainButton);

            Assert.Equal([(FakeScene.Lamp.Handle, "visibility_changed", FakeScene.Sound.Handle, "OnVisibilityChanged")], scene.Links);
            Assert.False(pane.Dialog.IsVisible);
            Assert.Equal("Connected visibility_changed to Sound.OnVisibilityChanged.", window.Status);
            Assert.Contains("Sound · OnVisibilityChanged", Texts(pane));
            Assert.Contains("1 joined", Texts(pane));
        });
    }

    [Fact]
    public void Connect_reads_as_a_sentence_and_names_the_kind_of_each_thing()
    {
        UiThread.Run(() =>
        {
            var (window, _) = Lamp();
            var pane = window.Signals;

            Assert.Equal(("Connect…", "Pick a thing to run a method on each time this happens."), (Card(ConnectOf(pane, "renamed")).Title, Card(ConnectOf(pane, "renamed")).Line));
            Click(ConnectOf(pane, "visibility_changed"));

            Assert.Equal("When Lamp is shown or hidden…", pane.Dialog.Title);
            Assert.Equal("Connect a Signal to a Method", pane.Dialog.EngineName);
            Assert.Equal("Pick who hears about it, and what they do.", pane.Dialog.Why.Text);
            Assert.Equal("Empty 3D point", pane.TargetList.Shown.Single(choice => choice.Name == "Lamp").Line);
            Assert.Equal(("Connect", "↵"), (Card(pane.Dialog.MainButton).Title, Card(pane.Dialog.MainButton).Key));
        });
    }

    [Fact]
    public void A_signal_with_no_plain_name_is_named_as_sent()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.Scripted[FakeScene.Lamp.Handle] = "res://spin.cs";
            var window = OpenAtReferenceSize(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;

            Click(ConnectOf(window.Signals, "hit"));

            Assert.Equal("When Lamp sends hit…", window.Signals.Dialog.Title);
        });
    }

    [Fact]
    public void Connect_waits_for_a_method_name_that_is_an_identifier()
    {
        UiThread.Run(() =>
        {
            var (window, _) = Lamp();
            var pane = window.Signals;
            Click(ConnectOf(pane, "renamed"));
            Assert.True(pane.Dialog.MainButton.IsEnabled);

            pane.MethodBox.Text = "On Renamed";
            Assert.False(pane.Dialog.MainButton.IsEnabled);
            Assert.Equal("Name the method with letters, numbers and _ only.", Card(pane.Dialog.MainButton).Line);

            pane.MethodBox.Text = "OnRenamed";
            pane.TargetList.SearchBox.Text = "zzz";
            Assert.False(pane.Dialog.MainButton.IsEnabled);
            Assert.Equal("Pick who to tell first.", Card(pane.Dialog.MainButton).Line);
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

            Click(ConnectOf(pane, "renamed"));
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

            Click(ButtonOf(window.Signals, "Sound stops running OnRenamed when this happens."));

            Assert.Empty(scene.Links);
            Assert.DoesNotContain("Sound · OnRenamed", Texts(window.Signals));
            Assert.Equal("Disconnected renamed from Sound.OnRenamed.", window.Status);
        });
    }

    [Theory]
    [InlineData("VISIB", new[] { "From Lamp itself", "Entity3D", "Shown or hidden", "visibility_changed()" })]
    [InlineData("child came", new[] { "From any thing", "Entity", "A child came in", "child_entered_stage(entity)" })]
    public void A_search_keeps_the_signals_whose_plain_or_engine_name_matches_and_their_bars(string text, string[] expected)
    {
        UiThread.Run(() =>
        {
            var (window, _) = Lamp();

            window.Signals.FilterBox.Text = text;

            Assert.Equal("Find a signal", window.Signals.FilterBox.PlaceholderText);
            Assert.Equal(expected, Texts(window.Signals));
        });
    }

    [Theory]
    [InlineData("visibility_changed", "OnVisibilityChanged")]
    [InlineData("renamed", "OnRenamed")]
    [InlineData("_ready", "OnReady")]
    public void The_offered_method_is_On_and_the_signal_in_Pascal_case(string signal, string method) =>
        Assert.Equal(method, SignalsPane.MethodFor(signal));
}
