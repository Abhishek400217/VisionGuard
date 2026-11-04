using LibVLCSharp.Shared;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CameraObjectDetection
{
    public partial class MainForm : Form
    {

        // SQL Server connection
        private const string ConnectionString =
    @"Server=(localdb)\MSSQLLocalDB;Database=VisionGuardDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=15;";

        // Human detection event state
        private readonly object _presenceLock = new object();

        private bool _humanPresenceActive = false;
        private DateTime _lastHumanSeenUtc = DateTime.MinValue;



        // ==========================================
        // VLC
        // ==========================================

        private LibVLC _libVLC;

        // Separate player for frame capture
        private MediaPlayer _framePlayer;
        private Media _frameMedia;
        // ==========================================
        // YOLO
        // ==========================================

        private InferenceSession _yoloSession;

        private const int YoloWidth = 640;
        private const int YoloHeight = 640;
        private const float ConfidenceThreshold = 0.45f;

        // ==========================================
        // Frame buffer
        // ==========================================

        private IntPtr _frameBuffer = IntPtr.Zero;

        private const uint FrameWidth = 1280;
        private const uint FrameHeight = 720;

        private readonly object _frameLock = new object();

        // Prevent multiple YOLO processes running together
        private int _isProcessing = 0;
        // Latest camera frame with object detections
        private Bitmap _detectionBitmap;
        private readonly object _detectionBitmapLock = new object();
        // Latest camera frame, shared between VLC and AI
        private byte[] _latestFrameBytes;
        private int _latestFrameVersion = 0;
        private int _lastProcessedFrameVersion = 0;

        private volatile bool _hasReceivedFrame = false;
        private volatile bool _isCameraRunning = false;

        // Persistent bitmap for smooth live display
        private Bitmap _liveBitmap;
        private System.Windows.Forms.Timer _displayTimer;

        // Latest human detections
        private List<Detection> _latestDetections =
            new List<Detection>();


        // ==========================================
        // COCO class names
        // ==========================================



        private readonly string[] _classNames =
        {
            "person",
            "bicycle",
            "car",
            "motorcycle",
            "airplane",
            "bus",
            "train",
            "truck",
            "boat",
            "traffic light",
            "fire hydrant",
            "stop sign",
            "parking meter",
            "bench",
            "bird",
            "cat",
            "dog",
            "horse",
            "sheep",
            "cow",
            "elephant",
            "bear",
            "zebra",
            "giraffe",
            "backpack",
            "umbrella",
            "handbag",
            "tie",
            "suitcase",
            "frisbee",
            "skis",
            "snowboard",
            "sports ball",
            "kite",
            "baseball bat",
            "baseball glove",
            "skateboard",
            "surfboard",
            "tennis racket",
            "bottle",
            "wine glass",
            "cup",
            "fork",
            "knife",
            "spoon",
            "bowl",
            "banana",
            "apple",
            "sandwich",
            "orange",
            "broccoli",
            "carrot",
            "hot dog",
            "pizza",
            "donut",
            "cake",
            "chair",
            "couch",
            "potted plant",
            "bed",
            "dining table",
            "toilet",
            "TV",
            "laptop",
            "mouse",
            "remote",
            "keyboard",
            "cell phone",
            "microwave",
            "oven",
            "toaster",
            "sink",
            "refrigerator",
            "book",
            "clock",
            "vase",
            "scissors",
            "teddy bear",
            "hair drier",
            "toothbrush"
        };



        private bool isFullScreen = false;
        private FormBorderStyle previousBorderStyle;
        private Rectangle previousBounds;
        private FormWindowState previousWindowState;

        private void ToggleFullScreen()
        {
            if (!isFullScreen)
            {
                previousBorderStyle = this.FormBorderStyle;
                previousBounds = this.Bounds;
                previousWindowState = this.WindowState;

                isFullScreen = true;
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;

                pictureBoxDetection.Location = new System.Drawing.Point(20, 100);

                pictureBoxDetection.Size = new System.Drawing.Size(
                    this.ClientSize.Width - 40,
                    this.ClientSize.Height - 190);

                btnStartCamera.Location = new System.Drawing.Point(
    30, this.ClientSize.Height - 70);

                btnStopCamera.Location = new System.Drawing.Point(
                    240, this.ClientSize.Height - 70);

                btnFullScreen.Location = new System.Drawing.Point(
                    450, this.ClientSize.Height - 70);

                pictureBoxDetection.BringToFront();
                btnStartCamera.BringToFront();
                btnStopCamera.BringToFront();
                btnFullScreen.BringToFront();
            }
            else
            {
                ExitFullScreen();
            }
        }


        private void ExitFullScreen()
        {
            if (!isFullScreen) return;

            isFullScreen = false;

            this.FormBorderStyle = previousBorderStyle;
            this.WindowState = FormWindowState.Normal;
            this.Bounds = previousBounds;
            this.WindowState = previousWindowState;

            pictureBoxDetection.Location = new System.Drawing.Point(30, 180);
            pictureBoxDetection.Size = new System.Drawing.Size(1330, 440);

            btnStartCamera.Location = new System.Drawing.Point(480, 665);
            btnStopCamera.Location = new System.Drawing.Point(700, 665);
            btnFullScreen.Location = new System.Drawing.Point(920, 665);
        }



        // ==========================================
        // Constructor
        // ==========================================

        public MainForm()
        {
            InitializeComponent();

            this.KeyPreview = true;
            btnFullScreen.Click += (s, e) => ToggleFullScreen();

            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                    ExitFullScreen();
            };

            pictureBoxDetection.SizeMode = PictureBoxSizeMode.Zoom;


            cameraView.Visible = false;

            pictureBoxDetection.Location = new System.Drawing.Point(30, 180);
            pictureBoxDetection.Size = new System.Drawing.Size(1330, 440);
            pictureBoxDetection.Anchor =
                AnchorStyles.Top | AnchorStyles.Left |
                AnchorStyles.Right | AnchorStyles.Bottom;

            lblCameraTitle.Text = "LIVE CAMERA + HUMAN DETECTION";
            lblDetectionTitle.Visible = false;
            lblDetectionHint.Visible = false;
            lblCameraHint.Text = "Live stream with AI human detection";
            // Persistent live video bitmap
            _liveBitmap = new Bitmap(
                (int)FrameWidth,
                (int)FrameHeight,
                PixelFormat.Format32bppRgb);

            pictureBoxDetection.Image = _liveBitmap;
            pictureBoxDetection.Paint += PictureBoxDetection_Paint;

            // Display target: approximately 25 updates per second
            _displayTimer = new System.Windows.Forms.Timer();
            _displayTimer.Interval = 40;
            _displayTimer.Tick += DisplayTimer_Tick;
            _displayTimer.Start();




            // ==========================================
            // Load YOLO ONNX model
            // ==========================================

            string modelPath = Path.Combine(
            Application.StartupPath,
            "yolo26n.onnx");

            if (!File.Exists(modelPath))
            {
                MessageBox.Show(
                "YOLO model not found:\n" + modelPath,
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
                return;
            }

            using (var sessionOptions = new SessionOptions())
            {
                sessionOptions.GraphOptimizationLevel =
                    GraphOptimizationLevel.ORT_ENABLE_ALL;

                sessionOptions.ExecutionMode =
                    ExecutionMode.ORT_SEQUENTIAL;

                sessionOptions.IntraOpNumThreads = 4;
                sessionOptions.InterOpNumThreads = 1;

                _yoloSession = new InferenceSession(
                    modelPath,
                    sessionOptions);
            }

            // ==========================================
            // Initialize VLC
            // ==========================================

            Core.Initialize();

            _libVLC = new LibVLC();

            // ==========================================
            // Normal camera player
            // ==========================================

            

            // ==========================================
            // Frame capture player
            // ==========================================

            SetupFrameCapture();

            // ==========================================
            // Button events
            // ==========================================

            btnStartCamera.Click += btnStartCamera_Click;
            btnStopCamera.Click += button1_Click_1;
            _ = TestAndLogDatabaseConnectionAsync();
        }

        // ==========================================
        // Setup frame capture
        // ==========================================

        private void SetupFrameCapture()
        {
            _framePlayer = new MediaPlayer(_libVLC);

            _framePlayer.SetVideoFormat(
                "RV32",
                FrameWidth,
                FrameHeight,
                FrameWidth * 4);

            _framePlayer.SetVideoCallbacks(
                LockFrame,
                UnlockFrame,
                DisplayFrame);
        }

        // ==========================================
        // VLC Lock Frame
        // ==========================================

        private IntPtr LockFrame(
            IntPtr opaque,
            IntPtr planes)
        {
            if (_frameBuffer == IntPtr.Zero)
            {
                int bufferSize =
                    (int)(FrameWidth * FrameHeight * 4);

                _frameBuffer = Marshal.AllocHGlobal(
                    bufferSize);
            }

            Marshal.WriteIntPtr(
                planes,
                _frameBuffer);

            return IntPtr.Zero;
        }

        // ==========================================
        // VLC Unlock Frame
        // ==========================================

        private void UnlockFrame(
            IntPtr opaque,
            IntPtr picture,
            IntPtr planes)
        {
            // Nothing required here.
        }

        // ==========================================
        // VLC Display Frame
        // ==========================================

        private void DisplayFrame(
    IntPtr opaque,
    IntPtr picture)
        {
            if (!_isCameraRunning ||
                _frameBuffer == IntPtr.Zero)
                return;

            try
            {
                int bufferSize = checked(
                    (int)(FrameWidth * FrameHeight * 4));

                lock (_frameLock)
                {
                    if (_latestFrameBytes == null ||
                        _latestFrameBytes.Length != bufferSize)
                    {
                        _latestFrameBytes = new byte[bufferSize];
                    }

                    // Keep the newest decoded camera frame.
                    Marshal.Copy(
                        _frameBuffer,
                        _latestFrameBytes,
                        0,
                        bufferSize);

                    _latestFrameVersion++;
                    _hasReceivedFrame = true;
                }

                // Start AI processing only if no AI loop is running.
                if (System.Threading.Interlocked.CompareExchange(
                        ref _isProcessing, 1, 0) == 0)
                {
                    _ = Task.Run(ProcessLatestFramesLoop);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "DisplayFrame error: " + ex.Message);
            }
        }

        // ==========================================
        // Convert camera frame and run YOLO
        // ==========================================
        private void ProcessLatestFramesLoop()
        {
            try
            {
                while (_isCameraRunning)
                {
                    byte[] frameBytes;
                    int version;

                    lock (_frameLock)
                    {
                        if (_latestFrameBytes == null ||
                            _latestFrameVersion ==
                            _lastProcessedFrameVersion)
                        {
                            break;
                        }

                        version = _latestFrameVersion;

                        // Snapshot the latest frame for AI processing.
                        frameBytes =
                            (byte[])_latestFrameBytes.Clone();
                    }

                    try
                    {
                        ProcessFrame(frameBytes);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            "YOLO processing error: " + ex.Message);
                    }

                    lock (_frameLock)
                    {
                        _lastProcessedFrameVersion = version;

                        // If a newer frame arrived, process that next.
                        if (!_isCameraRunning ||
                            _latestFrameBytes == null ||
                            _latestFrameVersion == version)
                        {
                            break;
                        }
                    }
                }
            }
            finally
            {
                System.Threading.Interlocked.Exchange(
                    ref _isProcessing, 0);

                bool pending;

                lock (_frameLock)
                {
                    pending =
                        _isCameraRunning &&
                        _latestFrameBytes != null &&
                        _latestFrameVersion !=
                        _lastProcessedFrameVersion;
                }

                // Handle a newer frame arriving as the loop ends.
                if (pending &&
                    System.Threading.Interlocked.CompareExchange(
                        ref _isProcessing, 1, 0) == 0)
                {
                    _ = Task.Run(ProcessLatestFramesLoop);
                }
            }
        }

        private void DisplayTimer_Tick(
    object sender,
    EventArgs e)
        {
            if (!_hasReceivedFrame ||
                _liveBitmap == null ||
                pictureBoxDetection.IsDisposed)
            {
                return;
            }

            BitmapData data = null;

            try
            {
                data = _liveBitmap.LockBits(
                    new Rectangle(
                        0, 0,
                        _liveBitmap.Width,
                        _liveBitmap.Height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppRgb);

                lock (_frameLock)
                {
                    if (_latestFrameBytes != null)
                    {
                        Marshal.Copy(
                            _latestFrameBytes,
                            0,
                            data.Scan0,
                            _latestFrameBytes.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "Live display error: " + ex.Message);
            }
            finally
            {
                if (data != null)
                    _liveBitmap.UnlockBits(data);
            }

            pictureBoxDetection.Invalidate();
        }

        private void PictureBoxDetection_Paint(
    object sender,
    PaintEventArgs e)
        {
            if (!_hasReceivedFrame)
                return;

            List<Detection> detections;

            lock (_detectionBitmapLock)
            {
                detections = _latestDetections.ToList();
            }

            if (detections.Count == 0)
                return;

            float frameW = FrameWidth;
            float frameH = FrameHeight;

            float controlW = pictureBoxDetection.ClientSize.Width;
            float controlH = pictureBoxDetection.ClientSize.Height;

            if (controlW <= 0 || controlH <= 0)
                return;

            // Match PictureBoxSizeMode.Zoom scaling.
            float displayScale = Math.Min(
                controlW / frameW,
                controlH / frameH);

            float offsetX =
                (controlW - frameW * displayScale) / 2f;

            float offsetY =
                (controlH - frameH * displayScale) / 2f;

            // Mapping from the 640x640 model input to camera frame.
            float modelScale = Math.Min(
                YoloWidth / frameW,
                YoloHeight / frameH);

            float paddingX =
                (YoloWidth - frameW * modelScale) / 2f;

            float paddingY =
                (YoloHeight - frameH * modelScale) / 2f;

            using Pen pen = new Pen(Color.Lime, 2);
            using Font font = new Font(
                "Arial", 11, FontStyle.Bold);

            using SolidBrush textBrush =
                new SolidBrush(Color.White);

            using SolidBrush backgroundBrush =
                new SolidBrush(Color.DarkGreen);

            foreach (Detection d in detections)
            {
                float sx1 = (d.X1 - paddingX) / modelScale;
                float sy1 = (d.Y1 - paddingY) / modelScale;
                float sx2 = (d.X2 - paddingX) / modelScale;
                float sy2 = (d.Y2 - paddingY) / modelScale;

                sx1 = Math.Max(0, Math.Min(frameW - 1, sx1));
                sy1 = Math.Max(0, Math.Min(frameH - 1, sy1));
                sx2 = Math.Max(0, Math.Min(frameW - 1, sx2));
                sy2 = Math.Max(0, Math.Min(frameH - 1, sy2));

                float x1 = offsetX + sx1 * displayScale;
                float y1 = offsetY + sy1 * displayScale;
                float x2 = offsetX + sx2 * displayScale;
                float y2 = offsetY + sy2 * displayScale;

                if (x2 <= x1 || y2 <= y1)
                    continue;

                e.Graphics.DrawRectangle(
                    pen, x1, y1, x2 - x1, y2 - y1);

                string label = $"person {d.Confidence:P0}";

                SizeF labelSize =
                    e.Graphics.MeasureString(label, font);

                float labelY =
                    Math.Max(0, y1 - labelSize.Height);

                e.Graphics.FillRectangle(
                    backgroundBrush,
                    x1,
                    labelY,
                    labelSize.Width + 6,
                    labelSize.Height);

                e.Graphics.DrawString(
                    label,
                    font,
                    textBrush,
                    x1 + 3,
                    labelY);
            }
        }

        private void ProcessFrame(byte[] frameBytes)
        {
            try
            {
                using Mat cameraMat = new Mat(
                    (int)FrameHeight,
                    (int)FrameWidth,
                    MatType.CV_8UC4);

                Marshal.Copy(
                    frameBytes,
                    0,
                    cameraMat.Data,
                    frameBytes.Length);

                using Mat rgbMat = new Mat();

                Cv2.CvtColor(
                    cameraMat,
                    rgbMat,
                    ColorConversionCodes.BGRA2BGR);

                using Bitmap cameraBitmap = new Bitmap(
                    rgbMat.Width,
                    rgbMat.Height,
                    (int)rgbMat.Step(),
                    PixelFormat.Format24bppRgb,
                    rgbMat.Data);

                using Bitmap resizedBitmap =
                    ResizeImage(cameraBitmap, YoloWidth, YoloHeight);

                DenseTensor<float> inputTensor =
                    CreateInputTensor(resizedBitmap);

                string inputName =
                    _yoloSession.InputMetadata.Keys.First();

                var inputs = new[]
                {
            NamedOnnxValue.CreateFromTensor(
                inputName, inputTensor)
        };

                using IDisposableReadOnlyCollection<
                    DisposableNamedOnnxValue> results =
                        _yoloSession.Run(inputs);

                var output = results.First().AsTensor<float>();

                List<Detection> detections =
                    DetectObjects(output)
                        .Where(d => d.ClassId == 0)
                        .ToList();
                RegisterHumanDetection(detections);
                // Store the latest detections for the Paint overlay.
                lock (_detectionBitmapLock)
                {
                    _latestDetections = detections;
                }

                int personCount = detections.Count;

                if (!IsDisposed && IsHandleCreated)
                {
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            if (!IsDisposed)
                            {
                                lblPersonCount.Text =
                                    $"HUMANS DETECTED: {personCount}";

                                pictureBoxDetection.Invalidate();
                            }
                        }));
                    }
                    catch (InvalidOperationException)
                    {
                        // The form may be closing.
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "Object detection error: " + ex.Message);
            }
        }

        private void RegisterHumanDetection(
    List<Detection> detections)
        {
            bool shouldSave = false;

            lock (_presenceLock)
            {
                DateTime now = DateTime.UtcNow;

                if (detections.Count > 0)
                {
                    _lastHumanSeenUtc = now;

                    // Save only once when human presence starts.
                    if (!_humanPresenceActive)
                    {
                        _humanPresenceActive = true;
                        shouldSave = true;
                    }
                }
                else
                {
                    // Reset only after 3 seconds without detection.
                    if (_humanPresenceActive &&
                        (now - _lastHumanSeenUtc).TotalSeconds >= 3)
                    {
                        _humanPresenceActive = false;
                    }
                }
            }

            if (shouldSave)
            {
                float confidence =
                    detections.Max(d => d.Confidence);

                _ = SaveHumanDetectionEventAsync(
                    detections.Count,
                    confidence);
            }
        }


        private async Task SaveHumanDetectionEventAsync(
            int personCount,
            float confidence)
        {
            try
            {
                using (SqlConnection connection =
                    new SqlConnection(ConnectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                INSERT INTO dbo.HumanDetectionEvents
                    (CameraId, TrackingId, EventType,
                     PersonCount, Confidence)
                VALUES
                    (@CameraId, NULL, @EventType,
                     @PersonCount, @Confidence);";

                    using (SqlCommand command =
                        new SqlCommand(sql, connection))
                    {
                        command.Parameters.Add(
                            "@CameraId",
                            SqlDbType.NVarChar,
                            100).Value = "VIGI-Camera-01";

                        command.Parameters.Add(
                            "@EventType",
                            SqlDbType.NVarChar,
                            30).Value = "HUMAN_DETECTED";

                        command.Parameters.Add(
                            "@PersonCount",
                            SqlDbType.Int).Value = personCount;

                        command.Parameters.Add(
                            "@Confidence",
                            SqlDbType.Real).Value = confidence;

                        await command.ExecuteNonQueryAsync();
                    }
                }

                Debug.WriteLine(
                    "SUCCESS: HUMAN DETECTION SAVED TO DATABASE");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    "HUMAN LOG SAVE ERROR: " + ex.ToString());
            }
        }

        private void SetDatabaseStatus(string status)
        {
            if (IsDisposed)
                return;

            Action update = () =>
            {
                Text = "VisionGuard | " + status;
            };

            try
            {
                if (IsHandleCreated && InvokeRequired)
                    BeginInvoke(update);
                else
                    update();
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }



        private async Task TestAndLogDatabaseConnectionAsync()
        {
            try
            {
                using (SqlConnection connection =
                    new SqlConnection(ConnectionString))
                {
                    await connection.OpenAsync();

                    string sql = @"
                INSERT INTO dbo.DatabaseConnectionLogs
                    (AppName, MachineName)
                VALUES
                    (@AppName, @MachineName);";

                    using (SqlCommand command =
                        new SqlCommand(sql, connection))
                    {
                        command.Parameters.Add(
                            "@AppName",
                            SqlDbType.NVarChar,
                            100).Value = "VisionGuard";

                        command.Parameters.Add(
                            "@MachineName",
                            SqlDbType.NVarChar,
                            128).Value = Environment.MachineName;

                        await command.ExecuteNonQueryAsync();
                    }
                }

                SetDatabaseStatus("DATABASE CONNECTED");

                Debug.WriteLine(
                    "SUCCESS: SQL DATABASE CONNECTED AND TEST LOG SAVED");
            }
            catch (Exception ex)
            {
                SetDatabaseStatus("DATABASE CONNECTION FAILED");

                Debug.WriteLine(
                    "DATABASE ERROR: " + ex.ToString());
            }
        }

        private Bitmap CreateBitmapFromBytes(
            byte[] bytes)
        {
            Bitmap bitmap = new Bitmap(
            (int)FrameWidth,
            (int)FrameHeight,
            PixelFormat.Format32bppRgb);

            Rectangle rectangle =
            new Rectangle(
                0,
                0,
                 bitmap.Width,
                 bitmap.Height);

            BitmapData bitmapData =
            bitmap.LockBits(
            rectangle,
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppRgb);

            try
            {
                Marshal.Copy(
                bytes,
                0,
                bitmapData.Scan0,
                bytes.Length);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return bitmap;
        }

        // ==========================================
        // Resize image to 640 x 640
        // ==========================================


        private Bitmap ResizeImage(Bitmap source, int width, int height)
        {
            Bitmap resized = new Bitmap(
                width,
                height,
                PixelFormat.Format24bppRgb
            );

            using (Graphics graphics = Graphics.FromImage(resized))
            {
                graphics.Clear(Color.Black);

                float scale = Math.Min(
                    (float)width / source.Width,
                    (float)height / source.Height
                );

                int newWidth = (int)(source.Width * scale);
                int newHeight = (int)(source.Height * scale);

                int x = (width - newWidth) / 2;
                int y = (height - newHeight) / 2;

                graphics.DrawImage(
                    source,
                    new Rectangle(x, y, newWidth, newHeight)
                );
            }

            return resized;
        }

        // ==========================================
        // Convert Bitmap to YOLO Tensor
        // ==========================================

        private DenseTensor<float> CreateInputTensor(
    Bitmap bitmap)
        {
            DenseTensor<float> tensor =
                new DenseTensor<float>(
                    new[]
                    {
                1, 3, YoloHeight, YoloWidth
                    });

            Rectangle rect = new Rectangle(
                0, 0, YoloWidth, YoloHeight);

            BitmapData data = bitmap.LockBits(
                rect,
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);

            try
            {
                int stride = data.Stride;

                if (stride < 0)
                {
                    throw new InvalidOperationException(
                        "Negative bitmap stride is unsupported.");
                }

                byte[] pixels = new byte[stride * YoloHeight];

                Marshal.Copy(
                    data.Scan0,
                    pixels,
                    0,
                    pixels.Length);

                for (int y = 0; y < YoloHeight; y++)
                {
                    int rowOffset = y * stride;

                    for (int x = 0; x < YoloWidth; x++)
                    {
                        int i = rowOffset + x * 3;

                        // 24-bit bitmap memory order: B, G, R.
                        tensor[0, 0, y, x] =
                            pixels[i + 2] / 255.0f;

                        tensor[0, 1, y, x] =
                            pixels[i + 1] / 255.0f;

                        tensor[0, 2, y, x] =
                            pixels[i] / 255.0f;
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return tensor;
        }

        // ==========================================
        // Read YOLO 84 x 840 output
        // ==========================================

        // ==========================================
        // Read YOLO 84 x 8400 output + NMS
        // ==========================================



        private List<Detection> DetectObjects(Tensor<float> output)
        {
            List<Detection> detections = new List<Detection>();

            int dim1 = output.Dimensions[1];
            int dim2 = output.Dimensions[2];

            Debug.WriteLine(
                $"YOLO output shape: [1, {dim1}, {dim2}]");

            // FORMAT 1: YOLO26 processed output [1, 300, 6]
            // Each row: x1, y1, x2, y2, confidence, classId
            if (dim1 == 300 && dim2 == 6)
            {
                for (int i = 0; i < dim1; i++)
                {
                    float x1 = output[0, i, 0];
                    float y1 = output[0, i, 1];
                    float x2 = output[0, i, 2];
                    float y2 = output[0, i, 3];
                    float confidence = output[0, i, 4];
                    int classId = (int)output[0, i, 5];

                    if (confidence < ConfidenceThreshold)
                        continue;

                    if (classId < 0 || classId >= _classNames.Length)
                        continue;

                    if (x2 <= x1 || y2 <= y1)
                        continue;

                    detections.Add(new Detection
                    {
                        X1 = x1,
                        Y1 = y1,
                        X2 = x2,
                        Y2 = y2,
                        Confidence = confidence,
                        ClassId = classId
                    });
                }

                // This format already contains processed detections.
                return detections;
            }

            // FORMAT 2: Raw YOLO output [1, 84, N]
            // Four box values + 80 class confidence scores.
            if (dim1 == 84)
            {
                int candidates = dim2;

                for (int i = 0; i < candidates; i++)
                {
                    float centerX = output[0, 0, i];
                    float centerY = output[0, 1, i];
                    float width = output[0, 2, i];
                    float height = output[0, 3, i];

                    float bestConfidence = 0f;
                    int bestClassId = -1;

                    for (int classId = 0; classId < 80; classId++)
                    {
                        float confidence = output[0, classId + 4, i];

                        if (confidence > bestConfidence)
                        {
                            bestConfidence = confidence;
                            bestClassId = classId;
                        }
                    }

                    if (bestConfidence < ConfidenceThreshold)
                        continue;

                    if (bestClassId < 0 || bestClassId >= _classNames.Length)
                        continue;

                    detections.Add(new Detection
                    {
                        X1 = centerX - width / 2f,
                        Y1 = centerY - height / 2f,
                        X2 = centerX + width / 2f,
                        Y2 = centerY + height / 2f,
                        Confidence = bestConfidence,
                        ClassId = bestClassId
                    });
                }

                return ApplyNMS(detections, 0.45f);
            }

            Debug.WriteLine(
                $"Unsupported YOLO output shape: [1, {dim1}, {dim2}]");

            return detections;
        }



        // ==========================================
        // Detection information
        // ==========================================

        private class Detection
        {
            public float X1 { get; set; }
            public float Y1 { get; set; }
            public float X2 { get; set; }
            public float Y2 { get; set; }

            public float Confidence { get; set; }

            public int ClassId { get; set; }
        }


        // ==========================================
        // Non-Maximum Suppression
        // ==========================================

        private List<Detection> ApplyNMS(
            List<Detection> detections,
            float iouThreshold)
        {
            List<Detection> results =
                new List<Detection>();

            // Process highest confidence first
            List<Detection> sortedDetections =
                detections
                .OrderByDescending(d => d.Confidence)
                .ToList();

            while (sortedDetections.Count > 0)
            {
                // Take highest confidence detection
                Detection best =
                    sortedDetections[0];

                results.Add(best);

                sortedDetections.RemoveAt(0);

                // Remove overlapping detections
                // of the same class
                sortedDetections =
                    sortedDetections
                    .Where(d =>
                        d.ClassId != best.ClassId ||
                        CalculateIoU(best, d) < iouThreshold)
                    .ToList();
            }

            return results;
        }

        // ==========================================
        // Calculate Intersection over Union
        // ==========================================

        private float CalculateIoU(
            Detection a,
            Detection b)
        {
            float intersectionX1 =
            Math.Max(a.X1, b.X1);

            float intersectionY1 =
            Math.Max(a.Y1, b.Y1);

            float intersectionX2 =
                Math.Min(a.X2, b.X2);

            float intersectionY2 =
                Math.Min(a.Y2, b.Y2);

            float intersectionWidth =
                Math.Max(0, intersectionX2 - intersectionX1);

            float intersectionHeight =
            Math.Max(0, intersectionY2 - intersectionY1);

            float intersectionArea =
            intersectionWidth * intersectionHeight;

            float areaA =
             Math.Max(0, a.X2 - a.X1) *
             Math.Max(0, a.Y2 - a.Y1);

            float areaB =
            Math.Max(0, b.X2 - b.X1) *
            Math.Max(0, b.Y2 - b.Y1);

            float unionArea =
                areaA + areaB - intersectionArea;

            if (unionArea <= 0)
                return 0;

            return intersectionArea / unionArea;
        }




        private Bitmap DrawDetections(
            Bitmap source,
            List<Detection> detections)
        {
            Bitmap result = new Bitmap(source);

            // Convert YOLO's 640x640 coordinates to the original video.
            float scale = Math.Min(
                (float)YoloWidth / source.Width,
                (float)YoloHeight / source.Height);

            float paddingX =
                (YoloWidth - source.Width * scale) / 2f;

            float paddingY =
                (YoloHeight - source.Height * scale) / 2f;

            using (Graphics g = Graphics.FromImage(result))
            using (Pen pen = new Pen(Color.Lime, 5))
            using (Font font = new Font("Arial", 28, FontStyle.Bold))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            using (SolidBrush backgroundBrush = new SolidBrush(Color.DarkGreen))
            {
                foreach (Detection detection in detections)
                {
                    float x1 = (detection.X1 - paddingX) / scale;
                    float y1 = (detection.Y1 - paddingY) / scale;
                    float x2 = (detection.X2 - paddingX) / scale;
                    float y2 = (detection.Y2 - paddingY) / scale;

                    // Keep boxes inside the video.
                    x1 = Math.Max(0, Math.Min(source.Width - 1, x1));
                    y1 = Math.Max(0, Math.Min(source.Height - 1, y1));
                    x2 = Math.Max(0, Math.Min(source.Width - 1, x2));
                    y2 = Math.Max(0, Math.Min(source.Height - 1, y2));

                    float boxWidth = x2 - x1;
                    float boxHeight = y2 - y1;

                    if (boxWidth <= 0 || boxHeight <= 0)
                        continue;

                    string label =
                        $"{_classNames[detection.ClassId]} " +
                        $"{detection.Confidence:P0}";

                    g.DrawRectangle(
                        pen, x1, y1, boxWidth, boxHeight);

                    SizeF textSize = g.MeasureString(label, font);
                    float labelY = Math.Max(0, y1 - textSize.Height);

                    g.FillRectangle(
                        backgroundBrush,
                        x1,
                        labelY,
                        textSize.Width + 8,
                        textSize.Height);

                    g.DrawString(
                        label,
                        font,
                        textBrush,
                        x1 + 4,
                        labelY);
                }
            }

            return result;
        }

        // ==========================================
        // Show detection on Windows Form
        // ==========================================

        private void ShowDetection(string message)
        {
            if (IsDisposed)
                return;

            try
            {
                BeginInvoke(new Action(() =>
                {
                    Text = message;
                }));
            }
            catch
            {
                // Form may be closing.
            }
        }

        // ==========================================
        // Start Camera
        // ==========================================

        private void btnStartCamera_Click(object sender, EventArgs e)
        {
            try
            {
                string rtspUrl =
                    "rtsp://admin:admin@123@192.168.0.60:554/stream1";

                _isCameraRunning = false;
                _framePlayer.Stop();

                _frameMedia?.Dispose();

                _frameMedia = new Media(
                    _libVLC,
                    rtspUrl,
                    FromType.FromLocation);
                 
                _frameMedia.AddOption(":rtsp-tcp");
                _frameMedia.AddOption(":network-caching=200");
                _frameMedia.AddOption(":live-caching=200");

                lock (_frameLock)
                {
                    int size = checked(
                        (int)(FrameWidth * FrameHeight * 4));

                    _latestFrameBytes = new byte[size];

                    _latestFrameVersion++;
                    _lastProcessedFrameVersion = _latestFrameVersion;
                }

                lock (_detectionBitmapLock)
                {
                    _latestDetections.Clear();
                }

                _hasReceivedFrame = false;
                _isCameraRunning = true;

                _framePlayer.Play(_frameMedia);

                lblLiveStatus.Text = "● CAMERA CONNECTED";
                lblLiveStatus.ForeColor = Color.FromArgb(74, 222, 128);
            }
            catch (Exception ex)
            {
                _isCameraRunning = false;
                MessageBox.Show("Camera Error: " + ex.Message);
            }
        }

        // ==========================================
        // Stop Camera
        // ==========================================

        private void button1_Click_1(object sender, EventArgs e)
        {
            // Stop accepting new camera frames first.
            _isCameraRunning = false;
            _hasReceivedFrame = false;

            _framePlayer?.Stop();

            lblLiveStatus.Text = "● CAMERA DISCONNECTED";
            lblLiveStatus.ForeColor =
                Color.FromArgb(255, 194, 85);

            _frameMedia?.Dispose();
            _frameMedia = null;

            lock (_frameLock)
            {
                _latestFrameBytes = null;
                _latestFrameVersion++;

                _lastProcessedFrameVersion =
                    _latestFrameVersion;
            }

            lock (_detectionBitmapLock)
            {
                _latestDetections.Clear();
            }

            // Do not dispose the persistent live bitmap here.
            if (_liveBitmap != null)
            {
                using (Graphics g = Graphics.FromImage(_liveBitmap))
                {
                    g.Clear(Color.Black);
                }
            }

            pictureBoxDetection.Invalidate();
        }

        // ==========================================
        // Form closing
        // ==========================================


        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _isCameraRunning = false;
            _hasReceivedFrame = false;

            _displayTimer?.Stop();
            _displayTimer?.Dispose();
            _displayTimer = null;

            _framePlayer?.Stop();

            _frameMedia?.Dispose();
            _frameMedia = null;

            _framePlayer?.Dispose();
            _framePlayer = null;

            if (_frameBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_frameBuffer);
                _frameBuffer = IntPtr.Zero;
            }

            _yoloSession?.Dispose();
            _yoloSession = null;

            _libVLC?.Dispose();
            _libVLC = null;

            Image oldImage = pictureBoxDetection.Image;
            pictureBoxDetection.Image = null;
            oldImage?.Dispose();

            _liveBitmap = null;

            base.OnFormClosed(e);
        }


        private void pictureBoxDetection_Click(object sender, EventArgs e)
        {

        }
    }
}