using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using LoggerProviderExtensions.HubLogger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace UpdateTool.Views
{
    public partial class LogView : UserControl
    {
        private IClipboard? clipboard;
        private ContextMenu contextMenu = null!;

        private ScrollViewer scrollViewer = null!;
        private SelectableTextBlock textView = null!;
        private readonly CancellationTokenSource cancellationTokenSource;

        private static readonly SolidColorBrush GrayBrush = new(Color.Parse("#8C8C8C"));
        private static readonly SolidColorBrush TextBrush = new(Color.Parse("#262626"));
        private static readonly SolidColorBrush DebugBrush = new(Color.Parse("#1890FF"));
        private static readonly SolidColorBrush InfoBrush = new(Color.Parse("#52C41A"));
        private static readonly SolidColorBrush WarnBrush = new(Color.Parse("#FAAD14"));
        private static readonly SolidColorBrush ErrorBrush = new(Color.Parse("#FF4D4F"));
        private static readonly SolidColorBrush FatalBrush = new(Color.Parse("#FF4D4F"));
        private static readonly SolidColorBrush DefaultBrush = new(Color.Parse("#000000"));
        private readonly IHubLoggerPublisher loggerPublisher;
        public LogView()
        {
            InitializeComponent();
            cancellationTokenSource = new CancellationTokenSource();
            Init();
            loggerPublisher = App.Services.GetRequiredService<IHubLoggerPublisher>();
        }
        private void Init()
        {
            textView = this.FindControl<SelectableTextBlock>("LogTextView")
                ?? throw new InvalidOperationException("LogTextView is missing.");
            scrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer")
                ?? throw new InvalidOperationException("LogScrollViewer is missing.");
            contextMenu = this.FindControl<ContextMenu>("LogContextMenu")
                ?? throw new InvalidOperationException("LogContextMenu is missing.");
            textView.Text = string.Empty;
        }
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            var level = TopLevel.GetTopLevel(this);
            if (level == null) return;
            clipboard = level.Clipboard;
            loggerPublisher.OnLog += Process;
        }
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            cancellationTokenSource?.Cancel();
            loggerPublisher.OnLog -= Process;
        }

        private void Process(LoggerProviderExtensions.StructuredLogEntry logEntry)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Invoke(() => UpdateLogText(logEntry));
        }

        private void UpdateLogText(LoggerProviderExtensions.StructuredLogEntry logEntry)
        {

            var inlines = textView.Inlines;
            if (inlines == null) return;

            try
            {
                if (inlines.Count > 1000)
                {
                    for (var i = 0; i < 10; i++)
                    {
                        if (inlines.Count > 0)
                        {
                            if (inlines[0] is Run run)
                            {
                                run.Foreground = null;
                                run.Text = null;
                            }

                            inlines.RemoveAt(0);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                // 批量添加日志到UI
                var runs = new List<Inline>
                {
                    new Run($"{logEntry.Timestamp}")
                    {
                        Foreground = GrayBrush,
                        BaselineAlignment = BaselineAlignment.Center
                    }
                };
                var levelRun = new Run($"[{logEntry.LogLevel}]") // 修复中文乱码，使用方括号替代
                {
                    Foreground = GetLevelForeground(logEntry.LogLevel),
                };
                if (logEntry.LogLevel == LogLevel.Critical)
                {
                    levelRun.FontWeight = FontWeight.Bold;
                }

                runs.Add(levelRun);
                runs.Add(new Run(logEntry.MessageTemplate)
                {
                    Foreground = TextBrush,
                    BaselineAlignment = BaselineAlignment.Center
                });
                runs.Add(new Run(Environment.NewLine));


                var isAtBottom = IsAtVerticalBottom(scrollViewer);
                inlines.AddRange(runs);
                if (isAtBottom)
                {
                    scrollViewer.ScrollToEnd();
                }
            }
            catch
            {
                // ignored
            }

            static bool IsAtVerticalBottom(ScrollViewer scrollView, double tolerance = 1.0)
            {
                if (scrollView == null) return false;
                var scp = FindVisualDescendant<ScrollContentPresenter>(scrollView);
                if (scp == null) return false;

                double verticalOffset = scp.Offset.Y;
                double viewportHeight = scp.Viewport.Height;
                double extentHeight = scp.Extent.Height;
                double totalScrollable = extentHeight - viewportHeight;

                return totalScrollable <= 0 || verticalOffset >= totalScrollable - tolerance;
            }

            static T? FindVisualDescendant<T>(Visual visual) where T : Visual
            {
                if (visual == null) return null;
                foreach (var child in visual.GetVisualChildren())
                {
                    if (child is T target) return target;
                    var descendant = FindVisualDescendant<T>(child);
                    if (descendant != null) return descendant;
                }
                return null;
            }

            static IBrush GetLevelForeground(LogLevel level)
            {
                return level switch
                {
                    LogLevel.Debug => DebugBrush,
                    LogLevel.Information => InfoBrush,
                    LogLevel.Warning => WarnBrush,
                    LogLevel.Error => ErrorBrush,
                    LogLevel.Critical => FatalBrush,
                    _ => DefaultBrush
                };
            }
        }

        private void LogScrollViewer_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
        }

        private async void Copy_OnClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (textView.SelectedText.Length > 0 && clipboard != null)
                    await clipboard.SetTextAsync(textView.SelectedText);
            }
            catch
            {
                // ignored
            }
        }

        private void Clear_OnClick(object? sender, RoutedEventArgs e)
        {
            textView.Inlines?.Clear();
        }
    }
}