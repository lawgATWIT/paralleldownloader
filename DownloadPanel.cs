using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace DownloadManagerGUI
{
    public class DownloadPanel : Panel
    {
        private string url;
        private ProgressBar progressBar;
        private Label statusLabel;
        private Label speedLabel;
        private Button actionButton;
        private Panel titleBar;
        private Label titleLabel;
        private Point dragOffset;
        private CancellationTokenSource cts;
        private bool isDownloading = false;
        private string tempDirectory;
        private bool cleanupComplete = false;

        private static readonly HttpClient sharedClient = new HttpClient()
        {
            DefaultRequestHeaders = { ConnectionClose = false },
            Timeout = TimeSpan.FromMinutes(30)
        };

        public DownloadPanel(string url, Action<DownloadPanel> onRemove)
        {
            this.url = url;
            this.tempDirectory = Path.Combine(Path.GetTempPath(), "DownloadManagerGUI_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDirectory);

            this.Width = this.Parent?.Width ?? 550; // Adjust width to parent container
            this.Dock = DockStyle.Top; // Dock to top for vertical stacking
            this.Height = 120; // Adjusted height to accommodate additional UI elements
            this.BorderStyle = BorderStyle.FixedSingle;

            // Title bar with slight transparency
            titleBar = new Panel()
            {
                Height = 30,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(128, 0, 0, 0) // Semi-transparent black
            };
            this.Controls.Add(titleBar);

            // Title label
            titleLabel = new Label()
            {
                Text = "Download Panel",
                ForeColor = Color.White,
                AutoSize = true,
                Left = 10,
                Top = 7
            };
            titleBar.Controls.Add(titleLabel); // Add titleLabel to titleBar

            // Display the URL (or a part of it)
            Label urlLabel = new Label()
            {
                Text = url,
                AutoSize = true,
                Left = 10,
                Top = titleBar.Bottom + 5,
                Width = 250  // Reduced width to make room for button
            };
            this.Controls.Add(urlLabel);

            // Status and control layout repositioning
            progressBar = new ProgressBar()
            {
                Left = 10,
                Top = urlLabel.Bottom + 5,
                Width = 400
            };
            this.Controls.Add(progressBar);

            statusLabel = new Label()
            {
                Text = "Pending",
                Left = 10,
                Top = progressBar.Bottom + 5,
                AutoSize = true
            };
            this.Controls.Add(statusLabel);

            speedLabel = new Label()
            {
                Text = "Speed: 0 MB/s",
                Left = 150,
                Top = progressBar.Bottom + 5,
                AutoSize = true
            };
            this.Controls.Add(speedLabel);

            // Combined action button (Cancel/Remove)
            actionButton = new Button()
            {
                Text = "Cancel",
                Left = 420,
                Top = statusLabel.Top - 5, // Align with status label
                Width = 80,
                Height = 30,
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font(this.Font.FontFamily, 9, FontStyle.Bold)
            };
            actionButton.FlatAppearance.BorderSize = 0;
            actionButton.Click += (s, e) =>
            {
                if (isDownloading)
                {
                    cts?.Cancel();
                    UpdateStatus("Cancelled");
                    actionButton.Text = "Remove";
                    isDownloading = false;
                }
                else
                {
                    onRemove(this);
                }
            };
            this.Controls.Add(actionButton);
        }

        private void CleanupTempFiles()
        {
            if (!cleanupComplete && Directory.Exists(tempDirectory))
            {
                try
                {
                    Directory.Delete(tempDirectory, true);
                    cleanupComplete = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to cleanup temp directory: {ex.Message}");
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CleanupTempFiles();
            }
            base.Dispose(disposing);
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragOffset = new Point(e.X, e.Y);
            }
        }

        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.Left += e.X - dragOffset.X;
                this.Top += e.Y - dragOffset.Y;
            }
        }

        public async void StartDownload()
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.FileName = "downloaded_" + Path.GetFileName(new Uri(url).LocalPath);
                saveFileDialog.Filter = "All Files|*.*";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string destinationPath = saveFileDialog.FileName;
                    statusLabel.Text = "Downloading...";
                    isDownloading = true;
                    actionButton.Text = "Cancel";
                    try
                    {
                        cts = new CancellationTokenSource();
                        await StartDownload(url, destinationPath, cts.Token);
                    }
                    catch (Exception ex)
                    {
                        statusLabel.Text = "Error";
                        MessageBox.Show("Error downloading: " + ex.Message);
                    }
                    finally
                    {
                        isDownloading = false;
                        actionButton.Text = "Remove";
                    }
                }
                else
                {
                    statusLabel.Text = "Cancelled";
                    actionButton.Text = "Remove";
                }
            }
        }

        private int CalculateOptimalChunkCount(long fileSize)
        {
            if (fileSize < 1024 * 1024) return 2;        // < 1MB
            if (fileSize < 10 * 1024 * 1024) return 4;   // < 10MB
            if (fileSize < 100 * 1024 * 1024) return 8;  // < 100MB
            return 16;                                    // >= 100MB
        }

        private int DetermineBufferSize(long chunkSize)
        {
            if (chunkSize < 1024 * 1024) return 4096;      // 4KB for small chunks
            if (chunkSize < 10 * 1024 * 1024) return 8192; // 8KB for medium chunks
            return 32768;                                   // 32KB for large chunks
        }

        public async Task StartDownload(string url, string destinationPath, CancellationToken cancellationToken)
        {
            try
            {
                long fileSize = await GetFileSizeAsync(url);
                int chunkCount = CalculateOptimalChunkCount(fileSize);
                await StartParallelDownload(url, destinationPath, chunkCount, cancellationToken);
                UpdateStatus("Downloaded");
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("Cancelled");
            }
            catch (Exception ex)
            {
                UpdateStatus("Error: " + ex.Message);
            }
        }

        private async Task StartParallelDownload(string url, string destinationPath, int chunkCount, CancellationToken cancellationToken)
        {
            long fileSize = await GetFileSizeAsync(url);
            long chunkSize = fileSize / chunkCount;

            var tasks = new Task[chunkCount];
            var progress = new long[chunkCount];
            var startTime = DateTime.Now;

            for (int i = 0; i < chunkCount; i++)
            {
                long start = i * chunkSize;
                long end = (i == chunkCount - 1) ? fileSize - 1 : (start + chunkSize - 1);
                int bufferSize = DetermineBufferSize(chunkSize);

                int chunkIndex = i;
                tasks[chunkIndex] = DownloadChunkWithRetry(chunkIndex, start, end, destinationPath, url, bufferSize, progress, fileSize, startTime, cancellationToken);
            }

            await Task.WhenAll(tasks);
            await MergeChunksAsync(destinationPath, chunkCount);
        }

        private async Task DownloadChunkWithRetry(int chunkIndex, long start, long end, string destinationPath, string url, 
            int bufferSize, long[] progress, long fileSize, DateTime startTime, CancellationToken cancellationToken, int maxRetries = 3)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, end);
                    
                    using var response = await sharedClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var chunkPath = Path.Combine(tempDirectory, $"chunk_{chunkIndex}");
                    using var fileStream = new FileStream(chunkPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize);
                    using var downloadStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    
                    var buffer = new byte[bufferSize];
                    int bytesRead;
                    while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        progress[chunkIndex] += bytesRead;
                        UpdateProgressBar(progress, fileSize);
                        UpdateSpeed(progress, fileSize, startTime);
                    }
                    return;
                }
                catch (Exception) when (attempt < maxRetries - 1)
                {
                    await Task.Delay(1000 * (attempt + 1), cancellationToken); // Exponential backoff
                }
            }
        }

        private async Task MergeChunksAsync(string destinationPath, int chunkCount, int bufferSize = 81920)
        {
            try
            {
                using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 
                    bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            
                for (int i = 0; i < chunkCount; i++)
                {
                    var chunkPath = Path.Combine(tempDirectory, $"chunk_{i}");
                    using var chunkStream = new FileStream(chunkPath, FileMode.Open, FileAccess.Read, FileShare.None, 
                        bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    await chunkStream.CopyToAsync(destinationStream, bufferSize);
                }
            }
            finally
            {
                CleanupTempFiles();
            }
        }

        private async Task<long> GetFileSizeAsync(string url)
        {
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await sharedClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            return response.Content.Headers.ContentLength ?? 0;
        }

        private void UpdateStatus(string status)
        {
            statusLabel.Invoke((Action)(() => statusLabel.Text = status));
        }

        private void UpdateProgressBar(long[] progress, long totalSize)
        {
            long totalProgress = 0;
            foreach (var p in progress)
            {
                totalProgress += p;
            }

            int percentage = (int)((totalProgress * 100) / totalSize);
            progressBar.Invoke((Action)(() => progressBar.Value = percentage));
        }

        private void UpdateSpeed(long[] progress, long totalSize, DateTime startTime)
        {
            long totalProgress = 0;
            foreach (var p in progress)
            {
                totalProgress += p;
            }

            double elapsedSeconds = (DateTime.Now - startTime).TotalSeconds;
            double speed = totalProgress / (1024.0 * 1024.0) / elapsedSeconds; // MB/s

            speedLabel.Invoke((Action)(() => speedLabel.Text = $"Speed: {speed:F2} MB/s"));
        }
    }
}
