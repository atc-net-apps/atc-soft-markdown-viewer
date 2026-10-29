using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;

namespace SpikeWinUI;

/// <summary>
/// Custom entry point (DISABLE_XAML_GENERATED_MAIN) so the WebView2 environment can be created before
/// XAML starts when --early-env is passed.
/// </summary>
public static class Program
{
    internal static Task<CoreWebView2Environment>? EarlyEnvironment { get; private set; }

    [STAThread]
    public static void Main()
    {
        Marks.Add("main");
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (Options.Current.EarlyEnvironment)
        {
            EarlyEnvironment = CreateEnvironment();
        }

        Application.Start(callbackParams =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });
    }

    internal static Task<CoreWebView2Environment> CreateEnvironment()
    {
        var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AtcMarkdownViewerSpike", "WebView2");
        return CoreWebView2Environment.CreateWithOptionsAsync(string.Empty, userData, new CoreWebView2EnvironmentOptions()).AsTask();
    }
}
