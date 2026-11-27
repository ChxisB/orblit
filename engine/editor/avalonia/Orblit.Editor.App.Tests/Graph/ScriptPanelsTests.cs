using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Orblit.Editor.App.Graph;
using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class ScriptPanelsTests
{
    static GraphVariable Total() => new("total", 2, "0", false, 0, "", "", "");

    static (FakeGraphSource Source, ScriptPanels Panels) Open()
    {
        var source = FakeGraphSource.Sample();
        source.Put(Total());
        source.Put(new GraphSignal("scored", [new GraphTyped("amount", 2)]));
        source.PutFunction("helper");
        return (source, new ScriptPanels(source));
    }

    static void Press(Control target, Key key) =>
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

    static void Type(ScriptPanels panels, string id, string text)
    {
        var box = panels.Find<TextBox>(id)!;
        box.Text = text;
        Press(box, Key.Enter);
    }

    static void Click(ScriptPanels panels, string id) => panels.Find<Button>(id)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public void Each_variable_function_and_signal_has_a_row()
    {
        UiThread.Run(() =>
        {
            var (_, panels) = Open();

            Assert.Equal("total", panels.Find<TextBox>("variable:total:name")!.Text);
            Assert.Equal(2, panels.Find<ComboBox>("variable:total:type")!.SelectedIndex);
            Assert.Equal("0", panels.Find<TextBox>("variable:total:default")!.Text);
            Assert.Equal("main", panels.Find<TextBox>("function:main:name")!.Text);
            Assert.Equal("helper", panels.Find<TextBox>("function:helper:name")!.Text);
            Assert.Equal("scored", panels.Find<TextBox>("signal:scored:name")!.Text);
            Assert.Equal("amount", panels.Find<TextBox>("signal:scored:arguments:0:name")!.Text);
            Assert.Equal("", panels.Notice);
        });
    }

    [Fact]
    public void The_add_buttons_name_the_new_thing_so_it_does_not_clash()
    {
        UiThread.Run(() =>
        {
            var (source, panels) = Open();

            Click(panels, "variable:add");
            panels.VariableTool.AddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Click(panels, "variable:add");
            panels.VariableTool.AddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Click(panels, "signal:add");
            Click(panels, "function:add");

            Assert.Equal(["AddVariable value 0", "AddVariable value2 0", "AddSignal message", "AddFunction action"], source.Calls);
            Assert.NotNull(panels.Find<TextBox>("variable:value2:name"));
        });
    }

    [Fact]
    public void The_variable_popover_adds_the_variable_with_its_type_default_and_inspector_choice()
    {
        UiThread.Run(() =>
        {
            var (source, panels) = Open();
            Click(panels, "variable:add");
            var tool = panels.VariableTool;
            Assert.Equal("value", tool.NameBox.Text);

            tool.NameBox.Text = "speed";
            tool.TypeBox.SelectedIndex = 3;
            tool.DefaultBox.Text = "2.5";
            tool.Shown.IsChecked = true;
            tool.AddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(["AddVariable speed 3", "SetVariableDefault speed 2.5", "SetVariableExported speed True"], source.Calls);
            Assert.Equal("2.5", panels.Find<TextBox>("variable:speed:default")!.Text);

            tool.NameBox.Text = " ";
            Assert.False(tool.AddButton.IsEnabled);
        });
    }

    static GraphStepEntry Virtual(string name, string group = "Entity") => new("event", group, name, new Dictionary<string, string> { ["arguments"] = "[]" });

    static ScriptPanels WithStarts(FakeGraphSource source, params string[] names)
    {
        source.Overrides.AddRange(names.Select(name => Virtual(name)));
        return new ScriptPanels(source);
    }

    static MenuFlyout StartMenu(ScriptPanels panels) => (MenuFlyout)panels.Find<Button>("start:add")!.Flyout!;

    // Each item by its name, with a heading as its upper-case words and a submenu as its name, then its items.
    static IReadOnlyList<string> Listed(IEnumerable<object?> items) =>
        [.. items.OfType<MenuItem>().SelectMany(item => item.Header is TextBlock heading
            ? [heading.Text!]
            : item.ItemCount > 0 ? Listed(item.Items).Select(under => $"{AutomationProperties.GetName(item)} > {under}") : [AutomationProperties.GetName(item) ?? ""])];

    static MenuItem StartItem(ScriptPanels panels, string name) =>
        StartMenu(panels).Items.OfType<MenuItem>().SelectMany(item => item.ItemCount > 0 ? item.Items.OfType<MenuItem>() : [item]).First(item => AutomationProperties.GetName(item) == name);

    [Fact]
    public void The_start_menu_groups_the_plain_starts_and_puts_the_rest_under_A_to_Z()
    {
        UiThread.Run(() =>
        {
            var panels = WithStarts(FakeGraphSource.Sample(), "_input", "_zz_custom", "_frame", "_step", "_a_custom");

            Assert.Equal(
                [
                    "COMMON",
                    "Every frame",
                    "KEYS AND CLICKS",
                    "A key or click happens",
                    "PHYSICS",
                    "Every physics step",
                    "All starts, A to Z > _a_custom",
                    "All starts, A to Z > _zz_custom",
                ],
                Listed(StartMenu(panels).Items));
        });
    }

    [Fact]
    public void Choosing_a_start_adds_the_function_that_overrides_it_and_opens_it()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            var panels = WithStarts(source, "_frame", "_start");
            var opened = new List<string>();
            panels.FunctionOpened += opened.Add;

            StartItem(panels, "Every frame").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal(["AddOverride _frame", "OpenFunction _frame"], source.Calls);
            Assert.Equal(["_frame"], opened);
            Assert.Equal(["COMMON", "The game starts"], Listed(StartMenu(panels).Items));
        });
    }

    [Fact]
    public void A_start_is_listed_by_its_sentence_and_engine_name_and_an_action_by_its_name()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.PutFunction("_frame");
            source.PutFunction("helper");
            var panels = WithStarts(source, "_frame");

            var start = panels.Find<StackPanel>("function:_frame:start")!;
            Assert.Equal(["Every frame", "_frame"], start.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text));
            Assert.Null(panels.Find<TextBox>("function:_frame:name"));
            Assert.Equal("helper", panels.Find<TextBox>("function:helper:name")!.Text);
        });
    }

    [Fact]
    public void A_start_that_already_has_a_function_is_not_offered_and_one_named_in_two_classes_is_offered_once()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.PutFunction("_start");
            source.Overrides.Add(Virtual("_frame", "Object"));

            Assert.Equal(["COMMON", "Every frame"], Listed(StartMenu(WithStarts(source, "_start", "_frame")).Items));
        });
    }

    [Fact]
    public void The_add_start_button_says_why_it_is_off()
    {
        UiThread.Run(() =>
        {
            var none = new ScriptPanels(FakeGraphSource.Sample()).Find<Button>("start:add")!;
            var source = FakeGraphSource.Sample();
            source.PutFunction("_frame");
            var used = WithStarts(source, "_frame").Find<Button>("start:add")!;

            Assert.False(none.IsEnabled);
            Assert.Equal("This kind of thing has no starts.", ((HoverCard)ToolTip.GetTip(none)!).Line);
            Assert.False(used.IsEnabled);
            Assert.Equal("Every start is in the graph already.", ((HoverCard)ToolTip.GetTip(used)!).Line);
        });
    }

    [Fact]
    public void The_sections_are_named_in_plain_words_with_the_engine_names_dim()
    {
        UiThread.Run(() =>
        {
            var (_, panels) = Open();
            var texts = ReferenceWindow.Texts(panels).ToList();

            var at = new[] { "Problems", "Starts when", "Remembers", "Variables", "My actions", "Functions", "Values only main uses", "Locals", "Messages", "Signals" }.Select(word => texts.IndexOf(word)).ToList();

            Assert.DoesNotContain(-1, at);
            Assert.Equal(at.Order(), at);
            Assert.Contains("It gets", texts);
            Assert.Contains("It gives back", texts);
        });
    }

    [Fact]
    public void My_actions_says_what_an_action_is_when_there_are_none()
    {
        UiThread.Run(() =>
        {
            var panels = new ScriptPanels(new FakeGraphSource { Empty = true });

            Assert.Equal("An action is a set of steps you can run from many places, like open_door.", panels.Find<TextBlock>("actions:empty")!.Text);
        });
    }

    [Fact]
    public void The_function_on_show_says_why_it_cannot_be_shown_again()
    {
        UiThread.Run(() =>
        {
            var (_, panels) = Open();
            var showing = panels.Find<Button>("function:main:open")!;

            Assert.Equal("Its steps are on the canvas already.", ((HoverCard)ToolTip.GetTip(showing)!).Line);
            Assert.Equal("Shows its steps on the canvas.", ((HoverCard)ToolTip.GetTip(panels.Find<Button>("function:helper:open")!)!).Line);
        });
    }

    [Fact]
    public void The_add_a_remembered_value_card_says_what_to_do_first_and_Enter_adds()
    {
        UiThread.Run(() =>
        {
            var (source, panels) = Open();
            Click(panels, "variable:add");
            var tool = panels.VariableTool;
            var card = (HoverCard)ToolTip.GetTip(tool.AddButton)!;

            Assert.Equal("Add a remembered value", tool.Head.Heading.Text);
            Assert.Equal("Add Variable", tool.Head.EngineName);
            tool.NameBox.Text = "";
            Assert.Equal("Give it a name first.", card.Line);
            tool.NameBox.Text = "health";
            Assert.Equal("Adds the value to the graph.", card.Line);
            tool.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.Equal(["AddVariable health 0"], source.Calls);
        });
    }

    [Theory]
    [InlineData(0, "Any")]
    [InlineData(1, "Yes or no")]
    [InlineData(4, "Text")]
    [InlineData(9, "Vector3")]
    public void A_type_goes_by_its_plain_words(int type, string words) => Assert.Equal(words, PanelFields.TypeWords(type));

    [Fact]
    public void Enter_in_the_name_box_renames_once_and_the_row_follows()
    {
        UiThread.Run(() =>
        {
            var (source, panels) = Open();
            var box = panels.Find<TextBox>("variable:total:name")!;

            box.Text = "sum";
            Press(box, Key.Enter);
            box.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));

            Assert.Equal(["RenameVariable total sum"], source.Calls);
            Assert.Null(panels.Find<TextBox>("variable:total:name"));
            Assert.Equal("sum", panels.Find<TextBox>("variable:sum:name")!.Text);
        });
    }

    [Fact]
    public void Leaving_a_box_alone_or_pressing_Escape_edits_nothing()
    {
        UiThread.Run(() =>
        {
            var (source, panels) = Open();
            var box = panels.Find<TextBox>("variable:total:name")!;

            box.RaiseEvent(new FocusChangedEventArgs(InputElement.LostFocusEvent));
            var again = panels.Find<TextBox>("variable:total:name")!;
            again.Text = "sum";
            Press(again, Key.Escape);

            Assert.Empty(source.Calls);
            Assert.Equal("total", again.Text);
        });
    }

    [Fact]
    public void A_variable_is_retyped_exported_and_described_through_its_own_calls()
    {
        UiThread.Run(() =>
        {
            var (source, panels) = Open();

            panels.Find<ComboBox>("variable:total:type")!.SelectedIndex = 3;
            panels.Find<CheckBox>("variable:total:exported")!.IsChecked = true;
            Type(panels, "variable:total:default", "5");
            Type(panels, "variable:total:category", "Score");
            Type(panels, "variable:total:description", "Points so far");

            Assert.Equal(
                [
                    "SetVariableType total 3",
                    "SetVariableExported total True",
                    "SetVariableDefault total 5",
                    "SetVariableInfo total Score ",
                    "SetVariableInfo total Score Points so far",
                ],
                source.Calls);
        });
    }

    [Fact]
    public void Remove_takes_the_row_away()
    {
        UiThread.Run(() =>
        {
            var (source, panels) = Open();

            Click(panels, "variable:total:remove");
            Click(panels, "signal:scored:remove");
            Click(panels, "function:helper:remove");

            Assert.Equal(["RemoveVariable total", "RemoveSignal scored", "RemoveFunction helper"], source.Calls);
            Assert.Null(panels.Find<TextBox>("variable:total:name"));
            Assert.Null(panels.Find<TextBox>("signal:scored:name"));
            Assert.Null(panels.Find<TextBox>("function:helper:name"));
        });
    }

    [Fact]
    public void A_refused_rename_shows_the_reason_and_the_box_goes_back_to_the_name()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.Put(Total());
            source.Put(new GraphVariable("count", 2, "0", false, 0, "", "", ""));
            var panels = new ScriptPanels(source);
            Type(panels, "variable:total:name", "count");

            Assert.Equal("There is already something named count.", panels.Notice);
            Assert.Equal("total", panels.Find<TextBox>("variable:total:name")!.Text);
        });
    }

    [Fact]
    public void A_name_the_engine_does_not_take_is_refused_with_its_reason()
    {
        UiThread.Run(() =>
        {
            var (_, panels) = Open();

            Type(panels, "signal:scored:name", "two words");

            Assert.Equal("That is not a name the engine takes.", panels.Notice);
            Assert.Equal("scored", panels.Find<TextBox>("signal:scored:name")!.Text);
        });
    }

    [Fact]
    public void An_edit_the_engine_takes_clears_the_last_reason()
    {
        UiThread.Run(() =>
        {
            var (_, panels) = Open();
            Type(panels, "signal:scored:name", "two words");
            Assert.NotEqual("", panels.Notice);

            Type(panels, "signal:scored:name", "points");

            Assert.Equal("", panels.Notice);
        });
    }

    [Fact]
    public void The_signature_edits_the_entry_step_of_the_function_on_show()
    {
        UiThread.Run(() =>
        {
            var (source, panels) = Open();

            Click(panels, "function:arguments:add");
            panels.Find<ComboBox>("function:returns")!.SelectedIndex = 4;
            Type(panels, "function:description", "Adds up");

            Assert.Equal(
                [
                    "SetStepParameter 1 arguments [{\"name\":\"value\",\"type\":0}]",
                    "SetStepParameter 1 returns 4",
                    "SetStepParameter 1 description \"Adds up\"",
                ],
                source.Calls);
        });
    }

    [Fact]
    public void An_argument_is_renamed_retyped_and_removed_with_the_rest_of_the_list_kept()
    {
        UiThread.Run(() =>
        {
            var (source, panels) = Open();
            source.SetStepParameter(1, "arguments", GraphTyped.WriteList([new GraphTyped("a", 2), new GraphTyped("b", 3)]));
            source.Calls.Clear();
            panels = new ScriptPanels(source);

            Type(panels, "function:arguments:1:name", "c");
            panels.Find<ComboBox>("function:arguments:0:type")!.SelectedIndex = 4;
            Click(panels, "function:arguments:0:remove");

            Assert.Equal(
                [
                    "SetStepParameter 1 arguments [{\"name\":\"a\",\"type\":2},{\"name\":\"c\",\"type\":3}]",
                    "SetStepParameter 1 arguments [{\"name\":\"a\",\"type\":4},{\"name\":\"c\",\"type\":3}]",
                    "SetStepParameter 1 arguments [{\"name\":\"c\",\"type\":3}]",
                ],
                source.Calls);
        });
    }

    [Fact]
    public void Locals_are_edited_on_the_entry_step_and_each_has_a_grip()
    {
        UiThread.Run(() =>
        {
            var (source, panels) = Open();
            source.SetStepParameter(1, "locals", GraphTyped.WriteList([new GraphTyped("n", 2)]));
            source.Calls.Clear();
            panels = new ScriptPanels(source);

            Assert.NotNull(panels.Find<TextBlock>("local:0:drag"));
            Click(panels, "local:add");

            Assert.Equal(["SetStepParameter 1 locals [{\"name\":\"n\",\"type\":2},{\"name\":\"value\",\"type\":0}]"], source.Calls);
        });
    }

    [Fact]
    public void A_signal_takes_its_arguments_as_a_whole_list()
    {
        UiThread.Run(() =>
        {
            var (source, panels) = Open();

            Click(panels, "signal:scored:arguments:add");

            Assert.Equal(["SetSignalArguments scored [{\"name\":\"amount\",\"type\":2},{\"name\":\"value\",\"type\":0}]"], source.Calls);
        });
    }

    [Fact]
    public void A_grip_carries_the_kind_and_name_of_its_row()
    {
        UiThread.Run(() =>
        {
            var (_, panels) = Open();

            Assert.NotNull(panels.Find<TextBlock>("variable:total:drag"));
            Assert.NotNull(panels.Find<TextBlock>("function:helper:drag"));
            Assert.NotNull(panels.Find<TextBlock>("signal:scored:drag"));
            var transfer = new GraphDrop(GraphDropKind.Variable, "total").ToTransfer();
            Assert.True(GraphDrop.TryParse(transfer.TryGetValue(GraphDrop.Format), out var drop));
            Assert.Equal(new GraphDrop(GraphDropKind.Variable, "total"), drop);
        });
    }

    [Fact]
    public void Show_opens_another_function_and_tells_the_canvas()
    {
        UiThread.Run(() =>
        {
            var (source, panels) = Open();
            var opened = new List<string>();
            panels.FunctionOpened += opened.Add;

            Click(panels, "function:helper:open");

            Assert.Equal(["OpenFunction helper"], source.Calls);
            Assert.Equal(["helper"], opened);
        });
    }

    [Fact]
    public void The_function_on_show_cannot_be_opened_again()
    {
        UiThread.Run(() =>
        {
            var (_, panels) = Open();

            Assert.False(panels.Find<Button>("function:main:open")!.IsEnabled);
            Assert.True(panels.Find<Button>("function:helper:open")!.IsEnabled);
        });
    }

    static GraphStep Signaling(int id, string kind, string signal) =>
        FakeGraphSource.MakeStep(id, kind, 0, 0, [], [], null, ("signal", $"\"{signal}\""));

    static (FakeGraphSource Source, ScriptPanels Panels) OpenWithLinks()
    {
        var source = FakeGraphSource.Sample();
        source.Put(new GraphSignal("scored", [new GraphTyped("amount", 2)]));
        source.Put(Signaling(4, "emit", "scored"));
        source.PutFunction("helper", Signaling(5, "await_signal", "scored"));
        return (source, new ScriptPanels(source));
    }

    [Fact]
    public void Each_signal_lists_the_steps_that_use_it_in_every_function()
    {
        UiThread.Run(() =>
        {
            var (_, panels) = OpenWithLinks();

            Assert.Equal("scored", panels.Find<TextBlock>("connections:scored")!.Text);
            Assert.Equal("main: emits scored", panels.Find<Button>("connection:main:4")!.Content);
            Assert.Equal("helper: waits for scored", panels.Find<Button>("connection:helper:5")!.Content);
        });
    }

    [Fact]
    public void A_link_in_another_function_opens_that_function_and_then_reveals_the_step()
    {
        UiThread.Run(() =>
        {
            var (source, panels) = OpenWithLinks();
            var events = new List<string>();
            panels.FunctionOpened += name => events.Add($"opened {name}");
            panels.StepRevealed += id => events.Add($"revealed {id}");

            Click(panels, "connection:helper:5");

            Assert.Equal(["OpenFunction helper"], source.Calls);
            Assert.Equal(["opened helper", "revealed 5"], events);
        });
    }

    [Fact]
    public void A_link_in_the_function_on_show_only_reveals_the_step()
    {
        UiThread.Run(() =>
        {
            var (source, panels) = OpenWithLinks();
            var events = new List<string>();
            panels.FunctionOpened += name => events.Add($"opened {name}");
            panels.StepRevealed += id => events.Add($"revealed {id}");

            Click(panels, "connection:main:4");

            Assert.Empty(source.Calls);
            Assert.Equal(["revealed 4"], events);
        });
    }

    static (FakeGraphSource Source, ScriptPanels Panels) OpenWithProblems()
    {
        var source = FakeGraphSource.Sample();
        source.PutFunction("helper", FakeGraphSource.MakeStep(5, "print", 0, 0, [], []) with { Problem = "Wire a value." });
        source.Put(new GraphWarning("unreachable_step", "main", 3, "", "Nothing runs this step."));
        return (source, new ScriptPanels(source));
    }

    [Fact]
    public void The_problems_of_the_script_are_listed_with_a_row_for_each()
    {
        UiThread.Run(() =>
        {
            var (_, panels) = OpenWithProblems();

            Assert.Equal("Wire a value.", panels.Find<TextBlock>("problem:helper:5:text")!.Text);
            Assert.Equal("Nothing runs this step.", panels.Find<TextBlock>("problem:main:3:text")!.Text);
        });
    }

    [Fact]
    public void A_problem_in_another_function_opens_that_function_and_then_reveals_the_step()
    {
        UiThread.Run(() =>
        {
            var (source, panels) = OpenWithProblems();
            var events = new List<string>();
            panels.FunctionOpened += name => events.Add($"opened {name}");
            panels.StepRevealed += id => events.Add($"revealed {id}");

            Click(panels, "problem:helper:5");
            Click(panels, "problem:main:3");

            Assert.Equal(["OpenFunction helper", "OpenFunction main"], source.Calls);
            Assert.Equal(["opened helper", "revealed 5", "opened main", "revealed 3"], events);
        });
    }
}
