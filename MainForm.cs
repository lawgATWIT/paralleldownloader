using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DownloadManagerGUI
{
    public class MainForm : Form
    {
        private TableLayoutPanel downloadsPanel;
        private Button addButton;

        public MainForm()
        {
            this.Text = "Download Manager";
            this.Width = 600;
            this.Height = 400;

            // Initialize TableLayoutPanel for grid-like structure
            downloadsPanel = new TableLayoutPanel()
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 1,
                RowCount = 0
            };
            this.Controls.Add(downloadsPanel);

            // Add button to add new downloads
            addButton = new Button()
            {
                Text = "Add Download",
                Dock = DockStyle.Top,
                Height = 40
            };
            addButton.Click += AddButton_Click;
            this.Controls.Add(addButton);
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            string url = Microsoft.VisualBasic.Interaction.InputBox("Enter the URL to download:", "Add Download", "");
            if (!string.IsNullOrWhiteSpace(url))
            {
                AddDownloadPanel(url);
            }
        }

        private void AddDownloadPanel(string url)
        {
            var downloadPanel = new DownloadPanel(url, RemoveDownloadPanel);
            downloadsPanel.RowCount++;
            downloadsPanel.Controls.Add(downloadPanel);
            downloadPanel.StartDownload(); // Start the download immediately
        }

        private void RemoveDownloadPanel(DownloadPanel panel)
        {
            downloadsPanel.Controls.Remove(panel);
            downloadsPanel.RowCount--;
        }
    }
}
