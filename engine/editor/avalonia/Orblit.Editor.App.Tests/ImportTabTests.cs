using Avalonia.Controls;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class ImportTabTests
{
    static PropertyRow Row(EditorWindow window, string label) =>
        window.Import.Groups.Children.OfType<PropertyGroup>().SelectMany(group => ((StackPanel)group.Body).Children.OfType<PropertyRow>()).Single(row => row.Label == label);

    [Fact]
    public void Picking_an_imported_file_shows_its_options_in_the_import_tab()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.ImportText["res://rock.glb"] = ImportFileTests.Rock;
            var window = OpenAtReferenceSize(scene);

            window.Scenes.Tree.SelectedItem = FileNodes(window).Single(node => node.Path == "res://rock.glb");

            Assert.Equal(1, window.SceneDock.SelectedIndex);
            Assert.Equal("Scene", window.Import.Importer);
            Assert.Equal(["Nodes", "Meshes", "Animation", "Process"], window.Import.Groups.Children.OfType<PropertyGroup>().Select(group => group.Title));
            Assert.IsType<ToggleSwitch>(Row(window, "Ensure Tangents").Editor);
            Assert.False(window.Import.ReimportButton.IsEnabled);
            Assert.Equal("Change an option first.", ((HoverCard)ToolTip.GetTip(window.Import.ReimportButton)!).Line);
        });
    }

    [Fact]
    public void Reimport_writes_the_changed_option_and_asks_the_engine_and_reset_puts_the_file_back()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.ImportText["res://rock.glb"] = ImportFileTests.Rock;
            var window = OpenAtReferenceSize(scene);
            window.Scenes.Tree.SelectedItem = FileNodes(window).Single(node => node.Path == "res://rock.glb");
            var tangents = (ToggleSwitch)Row(window, "Ensure Tangents").Editor;

            tangents.IsChecked = false;
            Click(window.Import.ResetButton);
            Assert.True(((ToggleSwitch)Row(window, "Ensure Tangents").Editor).IsChecked);
            Assert.Equal(0, scene.Reimports);

            ((ToggleSwitch)Row(window, "Ensure Tangents").Editor).IsChecked = false;
            Click(window.Import.ReimportButton);

            Assert.Contains("meshes/ensure_tangents=false", scene.ImportText["res://rock.glb"], StringComparison.Ordinal);
            Assert.Equal(1, scene.Reimports);
            Assert.Equal("Reimporting rock.glb. Output shows the engine's import.", window.Import.Status);
            Assert.False(window.Import.ReimportButton.IsEnabled);
        });
    }

    [Fact]
    public void A_missing_import_file_is_said_in_the_tab()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);

            window.Import.Open("res://gone.png");

            Assert.Equal("No res://gone.png.import.", window.Import.Status);
            Assert.Empty(window.Import.Groups.Children);
        });
    }
}
