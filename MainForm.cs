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


namespace CameraObjectDetection
{
    public partial class MainForm : Form
    {
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

        private const uint FrameWidth = 2560;
        private const uint FrameHeight = 1440;

        private readonly object _frameLock = new object();

        // Prevent multiple YOLO processes running together
        private int _isProcessing = 0;
        // Latest camera frame with object detections
        private Bitmap _detectionBitmap;
        private readonly object _detectionBitmapLock = new object();
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


            

            // ==========================================
            // Load YOLO ONNX model
            // ==========================================

            string modelPath = Path.Combine(
            Application.StartupPath,
            "yolo26s.onnx");

            if (!File.Exists(modelPath))
            {
                MessageBox.Show(
                "YOLO model not found:\n" + modelPath,
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
                return;
            }

            _yoloSession = new InferenceSession(modelPath);

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
            if (_frameBuffer == IntPtr.Zero)
                return;

            // Don't start another YOLO process
            // if previous frame is still processing.
            if (System.Threading.Interlocked.CompareExchange(
                    ref _isProcessing,
                    1,
                    0) != 0)
            {
                return;
            }

            try
            {
                int bufferSize =
               (int)(FrameWidth * FrameHeight * 4);

                byte[] frameBytes =
                    new byte[bufferSize];

                // Copy VLC frame into managed memory
                lock (_frameLock)
                {
                    Marshal.Copy(
                        _frameBuffer,
                        frameBytes,
                        0,
                        bufferSize);
                }

                // Process frame in background
                Task.Run(() =>
                {
                    try
                    {
                        ProcessFrame(frameBytes);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                        "YOLO Error: " + ex.Message);
                    }
                    finally
                    {
                        System.Threading.Interlocked.Exchange(
                        ref _isProcessing,
                        0);
                    }
                });
            }
            catch
            {
                System.Threading.Interlocked.Exchange(
                 ref _isProcessing,
                 0);
            }
        }

        // ==========================================
        // Convert camera frame and run YOLO
        // ==========================================


        private void ProcessFrame(byte[] frameBytes)
        {
            Bitmap annotatedBitmap = null;

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
            NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
        };

                    using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results =
                    _yoloSession.Run(inputs);

                var output = results.First().AsTensor<float>();

                // Run object detection.
                List<Detection> detections = DetectObjects(output)
                .Where(d => d.ClassId == 0)
                .ToList();

                int personCount = detections.Count;

                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (!IsDisposed)
                            lblPersonCount.Text = $"HUMANS DETECTED: {personCount}";
                    }));
                }

                // Draw the boxes on the original camera frame.
                annotatedBitmap = DrawDetections(cameraBitmap, detections);

                // Transfer the annotated image to the PictureBox.
                Bitmap imageToDisplay = annotatedBitmap;
                annotatedBitmap = null;

                try
                {
                    if (pictureBoxDetection.IsDisposed ||
                        !pictureBoxDetection.IsHandleCreated)
                    {
                        imageToDisplay.Dispose();
                        return;
                    }

                    pictureBoxDetection.BeginInvoke(new Action(() =>
                    {
                        if (pictureBoxDetection.IsDisposed)
                        {
                            imageToDisplay.Dispose();
                            return;
                        }

                        Image previousImage = pictureBoxDetection.Image;
                        pictureBoxDetection.Image = imageToDisplay;
                        previousImage?.Dispose();
                    }));
                }
                catch
                {
                    imageToDisplay.Dispose();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Object detection error: " + ex.Message);
            }
            finally
            {
                annotatedBitmap?.Dispose();
            }
        }


        // ==========================================
        // Create Bitmap from VLC bytes
        // ==========================================

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
                        1,
                        3,
                        YoloHeight,
                        YoloWidth
                 });

            for (int y = 0; y < YoloHeight; y++)
            {
                for (int x = 0; x < YoloWidth; x++)
                {
                    Color pixel =
                    bitmap.GetPixel(x, y);

                    // Normalize RGB values 0-255
                    // to 0-1

                    tensor[0, 0, y, x] =
                    pixel.R / 255.0f;

                    tensor[0, 1, y, x] =
                    pixel.G / 255.0f;

                    tensor[0, 2, y, x] =
                        pixel.B / 255.0f;
                }
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

                _framePlayer.Stop();

                _frameMedia?.Dispose();

                _frameMedia = new Media(
                    _libVLC,
                    rtspUrl,
                    FromType.FromLocation);

                _framePlayer.Play(_frameMedia);

                lblLiveStatus.Text = "● CAMERA CONNECTED";
                lblLiveStatus.ForeColor = Color.FromArgb(74, 222, 128);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Camera Error: " + ex.Message);
            }
        }

        // ==========================================
        // Stop Camera
        // ==========================================

        private void button1_Click_1(object sender, EventArgs e)
        {
            _framePlayer?.Stop();
            lblLiveStatus.Text = "● CAMERA DISCONNECTED";
            lblLiveStatus.ForeColor = Color.FromArgb(255, 194, 85);

            _frameMedia?.Dispose();
            _frameMedia = null;

            Image oldImage = pictureBoxDetection.Image;
            pictureBoxDetection.Image = null;
            oldImage?.Dispose();
        }

        // ==========================================
        // Form closing
        // ==========================================


        protected override void OnFormClosed(FormClosedEventArgs e)
        {
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

            base.OnFormClosed(e);
        }


        private void pictureBoxDetection_Click(object sender, EventArgs e)
        {

        }
    }
}