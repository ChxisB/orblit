using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;

namespace Orblit.Editor.App.Tests;

public sealed class StoreViewTests
{
    const string Two = """
        {"formatVersion":1,"kits":[
          {"name":"Platformer","publisher":"ada","slug":"platformer","version":"1.2.0","summary":"A side scroller."},
          {"name":"Racing","publisher":"bo","slug":"racing","version":"0.1.0"}]}
        """;

    static Func<CancellationToken, Task<string>> Answer(string text) => _ => Task.FromResult(text);

    static string[] Shown(StoreView view)
    {
        ((Window)TopLevel.GetTopLevel(view)!).UpdateLayout();
        return [.. view.List.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text!)];
    }

    [Fact]
    public void It_starts_with_no_rows_and_has_asked_for_nothing()
    {
        UiThread.Run(() =>
        {
            var asked = 0;
            var view = Open(_ => { asked++; return Task.FromResult(Two); });

            Assert.Equal("Press Load to read the store.", view.Status.Text);
            Assert.Null(view.List.ItemsSource);
            Assert.Equal(0, asked);
        });
    }

    [Fact]
    public void Load_shows_a_row_for_each_kit_with_its_name_version_publisher_and_summary()
    {
        UiThread.Run(() =>
        {
            var view = Open(Answer(Two));

            view.LoadAsync().Wait();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("2 kits.", view.Status.Text);
            Assert.Equal(["Platformer", "1.2.0 by ada", "A side scroller.", "Racing", "0.1.0 by bo"], Shown(view));
        });
    }

    [Fact]
    public void One_kit_reads_as_one_kit_and_none_as_none()
    {
        UiThread.Run(() =>
        {
            var one = Open(Answer("""{"formatVersion":1,"kits":[{"name":"A","publisher":"p","slug":"a","version":"1"}]}"""));
            var none = Open(Answer("""{"formatVersion":1,"kits":[]}"""));

            one.LoadAsync().Wait();
            none.LoadAsync().Wait();

            Assert.Equal("1 kit.", one.Status.Text);
            Assert.Equal("0 kits.", none.Status.Text);
        });
    }

    [Fact]
    public void A_failed_read_says_why_and_keeps_the_rows_from_before()
    {
        UiThread.Run(() =>
        {
            var text = Two;
            var view = Open(_ => text.Length == 0 ? throw new IOException("The store did not answer.") : Task.FromResult(text));
            view.LoadAsync().Wait();
            text = "";

            view.LoadAsync().Wait();

            Assert.Equal("The store did not answer.", view.Status.Text);
            Assert.Equal(2, ((System.Collections.ICollection)view.List.ItemsSource!).Count);
            Assert.True(view.LoadButton.IsEnabled);
        });
    }

    [Fact]
    public void An_index_the_reader_refuses_is_said_in_the_status()
    {
        UiThread.Run(() =>
        {
            var view = Open(Answer("""{"formatVersion":9,"kits":[]}"""));

            view.LoadAsync().Wait();

            Assert.Equal("The index is in a format this editor does not read.", view.Status.Text);
        });
    }

    [Fact]
    public void A_second_load_replaces_the_rows_and_a_press_during_a_read_does_nothing()
    {
        UiThread.Run(() =>
        {
            var reads = new Queue<TaskCompletionSource<string>>();
            var view = Open(_ =>
            {
                var read = new TaskCompletionSource<string>();
                reads.Enqueue(read);
                return read.Task;
            });

            var first = view.LoadAsync();
            var ignored = view.LoadAsync();

            Assert.Single(reads);
            Assert.False(view.LoadButton.IsEnabled);
            Assert.Equal("Loading the store.", view.Status.Text);
            reads.Dequeue().SetResult(Two);
            Dispatcher.UIThread.RunJobs();
            Assert.True(first.IsCompleted);
            Assert.True(ignored.IsCompleted);
            Assert.True(view.LoadButton.IsEnabled);

            var second = view.LoadAsync();
            reads.Dequeue().SetResult("""{"formatVersion":1,"kits":[{"name":"Only","publisher":"p","slug":"o","version":"1"}]}""");
            Dispatcher.UIThread.RunJobs();

            Assert.True(second.IsCompleted);
            Assert.Equal(["Only", "1 by p"], Shown(view));
        });
    }

    // The rows are measured only when the view has a parent window to lay out in.
    static StoreView Open(Func<CancellationToken, Task<string>> read)
    {
        var view = new StoreView(read);
        new Window { Content = view, Width = 600, Height = 400 }.Show();
        return view;
    }
}
