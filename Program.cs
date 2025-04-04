using System;
using System.Windows.Forms;

namespace DownloadManagerGUI
{
    // The Program class contains the main entry point for the application
    static class Program
    {
        // Main method is the entry point of the application
        [STAThread] // Required for Windows Forms applications to work correctly
        static void Main()
        {
            // Set high DPI mode for better scaling on high DPI screens
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            // Enable visual styles for the UI elements (like buttons, forms, etc.)
            Application.EnableVisualStyles();

            // Set default text rendering behavior for compatibility
            Application.SetCompatibleTextRenderingDefault(false);

            // Run the main form (the first window that will open)
            Application.Run(new MainForm()); // Make sure the MainForm class exists
        }
    }
}
