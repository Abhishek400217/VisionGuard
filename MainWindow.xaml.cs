using CameraViewer.AI;
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace CameraViewer
{
    public partial class MainWindow : Window
    {
        private LibVLC? _libVLC;
        private LibVLCSharp.Shared.MediaPlayer? _mediaPlayer; private LiveDetectionService? _liveDetection;

        private DetectionOverlayWindow? _detectionOverlay;


        private const string RtspUrl =
            "rtsp://admin:admin%40123@192.168.0.60:554/stream1";

        private static readonly object LogLock = new();

        private static readonly Regex UrlCredentialsRegex = new(
            @"\b([a-z][a-z0-9+.\-]*)://[^/\s]*@",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex AuthHeaderRegex = new(
            @"(Authorization\s*:\s*).*",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly string[] Secrets = BuildSecrets();

        private string? _logFilePath;

        private volatile bool _isClosing;
        private bool _isCleanedUp;

        private int _errorLatched;

        private volatile string? _lastVlcError;

        public MainWindow()
        {
            InitializeComponent();

            _detectionOverlay = new DetectionOverlayWindow();

            Loaded += MainWindow_Loaded;
            LocationChanged += MainWindow_LocationChanged;
            SizeChanged += MainWindow_SizeChanged;

            InitLogging();

            WriteLog("INF", "=== CameraViewer starting ===");
            WriteLog("INF", "Log file: " + (_logFilePath ?? "(file logging unavailable)"));
            WriteLog("INF", "RTSP target: " + MaskSecrets(RtspUrl));

            try
            {
                Core.Initialize();

                _libVLC = new LibVLC(
                    "--no-video-title-show",
                    "--rtsp-tcp",
                    "--network-caching=1000",
                    "--live-caching=1000"
                );

                _libVLC.Log += LibVLC_Log;

                _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);

                VideoView.MediaPlayer = _mediaPlayer;

                string modelPath = Path.Combine(
    AppContext.BaseDirectory,
    "AI",
    "yolo26n.onnx"
);

                

                

                string aiRtspUrl = RtspUrl.Replace("/stream1", "/stream2");

                _liveDetection = new LiveDetectionService(
                    _libVLC,
                    aiRtspUrl,
                    modelPath
                );

                _liveDetection.Diagnostic += message =>
                {
                    WriteLog("AI", message);
                };

                _liveDetection.DetectionsAvailable += detections =>
                {
                    int personCount = detections.Count;

                    Dispatcher.BeginInvoke(() =>
                    {
                        if (personCount > 0)
                        {
                            StatusText.Text = $"● LIVE | PERSONS: {personCount}";
                            StatusText.Foreground = Brushes.LimeGreen;
                        }
                        else
                        {
                            StatusText.Text = "● LIVE | PERSONS: 0";
                            StatusText.Foreground = Brushes.Gray;
                        }
                    });
                };

                _mediaPlayer.Playing += MediaPlayer_Playing;
                _mediaPlayer.Stopped += MediaPlayer_Stopped;
                _mediaPlayer.EncounteredError += MediaPlayer_EncounteredError;

                _mediaPlayer.Opening += MediaPlayer_Opening;
                _mediaPlayer.EndReached += MediaPlayer_EndReached;

                WriteLog("INF", "LibVLC and MediaPlayer initialized");
            }
            catch (Exception ex)
            {
                WriteLog("ERR", "Startup failed: " + ex);
                throw; 
            }

            SetStatus("● OFFLINE", "Gray");
        }
        
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PositionDetectionOverlay();

            _detectionOverlay?.Show();
            PositionDetectionOverlay();
        }

        private void MainWindow_LocationChanged(
            object? sender,
            EventArgs e)
        {
            PositionDetectionOverlay();
        }

        private void MainWindow_SizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            PositionDetectionOverlay();
        }

        private void PositionDetectionOverlay()
        {
            if (_detectionOverlay == null)
                return;

            if (!IsLoaded)
                return;

            try
            {
                Point topLeft =
                    VideoView.PointToScreen(
                        new Point(0, 0));

                Point bottomRight =
                    VideoView.PointToScreen(
                        new Point(
                            VideoView.ActualWidth,
                            VideoView.ActualHeight));

                _detectionOverlay.Left = topLeft.X;
                _detectionOverlay.Top = topLeft.Y;

                _detectionOverlay.Width =
                    bottomRight.X - topLeft.X;

                _detectionOverlay.Height =
                    bottomRight.Y - topLeft.Y;
            }
            catch
            {
                // Ignore temporary positioning errors.
            }
        }

        private void InitLogging()
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CameraViewer",
                    "Logs"
                );

                Directory.CreateDirectory(folder);

                _logFilePath = Path.Combine(
                    folder,
                    "CameraViewer_" + DateTime.Now.ToString("yyyyMMdd") + ".log"
                );
            }
            catch (Exception ex)
            {
                _logFilePath = null;
                Trace.WriteLine("CameraViewer: could not create log folder: " + ex.Message);
            }
        }

        private void WriteLog(string level, string message)
        {
            try
            {
                string line =
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                    " [" + level + "] " +
                    MaskSecrets(message);

                Trace.WriteLine(line);

                string? path = _logFilePath;

                if (path != null)
                {
                    lock (LogLock)
                    {
                        File.AppendAllText(
                            path,
                            line + Environment.NewLine,
                            Encoding.UTF8
                        );
                    }
                }
            }
            catch
            {
            }
        }


        private static string MaskSecrets(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text ?? string.Empty;
            }

            string result = UrlCredentialsRegex.Replace(text, "$1://***:***@");

            result = AuthHeaderRegex.Replace(result, "$1***");

            foreach (string secret in Secrets)
            {
                result = result.Replace(secret, "***", StringComparison.Ordinal);
            }

            return result;
        }


        private static string[] BuildSecrets()
        {
            var list = new List<string>();

            try
            {
                var uri = new Uri(RtspUrl);
                string info = uri.UserInfo; 
                int idx = info.IndexOf(':');

                if (idx >= 0)
                {
                    string user = Uri.UnescapeDataString(info.Substring(0, idx));
                    string passwordEncoded = info.Substring(idx + 1);
                    string passwordDecoded = Uri.UnescapeDataString(passwordEncoded);

                    list.Add(passwordEncoded);
                    list.Add(passwordDecoded);
                    list.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + passwordDecoded)));
                    list.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + passwordEncoded)));
                }
            }
            catch
            {
            }

            return list
                .Where(s => s.Length >= 4)
                .Distinct()
                .OrderByDescending(s => s.Length)
                .ToArray();
        }

        private void LibVLC_Log(object? sender, LogEventArgs e)
        {
            try
            {
                string level;

                switch (e.Level)
                {
                    case LogLevel.Debug:
                        level = "VLC-DBG";
                        break;
                    case LogLevel.Notice:
                        level = "VLC-INF";
                        break;
                    case LogLevel.Warning:
                        level = "VLC-WRN";
                        break;
                    case LogLevel.Error:
                        level = "VLC-ERR";
                        break;
                    default:
                        level = "VLC";
                        break;
                }

                string message = "[" + e.Module + "] " + e.Message;

                if (e.Level == LogLevel.Error)
                {
                    _lastVlcError = MaskSecrets(message);
                }

                WriteLog(level, message);
            }
            catch
            {
            }
        }

        private void PostToUi(Action action, string what)
        {
            if (_isClosing)
            {
                return;
            }

            try
            {
                Dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(() =>
                    {
                        if (_isClosing)
                        {
                            return;
                        }

                        try
                        {
                            action();
                        }
                        catch (Exception ex)
                        {
                            WriteLog("ERR", "UI action '" + what + "' failed: " + ex.Message);
                        }
                    })
                );
            }
            catch (Exception ex)
            {
                WriteLog("ERR", "Could not post '" + what + "' to UI thread: " + ex.Message);
            }
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            ConnectCamera();
        }


        private void ConnectCamera()
        {
            try
            {
                WriteLog(
                    "INF",
                    "Connect requested: " +
                    MaskSecrets(RtspUrl));

                StopCamera();

                SetStatus(
                    "● CONNECTING...",
                    "Orange");

                using var media =
                    new Media(
                        _libVLC!,
                        new Uri(RtspUrl));

                media.AddOption(
                    ":rtsp-tcp");

                media.AddOption(
                    ":network-caching=1000");

                media.AddOption(
                    ":live-caching=1000");

                bool result =
                    _mediaPlayer!.Play(media);

                if (!result)
                {
                    SetStatus(
                        "● ERROR",
                        "Red");

                    WriteLog(
                        "ERR",
                        "Main camera Play() returned false.");

                    return;
                }

                WriteLog(
                    "INF",
                    "Main camera Play() accepted.");

                try
                {
                    _liveDetection?.Start();

                    WriteLog(
                        "AI",
                        "AI detection stream started.");
                }
                catch (Exception ex)
                {
                    WriteLog(
                        "ERR",
                        "AI detection start failed: " +
                        ex);
                }
            }
            catch (Exception ex)
            {
                SetStatus(
                    "● ERROR",
                    "Red");

                WriteLog(
                    "ERR",
                    "Camera connection failed: " +
                    ex);
            }
        }
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("INF", "Stop requested by user");
            StopCamera();
        }

        private void StopCamera()
        {
            try
            {
                Interlocked.Exchange(
                    ref _errorLatched,
                    0);

                try
                {
                    _liveDetection?.Stop();

                    WriteLog(
                        "AI",
                        "AI detection stream stopped.");
                }
                catch (Exception ex)
                {
                    WriteLog(
                        "WRN",
                        "AI stop failed: " +
                        ex.Message);
                }

                if (_mediaPlayer != null &&
                    (_mediaPlayer.IsPlaying ||
                     _mediaPlayer.State != VLCState.Stopped))
                {
                    _mediaPlayer.Stop();
                }
                _detectionOverlay?.ClearDetections();
                SetStatus(
                    "● OFFLINE",
                    "Gray");
            }
            catch (Exception ex)
            {
                WriteLog(
                    "WRN",
                    "StopCamera error (ignored): " +
                    ex.Message);

                SetStatus(
                    "● OFFLINE",
                    "Gray");
            }
        }

        private void MediaPlayer_Playing(object? sender, EventArgs e)
        {
            WriteLog("INF", "MediaPlayer event: Playing");

            PostToUi(() =>
            {
                if (Volatile.Read(ref _errorLatched) == 1)
                {
                    return;
                }

                SetStatus("● LIVE", "LimeGreen");
            }, "Playing");
        }

        private void MediaPlayer_Stopped(object? sender, EventArgs e)
        {
            WriteLog("INF", "MediaPlayer event: Stopped");

            PostToUi(() =>
            {
                if (Volatile.Read(ref _errorLatched) == 1)
                {
                    return;
                }

                var player = _mediaPlayer;

                if (player != null &&
                    (player.State == VLCState.Opening ||
                     player.State == VLCState.Buffering ||
                     player.State == VLCState.Playing))
                {
                    return;
                }

                SetStatus("● OFFLINE", "Gray");
            }, "Stopped");
        }

        private void MediaPlayer_Opening(object? sender, EventArgs e)
        {
            WriteLog("INF", "MediaPlayer event: Opening");
        }


        private void MediaPlayer_EndReached(object? sender, EventArgs e)
        {
            WriteLog("WRN", "MediaPlayer event: EndReached");
        }

        private void MediaPlayer_EncounteredError(object? sender, EventArgs e)
        {
            if (Interlocked.Exchange(ref _errorLatched, 1) == 1)
            {
                WriteLog("WRN", "MediaPlayer event: EncounteredError (duplicate, already handled)");
                return;
            }

            WriteLog("ERR", "MediaPlayer event: EncounteredError");

            PostToUi(HandleRtspErrorOnUiThread, "EncounteredError");
        }


        private void HandleRtspErrorOnUiThread()
        {
            if (Volatile.Read(ref _errorLatched) != 1)
            {
                WriteLog("INF", "RTSP error handling skipped (state already changed by user)");
                return;
            }

            SetStatus("● ERROR", "Red");

            string? lastError = _lastVlcError;

            WriteLog(
                "ERR",
                "RTSP playback failed. Last LibVLC error: " +
                (string.IsNullOrEmpty(lastError) ? "(none captured)" : lastError)
            );

            ResetPlayerAfterError();
        }


        private void ResetPlayerAfterError()
        {
            try
            {
                var player = _mediaPlayer;

                if (player != null && player.State != VLCState.Stopped)
                {
                    WriteLog("INF", "Resetting player after error (Stop)");
                    player.Stop();
                }
            }
            catch (Exception ex)
            {
                WriteLog("WRN", "Player reset after error failed (ignored): " + ex.Message);
            }
        }

        private void SnapshotButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_mediaPlayer!.IsPlaying)
                {
                    MessageBox.Show(
                        "Camera is not streaming.",
                        "Snapshot",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );

                    return;
                }


                string folder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.MyPictures
                    ),
                    "CameraViewer"
                );


                Directory.CreateDirectory(folder);


                string fileName =
                    "Camera_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss") +
                    ".jpg";


                string filePath = Path.Combine(
                    folder,
                    fileName
                );


                bool result = _mediaPlayer.TakeSnapshot(
                    0,
                    filePath,
                    0,
                    0
                );


                if (result)
                {
                    MessageBox.Show(
                        "Snapshot saved successfully!\n\n" +
                        filePath,
                        "Snapshot",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    MessageBox.Show(
                        "Could not capture snapshot.",
                        "Snapshot Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Snapshot error:\n\n" + ex.Message,
                    "Snapshot Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void FullScreenButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowStyle == WindowStyle.None)
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = WindowState.Normal;
                ResizeMode = ResizeMode.CanResize;
            }
            else
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                ResizeMode = ResizeMode.NoResize;
            }
        }

        private void SetStatus(string text, string color)
        {
            StatusText.Text = text;

            switch (color)
            {
                case "LimeGreen":
                    StatusText.Foreground =
                        System.Windows.Media.Brushes.LimeGreen;
                    break;

                case "Orange":
                    StatusText.Foreground =
                        System.Windows.Media.Brushes.Orange;
                    break;

                case "Red":
                    StatusText.Foreground =
                        System.Windows.Media.Brushes.Red;
                    break;

                default:
                    StatusText.Foreground =
                        System.Windows.Media.Brushes.Gray;
                    break;
            }
        }
        protected override void OnClosed(EventArgs e)
        {
            _isClosing = true;

            WriteLog("INF", "Window closing - starting cleanup");

            if (!_isCleanedUp)
            {
                _isCleanedUp = true;
                CleanupPlayer();
            }

            WriteLog("INF", "=== CameraViewer closed ===");

            base.OnClosed(e);
        }


        private void CleanupPlayer()
        {
            var player = _mediaPlayer;
            var vlc = _libVLC;

            _liveDetection?.Dispose();
            _liveDetection = null;

            _mediaPlayer = null;
            _libVLC = null;

            SafeRun("unsubscribe MediaPlayer events", () =>
            {
                if (player != null)
                {
                    player.Playing -= MediaPlayer_Playing;
                    player.Stopped -= MediaPlayer_Stopped;
                    player.EncounteredError -= MediaPlayer_EncounteredError;
                    player.Opening -= MediaPlayer_Opening;
                    player.EndReached -= MediaPlayer_EndReached;
                }
            });

            SafeRun("stop player", () =>
            {
                if (player != null && player.State != VLCState.Stopped)
                {
                    player.Stop();
                }
            });

            SafeRun("detach VideoView", () =>
            {
                VideoView.MediaPlayer = null;
            });

            SafeRun("dispose MediaPlayer", () =>
            {
                player?.Dispose();
            });

            SafeRun("unsubscribe LibVLC log", () =>
            {
                if (vlc != null)
                {
                    vlc.Log -= LibVLC_Log;
                }
            });

            SafeRun("dispose LibVLC", () =>
            {
                vlc?.Dispose();
            });
        }
        private void SafeRun(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                WriteLog("WRN", "Cleanup step '" + what + "' failed (ignored): " + ex.Message);
            }
        }
    }
}