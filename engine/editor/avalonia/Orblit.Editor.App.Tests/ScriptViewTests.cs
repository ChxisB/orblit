using Avalonia.Controls;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class ScriptViewTests
{
    sealed class Files : IScriptFiles
    {
        public Dictionary<string, string> Text { get; } = new()
        {
            ["res://spin.cs"] = "class Spin\n{\n}\n",
            ["res://slime.cs"] = "class Slime { }",
        };

        public List<(string Path, string Text)> Written { get; } = [];

        public bool WriteFails { get; set; }

        public bool Denied { get; set; }

        // A listed file that cannot be read, like one deleted since the list was made.
        public string? Gone { get; set; }

        public IReadOnlyList<string> Scripts() => Gone is null ? [.. Text.Keys] : [.. Text.Keys, Gone];

        public string ReadScript(string resourcePath) =>
            Text.TryGetValue(resourcePath, out var text) ? text : throw new IOException($"No {resourcePath}.");

        public void WriteScript(string resourcePath, string text)
        {
            if (Denied)
            {
                throw new UnauthorizedAccessException("Access denied.");
            }

            if (WriteFails)
            {
                throw new IOException("The disk is full.");
            }

            Written.Add((resourcePath, text));
            Text[resourcePath] = text;
        }
    }

    static ScriptView Open(Files files)
    {
        var view = new ScriptView();
        new Window { Content = view, Width = 800, Height = 600 }.Show();
        view.Show(files);
        return view;
    }

    static void Pick(ScriptView view, string path) => view.List.SelectedItem = path;

    [Fact]
    public void It_starts_with_the_files_listed_and_none_open()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());

            Assert.Equal(["res://spin.cs", "res://slime.cs"], (IEnumerable<string>)view.List.ItemsSource!);
            Assert.Equal("Pick a script.", view.Heading.Text);
            Assert.False(view.Editor.IsEnabled);
            Assert.False(view.SaveButton.IsEnabled);
            Assert.False(view.IsDirty);
            Assert.True(view.Save());
        });
    }

    [Fact]
    public void Picking_a_script_shows_its_text_and_numbers_the_lines()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());

            Pick(view, "res://spin.cs");

            Assert.Equal("class Spin\n{\n}\n", view.Editor.Text);
            Assert.Equal("1\n2\n3\n4", view.Gutter.Text);
            Assert.Equal("res://spin.cs", view.Heading.Text);
            Assert.True(view.Editor.IsEnabled);
            Assert.False(view.IsDirty);
            Assert.False(view.SaveButton.IsEnabled);
        });
    }

    [Fact]
    public void A_file_with_CR_LF_line_ends_is_not_an_edit_once_opened()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            files.Text["res://crlf.cs"] = "a\r\nb\r\n";
            var view = Open(files);

            Pick(view, "res://crlf.cs");

            Assert.False(view.IsDirty);
            Assert.True(view.Save());
            Assert.Empty(files.Written);
        });
    }

    [Fact]
    public void Typing_marks_the_script_edited_and_putting_the_text_back_clears_it()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());
            Pick(view, "res://slime.cs");

            view.Editor.Text = "class Slime { int hp; }";
            Assert.True(view.IsDirty);
            Assert.True(view.SaveButton.IsEnabled);
            Assert.Equal("1", view.Gutter.Text);

            view.Editor.Text = "class Slime { }";
            Assert.False(view.IsDirty);
            Assert.False(view.SaveButton.IsEnabled);
        });
    }

    [Fact]
    public void The_gutter_follows_the_number_of_lines()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());
            Pick(view, "res://slime.cs");

            view.Editor.Text = "a\nb\nc";

            Assert.Equal("1\n2\n3", view.Gutter.Text);
        });
    }

    [Fact]
    public void Save_writes_the_text_and_clears_the_mark()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            var view = Open(files);
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { }";

            Click(view.SaveButton);

            Assert.Equal([("res://spin.cs", "class Spin { }")], files.Written);
            Assert.False(view.IsDirty);
            Assert.False(view.SaveButton.IsEnabled);
            Assert.Equal("Saved res://spin.cs.", view.Status.Text);
        });
    }

    [Fact]
    public void A_write_the_system_refuses_is_said_and_keeps_the_edits()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files { Denied = true });
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { }";

            Assert.False(view.Save());

            Assert.True(view.IsDirty);
            Assert.Equal("Access denied.", view.Status.Text);
        });
    }

    [Fact]
    public void Save_with_no_edits_writes_nothing()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            var view = Open(files);
            Pick(view, "res://spin.cs");

            Assert.True(view.Save());

            Assert.Empty(files.Written);
        });
    }

    [Fact]
    public void A_failed_write_keeps_the_edits_and_says_why()
    {
        UiThread.Run(() =>
        {
            var files = new Files { WriteFails = true };
            var view = Open(files);
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { }";

            Assert.False(view.Save());

            Assert.True(view.IsDirty);
            Assert.True(view.SaveButton.IsEnabled);
            Assert.Equal("The disk is full.", view.Status.Text);
            Assert.Equal("class Spin { }", view.Editor.Text);
        });
    }

    [Fact]
    public void Picking_another_script_writes_the_open_one_first()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            var view = Open(files);
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { }";

            Pick(view, "res://slime.cs");

            Assert.Equal([("res://spin.cs", "class Spin { }")], files.Written);
            Assert.Equal("class Slime { }", view.Editor.Text);
            Assert.Equal("res://slime.cs", view.Heading.Text);
            Assert.Equal("", view.Status.Text);
            Assert.False(view.IsDirty);
        });
    }

    [Fact]
    public void Picking_another_script_after_a_failed_write_stays_on_the_open_one()
    {
        UiThread.Run(() =>
        {
            var files = new Files { WriteFails = true };
            var view = Open(files);
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { }";

            Pick(view, "res://slime.cs");

            Assert.Equal("class Spin { }", view.Editor.Text);
            Assert.Equal("res://spin.cs", view.Heading.Text);
            Assert.Equal("res://spin.cs", view.List.SelectedItem);
            Assert.True(view.IsDirty);
            Assert.Equal("The disk is full.", view.Status.Text);
        });
    }

    [Fact]
    public void A_script_that_cannot_be_read_says_why_and_keeps_the_open_one()
    {
        UiThread.Run(() =>
        {
            var files = new Files { Gone = "res://gone.cs" };
            var view = Open(files);
            Pick(view, "res://spin.cs");

            Pick(view, "res://gone.cs");

            Assert.Equal("No res://gone.cs.", view.Status.Text);
            Assert.Equal("res://spin.cs", view.Heading.Text);
            Assert.Equal("res://spin.cs", view.List.SelectedItem);
            Assert.Equal("class Spin\n{\n}\n", view.Editor.Text);
        });
    }

    [Fact]
    public void A_script_that_cannot_be_read_as_the_first_choice_opens_nothing()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files { Gone = "res://gone.cs" });

            Pick(view, "res://gone.cs");

            Assert.Equal("No res://gone.cs.", view.Status.Text);
            Assert.Equal("Pick a script.", view.Heading.Text);
            Assert.False(view.Editor.IsEnabled);
            Assert.Null(view.List.SelectedItem);
        });
    }

    [Fact]
    public void Discard_counts_the_text_as_saved_and_writes_nothing()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            var view = Open(files);
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { }";

            view.Discard();

            Assert.False(view.IsDirty);
            Assert.False(view.SaveButton.IsEnabled);
            Assert.Empty(files.Written);
            Assert.Equal("class Spin { }", view.Editor.Text);
        });
    }

    [Fact]
    public void Showing_the_view_again_lists_new_files_and_keeps_the_open_script_and_its_edits()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            var view = Open(files);
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { }";

            view.IsVisible = false;
            files.Text["res://new.cs"] = "";
            view.IsVisible = true;

            Assert.Equal(["res://spin.cs", "res://slime.cs", "res://new.cs"], (IEnumerable<string>)view.List.ItemsSource!);
            Assert.Equal("res://spin.cs", view.List.SelectedItem);
            Assert.Equal("class Spin { }", view.Editor.Text);
            Assert.True(view.IsDirty);
        });
    }

    [Fact]
    public void Showing_a_project_again_closes_the_open_script()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { }";

            view.Show(new Files());

            Assert.Equal("Pick a script.", view.Heading.Text);
            Assert.Equal("", view.Editor.Text);
            Assert.False(view.Editor.IsEnabled);
            Assert.False(view.IsDirty);
            Assert.Null(view.List.SelectedItem);
        });
    }
}
