using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
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

    static string[] Names(StoreView view) => [.. view.Shown.Select(kit => kit.Name)];

    static string[] Texts(Control control)
    {
        ((Window)TopLevel.GetTopLevel(control)!).UpdateLayout();
        return [.. control.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).OfType<string>()];
    }

    static Control CardOf(StoreView view, string name) => view.Cards.Single(card => card.Tag is KitEntry kit && kit.Name == name);

    static StoreView Loaded(string index)
    {
        var view = Open(Answer(index));
        view.LoadAsync().Wait();
        Dispatcher.UIThread.RunJobs();
        return view;
    }

    [Fact]
    public void It_starts_with_only_the_publish_card_and_has_asked_for_nothing()
    {
        UiThread.Run(() =>
        {
            var asked = 0;
            var view = Open(_ => { asked++; return Task.FromResult(Two); });

            Assert.Equal("Press Reload to read the store.", view.Status.Text);
            Assert.Empty(view.Shown);
            Assert.Contains("Publish your own kit", Texts(view.Cards.Single()));
            Assert.Contains("Nothing read yet", Texts(view));
            Assert.Equal(0, asked);
        });
    }

    [Fact]
    public void Reload_shows_a_card_for_each_kit_with_its_version_publisher_and_summary()
    {
        UiThread.Run(() =>
        {
            var view = Loaded(Two);

            Assert.Equal("2 kits in the index.", view.Status.Text);
            Assert.Equal(["Platformer", "Racing"], Names(view));
            Assert.Equal(["1.2.0", "Platformer", "by ada", "A side scroller.", "Install"], Texts(CardOf(view, "Platformer")));
            Assert.Equal(["0.1.0", "Racing", "by bo", "No summary.", "Install"], Texts(CardOf(view, "Racing")));
            Assert.Contains("Publish your own kit", Texts(view.Cards.Last()));
        });
    }

    [Fact]
    public void One_kit_reads_as_one_kit_and_none_shows_an_empty_store()
    {
        UiThread.Run(() =>
        {
            var one = Loaded("""{"formatVersion":1,"kits":[{"name":"A","publisher":"p","slug":"a","version":"1"}]}""");
            var none = Loaded("""{"formatVersion":1,"kits":[]}""");

            Assert.Equal("1 kit in the index.", one.Status.Text);
            Assert.Equal("No kits in the index.", none.Status.Text);
            Assert.Contains("No kits yet", Texts(none));
        });
    }

    [Theory]
    [InlineData("racing", "Racing")]
    [InlineData("ADA", "Platformer")]
    [InlineData("scroller", "Platformer")]
    public void The_search_keeps_the_kits_whose_name_publisher_or_summary_holds_the_words(string words, string kept)
    {
        UiThread.Run(() =>
        {
            var view = Loaded(Two);

            view.SearchBox.Text = words;

            Assert.Equal([kept], Names(view));
        });
    }

    [Fact]
    public void A_search_that_keeps_nothing_says_so_and_clearing_it_shows_every_kit()
    {
        UiThread.Run(() =>
        {
            var view = Loaded(Two);

            view.SearchBox.Text = "boat";
            Assert.Empty(view.Shown);
            Assert.Contains("Nothing matches", Texts(view));

            view.SearchBox.Text = "";
            Assert.Equal(["Platformer", "Racing"], Names(view));
        });
    }

    [Fact]
    public void A_click_on_a_card_opens_the_drawer_on_its_kit_and_marks_the_card()
    {
        UiThread.Run(() =>
        {
            var view = Loaded(Two);
            Assert.False(view.Drawer.IsVisible);

            ClickOn(CardOf(view, "Racing"));

            Assert.True(view.Drawer.IsVisible);
            Assert.Equal(["Racing", "by bo", "0.1.0", "bo", "racing"], view.Details);
            Assert.Equal(0, ((Border)CardOf(view, "Platformer")).BoxShadow.Count);
            Assert.NotEqual(0, ((Border)CardOf(view, "Racing")).BoxShadow.Count);
        });
    }

    [Fact]
    public void The_drawer_closes_on_its_cross_and_on_a_new_read()
    {
        UiThread.Run(() =>
        {
            var view = Loaded(Two);
            view.Choose(view.Shown.First());

            ReferenceWindow.Click(view.CloseButton);
            Assert.False(view.Drawer.IsVisible);
            Assert.Equal(0, ((Border)CardOf(view, "Platformer")).BoxShadow.Count);

            view.Choose(view.Shown.First());
            view.LoadAsync().Wait();
            Assert.False(view.Drawer.IsVisible);
        });
    }

    [Fact]
    public void Install_the_kit_page_the_filters_and_publishing_take_no_input_until_they_are_built()
    {
        UiThread.Run(() =>
        {
            var view = Loaded(Two);
            view.Choose(view.Shown.First());
            var waiting = view.GetLogicalDescendants().Concat(view.Drawer.GetLogicalDescendants()).OfType<Button>()
                .Where(button => button is not RadioButton && button != view.ReloadButton && button != view.CloseButton && button != view.InstallFileButton)
                .Concat(view.Filter.Choices.Skip(1))
                .ToList();

            // Two cards and the drawer each hold Install and a page button, and the publish card holds Learn how.
            Assert.Equal(9, waiting.Count);
            Assert.All(waiting, button => Assert.False(button.IsEnabled));
            Assert.All(waiting, button => Assert.Equal("Not built yet", (string?)ToolTip.GetTip(button)));
            Assert.True(view.Filter.Choices[0].IsEnabled);
        });
    }

    [Fact]
    public void Install_from_file_waits_for_a_project()
    {
        UiThread.Run(() =>
        {
            var view = Open(Answer(Two));

            Assert.False(view.InstallFileButton.IsEnabled);
            Assert.Equal("Unpack a kit's zip into the project", (string?)ToolTip.GetTip(view.InstallFileButton));
        });
    }

    [Fact]
    public void A_kit_zip_is_unpacked_into_the_project_and_said_in_the_status()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var installed = 0;
            var view = Open(Answer(Two));
            view.Installed += () => installed++;
            view.Show(scene);

            view.InstallFile("/kits/platformer.zip");

            Assert.True(view.InstallFileButton.IsEnabled);
            Assert.Equal(["/kits/platformer.zip"], scene.Kits);
            Assert.Equal("Installed platformer.zip into the project.", view.Status.Text);
            Assert.Equal(1, installed);
        });
    }

    [Fact]
    public void A_refused_kit_says_why_and_tells_no_one_it_installed()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { KitFault = new IOException("player.oscn is in the project already.") };
            var installed = 0;
            var view = Open(Answer(Two));
            view.Installed += () => installed++;
            view.Show(scene);

            view.InstallFile("/kits/platformer.zip");

            Assert.Equal("platformer.zip was not installed. player.oscn is in the project already.", view.Status.Text);
            Assert.Equal(0, installed);
        });
    }

    [Fact]
    public void The_window_gives_the_store_its_project()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = ReferenceWindow.OpenAtReferenceSize(scene);

            window.Store.InstallFile("/kits/platformer.zip");

            Assert.Equal(["/kits/platformer.zip"], scene.Kits);
        });
    }

    [Fact]
    public void A_failed_read_says_why_and_keeps_the_cards_from_before()
    {
        UiThread.Run(() =>
        {
            var text = Two;
            var view = Open(_ => text.Length == 0 ? throw new IOException("The store did not answer.") : Task.FromResult(text));
            view.LoadAsync().Wait();
            text = "";

            view.LoadAsync().Wait();

            Assert.Equal("The store did not answer.", view.Status.Text);
            Assert.Equal(["Platformer", "Racing"], Names(view));
            Assert.True(view.ReloadButton.IsEnabled);
        });
    }

    [Fact]
    public void An_index_the_reader_refuses_is_said_in_the_status()
    {
        UiThread.Run(() =>
        {
            var view = Loaded("""{"formatVersion":9,"kits":[]}""");

            Assert.Equal("The index is in a format this editor does not read.", view.Status.Text);
        });
    }

    [Fact]
    public void A_second_read_replaces_the_cards_and_a_press_during_a_read_does_nothing()
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
            Assert.False(view.ReloadButton.IsEnabled);
            Assert.Equal("Reading the store.", view.Status.Text);
            reads.Dequeue().SetResult(Two);
            Dispatcher.UIThread.RunJobs();
            Assert.True(first.IsCompleted);
            Assert.True(ignored.IsCompleted);
            Assert.True(view.ReloadButton.IsEnabled);

            var second = view.LoadAsync();
            reads.Dequeue().SetResult("""{"formatVersion":1,"kits":[{"name":"Only","publisher":"p","slug":"o","version":"1"}]}""");
            Dispatcher.UIThread.RunJobs();

            Assert.True(second.IsCompleted);
            Assert.Equal(["Only"], Names(view));
        });
    }

    static void ClickOn(Control control)
    {
        var window = (Window)TopLevel.GetTopLevel(control)!;
        window.UpdateLayout();
        var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
    }

    // The cards are measured only when the view has a parent window to lay out in.
    static StoreView Open(Func<CancellationToken, Task<string>> read)
    {
        var view = new StoreView(read);
        new Window { Content = view, Width = 1200, Height = 800 }.Show();
        return view;
    }
}
