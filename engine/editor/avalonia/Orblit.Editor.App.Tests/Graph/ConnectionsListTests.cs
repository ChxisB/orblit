using Avalonia.Controls;
using Avalonia.Interactivity;
using Orblit.Editor.App.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class ConnectionsListTests
{
    static GraphStep Step(int id, string kind, params (string Name, string Value)[] parameters) =>
        FakeGraphSource.MakeStep(id, kind, 0, 0, [], [], null, parameters);

    static GraphDocument Document(IReadOnlyList<GraphSignal> signals, params GraphFunction[] functions) =>
        new("Entity", "", [], signals, functions, []);

    static GraphFunction Function(string name, params GraphStep[] steps) => new(name, "", steps, [], []);

    // A script that emits and listens to its own signal, connects to a button, and prints.
    static GraphDocument Sample() => Document(
        [new GraphSignal("scored", []), new GraphSignal("idle", [])],
        Function("main", Step(1, "emit", ("signal", "\"scored\"")), Step(2, "connect", ("signal", "\"pressed\""), ("method", "\"on_pressed\"")), Step(3, "print")),
        Function("helper", Step(4, "signal_received", ("signal", "\"scored\"")), Step(5, "await_signal", ("signal", "\"scored\"")), Step(6, "emit_on", ("class", "\"Button\""), ("signal", "\"pressed\""))));

    [Fact]
    public void Find_lists_the_steps_that_emit_listen_connect_or_wait_in_the_order_of_signal_function_and_step()
    {
        var links = ConnectionsList.Find(Sample());

        Assert.Equal(
            [
                new SignalLink("pressed", "helper", 6, "emits on"),
                new SignalLink("pressed", "main", 2, "connects to"),
                new SignalLink("scored", "helper", 4, "listens for"),
                new SignalLink("scored", "helper", 5, "waits for"),
                new SignalLink("scored", "main", 1, "emits"),
            ],
            links);
    }

    [Fact]
    public void A_step_with_no_signal_set_is_listed_under_a_blank_name()
    {
        var links = ConnectionsList.Find(Document([], Function("main", Step(1, "emit"))));

        Assert.Equal([new SignalLink("", "main", 1, "emits")], links);
    }

    [Fact]
    public void Steps_of_one_function_that_use_one_signal_are_listed_by_step_number()
    {
        var links = ConnectionsList.Find(Document([], Function("main", Step(7, "emit", ("signal", "\"a\"")), Step(3, "emit", ("signal", "\"a\"")))));

        Assert.Equal([3, 7], links.Select(link => link.Step));
    }

    [Fact]
    public void A_step_with_no_signal_set_is_shown_under_a_heading_that_says_so()
    {
        UiThread.Run(() =>
        {
            var list = new ConnectionsList(Document([], Function("main", Step(1, "emit"))), (_, _) => { });

            Assert.Equal("(no signal)", list.Find<TextBlock>("connections:")!.Text);
            Assert.Equal("main: emits (no signal)", list.Find<Button>("connection:main:1")!.Content);
        });
    }

    [Fact]
    public void A_signal_name_as_a_string_name_reads_the_same_as_a_string()
    {
        var links = ConnectionsList.Find(Document([], Function("main", Step(1, "emit", ("signal", "&\"scored\"")))));

        Assert.Equal("scored", Assert.Single(links).Signal);
    }

    [Fact]
    public void Each_signal_has_a_heading_and_each_link_a_row_that_jumps_to_its_step()
    {
        UiThread.Run(() =>
        {
            var jumps = new List<(string, int)>();
            var list = new ConnectionsList(Sample(), (function, step) => jumps.Add((function, step)));

            Assert.Equal("scored", list.Find<TextBlock>("connections:scored")!.Text);
            Assert.Equal("pressed", list.Find<TextBlock>("connections:pressed")!.Text);
            list.Find<Button>("connection:helper:5")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal([("helper", 5)], jumps);
            Assert.Equal("helper: waits for scored", list.Find<Button>("connection:helper:5")!.Content);
        });
    }

    [Fact]
    public void A_declared_signal_nothing_uses_says_so()
    {
        UiThread.Run(() =>
        {
            var list = new ConnectionsList(Sample(), (_, _) => { });

            Assert.Equal("idle", list.Find<TextBlock>("connections:idle")!.Text);
            Assert.Equal("No links.", list.Find<TextBlock>("connections:idle:none")!.Text);
            Assert.Null(list.Find<TextBlock>("connections:scored:none"));
        });
    }

    [Fact]
    public void A_script_with_no_signals_and_no_links_shows_one_line()
    {
        UiThread.Run(() =>
        {
            var list = new ConnectionsList(Document([], Function("main", Step(1, "print"))), (_, _) => { });

            Assert.Equal("No signals.", list.Find<TextBlock>("connections:empty")!.Text);
        });
    }
}
