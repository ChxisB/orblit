using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Orblit.Editor.App.Graph;
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
            Click(panels, "variable:add");
            Click(panels, "signal:add");
            Click(panels, "function:add");

            Assert.Equal(["AddVariable variable 0", "AddVariable variable2 0", "AddSignal signal", "AddFunction function"], source.Calls);
            Assert.NotNull(panels.Find<TextBox>("variable:variable2:name"));
        });
    }

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
}
