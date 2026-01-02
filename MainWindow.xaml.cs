using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace youtube_pc_app
{
    public partial class MainWindow : Window
    {
        private YoutubeClient _youtube;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Persistent WebView2 data folder
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "youtube_pc_app",
                "WebView2UserData");

            Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await webView.EnsureCoreWebView2Async(env);

            _youtube = new YoutubeClient();

            // Enable WebMessage bridge
            webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;

            // Block ad hosts
            string[] adHosts =
            {
                "doubleclick.net",
                "googlesyndication.com",
                "googleadservices.com",
                "youtube.com/api/stats/ads",
                "youtube.com/pagead/"
            };

            webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);

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

            // Inject scripts after navigation
            webView.CoreWebView2.NavigationCompleted += (s, args) =>
            {
                InjectAdBlocker();
                InjectDownloadButton();
            };

            webView.CoreWebView2.Navigate("https://www.youtube.com");
        }

        // -----------------------------
        //  JS → C# MESSAGE HANDLER
        // -----------------------------
        private async void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string message = e.TryGetWebMessageAsString();

            // Get current YouTube URL
            string rawUrl = await webView.CoreWebView2.ExecuteScriptAsync("document.location.href");
            string youtubeUrl = rawUrl.Trim('"');

            if (string.IsNullOrWhiteSpace(youtubeUrl) || !youtubeUrl.Contains("youtube.com/watch"))
            {
                ShowToast("No valid YouTube video detected");
                return;
            }

            // message examples:
            // download-audio-high
            // download-video-720p
            if (message.StartsWith("download-audio-"))
            {
                string quality = message.Replace("download-audio-", "");
                StartDownload(youtubeUrl, "audio", quality);
            }
            else if (message.StartsWith("download-video-"))
            {
                string quality = message.Replace("download-video-", "");
                StartDownload(youtubeUrl, "video", quality);
            }
        }

        // -----------------------------
        //  DOWNLOAD HANDLER
        // -----------------------------
        private async void StartDownload(string url, string type, string quality)
        {
            try
            {
                // Indicate activity (simple use of existing progress bar)
                DownloadProgress.Visibility = Visibility.Visible;
                DownloadProgress.IsIndeterminate = true;

                string downloadFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    "YouTubeDownloads");

                Directory.CreateDirectory(downloadFolder);

                var video = await _youtube.Videos.GetAsync(url);
                var manifest = await _youtube.Videos.Streams.GetManifestAsync(url);

                string safeTitle = string.Join("_",
                    video.Title.Split(Path.GetInvalidFileNameChars()));

                string outputPath = Path.Combine(downloadFolder,
                    type == "audio" ? $"{safeTitle}.mp3" : $"{safeTitle}.mp4");

                if (type == "audio")
                {
                    var audioStreams = manifest.GetAudioOnlyStreams().ToList();
                    if (!audioStreams.Any())
                    {
                        ShowToast("No audio streams available");
                        return;
                    }

                    var audio = quality switch
                    {
                        "high" => audioStreams.OrderByDescending(s => s.Bitrate).First(),
                        "medium" => audioStreams.OrderBy(s => s.Bitrate)
                                                .Skip(audioStreams.Count / 2)
                                                .First(),
                        "low" => audioStreams.OrderBy(s => s.Bitrate).First(),
                        _ => audioStreams.OrderByDescending(s => s.Bitrate).First()
                    };

                    await _youtube.Videos.Streams.DownloadAsync(audio, outputPath);
                }
                else // video
                {
                    var muxed = manifest.GetMuxedStreams().ToList();
                    if (!muxed.Any())
                    {
                        ShowToast("No video streams available");
                        return;
                    }

                    int targetHeight = 0;
                    int.TryParse(quality.Replace("p", ""), out targetHeight);

                    var videoStream = muxed
                        .OrderByDescending(s => s.VideoQuality.MaxHeight)
                        .FirstOrDefault(s => s.VideoQuality.MaxHeight <= targetHeight)
                        ?? muxed.OrderByDescending(s => s.VideoQuality.MaxHeight).First();

                    await _youtube.Videos.Streams.DownloadAsync(videoStream, outputPath);
                }

                ShowToast("Download complete");
            }
            catch
            {
                ShowToast("Download failed");
            }
            finally
            {
                DownloadProgress.IsIndeterminate = false;
                DownloadProgress.Visibility = Visibility.Collapsed;
            }
        }

        // -----------------------------
        //  SIMPLE TOAST NOTIFICATION
        // -----------------------------
        private void ShowToast(string message)
        {
            // Ensure we run on UI thread
            Dispatcher.Invoke(() =>
            {
                var toast = new Border
                {
                    Background = Brushes.Black,
                    CornerRadius = new CornerRadius(6),
                    Opacity = 0.9,
                    Padding = new Thickness(12),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 20, 40),
                    Child = new TextBlock
                    {
                        Text = message,
                        Foreground = Brushes.White,
                        FontSize = 14
                    }
                };

                Panel.SetZIndex(toast, 9999);
                MainGrid.Children.Add(toast);

                var fade = new DoubleAnimation
                {
                    From = 0.9,
                    To = 0.0,
                    Duration = TimeSpan.FromSeconds(3),
                    BeginTime = TimeSpan.FromSeconds(2)
                };

                fade.Completed += (s, e) =>
                {
                    MainGrid.Children.Remove(toast);
                };

                toast.BeginAnimation(OpacityProperty, fade);
            });
        }

        // -----------------------------
        //  INJECT: AD BLOCKER
        // -----------------------------
        private void InjectAdBlocker()
        {
            string script = @"
                setInterval(() => {
                    let ad = document.querySelector('.ad-showing');
                    if (ad) {
                        let video = document.querySelector('video');
                        if (video) video.currentTime = video.duration;
                    }
                    let banners = document.querySelectorAll(
                        '.ytd-banner-promo-renderer, .ytp-ad-module, .ytp-ad-overlay-container, .ytp-ad-player-overlay'
                    );
                    banners.forEach(b => b.remove());
                }, 1000);
            ";

            webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        // -----------------------------
        //  INJECT: DOWNLOAD BUTTON
        // -----------------------------
        private void InjectDownloadButton()
        {
            string script = @"
(function() {
    function addButton() {
        const container = document.querySelector('#top-level-buttons-computed');
        if (!container) return;

        if (document.getElementById('yt-download-btn')) return;

        const btn = document.createElement('button');
        btn.id = 'yt-download-btn';
        btn.innerText = 'Download';
        btn.style.marginRight = '8px';
        btn.style.padding = '6px 10px';
        btn.style.background = '#ff0000';
        btn.style.color = 'white';
        btn.style.border = 'none';
        btn.style.borderRadius = '4px';
        btn.style.cursor = 'pointer';
        btn.style.fontSize = '14px';

        btn.onclick = () => {
            const type = prompt('Choose format: MP3 or MP4', 'MP3');
            if (!type) return;

            if (type.toLowerCase() === 'mp3') {
                const q = prompt('Audio quality: high / medium / low', 'high');
                if (q) chrome.webview.postMessage('download-audio-' + q.toLowerCase());
            }
            else if (type.toLowerCase() === 'mp4') {
                const q = prompt('Video quality: 1080p / 720p / 480p / 360p', '1080p');
                if (q) chrome.webview.postMessage('download-video-' + q.toLowerCase());
            }
        };

        container.prepend(btn);
    }

    setInterval(addButton, 1000);
})();
";
            webView.CoreWebView2.ExecuteScriptAsync(script);
        }
    }
}
