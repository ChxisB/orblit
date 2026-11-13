using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Orblit.Editor.App.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class StepInspectorTests
{
    // Step 3 prints a text. The sample wires its run input and leaves the text as a value.
    static (FakeGraphSource Source, StepInspector Inspector) Open(int? step = 3, Action<FakeGraphSource>? shape = null)
    {
        var source = FakeGraphSource.Sample();
        shape?.Invoke(source);
        var inspector = new StepInspector(source);
        inspector.Show(step);
        return (source, inspector);
    }

    static void Press(Control target, Key key) =>
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

    static void Click(StepInspector inspector, string id) => inspector.Find<Button>(id)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static GraphStep Step(int id, string kind, GraphPort[] inputs, GraphPort[] outputs, params (string Name, string Value)[] parameters) =>
        FakeGraphSource.MakeStep(id, kind, 100, 40, inputs, outputs, null, parameters);

    [Fact]
    public void No_step_shows_a_hint_and_no_rows()
    {
        UiThread.Run(() =>
        {
            var (_, inspector) = Open(null);

            Assert.Equal("No step selected.", inspector.Heading);
            Assert.Null(inspector.Find<TextBox>("input:1:value"));
        });
    }

    [Fact]
    public void A_step_that_is_gone_shows_the_hint()
    {
        UiThread.Run(() =>
        {
            var (_, inspector) = Open(99);

            Assert.Equal("No step selected.", inspector.Heading);
        });
    }

    [Fact]
    public void An_unwired_data_input_shows_its_value_and_a_flow_input_shows_nothing()
    {
        UiThread.Run(() =>
        {
            var (_, inspector) = Open();

            Assert.Equal("print", inspector.Heading);
            Assert.Equal("\"hi\"", inspector.Find<TextBox>("input:1:value")!.Text);
            Assert.Null(inspector.Find<TextBox>("input:0:value"));
        });
    }

    [Fact]
    public void A_wired_input_says_so_and_has_no_value_box()
    {
        UiThread.Run(() =>
        {
            var (_, inspector) = Open(3, source =>
            {
                source.Put(Step(4, "constant", [], [FakeGraphSource.Data("value", 0)]));
                source.Put(new GraphWire(4, 0, 3, 1));
            });

            Assert.Null(inspector.Find<TextBox>("input:1:value"));
            Assert.NotNull(inspector.Find<TextBlock>("input:1:wired"));
        });
    }

    [Fact]
    public void Editing_a_value_sets_the_parameter_the_port_names()
    {
        UiThread.Run(() =>
        {
            var (source, inspector) = Open();
            var box = inspector.Find<TextBox>("input:1:value")!;

            box.Text = "\"bye\"";
            Press(box, Key.Enter);

            Assert.Equal(["SetStepParameter 3 text \"bye\""], source.Calls);
            Assert.Equal("\"bye\"", inspector.Find<TextBox>("input:1:value")!.Text);
        });
    }

    [Fact]
    public void Reset_writes_the_zero_of_the_type()
    {
        UiThread.Run(() =>
        {
            var (source, inspector) = Open();

            Click(inspector, "input:1:reset");

            Assert.Equal(["SetStepParameter 3 text \"\""], source.Calls);
        });
    }

    [Fact]
    public void A_type_with_no_zero_has_no_reset_button()
    {
        UiThread.Run(() =>
        {
            var (_, inspector) = Open(4, source => source.Put(Step(4, "call", [FakeGraphSource.Data("target", 25, "target")], [])));

            Assert.NotNull(inspector.Find<TextBox>("input:0:value"));
            Assert.Null(inspector.Find<Button>("input:0:reset"));
        });
    }

    [Fact]
    public void A_parameter_no_pin_holds_has_a_box_of_its_own()
    {
        UiThread.Run(() =>
        {
            var (source, inspector) = Open(4, s => s.Put(Step(4, "get", [], [FakeGraphSource.Data("value", 0)], ("name", "\"total\""))));
            var box = inspector.Find<TextBox>("parameter:name")!;

            box.Text = "\"sum\"";
            Press(box, Key.Enter);

            Assert.Equal(["SetStepParameter 4 name \"sum\""], source.Calls);
        });
    }

    [Fact]
    public void The_entry_step_leaves_the_signature_to_the_function_panel()
    {
        UiThread.Run(() =>
        {
            var (_, inspector) = Open(4, source => source.Put(Step(4, "entry", [], [FakeGraphSource.Flow("then")], ("arguments", "[]"), ("locals", "[]"), ("returns", "0"), ("description", "\"\""), ("note", "1"))));

            Assert.Null(inspector.Find<TextBox>("parameter:arguments"));
            Assert.Null(inspector.Find<TextBox>("parameter:locals"));
            Assert.Null(inspector.Find<TextBox>("parameter:returns"));
            Assert.Null(inspector.Find<TextBox>("parameter:description"));
            Assert.NotNull(inspector.Find<TextBox>("parameter:note"));
        });
    }

    [Fact]
    public void An_input_becomes_a_variable_and_a_Get_step_wired_to_it()
    {
        UiThread.Run(() =>
        {
            var (source, inspector) = Open();

            Click(inspector, "input:1:variable");

            Assert.Equal(
                [
                    "AddVariable text 4",
                    "AddStep 4 get",
                    "SetStepParameter 4 name \"text\"",
                    "MoveStep 4 280 40",
                    "Connect 4.0 3.1",
                ],
                source.Calls);
            Assert.Equal("text", Assert.Single(source.Document.Variables).Name);
        });
    }

    [Fact]
    public void An_input_becomes_a_local_on_the_entry_step_and_a_Local_step()
    {
        UiThread.Run(() =>
        {
            var (source, inspector) = Open();

            Click(inspector, "input:1:local");

            Assert.Equal(
                [
                    "SetStepParameter 1 locals [{\"name\":\"text\",\"type\":4}]",
                    "AddStep 4 local",
                    "SetStepParameter 4 name \"text\"",
                    "MoveStep 4 280 40",
                    "Connect 4.0 3.1",
                ],
                source.Calls);
        });
    }

    [Fact]
    public void An_output_becomes_a_variable_and_a_Set_step_it_feeds()
    {
        UiThread.Run(() =>
        {
            var (source, inspector) = Open(4, s => s.Put(Step(4, "constant", [], [FakeGraphSource.Data("value", 0)])));

            Click(inspector, "output:0:variable");

            Assert.Equal(
                [
                    "AddVariable value 0",
                    "AddStep 5 set",
                    "SetStepParameter 5 name \"value\"",
                    "MoveStep 5 340 40",
                    "Connect 4.0 5.1",
                ],
                source.Calls);
        });
    }

    [Fact]
    public void A_vector_output_splits_into_a_Decompose_step_of_its_parts()
    {
        UiThread.Run(() =>
        {
            var (source, inspector) = Open(4, s => s.Put(Step(4, "position", [], [FakeGraphSource.Data("value", 9)])));

            Click(inspector, "output:0:split");

            Assert.Equal(
                [
                    "AddStep 5 decompose",
                    "SetStepParameter 5 type \"Vector3\"",
                    "SetStepPins 5 3",
                    "MoveStep 5 340 40",
                    "Connect 4.0 5.0",
                ],
                source.Calls);
        });
    }

    [Fact]
    public void A_vector_input_combines_from_a_Compose_step_of_its_parts()
    {
        UiThread.Run(() =>
        {
            var (source, inspector) = Open(4, s => s.Put(Step(4, "move", [FakeGraphSource.Data("to", 5, "to")], [])));

            Click(inspector, "input:0:combine");

            Assert.Equal(
                [
                    "AddStep 5 compose",
                    "SetStepParameter 5 type \"Vector2\"",
                    "SetStepPins 5 2",
                    "MoveStep 5 -140 40",
                    "Connect 5.0 4.0",
                ],
                source.Calls);
        });
    }

    [Fact]
    public void A_type_with_no_parts_has_no_split_or_combine()
    {
        UiThread.Run(() =>
        {
            var (_, inspector) = Open(4, s => s.Put(Step(4, "name", [FakeGraphSource.Data("in", 4, "in")], [FakeGraphSource.Data("out", 4)])));

            Assert.Null(inspector.Find<Button>("input:0:combine"));
            Assert.Null(inspector.Find<Button>("output:0:split"));
            Assert.NotNull(inspector.Find<Button>("output:0:variable"));
        });
    }

    [Fact]
    public void A_variable_name_that_is_taken_gets_a_number()
    {
        UiThread.Run(() =>
        {
            var (source, inspector) = Open(3, s => s.Put(new GraphVariable("text", 4, "\"\"", false, 0, "", "", "")));
            source.Calls.Clear();

            Click(inspector, "input:1:variable");

            Assert.Equal("", inspector.Notice);
            Assert.Equal(["AddVariable text2 4", "AddStep 4 get", "SetStepParameter 4 name \"text2\"", "MoveStep 4 280 40", "Connect 4.0 3.1"], source.Calls);
        });
    }

    [Fact]
    public void A_local_name_avoids_the_arguments_and_keeps_the_locals_already_there()
    {
        UiThread.Run(() =>
        {
            var (source, inspector) = Open(3, s =>
            {
                s.SetStepParameter(1, "arguments", GraphTyped.WriteList([new GraphTyped("text", 4)]));
                s.SetStepParameter(1, "locals", GraphTyped.WriteList([new GraphTyped("n", 2)]));
            });
            source.Calls.Clear();

            Click(inspector, "input:1:local");

            Assert.Equal("SetStepParameter 1 locals [{\"name\":\"n\",\"type\":2},{\"name\":\"text2\",\"type\":4}]", source.Calls[0]);
        });
    }

    [Fact]
    public void A_parameter_a_pin_holds_is_not_listed_twice()
    {
        UiThread.Run(() =>
        {
            var (_, inspector) = Open();

            Assert.NotNull(inspector.Find<TextBox>("input:1:value"));
            Assert.Null(inspector.Find<TextBox>("parameter:text"));
        });
    }

    [Fact]
    public void A_wire_the_engine_refuses_is_reported_after_the_steps_it_made()
    {
        UiThread.Run(() =>
        {
            var (source, inspector) = Open();
            // Another editor wired the input after this one drew it, so the pin on show is out of date.
            source.Put(new GraphWire(3, 0, 3, 1));
            source.Calls.Clear();

            Click(inspector, "input:1:variable");

            Assert.Equal("That input already has a wire.", inspector.Notice);
            Assert.Equal("AddVariable text 4", source.Calls[0]);
            Assert.Equal("Connect 4.0 3.1", source.Calls[^1]);
        });
    }

    [Fact]
    public void A_local_with_no_entry_step_is_refused()
    {
        UiThread.Run(() =>
        {
            var (source, inspector) = Open(3);
            source.Forget(1);
            source.Calls.Clear();

            Click(inspector, "input:1:local");

            Assert.Equal("This function has no Entry step to hold a local.", inspector.Notice);
            Assert.Empty(source.Calls);
        });
    }

    [Fact]
    public void Putting_back_the_zero_of_a_type_with_none_is_refused()
    {
        UiThread.Run(() =>
        {
            var (source, _) = Open(4, s => s.Put(Step(4, "call", [FakeGraphSource.Data("target", 25, "target")], [])));
            var edits = new PinEdits(source);

            var result = edits.Reset(new Pin(source.Function!.Steps[^1], true, 0));

            Assert.False(result.Ok);
            Assert.Empty(source.Calls);
        });
    }
}
