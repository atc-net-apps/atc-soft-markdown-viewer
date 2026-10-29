using Microsoft.UI.Xaml;
using SpikeMarkdown;

namespace SpikeWinUI;

public partial class App : Application
{
    private MainWindow? window;

    public App()
    {
        Marks.Add("appCtor");

        // ARC-006 step 2: read + parse + render on a background thread while the UI starts.
        if (Options.Current.File is { } file)
        {
            Document = Task.Run(() =>
            {
                var text = File.ReadAllText(file);
                var output = SpikeRenderer.Render(text);
                Marks.Add("renderDone");
                return (text, output);
            });
        }

        InitializeComponent();
    }

    internal static Task<(string Text, RenderOutput Output)>? Document { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Marks.Add("onLaunched");

        // ARC-006 step 3: start the WebView2 environment before the window exists
        // (or even before XAML, when Program created it with --early-env).
        var environment = Program.EarlyEnvironment ?? Program.CreateEnvironment();

        window = new MainWindow(environment);
        window.Activate();
        Marks.Add("activated");
    }
}
