using Avalonia.Controls;
using Avalonia.Interactivity;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class ExportToolTests
{
    static readonly string Here = ExportPresets.ThisComputer.Platform;

    static void Press(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static ExportTool Opened(FakeScene scene)
    {
        var tool = new ExportTool();
        tool.Show(scene);
        tool.Open();
        return tool;
    }

    [Fact]
    public void Open_lists_the_presets_and_fills_in_where_the_first_exports_to()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.ExportList.AddRange([new ExportPreset("Desktop", Here, "/games/a/build/Game"), new ExportPreset("Web", "Web", "/games/a/web/index.html")]);

            var tool = Opened(scene);

            Assert.True(tool.Dialog.IsVisible);
            Assert.Equal("Export game", tool.Dialog.Title);
            Assert.Equal("Export", tool.Dialog.EngineName);
            Assert.Equal("Builds your game as an app that runs without Orblit.", tool.Dialog.Why.Text);
            Assert.False(tool.NoneYet.IsVisible);
            Assert.False(tool.WhyOff.IsVisible);
            Assert.Equal(["Desktop", "Web"], tool.PresetBox.Items.Cast<string>());
            Assert.Equal("/games/a/build/Game", tool.PathBox.Text);
            Assert.True(tool.Dialog.MainButton.IsEnabled);
            Assert.False(tool.AddPresetButton.IsEnabled);
        });
    }

    [Fact]
    public void A_project_with_no_presets_waits_for_one_and_Add_Preset_makes_it()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var tool = Opened(scene);

            Assert.False(tool.Dialog.MainButton.IsEnabled);
            Assert.True(tool.NoneYet.IsVisible);
            Assert.Equal(["Nowhere to export to yet", $"Add a place the game will run, like {ExportTool.ThisMachine}. Each one keeps its own settings."], Texts(tool.NoneYet));
            Assert.Equal(ExportTool.NoPlace, tool.WhyOff.Text);
            Assert.True(tool.WhyOff.IsVisible);
            Assert.Equal($"Add {ExportTool.ThisMachine}", tool.AddPresetButton.Content);

            Press(tool.AddPresetButton);

            Assert.Equal(1, scene.PresetsAdded);
            Assert.Equal([Here], tool.PresetBox.Items.Cast<string>());
            Assert.False(tool.NoneYet.IsVisible);
            Assert.False(tool.WhyOff.IsVisible);
            Assert.Equal($"Added {ExportTool.ThisMachine}. It keeps its own settings.", tool.Status.Text);
            Assert.True(tool.Dialog.MainButton.IsEnabled);
            Assert.False(tool.AddPresetButton.IsEnabled);
            Assert.EndsWith("already has one.", ((HoverCard)ToolTip.GetTip(tool.AddPresetButton)!).Line);
        });
    }

    [Fact]
    public void Export_hands_the_engine_the_preset_and_the_path_in_the_box_and_says_where_it_went()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.ExportList.Add(new ExportPreset("Desktop", Here, "/games/a/build/Game"));
            var tool = Opened(scene);
            tool.PathBox.Text = "/games/out/Game";

            Press(tool.Dialog.MainButton);

            Assert.Equal([("Desktop", "/games/out/Game")], scene.Exports);
            Assert.Equal("Exported Desktop to /games/out/Game.", tool.Status.Text);
            Assert.True(tool.Dialog.MainButton.IsEnabled);
        });
    }

    [Fact]
    public void A_failed_export_says_why()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { ExportFault = new InvalidOperationException("No export template found.") };
            scene.ExportList.Add(new ExportPreset("Desktop", Here, "/games/a/build/Game"));
            var tool = Opened(scene);

            Press(tool.Dialog.MainButton);

            Assert.Equal("Desktop did not export. No export template found.", tool.Status.Text);
            Assert.True(tool.Dialog.MainButton.IsEnabled);
        });
    }

    [Fact]
    public void A_preset_with_no_path_waits_for_one()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.ExportList.Add(new ExportPreset("Desktop", Here, ""));
            var tool = Opened(scene);

            Assert.False(tool.Dialog.MainButton.IsEnabled);
            Assert.Equal(ExportTool.NoPath, tool.WhyOff.Text);

            tool.PathBox.Text = "/games/out/Game";

            Assert.True(tool.Dialog.MainButton.IsEnabled);
            Assert.False(tool.WhyOff.IsVisible);
        });
    }

    [Fact]
    public void Installed_templates_are_said_in_the_status_and_refused_ones_say_why()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var tool = Opened(scene);

            tool.InstallFile("/downloads/orblit-1.0.0-export-templates.tpz");

            Assert.Equal(["/downloads/orblit-1.0.0-export-templates.tpz"], scene.Templates);
            Assert.Equal("Installed the build parts from orblit-1.0.0-export-templates.tpz.", tool.Status.Text);

            scene.TemplateFault = new InvalidDataException("The file holds no templates/version.txt with a version such as 1.0.stable.");
            tool.InstallFile("/downloads/other.tpz");

            Assert.Equal("other.tpz was not installed. The file holds no templates/version.txt with a version such as 1.0.stable.", tool.Status.Text);
        });
    }

    [Fact]
    public void A_download_says_it_installed_the_templates_or_says_why_not()
    {
        UiThread.Run(async () =>
        {
            var scene = new FakeScene();
            var tool = Opened(scene);

            Press(tool.DownloadButton);
            await Task.Yield();

            Assert.Equal(1, scene.Downloads);
            Assert.Equal("Downloaded and installed the build parts.", tool.Status.Text);
            Assert.True(tool.DownloadButton.IsEnabled);

            scene.DownloadFault = new IOException("No export templates are published for 1.0.stable. Install a .tpz from a release instead.");
            Press(tool.DownloadButton);
            await Task.Yield();

            Assert.Equal("The build parts were not downloaded. No export templates are published for 1.0.stable. Install a .tpz from a release instead.", tool.Status.Text);
        });
    }
}
