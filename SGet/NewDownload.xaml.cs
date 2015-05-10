using SGet.Properties;
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace SGet
{
    public partial class NewDownload : Window
    {
        private bool urlValid;
        private bool startImmediately;
        private MainWindow mainWindow;
        private NumberFormatInfo numberFormat = NumberFormatInfo.InvariantInfo;

        #region Constructor

        public NewDownload(MainWindow mainWin)
        {
            InitializeComponent();
            mainWindow = mainWin;
            tbDownloadFolder.Text = Settings.Default.DownloadLocation;
            urlValid = false;
            startImmediately = true;

            if (System.Windows.Clipboard.ContainsText())
            {
                string clipboardText = System.Windows.Clipboard.GetText();

                if (IsUrlValid(clipboardText))
                {
                    urlValid = true;
                    tbURL.Text = clipboardText;
                    tbSaveAs.Text = tbURL.Text.Substring(tbURL.Text.LastIndexOf("/") + 1);
                }
            }
        }

        #endregion

        #region Methods

        // Validate the URL
        private bool IsUrlValid(string Url)
        {
            if (Url.StartsWith("http") && Url.Contains(":") && (Url.Length > 15)
                && (Utilities.CountOccurence(Url, '/') >= 3) && (Url.LastIndexOf('/') != Url.Length - 1))
            {
                string lastChars = Url.Substring(Url.Length - 9);

                // Check if the URL contains a dot in the last 8 characters
                if (lastChars.Contains(".") && (lastChars.LastIndexOf('.') != lastChars.Length - 1))
                {
                    // Get the extension string based on the index of the last dot
                    string ext = lastChars.Substring(lastChars.LastIndexOf('.') + 1);

                    // Check if the extension string contains some illegal characters
                    string chars = " ?#&%=[]_-+~:;\\/!$<>\"\'*";

                    foreach (char c in ext)
                    {
                        foreach (char s in chars)
                        {
                            if (c == s)
                                return false;
                        }
                    }

                    return true;
                }
                return false;
            }
            return false;
        }

        // Return the amount of free disk space on a given partition
        private string GetFreeDiskSpace(string driveName)
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.Name == driveName)
                {
                    long freeSpace = drive.AvailableFreeSpace;
                    double mbFreeSpace = (double)freeSpace / Math.Pow(1024, 2);
                    double gbFreeSpace = mbFreeSpace / 1024D;

                    if (freeSpace < Math.Pow(1024, 3))
                    {
                        return mbFreeSpace.ToString("#.00", numberFormat) + " MB";
                    }
                    return gbFreeSpace.ToString("#.00", numberFormat) + " GB";
                }
            }
            return String.Empty;
        }

        #endregion

        #region Event Handlers

        private void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (urlValid)
            {
                if (tbSaveAs.Text.Length < 3 || !tbSaveAs.Text.Contains("."))
                {
                    Xceed.Wpf.Toolkit.MessageBox.Show("The local file name is not valid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                try
                {
                    WebDownloadClient download = new WebDownloadClient(tbURL.Text.Trim());

                    download.FileName = tbSaveAs.Text.Trim();

                    // Register WebDownloadClient events
                    download.DownloadProgressChanged += download.DownloadProgressChangedHandler;
                    download.DownloadCompleted += download.DownloadCompletedHandler;
                    download.PropertyChanged += this.mainWindow.PropertyChangedHandler;
                    download.StatusChanged += this.mainWindow.StatusChangedHandler;
                    download.DownloadCompleted += this.mainWindow.DownloadCompletedHandler;

                    // Create path to temporary file
                    if (!Directory.Exists(tbDownloadFolder.Text))
                    {
                        Directory.CreateDirectory(tbDownloadFolder.Text);
                    }
                    string filePath = tbDownloadFolder.Text + download.FileName;
                    string tempPath = filePath + ".tmp";

                    // Check if there is already an ongoing download on that path
                    if (File.Exists(tempPath))
                    {
                        string message = "There is already a download in progress at the specified path.";
                        Xceed.Wpf.Toolkit.MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Check if the file already exists
                    if (File.Exists(filePath))
                    {
                        string message = "There is already a file with the same name, do you want to overwrite it? "
                                       + "If not, please change the file name or download folder.";
                        MessageBoxResult result = Xceed.Wpf.Toolkit.MessageBox.Show(message, "File Name Conflict: " + filePath, MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes)
                        {
                            File.Delete(filePath);
                        }
                        else
                            return;
                    }

                    // Set username and password if HTTP authentication is required
                    if (cbLoginToServer.IsChecked.Value && (tbUsername.Text.Trim().Length > 0) && (tbPassword.Password.Trim().Length > 0))
                    {
                        download.ServerLogin = new NetworkCredential(tbUsername.Text.Trim(), tbPassword.Password.Trim());
                    }

                    // Check the URL
                    download.CheckUrl();
                    if (download.HasError)
                        return;

                    download.TempDownloadPath = tempPath;

                    download.AddedOn = DateTime.UtcNow;
                    download.CompletedOn = DateTime.MinValue;
                    download.OpenFileOnCompletion = this.cbOpenFileOnCompletion.IsChecked.Value;

                    // Add the download to the downloads list
                    DownloadManager.Instance.DownloadsList.Add(download);

                    // Start downloading the file
                    if (startImmediately)
                        download.Start();
                    else
                        download.Status = DownloadStatus.Paused;

                    // Close the Add New Download window
                    this.Close();
                }
                catch (Exception ex)
                {
                    Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                Xceed.Wpf.Toolkit.MessageBox.Show("The URL is not a valid download link.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbDialog = new FolderBrowserDialog();
            fbDialog.Description = "Choose Download Folder";
            fbDialog.ShowNewFolderButton = true;
            DialogResult result = fbDialog.ShowDialog();

            if (result.ToString().Equals("OK"))
            {
                string path = fbDialog.SelectedPath;
                if (path.EndsWith("\\") == false)
                    path += "\\";
                tbDownloadFolder.Text = path;
            }
        }

        private void tbURL_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsUrlValid(tbURL.Text))
            {
                urlValid = true;
                tbSaveAs.Text = tbURL.Text.Substring(tbURL.Text.LastIndexOf("/") + 1);
            }
            else
            {
                urlValid = false;
                tbSaveAs.Text = String.Empty;
            }
        }

        private void tbDownloadFolder_TextChanged(object sender, TextChangedEventArgs e)
        {
            string drive = String.Empty;
            if (tbDownloadFolder.Text.Length > 3)
                drive = tbDownloadFolder.Text.Remove(3);
            else
                drive = tbDownloadFolder.Text;
            lblFreeSpace.Content = "Free Disk Space: " + GetFreeDiskSpace(drive);
        }

        private void cbStartImmediately_Click(object sender, RoutedEventArgs e)
        {
            startImmediately = this.cbStartImmediately.IsChecked.Value;
        }

        private void cbLoginToServer_Click(object sender, RoutedEventArgs e)
        {
            tbUsername.IsEnabled = cbLoginToServer.IsChecked.Value;
            tbPassword.IsEnabled = cbLoginToServer.IsChecked.Value;
        }

        #endregion
    }
}
