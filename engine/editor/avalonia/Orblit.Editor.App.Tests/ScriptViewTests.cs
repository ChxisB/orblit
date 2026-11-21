using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
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

        public IReadOnlyList<string> AddableClasses() => ["Entity", "Entity3D"];

        public void CreateScript(string resourcePath, string baseType)
        {
            if (Text.ContainsKey(resourcePath))
            {
                throw new IOException($"{resourcePath} exists already.");
            }

            WriteScript(resourcePath, $"class New : {baseType} {{ }}");
        }

        public List<ScriptProblem> Problems { get; } = [];

        public int Checks { get; private set; }

        public bool CheckFails { get; set; }

        public Task<IReadOnlyList<ScriptProblem>> CheckScripts()
        {
            Checks++;
            return CheckFails ? throw new IOException("dotnet was not found.") : Task.FromResult<IReadOnlyList<ScriptProblem>>([.. Problems]);
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

    // A view with the player script open, the one the colour tests read.
    static ScriptView OpenPlayer()
    {
        var files = new Files();
        files.Text["res://player.cs"] = CodeColoursTests.Player;
        var view = Open(files);
        Pick(view, "res://player.cs");
        view.UpdateLayout();
        return view;
    }

    [Fact]
    public void The_text_shows_in_its_colours_over_the_text_box_line_for_line()
    {
        UiThread.Run(() =>
        {
            var view = OpenPlayer();
            var runs = view.Code.Colors.Inlines!.OfType<Run>().ToList();
            var text = view.Editor.GetVisualDescendants().OfType<TextPresenter>().Single();

            Assert.Equal(CodeColoursTests.Player, string.Concat(runs.Select(run => run.Text)));
            Assert.Same(EditorBrushes.CodeKeyword, runs.First(run => run.Text == "public").Foreground);
            Assert.Same(EditorBrushes.CodeString, runs.Single(run => run.Text == "\"jump\"").Foreground);
            Assert.Equal(BoxOf(text, view).TopLeft, BoxOf(view.Code.Colors, view).TopLeft);
            Assert.Equal(text.Bounds.Height, view.Code.Colors.Bounds.Height, 0.5);
            Assert.Equal(view.Code.Colors.Bounds.Height, view.Gutter.Bounds.Height, 0.5);
        });
    }

    [Fact]
    public void The_status_line_shows_the_caret_and_the_indent()
    {
        UiThread.Run(() =>
        {
            var view = OpenPlayer();

            Assert.Equal("Line 1, column 1", view.CaretText.Text);
            Assert.Equal("Spaces: 4", view.IndentText.Text);

            view.Editor.Text = "class A\n{\n\tint b;\n}";
            view.Editor.CaretIndex = 12;

            Assert.Equal("Line 3, column 3", view.CaretText.Text);
            Assert.Equal("Tabs", view.IndentText.Text);
        });
    }

    [Fact]
    public void A_member_in_the_outline_takes_the_caret_to_it_and_lights_its_line()
    {
        UiThread.Run(() =>
        {
            var view = OpenPlayer();

            Assert.Equal(["JumpHeight", "OnFrame"], ((IEnumerable<ScriptMember>)view.Outline.ItemsSource!).Select(member => member.Name));

            view.Outline.SelectedItem = ((IEnumerable<ScriptMember>)view.Outline.ItemsSource!).Last();

            Assert.Equal(CodeColoursTests.Player.IndexOf("OnFrame", StringComparison.Ordinal), view.Editor.CaretIndex);
            Assert.Equal("Line 8, column 26", view.CaretText.Text);
            Assert.Equal(7 * CodeBox.LineHeight, view.Code.CaretLine.Margin.Top);
            Assert.Null(view.Outline.SelectedItem);
        });
    }

    [Fact]
    public void The_filter_narrows_the_list_and_keeps_the_open_script()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());
            Pick(view, "res://spin.cs");

            view.Filter.Text = "SL";

            Assert.Equal(["res://slime.cs"], (IEnumerable<string>)view.List.ItemsSource!);
            Assert.Null(view.List.SelectedItem);
            Assert.Equal("res://spin.cs", view.Heading.Text);

            view.Filter.Text = "";

            Assert.Equal("res://spin.cs", view.List.SelectedItem);
        });
    }

    [Fact]
    public void Picking_another_script_after_an_edit_saves_it_and_selects_the_new_one()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            var view = Open(files);
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { int turns; }";

            Pick(view, "res://slime.cs");

            Assert.Equal([("res://spin.cs", "class Spin { int turns; }")], files.Written);
            Assert.Equal("res://slime.cs", view.List.SelectedItem);
            Assert.Equal("res://slime.cs", view.Heading.Text);
        });
    }

    [Fact]
    public void Check_saves_the_open_script_then_counts_the_problems_and_a_pick_opens_the_file_at_its_place()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            files.Problems.Add(new ScriptProblem("res://slime.cs", 1, 7, true, "CS1002: ; expected"));
            files.Problems.Add(new ScriptProblem("CSC", 0, 0, false, "CS8032: An analyzer could not load"));
            var view = Open(files);
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { }";

            Assert.Equal("Check problems", view.ProblemsText);
            Assert.True(view.Check().IsCompletedSuccessfully);

            Assert.Equal([("res://spin.cs", "class Spin { }")], files.Written);
            Assert.Equal("1 error, 1 warning", view.ProblemsText);
            Assert.Same(EditorBrushes.FaultText, view.ProblemsButton.Foreground);
            var items = view.ProblemList().Items.OfType<MenuItem>().ToList();
            Assert.Equal(["slime.cs:1  CS1002: ; expected", "CS8032: An analyzer could not load", "Check again"], items.Select(item => item.Header is TextBlock text ? text.Text : item.Header));
            Assert.False(items[1].IsEnabled);

            items[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal("res://slime.cs", view.Heading.Text);
            Assert.Equal(6, view.Editor.CaretIndex);
        });
    }

    [Fact]
    public void A_clean_check_says_no_problems_and_a_failed_one_says_why()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            var view = Open(files);

            view.ProblemsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(1, files.Checks);
            Assert.Equal("No problems", view.ProblemsText);
            Assert.Same(EditorBrushes.Done, view.ProblemsButton.Foreground);

            files.CheckFails = true;
            view.ProblemsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal("Check problems", view.ProblemsText);
            Assert.Equal("dotnet was not found.", view.Status.Text);
            Assert.True(view.ProblemsButton.IsEnabled);
        });
    }

    static void Press(Control target, Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });

    [Fact]
    public void New_script_writes_the_class_and_opens_it()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            var view = Open(files);

            Click(view.NewButton);

            Assert.True(view.CreateDialog.IsVisible);
            Assert.Equal("New C# Script", view.CreateDialog.Title);
            Assert.Equal(["Entity", "Entity3D"], view.Form.ExtendsBox.Items.Cast<string>());

            view.Form.NameBox.Text = "Mover";
            view.Form.ExtendsBox.SelectedItem = "Entity3D";
            Click(view.Form.CreateButton);

            Assert.False(view.CreateDialog.IsVisible);
            Assert.Equal("class New : Entity3D { }", files.Text["res://Mover.cs"]);
            Assert.Contains("res://Mover.cs", (IEnumerable<string>)view.List.ItemsSource!);
            Assert.Equal("res://Mover.cs", view.Heading.Text);
            Assert.Equal("Created res://Mover.cs.", view.Status.Text);
        });
    }

    [Fact]
    public void A_script_that_cannot_be_made_keeps_the_dialog_open_and_says_why()
    {
        UiThread.Run(() =>
        {
            var files = new Files { Denied = true };
            var view = Open(files);

            Click(view.NewButton);
            Click(view.Form.CreateButton);

            Assert.True(view.CreateDialog.IsVisible);
            Assert.Equal("Access denied.", view.Form.Fault);
            Assert.Equal("Pick a script.", view.Heading.Text);
        });
    }

    [Fact]
    public void Sort_turns_the_list_round_and_a_second_press_turns_it_back()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());

            Click(view.SortButton);

            Assert.Equal(["res://slime.cs", "res://spin.cs"], (IEnumerable<string>)view.List.ItemsSource!);
            Assert.Equal("Sort the scripts A to Z", ToolTip.GetTip(view.SortButton));

            Click(view.SortButton);

            Assert.Equal(["res://spin.cs", "res://slime.cs"], (IEnumerable<string>)view.List.ItemsSource!);
            Assert.Equal("Sort the scripts Z to A", ToolTip.GetTip(view.SortButton));
        });
    }

    [Fact]
    public void Find_is_off_until_a_script_opens()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());

            Assert.False(view.FindButton.IsEnabled);

            Pick(view, "res://spin.cs");

            Assert.True(view.FindButton.IsEnabled);
        });
    }

    [Fact]
    public void Enter_selects_each_match_in_turn_and_goes_round_past_the_end()
    {
        UiThread.Run(() =>
        {
            var files = new Files { Text = { ["res://ab.cs"] = "ab Ab\nAB" } };
            var view = Open(files);
            Pick(view, "res://ab.cs");

            Click(view.FindButton);
            view.Finder.Text.Text = "ab";

            Assert.True(view.Finder.IsVisible);
            foreach (var (start, count) in new[] { (0, "1 of 3"), (3, "2 of 3"), (6, "3 of 3"), (0, "1 of 3") })
            {
                Press(view.Finder.Text, Key.Enter);
                Assert.Equal((start, start + 2), (view.Editor.SelectionStart, view.Editor.SelectionEnd));
                Assert.Equal(count, view.Finder.Count);
            }
        });
    }

    [Fact]
    public void Shift_and_Enter_selects_the_match_before_and_goes_round_past_the_start()
    {
        UiThread.Run(() =>
        {
            var files = new Files { Text = { ["res://ab.cs"] = "ab Ab\nAB" } };
            var view = Open(files);
            Pick(view, "res://ab.cs");
            Click(view.FindButton);
            view.Finder.Text.Text = "ab";

            Press(view.Finder.Text, Key.Enter, KeyModifiers.Shift);

            Assert.Equal("3 of 3", view.Finder.Count);

            Press(view.Finder.Text, Key.Enter, KeyModifiers.Shift);

            Assert.Equal((3, 5), (view.Editor.SelectionStart, view.Editor.SelectionEnd));
            Assert.Equal("2 of 3", view.Finder.Count);
        });
    }

    [Fact]
    public void A_missing_word_says_no_match_and_Escape_closes_the_card()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());
            Pick(view, "res://spin.cs");
            Click(view.FindButton);
            view.Finder.Text.Text = "slime";

            Press(view.Finder.Text, Key.Enter);

            Assert.Equal("No match", view.Finder.Count);

            Press(view.Finder.Text, Key.Escape);

            Assert.False(view.Finder.IsVisible);
        });
    }
}
