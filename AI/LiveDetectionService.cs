using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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

/// <summary>
/// Live RTSP -> LibVLC decoded frame -> YOLO26 ONNX pipeline.
/// This is a SECOND, headless player. The existing VideoView player is untouched.
/// </summary>
public sealed class LiveDetectionService : IDisposable
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _player;
    private readonly InferenceSession _session;
    private readonly string _rtspUrl;

    private const int InputSize = 640;

    // TP-Link sub-stream is expected to be 640x360.
    // If your camera reports another stream2 resolution, we will make this dynamic
    // in the next step instead of changing the display pipeline.
    private const int FrameWidth = 640;
    private const int FrameHeight = 360;
    private const int BytesPerPixel = 4;
    private const int Pitch = FrameWidth * BytesPerPixel;
    private const int BufferSize = Pitch * FrameHeight;

    private IntPtr _frameBuffer = IntPtr.Zero;
    private readonly object _frameLock = new();
    private long _lastInferenceMs;
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

        _session = new InferenceSession(modelPath);

        _frameBuffer = Marshal.AllocHGlobal(BufferSize);

        _player = new MediaPlayer(_libVlc);

        // Force a predictable BGRA/RV32 frame format for the AI pipeline.
        _player.SetVideoFormat(
            "RV32",
            FrameWidth,
            FrameHeight,
            Pitch);

        _player.SetVideoCallbacks(
            VideoLock,
            null,
            VideoDisplay);
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

        if (!_player.Play(media))
        {
            throw new InvalidOperationException(
                "AI RTSP stream could not be started.");
        }

        Diagnostic?.Invoke("AI live stream started.");
    }

    public void Stop()
    {
        if (_disposed)
            return;

        try
        {
            if (_player.IsPlaying)
                _player.Stop();
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
        // LibVLC asks us where it should decode the next frame.
        Marshal.WriteIntPtr(planes, _frameBuffer);
        return _frameBuffer;
    }

    private void VideoDisplay(
        IntPtr opaque,
        IntPtr picture)
    {
        try
        {
            // Copy decoded native pixels before LibVLC reuses the buffer.
            var frame = new byte[BufferSize];

            lock (_frameLock)
            {
                Marshal.Copy(
                    _frameBuffer,
                    frame,
                    0,
                    BufferSize);
            }

            // Cap inference at about 10 FPS so the live display is not blocked.
            long now = Environment.TickCount64;

            if (now - _lastInferenceMs < 100)
                return;

            _lastInferenceMs = now;

            _ = Task.Run(() => RunInference(frame));
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke(
                "AI frame callback error: " + ex.Message);
        }
    }

    private void RunInference(byte[] bgra)
    {
        try
        {
            var tensor = new DenseTensor<float>(
                new[] { 1, 3, InputSize, InputSize });

            // Fill 640x640 with gray padding.
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

            // 640x360 -> 640x640 letterbox.
            const int padY = 140;

            for (int y = 0; y < FrameHeight; y++)
            {
                int sourceRow = y * Pitch;
                int targetY = y + padY;

                for (int x = 0; x < FrameWidth; x++)
                {
                    int p = sourceRow + x * 4;

                    float b = bgra[p] / 255f;
                    float g = bgra[p + 1] / 255f;
                    float r = bgra[p + 2] / 255f;

                    tensor[0, 0, targetY, x] = r;
                    tensor[0, 1, targetY, x] = g;
                    tensor[0, 2, targetY, x] = b;
                }
            }

            string inputName =
                _session.InputMetadata.Keys.First();

            using var results = _session.Run(
                new[]
                {
                    NamedOnnxValue.CreateFromTensor(
                        inputName,
                        tensor)
                });

            var detections = ParseOutput(results);

            // COCO "person" = class 0.
            var people = detections
                .Where(d =>
                    d.ClassId == 0 &&
                    d.Confidence >= 0.45f)
                .ToList();

            DetectionsAvailable?.Invoke(people);
        }
        catch (Exception ex)
        {
            Diagnostic?.Invoke(
                "YOLO inference error: " + ex.Message);
        }
    }

    private static List<Detection> ParseOutput(
        IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
    {
        var output = results.FirstOrDefault();

        if (output == null)
            return new List<Detection>();

        var tensor = output.AsTensor<float>();
        int[] dims = tensor.Dimensions.ToArray();

        // YOLO26 end-to-end ONNX export:
        // [1, N, 6] => x1,y1,x2,y2,confidence,class_id
        if (dims.Length != 3 || dims[^1] < 6)
        {
            throw new InvalidOperationException(
                "Unexpected YOLO output shape: [" +
                string.Join(",", dims) + "]");
        }

        int count = dims[^2];
        var detections = new List<Detection>(count);

        for (int i = 0; i < count; i++)
        {
            float x1 = tensor[0, i, 0];
            float y1 = tensor[0, i, 1];
            float x2 = tensor[0, i, 2];
            float y2 = tensor[0, i, 3];
            float confidence = tensor[0, i, 4];
            int classId = (int)tensor[0, i, 5];

            // Undo 640x640 -> 640x360 letterbox.
            y1 -= 140f;
            y2 -= 140f;

            x1 = Math.Clamp(x1, 0, FrameWidth);
            x2 = Math.Clamp(x2, 0, FrameWidth);
            y1 = Math.Clamp(y1, 0, FrameHeight);
            y2 = Math.Clamp(y2, 0, FrameHeight);

            detections.Add(
                new Detection(
                    x1 / FrameWidth,
                    y1 / FrameHeight,
                    x2 / FrameWidth,
                    y2 / FrameHeight,
                    confidence,
                    classId));
        }

        return detections;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(
                nameof(LiveDetectionService));
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

        _player.Dispose();
        _session.Dispose();

        if (_frameBuffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_frameBuffer);
            _frameBuffer = IntPtr.Zero;
        }
    }
}
