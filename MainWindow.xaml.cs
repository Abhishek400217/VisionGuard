using System;
using System.IO;
using System.Windows;
using LibVLCSharp.Shared;

namespace CameraViewer
{
    public partial class MainWindow : Window
    {
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;

        // =====================================================
        // CAMERA RTSP URL
        // Password = admin@123
        // =====================================================

        private const string RtspUrl =
            "rtsp://admin:admin%40123@192.168.0.60:554/stream1";


        public MainWindow()
        {
            InitializeComponent();

            // Initialize LibVLC
            Core.Initialize();

            _libVLC = new LibVLC(
                "--no-video-title-show",
                "--rtsp-tcp",
                "--network-caching=1000",
                "--live-caching=1000"
            );

            _mediaPlayer = new MediaPlayer(_libVLC);

            VideoView.MediaPlayer = _mediaPlayer;


            // VLC EVENTS
            _mediaPlayer.Playing += MediaPlayer_Playing;
            _mediaPlayer.Stopped += MediaPlayer_Stopped;
            _mediaPlayer.EncounteredError += MediaPlayer_EncounteredError;

            SetStatus("● OFFLINE", "Gray");
        }


        // =====================================================
        // CONNECT
        // =====================================================

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            ConnectCamera();
        }


        private void ConnectCamera()
        {
            try
            {
                StopCamera();

                SetStatus("● CONNECTING...", "Orange");


                using var media = new Media(
                    _libVLC,
                    new Uri(RtspUrl)
                );


                media.AddOption(":rtsp-tcp");
                media.AddOption(":network-caching=1000");
                media.AddOption(":live-caching=1000");


                bool result = _mediaPlayer.Play(media);

                if (!result)
                {
                    SetStatus("● ERROR", "Red");

                    MessageBox.Show(
                        "LibVLC could not start the RTSP stream.",
                        "RTSP Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                SetStatus("● ERROR", "Red");

                MessageBox.Show(
                    "Camera connection failed.\n\n" + ex.Message,
                    "Camera Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }


        // =====================================================
        // STOP
        // =====================================================

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopCamera();
        }


        private void StopCamera()
        {
            try
            {
                if (_mediaPlayer != null &&
                    (_mediaPlayer.IsPlaying || _mediaPlayer.State != VLCState.Stopped))
                {
                    _mediaPlayer.Stop();
                }

                SetStatus("● OFFLINE", "Gray");
            }
            catch
            {
                SetStatus("● OFFLINE", "Gray");
            }
        }


        // =====================================================
        // VLC PLAYING EVENT
        // =====================================================

        private void MediaPlayer_Playing(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                SetStatus("● LIVE", "LimeGreen");
            });
        }


        // =====================================================
        // VLC STOPPED EVENT
        // =====================================================

        private void MediaPlayer_Stopped(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                SetStatus("● OFFLINE", "Gray");
            });
        }


        // =====================================================
        // VLC ERROR EVENT
        // =====================================================

        private void MediaPlayer_EncounteredError(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                SetStatus("● ERROR", "Red");

                MessageBox.Show(
                    "RTSP playback failed.\n\n" +
                    "Check camera connection and RTSP URL.",
                    "RTSP Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            });
        }


        // =====================================================
        // SNAPSHOT
        // =====================================================

        private void SnapshotButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_mediaPlayer.IsPlaying)
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


        // =====================================================
        // FULL SCREEN
        // =====================================================

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


        // =====================================================
        // STATUS HELPER
        // =====================================================

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


        // =====================================================
        // CLEANUP
        // =====================================================

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                StopCamera();

                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Playing -= MediaPlayer_Playing;
                    _mediaPlayer.Stopped -= MediaPlayer_Stopped;
                    _mediaPlayer.EncounteredError -= MediaPlayer_EncounteredError;

                    _mediaPlayer.Dispose();
                }

                _libVLC?.Dispose();
            }
            catch
            {
                // Ignore cleanup errors
            }

            base.OnClosed(e);
        }
    }
}