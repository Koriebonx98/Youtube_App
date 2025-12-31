using System.Text;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using System.IO;

namespace youtube_pc_app
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Set persistent user data folder for WebView2
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "youtube_pc_app", "WebView2UserData");
            Directory.CreateDirectory(userDataFolder);
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await webView.EnsureCoreWebView2Async(env);

            // Add filter for all requests
            webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);

            // Example: block some common ad domains (expand this list as needed)
            string[] adHosts = new[]
            {
                "doubleclick.net",
                "googlesyndication.com",
                "googleadservices.com",
                "youtube.com/api/stats/ads",
                "youtube.com/pagead/"
            };

            webView.CoreWebView2.WebResourceRequested += (s, args) =>
            {
                var uri = args.Request.Uri;
                foreach (var host in adHosts)
                {
                    if (uri.Contains(host))
                    {
                        args.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(
                            Stream.Null, 403, "Blocked", "Content-Type: text/plain");
                        break;
                    }
                }
            };

            // Inject ad-blocking script after navigation (optional, for UI ad elements)
            webView.CoreWebView2.NavigationCompleted += (s, args) =>
            {
                string adBlockScript = @"
                    // Remove YouTube video ads
                    setInterval(() => {
                        let ad = document.querySelector('.ad-showing');
                        if (ad) {
                            let video = document.querySelector('video');
                            if (video) video.currentTime = video.duration;
                        }
                        // Remove banner ads
                        let banners = document.querySelectorAll('.ytd-banner-promo-renderer, .ytp-ad-module, .ytp-ad-overlay-container, .ytp-ad-player-overlay');
                        banners.forEach(b => b.remove());
                    }, 1000);
                ";
                webView.CoreWebView2.ExecuteScriptAsync(adBlockScript);
            };

            webView.CoreWebView2.Navigate("https://www.youtube.com");
        }

        private OnScreenKeyboardWindow oskWindow;
        private void ShowOnScreenKeyboard()
        {
            if (oskWindow == null)
            {
                oskWindow = new OnScreenKeyboardWindow();
                oskWindow.KeyPressed += OnScreenKeyboard_KeyPressed;
                oskWindow.CloseRequested += () => { oskWindow.Close(); oskWindow = null; };
                oskWindow.Owner = this;
                oskWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                oskWindow.Show();
            }
            else
            {
                oskWindow.Activate();
            }
        }

        private void OnScreenKeyboard_KeyPressed(string key)
        {
            // Send key to search box in WebView2
            string js;
            if (key == "<")
                js = @"var s=document.querySelector('input#search');if(s){s.value=s.value.slice(0,-1);}";
            else if (key == "_")
                js = @"var s=document.querySelector('input#search');if(s){s.value+='_';}";
            else if (key == " ")
                js = @"var s=document.querySelector('input#search');if(s){s.value+=' ';}";
            else
                js = $"var s=document.querySelector('input#search');if(s){{s.value+=\"{key}\";}}";
            webView.CoreWebView2.ExecuteScriptAsync(js);
        }
    }
}