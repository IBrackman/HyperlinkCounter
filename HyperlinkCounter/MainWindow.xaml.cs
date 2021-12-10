using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Documents;
using System.Windows.Media;

namespace HyperlinkCounter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly List<string> UrlList;
        private CancellationTokenSource TokenSource;

        public MainWindow()
        {
            InitializeComponent();

            UrlList = new List<string>();

            OutputScroll.Visibility = Visibility.Collapsed;
            StartCounting.Visibility = Visibility.Collapsed;
            StopCounting.Visibility = Visibility.Collapsed;
        }

        private void Border_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            DragDropLabel.Content = "Отпустите мышь";
            e.Effects = DragDropEffects.Copy;
        }

        private void Border_DragLeave(object sender, DragEventArgs e)
        {
            DragDropLabel.Content = "Перетащите файл сюда";
        }

        private void Border_Drop(object sender, DragEventArgs e)
        {
            DragDropLabel.Content = "";

            DragDropZone.Visibility = Visibility.Collapsed;
            OutputScroll.Visibility = Visibility.Visible;

            var paths = new List<string>();

            foreach (var path in e.Data.GetData(DataFormats.FileDrop) as string[])
            {
                if (Directory.Exists(path))
                    paths.AddRange(Directory.GetFiles(path, "*.*", SearchOption.AllDirectories));
                else
                    paths.Add(path);
            }

            OutputTextBlock.Text = $"Вы выбрали файлы:{Environment.NewLine}{string.Join(Environment.NewLine, paths)}";

            ReadFiles(paths);
        }

        private async void StartCounting_Click(object sender, RoutedEventArgs e)
        {
            StartCounting.IsEnabled = false;
            StopCounting.IsEnabled = true;

            OutputTextBlock.Text = "";
            TokenSource = new CancellationTokenSource();

            var cancelToken = TokenSource.Token;
            var max = 0;
            var progess = new Progress<string>(text => OutputTextBlock.Text = text);
            List<string> res = null;

            try
            {
                OutputTextBlock.Text = "Подсчёт начат";

                await Task.Run(() =>
                {
                    res = ComputeCount(ref max, cancelToken, progess);
                }, cancelToken);
            }
            catch (OperationCanceledException)
            {
                OutputTextBlock.Text = "Подсчёт прекращён";
                return;
            }
            catch (Exception exception)
            {
                OutputTextBlock.Text = $"Произошла ошибка:{Environment.NewLine}{exception}";
                StopCounting.IsEnabled = false;
                return;
            }

            PrintResult(res, max);

            StopCounting.IsEnabled = false;
        }

        private void StopCounting_Click(object sender, RoutedEventArgs e)
        {
            TokenSource.Cancel();

            StartCounting.IsEnabled = true;
            StopCounting.IsEnabled = false;
        }

        private async void ReadFiles(List<string> paths)
        {
            foreach (var path in paths)
                await Task.Run(() =>
                {
                    UrlList.AddRange(File.ReadLines(path, Encoding.GetEncoding(1251)));
                });

            StartCounting.Visibility = Visibility.Visible;
            StopCounting.Visibility = Visibility.Visible;
            StopCounting.IsEnabled = false;
        }

        private List<string> ComputeCount(ref int max, CancellationToken cancelToken, IProgress<string> progress)
        {
            var res = new List<string>();

            using (var wc = new WebClient())
            {
                foreach (var url in UrlList.Distinct())
                {
                    progress.Report($"Анализируется {url}{Environment.NewLine}");

                    cancelToken.ThrowIfCancellationRequested();

                    var cnt = new Regex(@"</a>").Matches(wc.DownloadString(url)).Count;

                    if (cnt > max) max = cnt;

                    res.Add($"{url} - {cnt}{Environment.NewLine}");
                }
            }

            return res;
        }

        private void PrintResult(List<string> res, int max)
        {
            OutputTextBlock.Text = "";

            foreach (var line in res)
            {
                var run = new Run(line);

                if (line.EndsWith($"{max}{Environment.NewLine}"))
                    run.Background = Brushes.Yellow;

                OutputTextBlock.Inlines.Add(run);
            }
        }
    }
}