using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Microsoft.CodeAnalysis.Completion;
using Orblit.Editor.App.Graph;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Tests.Graph;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;
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

        public IReadOnlyList<string> ScriptReferences() => [];

        public FakeDebugChannel Channel { get; } = new();

        public IDebugChannel Csharp => Channel;
    }

    static ScriptView Open(Files files)
    {
        var view = new ScriptView(KeyModifiers.Meta);
        new Window { Content = view, Width = 800, Height = 600 }.Show();
        view.Show(files);
        return view;
    }

    static void Pick(ScriptView view, string path) => view.List.SelectedItem = path;

    [Theory]
    [InlineData("a\nb\nc", "a\nb\nc", 0)]
    [InlineData("a\nb\nc", "a\nB\nc", 1)]
    [InlineData("a\nb", "a\nb\nc\nd", 2)]
    [InlineData("a\nb\nc\nd", "a", 3)]
    [InlineData("a\nb\nc", "c\nb\na", 0)]
    public void A_changed_line_counts_once_and_a_moved_line_not_at_all(string saved, string text, int changed) =>
        Assert.Equal(changed, ScriptView.LinesChanged(saved, text));

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
    public void Save_is_a_small_primary_icon_with_its_key_on_its_card()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());
            Pick(view, "res://spin.cs");
            view.Editor.Text += "// edit\n";
            view.UpdateLayout();

            Assert.Same(EditorBrushes.Accent, view.SaveButton.Background);
            Assert.Equal((26, 26), (view.SaveButton.Bounds.Width, view.SaveButton.Bounds.Height));
            Assert.Equal("Save", view.SaveButton.GetVisualDescendants().OfType<EditorIcon>().Single().IconName);
            Assert.Equal("Save", AutomationProperties.GetName(view.SaveButton));
            Assert.Equal("⌘S", ((HoverCard)ToolTip.GetTip(view.SaveButton)!).Key);
        });
    }

    [Fact]
    public void A_picked_script_is_a_tree_row_lit_in_the_soft_accent()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());
            Pick(view, "res://spin.cs");
            view.UpdateLayout();

            var row = view.List.ContainerFromIndex(0)!;
            var fill = row.GetVisualDescendants().OfType<ContentPresenter>().First().Background;
            Assert.Equal(EditorColors.AccentSoft, ((ISolidColorBrush)fill!).Color);
            Assert.Equal((26.0, EditorTheme.Corners), (row.Bounds.Height, ((ListBoxItem)row).CornerRadius));
            Assert.Equal(4, BoxOf(row, view.List).X);
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
            view.UpdateLayout();
            Assert.Equal(view.Tab.DesiredSize.Width, view.Tab.Bounds.Width);
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
            Assert.Equal("Spaces 4", view.IndentText.Text);

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

            Assert.False(view.ProblemsButton.IsVisible);
            Assert.True(view.Check().IsCompletedSuccessfully);

            Assert.Equal([("res://spin.cs", "class Spin { }")], files.Written);
            Assert.True(view.ProblemsButton.IsVisible);
            Assert.Equal("2 problems", view.ProblemsText);
            Assert.Same(EditorBrushes.FaultText, view.ProblemsButton.Foreground);
            var items = view.ProblemList().Items.OfType<MenuItem>().ToList();
            Assert.Equal("1 ERROR, 1 WARNING", ((TextBlock)items[0].Header!).Text);
            Assert.Equal(["; expected", "An analyzer could not load", "Check again"], items.Skip(1).Select(AutomationProperties.GetName));
            Assert.Equal(["; expected", "CS1002", "slime.cs, line 1."], Texts((Control)items[1].Header!));
            Assert.Equal(["An analyzer could not load", "CS8032"], Texts((Control)items[2].Header!));
            Assert.False(items[2].IsEnabled);

            items[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

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

            Click(view.CheckButton);

            Assert.Equal(1, files.Checks);
            Assert.Equal("No problems", view.ProblemsText);
            Assert.Same(EditorBrushes.Done, view.ProblemsButton.Foreground);

            files.CheckFails = true;
            view.ProblemsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal("Not checked", view.ProblemsText);
            Assert.Equal("dotnet was not found.", view.Status.Text);
            Assert.True(view.ProblemsButton.IsEnabled);
            Assert.True(view.CheckButton.IsEnabled);
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
            Assert.Equal("New C# script", view.CreateDialog.Title);
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
            Assert.Equal("Sort the scripts A to Z", ((HoverCard)ToolTip.GetTip(view.SortButton)!).Title);

            Click(view.SortButton);

            Assert.Equal(["res://spin.cs", "res://slime.cs"], (IEnumerable<string>)view.List.ItemsSource!);
            Assert.Equal("Sort the scripts Z to A", ((HoverCard)ToolTip.GetTip(view.SortButton)!).Title);
        });
    }

    [Fact]
    public void With_no_script_open_the_middle_says_what_scripts_are_and_offers_two_ways_in()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());

            Assert.True(view.Empty.IsVisible);
            Assert.Equal(["No script is open", "Scripts tell things what to do. Pick one from the list, or make one."], Texts(view.Empty).Take(2));
            Assert.Equal("New script", view.StartButton.Content);
            Assert.Equal(["Find a script", "⌘K"], Texts(view.FindScriptButton));
            Assert.Equal("Type part of a name to open it.", ((HoverCard)ToolTip.GetTip(view.FindScriptButton)!).Line);

            Click(view.StartButton);

            Assert.True(view.CreateDialog.IsVisible);
            Assert.Equal("New C# script", view.CreateDialog.Title);
        });
    }

    [Fact]
    public void Close_saves_the_open_script_and_shows_the_middle_again()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            var view = Open(files);
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { int turns; }";

            Assert.False(view.Empty.IsVisible);

            Click(view.CloseButton);

            Assert.Equal([("res://spin.cs", "class Spin { int turns; }")], files.Written);
            Assert.True(view.Empty.IsVisible);
            Assert.Null(view.List.SelectedItem);
            Assert.Equal("Pick a script.", view.Heading.Text);

            Pick(view, "res://spin.cs");

            Assert.Equal("res://spin.cs", view.Heading.Text);
        });
    }

    [Fact]
    public void The_keys_are_the_command_key_the_view_is_given()
    {
        UiThread.Run(() =>
        {
            var view = new ScriptView(KeyModifiers.Control);
            HoverCard Card(Control control) => (HoverCard)ToolTip.GetTip(control)!;

            Assert.Equal(["Find a script", "⌃K"], Texts(view.FindScriptButton));
            Assert.Equal(("⌃F", "⌃⇧B"), (Card(view.FindButton).Key, Card(view.CheckButton).Key));
        });
    }

    [Fact]
    public void Find_Save_and_Check_say_what_they_do_and_why_they_are_off()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());
            HoverCard Card(Control control) => (HoverCard)ToolTip.GetTip(control)!;

            Assert.Equal(("Find", "⌘F", "Open a script first."), (Card(view.FindButton).Title, Card(view.FindButton).Key, Card(view.FindButton).Line));
            Assert.Equal(("Save", "Nothing new to save."), (Card(view.SaveButton).Title, Card(view.SaveButton).Line));
            Assert.Equal(("Check for problems", "⇧⌘B"), (Card(view.CheckButton).Title, Card(view.CheckButton).Key));

            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { int turns; }";

            Assert.Equal("Finds words in this script.", Card(view.FindButton).Line);
            Assert.Equal("Writes the script to its file.", Card(view.SaveButton).Line);
        });
    }

    [Fact]
    public void Match_case_counts_only_the_same_letters_and_Option_C_turns_it_on()
    {
        UiThread.Run(() =>
        {
            var files = new Files { Text = { ["res://ab.cs"] = "ab Ab\nAB" } };
            var view = Open(files);
            Pick(view, "res://ab.cs");
            Click(view.FindButton);
            view.Finder.Text.Text = "ab";
            Press(view.Finder.Text, Key.Enter);

            Press(view.Finder.Text, Key.C, KeyModifiers.Alt);

            Assert.True(view.Finder.CaseButton.IsChecked);
            Assert.Equal("1 of 1", view.Finder.Count);
            Assert.Equal(("Match case", "⌥C"), (((HoverCard)ToolTip.GetTip(view.Finder.CaseButton)!).Title, ((HoverCard)ToolTip.GetTip(view.Finder.CaseButton)!).Key));

            view.Finder.CaseButton.IsChecked = false;

            Assert.Equal("1 of 3", view.Finder.Count);
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

    // A stand-in for the engine's C# API, which a test run has not built.
    const string Api = "namespace Orblit { public struct Vector3 { public static Vector3 Zero => default; public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a; } }";

    static List<CompletionItem> Offered(ScriptView view) => [.. view.Completions.List.Items.Cast<CompletionItem>()];

    [Fact]
    public void Ctrl_Space_after_a_dot_lists_the_engine_types_members_and_Enter_puts_the_pick_in()
    {
        UiThread.Run(async () =>
        {
            var view = Open(new Files { Text = { ["res://api.cs"] = Api } });
            Pick(view, "res://spin.cs");
            view.Editor.Text = "using Orblit;\nclass Spin { void F() { var v = Vector3. } }";
            view.Editor.CaretIndex = view.Editor.Text.IndexOf("3.", StringComparison.Ordinal) + 2;

            Press(view.Editor, Key.Space, KeyModifiers.Control);
            await view.Completions.Asking;

            Assert.True(view.Completions.Popup.IsOpen);
            Assert.Contains("Zero", Offered(view).Select(item => item.DisplayText));
            Assert.Contains("Lerp", Offered(view).Select(item => item.DisplayText));

            view.Completions.List.SelectedItem = Offered(view).First(item => item.DisplayText == "Zero");
            Press(view.Editor, Key.Enter);
            await view.Completions.Asking;

            Assert.Contains("var v = Vector3.Zero }", view.Editor.Text);
            Assert.False(view.Completions.Popup.IsOpen);
        });
    }

    [Fact]
    public void A_suggestion_line_is_the_summary_of_its_member_or_its_signature_when_it_has_none()
    {
        UiThread.Run(async () =>
        {
            var files = new Files { Text = { ["res://api.cs"] = "namespace Orblit { public struct Vector3 {\n/// <summary>The point at the middle.</summary>\npublic static Vector3 Zero => default;\npublic static Vector3 One => default; } }" } };
            const string text = "using Orblit;\nclass Spin { void F() { var v = Vector3. } }";

            var set = (await ScriptCompletion.For(files).Complete("res://spin.cs", text, text.IndexOf("3.", StringComparison.Ordinal) + 2))!;

            Assert.Equal("The point at the middle.", await set.LineOf(set.Items.First(item => item.DisplayText == "Zero")));
            Assert.StartsWith("Vector3 Vector3.One", await set.LineOf(set.Items.First(item => item.DisplayText == "One")));
        });
    }

    [Fact]
    public void A_pick_asked_for_in_the_middle_of_a_word_replaces_the_whole_word()
    {
        UiThread.Run(async () =>
        {
            var view = Open(new Files { Text = { ["res://api.cs"] = Api } });
            Pick(view, "res://spin.cs");
            view.Editor.Text = "using Orblit;\nclass Spin { void F() { var v = Vector3.Zxx; } }";
            view.Editor.CaretIndex = view.Editor.Text.IndexOf("Zxx", StringComparison.Ordinal) + 1;

            Press(view.Editor, Key.Space, KeyModifiers.Control);
            await view.Completions.Asking;
            view.Completions.List.SelectedItem = Offered(view).First(item => item.DisplayText == "Zero");
            Press(view.Editor, Key.Enter);
            await view.Completions.Asking;

            Assert.Contains("var v = Vector3.Zero; }", view.Editor.Text);
        });
    }

    [Fact]
    public void A_list_closed_by_a_click_elsewhere_stays_closed_when_the_caret_moves()
    {
        UiThread.Run(async () =>
        {
            var view = Open(new Files { Text = { ["res://api.cs"] = Api } });
            Pick(view, "res://spin.cs");
            view.Editor.Text = "using Orblit;\nclass Spin { void F() { var v = Vector3.Ze } }";
            view.Editor.CaretIndex = view.Editor.Text.IndexOf("Ze", StringComparison.Ordinal) + 2;
            Press(view.Editor, Key.Space, KeyModifiers.Control);
            await view.Completions.Asking;
            Assert.True(view.Completions.Popup.IsOpen);

            // What a light dismiss does.
            view.Completions.Popup.IsOpen = false;
            view.Editor.CaretIndex--;

            Assert.False(view.Completions.Popup.IsOpen);
        });
    }

    [Fact]
    public void A_broken_line_shows_the_error_in_the_status_line_until_it_is_fixed()
    {
        UiThread.Run(async () =>
        {
            var view = Open(new Files());
            Pick(view, "res://spin.cs");

            view.Editor.Text = "class Spin { void F() { int x = } }";
            await view.ShowErrors();

            Assert.StartsWith("Line 1: CS", view.ErrorsText.Text);

            view.Editor.Text = "class Spin { }";
            await view.ShowErrors();

            Assert.Equal("", view.ErrorsText.Text);
        });
    }

    [Fact]
    public void A_click_in_the_mark_column_sets_and_clears_the_lines_breakpoint()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());
            Pick(view, "res://spin.cs");

            view.Code.ClickMark(1.5 * CodeBox.LineHeight);
            view.Code.ClickMark(40 * CodeBox.LineHeight);

            Assert.Equal([2], view.Breakpoints.Steps("res://spin.cs"));
            var mark = Assert.Single(view.Code.Marks.Children);
            Assert.InRange(Canvas.GetTop(mark), CodeBox.LineHeight, 2 * CodeBox.LineHeight);
            Pick(view, "res://slime.cs");
            Assert.Empty(view.Code.Marks.Children);

            Pick(view, "res://spin.cs");
            view.Code.ClickMark(1.5 * CodeBox.LineHeight);

            Assert.Empty(view.Breakpoints.Steps("res://spin.cs"));
            Assert.Empty(view.Code.Marks.Children);
        });
    }

    [Fact]
    public void A_stop_in_a_project_script_opens_it_on_the_paused_line_once()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            var view = Open(files);
            var paused = 0;
            view.Paused += () => paused++;
            Assert.False(view.IsDebugging);
            Assert.NotNull(view.DebugPanel);

            files.Channel.Next = FakeDebugChannel.Stopped("breakpoint", new DebugFrame("res://spin.cs", 2, "Spin.Run()"));
            view.PollDebugger();
            view.PollDebugger();

            Assert.Equal(1, paused);
            Assert.True(view.IsDebugging);
            Assert.Equal("res://spin.cs", view.List.SelectedItem);
            Assert.Equal("class Spin\n".Length, view.Editor.CaretIndex);
            Assert.True(view.Code.PausedLine.IsVisible);
            Assert.Equal(CodeBox.LineHeight, view.Code.PausedLine.Margin.Top);
            Pick(view, "res://slime.cs");
            Assert.False(view.Code.PausedLine.IsVisible);

            Pick(view, "res://spin.cs");
            files.Channel.Next = FakeDebugChannel.Running();
            view.PollDebugger();

            Assert.False(view.Code.PausedLine.IsVisible);
            Assert.True(view.IsDebugging);
        });
    }

    [Fact]
    public void A_stop_outside_the_project_opens_nothing()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            var view = Open(files);
            var paused = 0;
            view.Paused += () => paused++;

            files.Channel.Next = FakeDebugChannel.Stopped("pause", new DebugFrame("/usr/local/share/dotnet/Thread.cs", 30, "Thread.Sleep()"));
            view.PollDebugger();

            Assert.Equal(0, paused);
            Assert.Null(view.List.SelectedItem);
            Assert.False(view.Code.PausedLine.IsVisible);
        });
    }
}
