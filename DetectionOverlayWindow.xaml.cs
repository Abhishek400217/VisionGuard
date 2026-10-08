using CameraViewer.AI;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CameraViewer
{
    public partial class DetectionOverlayWindow : Window
    {
        public DetectionOverlayWindow()
        {
            InitializeComponent();
        }

        public void UpdateDetections(
            IReadOnlyList<Detection> detections)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                OverlayCanvas.Children.Clear();

                double width = OverlayCanvas.ActualWidth;
                double height = OverlayCanvas.ActualHeight;

                if (width <= 0 || height <= 0)
                    return;

                foreach (var detection in detections)
                {
                    double x = detection.X1 * width;
                    double y = detection.Y1 * height;

                    double boxWidth =
                        (detection.X2 - detection.X1) * width;

                    double boxHeight =
                        (detection.Y2 - detection.Y1) * height;

                    if (boxWidth <= 2 || boxHeight <= 2)
                        continue;

                    var rectangle = new Rectangle
                    {
                        Width = boxWidth,
                        Height = boxHeight,
                        Stroke = Brushes.LimeGreen,
                        StrokeThickness = 4,
                        Fill = Brushes.Transparent,
                        IsHitTestVisible = false
                    };

                    Canvas.SetLeft(rectangle, x);
                    Canvas.SetTop(rectangle, y);

                    OverlayCanvas.Children.Add(rectangle);

                    var label = new Border
                    {
                        Background = Brushes.LimeGreen,
                        Padding = new Thickness(5, 2, 5, 2),
                        IsHitTestVisible = false,

                        Child = new TextBlock
                        {
                            Text =
                                $"PERSON {detection.Confidence:P0}",

                            Foreground = Brushes.Black,
                            FontSize = 14,
                            FontWeight = FontWeights.Bold
                        }
                    };

                    Canvas.SetLeft(label, x);
                    Canvas.SetTop(
                        label,
                        Math.Max(0, y - 30));

                    OverlayCanvas.Children.Add(label);
                }
            }));
        }

        public void ClearDetections()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                OverlayCanvas.Children.Clear();
            }));
        }
    }
}