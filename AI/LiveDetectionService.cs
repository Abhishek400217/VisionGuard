using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace CameraViewer.AI;

public sealed record Detection(
    float X1,
    float Y1,
    float X2,
    float Y2,
    float Confidence,
    int ClassId);

public sealed class LiveDetectionService : IDisposable
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _player;
    private readonly InferenceSession _session;
    private readonly string _rtspUrl;

    private const int InputSize = 640;

    private const int FrameWidth = 640;
    private const int FrameHeight = 360;
    private const int BytesPerPixel = 4;
    private const int Pitch = FrameWidth * BytesPerPixel;
    private const int BufferSize = Pitch * FrameHeight;

    private const float ConfidenceThreshold = 0.35f;
    private const float NmsThreshold = 0.45f;

    private IntPtr _frameBuffer = IntPtr.Zero;

    private readonly object _frameLock = new();

    private long _lastInferenceMs;

    private int _inferenceRunning;

    private long _frameCount;

    private bool _disposed;

    public event Action<IReadOnlyList<Detection>>? DetectionsAvailable;

    public event Action<string>? Diagnostic;

    public LiveDetectionService(
        LibVLC libVlc,
        string rtspUrl,
        string modelPath)
    {
        _libVlc = libVlc ?? throw new ArgumentNullException(nameof(libVlc));

        _rtspUrl = rtspUrl ?? throw new ArgumentNullException(nameof(rtspUrl));

        if (string.IsNullOrWhiteSpace(modelPath))
            throw new ArgumentException(
                "Model path is empty.",
                nameof(modelPath));

        _session = new InferenceSession(modelPath);

        _frameBuffer = Marshal.AllocHGlobal(BufferSize);

        _player = new MediaPlayer(_libVlc);

        _player.SetVideoFormat(
            "RV32",
            FrameWidth,
            FrameHeight,
            Pitch);

        _player.SetVideoCallbacks(
            VideoLock,
            null,
            VideoDisplay);

        Diagnostic?.Invoke(
            "AI model loaded: " + modelPath);

        Diagnostic?.Invoke(
            "AI input: 640x640, camera frame: 640x360");
    }

    public void Start()
    {
        ThrowIfDisposed();

        Stop();

        using var media = new Media(
            _libVlc,
            new Uri(_rtspUrl));

        media.AddOption(":rtsp-tcp");
        media.AddOption(":network-caching=500");
        media.AddOption(":live-caching=500");
        media.AddOption(":no-audio");

        Diagnostic?.Invoke(
            "AI connecting to stream2...");

        bool result = _player.Play(media);

        if (!result)
        {
            throw new InvalidOperationException(
                "AI RTSP stream could not be started.");
        }

        Diagnostic?.Invoke(
            "AI live stream started.");
    }

    public void Stop()
    {
        if (_disposed)
            return;

        try
        {
            if (_player.State != VLCState.Stopped)
            {
                _player.Stop();
            }
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke(
                "AI stream stop failed: " + ex.Message);
        }
    }

    private IntPtr VideoLock(
        IntPtr opaque,
        IntPtr planes)
    {
        if (_frameBuffer == IntPtr.Zero)
            return IntPtr.Zero;

        Marshal.WriteIntPtr(
            planes,
            _frameBuffer);

        return _frameBuffer;
    }

    private void VideoDisplay(
        IntPtr opaque,
        IntPtr picture)
    {
        try
        {
            if (_disposed)
                return;

            long frameNumber =
                Interlocked.Increment(ref _frameCount);

            var frame = new byte[BufferSize];

            lock (_frameLock)
            {
                Marshal.Copy(
                    _frameBuffer,
                    frame,
                    0,
                    BufferSize);
            }

            if (frameNumber == 1)
            {
                Diagnostic?.Invoke(
                    "AI FIRST FRAME RECEIVED.");
            }

            if (frameNumber % 100 == 0)
            {
                Diagnostic?.Invoke(
                    "AI frames received: " + frameNumber);
            }

            long now = Environment.TickCount64;

            if (now - _lastInferenceMs < 100)
                return;

            _lastInferenceMs = now;

            if (Interlocked.CompareExchange(
                    ref _inferenceRunning,
                    1,
                    0) != 0)
            {
                return;
            }

            _ = Task.Run(() =>
            {
                try
                {
                    RunInference(frame);
                }
                finally
                {
                    Interlocked.Exchange(
                        ref _inferenceRunning,
                        0);
                }
            });
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke(
                "AI frame callback error: " +
                ex.Message);
        }
    }

    private void RunInference(byte[] bgra)
    {
        try
        {
            var tensor = new DenseTensor<float>(
                new[] { 1, 3, InputSize, InputSize });

            float padValue = 114f / 255f;

            for (int y = 0; y < InputSize; y++)
            {
                for (int x = 0; x < InputSize; x++)
                {
                    tensor[0, 0, y, x] = padValue;
                    tensor[0, 1, y, x] = padValue;
                    tensor[0, 2, y, x] = padValue;
                }
            }

            const int padY = 140;

            for (int y = 0; y < FrameHeight; y++)
            {
                int sourceRow = y * Pitch;

                int targetY = y + padY;

                for (int x = 0; x < FrameWidth; x++)
                {
                    int p =
                        sourceRow +
                        x * 4;

                    float b =
                        bgra[p] / 255f;

                    float g =
                        bgra[p + 1] / 255f;

                    float r =
                        bgra[p + 2] / 255f;

                    tensor[0, 0, targetY, x] = r;
                    tensor[0, 1, targetY, x] = g;
                    tensor[0, 2, targetY, x] = b;
                }
            }

            string inputName =
                _session.InputMetadata.Keys.First();

            using var results =
                _session.Run(
                    new[]
                    {
                        NamedOnnxValue.CreateFromTensor(
                            inputName,
                            tensor)
                    });

            var output =
                results.FirstOrDefault();

            if (output == null)
            {
                Diagnostic?.Invoke(
                    "YOLO ERROR: no output tensor.");

                return;
            }

            var outputTensor =
                output.AsTensor<float>();

            int[] dims =
                outputTensor.Dimensions.ToArray();

            string shape =
                "[" +
                string.Join(",", dims) +
                "]";

            if (Interlocked.Read(ref _frameCount) % 100 < 2)
            {
                Diagnostic?.Invoke(
                    "YOLO output shape: " + shape);
            }

            List<Detection> detections;

            /*
             * YOLO26 NMS-free output:
             *
             * [1, 300, 6]
             *
             * x1,y1,x2,y2,confidence,class
             */
            if (dims.Length == 3 &&
                dims[0] == 1 &&
                dims[2] == 6)
            {
                detections =
                    ParseEndToEndOutput(
                        outputTensor,
                        dims);
            }

            /*
             * Normal YOLO26 ONNX output:
             *
             * [1, 84, 8400]
             *
             * 4 box values + 80 COCO classes.
             */
            else if (dims.Length == 3 &&
                     dims[0] == 1 &&
                     dims[1] >= 5)
            {
                detections =
                    ParseRawOutput(
                        outputTensor,
                        dims);
            }
            else
            {
                Diagnostic?.Invoke(
                    "YOLO ERROR: unsupported output shape " +
                    shape);

                return;
            }

            var people =
                detections
                    .Where(d =>
                        d.ClassId == 0 &&
                        d.Confidence >=
                        ConfidenceThreshold)
                    .ToList();

            if (people.Count > 0)
            {
                Diagnostic?.Invoke(
                    "PERSON DETECTED: " +
                    people.Count +
                    " | confidence=" +
                    people.Max(x =>
                        x.Confidence).ToString("0.00"));
            }

            DetectionsAvailable?.Invoke(
                people);
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke(
                "YOLO inference error: " +
                ex);
        }
    }

    private static List<Detection>
        ParseEndToEndOutput(
            Tensor<float> tensor,
            int[] dims)
    {
        int count = dims[1];

        var detections =
            new List<Detection>();

        for (int i = 0; i < count; i++)
        {
            float x1 =
                tensor[0, i, 0];

            float y1 =
                tensor[0, i, 1];

            float x2 =
                tensor[0, i, 2];

            float y2 =
                tensor[0, i, 3];

            float confidence =
                tensor[0, i, 4];

            int classId =
                (int)tensor[0, i, 5];

            if (confidence <
                ConfidenceThreshold)
            {
                continue;
            }

            NormalizeBox(
                ref x1,
                ref y1,
                ref x2,
                ref y2);

            detections.Add(
                new Detection(
                    x1,
                    y1,
                    x2,
                    y2,
                    confidence,
                    classId));
        }

        return ApplyNms(
            detections);
    }

    private static List<Detection>
        ParseRawOutput(
            Tensor<float> tensor,
            int[] dims)
    {
        int channels = dims[1];

        int count = dims[2];

        int classCount =
            channels - 4;

        var detections =
            new List<Detection>();

        for (int i = 0; i < count; i++)
        {
            float cx =
                tensor[0, 0, i];

            float cy =
                tensor[0, 1, i];

            float width =
                tensor[0, 2, i];

            float height =
                tensor[0, 3, i];

            float bestConfidence =
                0f;

            int bestClass =
                -1;

            for (int c = 0;
                 c < classCount;
                 c++)
            {
                float confidence =
                    tensor[0, 4 + c, i];

                if (confidence >
                    bestConfidence)
                {
                    bestConfidence =
                        confidence;

                    bestClass = c;
                }
            }

            if (bestClass < 0 ||
                bestConfidence <
                ConfidenceThreshold)
            {
                continue;
            }

            float x1 =
                cx - width / 2f;

            float y1 =
                cy - height / 2f;

            float x2 =
                cx + width / 2f;

            float y2 =
                cy + height / 2f;

            NormalizeBox(
                ref x1,
                ref y1,
                ref x2,
                ref y2);

            detections.Add(
                new Detection(
                    x1,
                    y1,
                    x2,
                    y2,
                    bestConfidence,
                    bestClass));
        }

        return ApplyNms(
            detections);
    }

    private static void NormalizeBox(
        ref float x1,
        ref float y1,
        ref float x2,
        ref float y2)
    {
        /*
         * Camera frame:
         * 640 x 360
         *
         * YOLO input:
         * 640 x 640
         *
         * Therefore:
         * top padding = 140 pixels.
         */

        y1 -= 140f;
        y2 -= 140f;

        x1 =
            Math.Clamp(
                x1,
                0,
                FrameWidth);

        x2 =
            Math.Clamp(
                x2,
                0,
                FrameWidth);

        y1 =
            Math.Clamp(
                y1,
                0,
                FrameHeight);

        y2 =
            Math.Clamp(
                y2,
                0,
                FrameHeight);
    }

    private static List<Detection>
        ApplyNms(
            List<Detection> detections)
    {
        var result =
            new List<Detection>();

        var ordered =
            detections
                .OrderByDescending(
                    x => x.Confidence)
                .ToList();

        while (ordered.Count > 0)
        {
            var best =
                ordered[0];

            result.Add(best);

            ordered.RemoveAt(0);

            ordered.RemoveAll(
                other =>
                    other.ClassId ==
                    best.ClassId &&
                    IoU(best, other) >
                    NmsThreshold);
        }

        return result;
    }

    private static float IoU(
        Detection a,
        Detection b)
    {
        float x1 =
            Math.Max(a.X1, b.X1);

        float y1 =
            Math.Max(a.Y1, b.Y1);

        float x2 =
            Math.Min(a.X2, b.X2);

        float y2 =
            Math.Min(a.Y2, b.Y2);

        float intersectionWidth =
            Math.Max(0, x2 - x1);

        float intersectionHeight =
            Math.Max(0, y2 - y1);

        float intersection =
            intersectionWidth *
            intersectionHeight;

        float areaA =
            Math.Max(
                0,
                a.X2 - a.X1) *
            Math.Max(
                0,
                a.Y2 - a.Y1);

        float areaB =
            Math.Max(
                0,
                b.X2 - b.X1) *
            Math.Max(
                0,
                b.Y2 - b.Y1);

        float union =
            areaA +
            areaB -
            intersection;

        if (union <= 0)
            return 0;

        return intersection / union;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(LiveDetectionService));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _player.Stop();
        }
        catch
        {
        }

        try
        {
            _player.Dispose();
        }
        catch
        {
        }

        try
        {
            _session.Dispose();
        }
        catch
        {
        }

        if (_frameBuffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(
                _frameBuffer);

            _frameBuffer =
                IntPtr.Zero;
        }
    }
}