using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using SGet.Properties;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;

namespace SGet
{
    public partial class MainWindow
    {
        private List<string> propertyNames;
        private List<string> propertyValues;
        private List<PropertyModel> propertiesList;
        private bool trayExit;
        private string[] args;

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();

            args = Environment.GetCommandLineArgs();
            if (!Settings.Default.ShowWindowOnStartup && args.Length != 2)
            {
                this.ShowInTaskbar = false;
                this.Visibility = Visibility.Hidden;
            }

            // Bind DownloadsList to downloadsGrid
            downloadsGrid.ItemsSource = DownloadManager.Instance.DownloadsList;
            DownloadManager.Instance.DownloadsList.CollectionChanged += new NotifyCollectionChangedEventHandler(DownloadsList_CollectionChanged);

            // In case of computer shutdown or restart, save current list of downloads to an XML file
            SystemEvents.SessionEnding += new SessionEndingEventHandler(SystemEvents_SessionEnding);

            propertyNames = new List<string>();
            propertyNames.Add("URL");
            propertyNames.Add("Supports Resume");
            propertyNames.Add("File Type");
            propertyNames.Add("Download Folder");
            propertyNames.Add("Average Speed");
            propertyNames.Add("Total Time");

            propertyValues = new List<string>();
            propertiesList = new List<PropertyModel>();
            SetEmptyPropertiesGrid();
            propertiesGrid.ItemsSource = propertiesList;

            // Load downloads from the XML file
            LoadDownloadsFromXml();

            if (DownloadManager.Instance.TotalDownloads == 0)
            {
                EnableMenuItems(false);

                // Clean temporary files in the download directory if no downloads were loaded
                if (Directory.Exists(Settings.Default.DownloadLocation))
                {
                    DirectoryInfo downloadLocation = new DirectoryInfo(Settings.Default.DownloadLocation);
                    foreach (FileInfo file in downloadLocation.GetFiles())
                    {
                        if (file.FullName.EndsWith(".tmp"))
                            file.Delete();
                    }
                }
            }

            this.cbShowGrid.IsChecked = Settings.Default.ShowGrid;
            this.cbShowProperties.IsChecked = Settings.Default.ShowProperties;
            this.cbShowStatusBar.IsChecked = Settings.Default.ShowStatusBar;

            if (this.cbShowGrid.IsChecked.Value)
            {
                this.downloadsGrid.GridLinesVisibility = DataGridGridLinesVisibility.All;
            }
            else
            {
                this.downloadsGrid.GridLinesVisibility = DataGridGridLinesVisibility.None;
            }
            if (this.cbShowProperties.IsChecked.Value)
            {
                this.propertiesSplitter.Visibility = System.Windows.Visibility.Visible;
                this.propertiesPanel.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                this.propertiesSplitter.Visibility = System.Windows.Visibility.Collapsed;
                this.propertiesPanel.Visibility = System.Windows.Visibility.Collapsed;
            }
            if (this.cbShowStatusBar.IsChecked.Value)
            {
                this.statusBar.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                this.statusBar.Visibility = System.Windows.Visibility.Collapsed;
            }

            trayExit = false;
        }

        #endregion

        #region Methods

        private void SetEmptyPropertiesGrid()
        {
            if (propertiesList.Count > 0)
                propertiesList.Clear();
            for (int i = 0; i < 6; i++)
            {
                propertiesList.Add(new PropertyModel(propertyNames[i], String.Empty));
            }

            propertiesGrid.Items.Refresh();
        }

        private void PauseAllDownloads()
        {
            if (downloadsGrid.Items.Count > 0)
            {
                foreach (WebDownloadClient download in DownloadManager.Instance.DownloadsList)
                {
                    download.Pause();
                }
            }
        }

        private void SaveDownloadsToXml()
        {
            if (DownloadManager.Instance.TotalDownloads > 0)
            {
                // Pause downloads
                PauseAllDownloads();

                XElement root = new XElement("downloads");

                foreach (WebDownloadClient download in DownloadManager.Instance.DownloadsList)
                {
                    string username = String.Empty;
                    string password = String.Empty;
                    if (download.ServerLogin != null)
                    {
                        username = download.ServerLogin.UserName;
                        password = download.ServerLogin.Password;
                    }

                    XElement xdl = new XElement("download",
                                        new XElement("file_name", download.FileName),
                                        new XElement("url", download.Url.ToString()),
                                        new XElement("username", username),
                                        new XElement("password", password),
                                        new XElement("temp_path", download.TempDownloadPath),
                                        new XElement("file_size", download.FileSize),
                                        new XElement("downloaded_size", download.DownloadedSize),
                                        new XElement("status", download.Status.ToString()),
                                        new XElement("status_text", download.StatusText),
                                        new XElement("total_time", download.TotalElapsedTime.ToString()),
                                        new XElement("added_on", download.AddedOn.ToString()),
                                        new XElement("completed_on", download.CompletedOn.ToString()),
                                        new XElement("supports_resume", download.SupportsRange.ToString()),
                                        new XElement("has_error", download.HasError.ToString()),
                                        new XElement("open_file", download.OpenFileOnCompletion.ToString()),
                                        new XElement("temp_created", download.TempFileCreated.ToString()),
                                        new XElement("is_batch", download.IsBatch.ToString()),
                                        new XElement("url_checked", download.BatchUrlChecked.ToString()));
                    root.Add(xdl);
                }

                XDocument xd = new XDocument();
                xd.Add(root);
                // Save downloads to XML file
                xd.Save("Downloads.xml");
            }
        }

        private void LoadDownloadsFromXml()
        {
            try
            {
                if (File.Exists("Downloads.xml"))
                {
                    // Load downloads from XML file
                    XElement downloads = XElement.Load("Downloads.xml");
                    if (downloads.HasElements)
                    {
                        IEnumerable<XElement> downloadsList =
                            from el in downloads.Elements()
                            select el;
                        foreach (XElement download in downloadsList)
                        {
                            // Create WebDownloadClient object based on XML data
                            WebDownloadClient downloadClient = new WebDownloadClient(download.Element("url").Value);

                            downloadClient.FileName = download.Element("file_name").Value;

                            downloadClient.DownloadProgressChanged += downloadClient.DownloadProgressChangedHandler;
                            downloadClient.DownloadCompleted += downloadClient.DownloadCompletedHandler;
                            downloadClient.PropertyChanged += this.PropertyChangedHandler;
                            downloadClient.StatusChanged += this.StatusChangedHandler;
                            downloadClient.DownloadCompleted += this.DownloadCompletedHandler;

                            string username = download.Element("username").Value;
                            string password = download.Element("password").Value;
                            if (username != String.Empty && password != String.Empty)
                            {
                                downloadClient.ServerLogin = new NetworkCredential(username, password);
                            }

                            downloadClient.TempDownloadPath = download.Element("temp_path").Value;
                            downloadClient.FileSize = Convert.ToInt64(download.Element("file_size").Value);
                            downloadClient.DownloadedSize = Convert.ToInt64(download.Element("downloaded_size").Value);

                            DownloadManager.Instance.DownloadsList.Add(downloadClient);

                            if (download.Element("status").Value == "Completed")
                            {
                                downloadClient.Status = DownloadStatus.Completed;
                            }
                            else
                            {
                                downloadClient.Status = DownloadStatus.Paused;
                            }

                            downloadClient.StatusText = download.Element("status_text").Value;

                            downloadClient.ElapsedTime = TimeSpan.Parse(download.Element("total_time").Value);
                            downloadClient.AddedOn = DateTime.Parse(download.Element("added_on").Value);
                            downloadClient.CompletedOn = DateTime.Parse(download.Element("completed_on").Value);

                            downloadClient.SupportsRange = Boolean.Parse(download.Element("supports_resume").Value);
                            downloadClient.HasError = Boolean.Parse(download.Element("has_error").Value);
                            downloadClient.OpenFileOnCompletion = Boolean.Parse(download.Element("open_file").Value);
                            downloadClient.TempFileCreated = Boolean.Parse(download.Element("temp_created").Value);
                            downloadClient.IsBatch = Boolean.Parse(download.Element("is_batch").Value);
                            downloadClient.BatchUrlChecked = Boolean.Parse(download.Element("url_checked").Value);

                            if (downloadClient.Status == DownloadStatus.Paused && !downloadClient.HasError && Settings.Default.StartDownloadsOnStartup)
                            {
                                downloadClient.Start();
                            }
                        }

                        // Create empty XML file
                        XElement root = new XElement("downloads");
                        XDocument xd = new XDocument();
                        xd.Add(root);
                        xd.Save("Downloads.xml");
                    }
                }
            }
            catch (Exception)
            {
                Xceed.Wpf.Toolkit.MessageBox.Show("There was an error while loading the download list.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnableMenuItems(bool enabled)
        {
            btnDelete.IsEnabled = enabled;
            btnClearCompleted.IsEnabled = enabled;
            btnStart.IsEnabled = enabled;
            btnPause.IsEnabled = enabled;
            tcmStartAll.IsEnabled = enabled;
            tcmPauseAll.IsEnabled = enabled;
        }

        private void EnableDataGridMenuItems(bool enabled)
        {
            cmStart.IsEnabled = enabled;
            cmPause.IsEnabled = enabled;
            cmDelete.IsEnabled = enabled;
            cmRestart.IsEnabled = enabled;
            cmOpenFile.IsEnabled = enabled;
            cmOpenDownloadFolder.IsEnabled = enabled;
            cmStartAll.IsEnabled = enabled;
            cmPauseAll.IsEnabled = enabled;
            cmSelectAll.IsEnabled = enabled;
            cmCopyURLtoClipboard.IsEnabled = enabled;
        }

        #endregion

        #region Main Window Event Handlers

        private void mainWindow_ContentRendered(object sender, EventArgs e)
        {
            // In case the application was started from a web browser and receives command-line arguments
            if (args.Length == 2)
            {
                if (args[1].StartsWith("http"))
                {
                    System.Windows.Clipboard.SetText(args[1]);

                    NewDownload newDownloadDialog = new NewDownload(this);
                    newDownloadDialog.ShowDialog();
                }
            }
        }

        private void mainWindow_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == System.Windows.WindowState.Minimized && Settings.Default.MinimizeToTray)
            {
                this.ShowInTaskbar = false;
            }
        }

        private void mainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (Settings.Default.CloseToTray && !trayExit)
            {
                this.Hide();
                e.Cancel = true;
                return;
            }

            if (Settings.Default.ConfirmExit)
            {
                string message = "Are you sure you want to exit the application?";
                MessageBoxResult result = Xceed.Wpf.Toolkit.MessageBox.Show(message, "SGet", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    trayExit = false;
                    return;
                }
            }

            SaveDownloadsToXml();
        }

        private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            SaveDownloadsToXml();
        }

        private void mainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl + A selects all downloads in the list
            if ((Keyboard.Modifiers == ModifierKeys.Control) && (e.Key == Key.A))
            {
                this.downloadsGrid.SelectAll();
            }
        }

        private void downloadsGrid_KeyUp(object sender, KeyEventArgs e)
        {
            // Delete key clears selected downloads
            if (e.Key == Key.Delete)
            {
                btnDelete_Click(sender, e);
            }
        }

        private void downloadsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (downloadsGrid.SelectedItems.Count > 0)
            {
                foreach (WebDownloadClient downld in DownloadManager.Instance.DownloadsList)
                {
                    downld.IsSelected = false;
                }

                var download = (WebDownloadClient)downloadsGrid.SelectedItem;

                if (propertyValues.Count > 0)
                    propertyValues.Clear();

                propertyValues.Add(download.Url.ToString());
                string resumeSupported = "No";
                if (download.SupportsRange)
                    resumeSupported = "Yes";
                propertyValues.Add(resumeSupported);
                propertyValues.Add(download.FileType);
                propertyValues.Add(download.DownloadFolder);
                propertyValues.Add(download.AverageDownloadSpeed);
                propertyValues.Add(download.TotalElapsedTimeString);

                if (propertiesList.Count > 0)
                    propertiesList.Clear();

                for (int i = 0; i < 6; i++)
                {
                    propertiesList.Add(new PropertyModel(propertyNames[i], propertyValues[i]));
                }

                propertiesGrid.Items.Refresh();
                download.IsSelected = true;
            }
            else
            {
                if (DownloadManager.Instance.TotalDownloads > 0)
                {
                    foreach (WebDownloadClient downld in DownloadManager.Instance.DownloadsList)
                    {
                        downld.IsSelected = false;
                    }
                }
                SetEmptyPropertiesGrid();
            }
        }

        public void PropertyChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            WebDownloadClient download = (WebDownloadClient)sender;
            if (e.PropertyName == "AverageSpeedAndTotalTime" && download.Status != DownloadStatus.Deleting)
            {
                this.Dispatcher.Invoke(new PropertyChangedEventHandler(UpdatePropertiesList), sender, e);
            }
        }

        private void UpdatePropertiesList(object sender, PropertyChangedEventArgs e)
        {
            propertyValues.RemoveRange(4, 2);
            var download = (WebDownloadClient)downloadsGrid.SelectedItem;
            propertyValues.Add(download.AverageDownloadSpeed);
            propertyValues.Add(download.TotalElapsedTimeString);

            propertiesList.RemoveRange(4, 2);
            propertiesList.Add(new PropertyModel(propertyNames[4], propertyValues[4]));
            propertiesList.Add(new PropertyModel(propertyNames[5], propertyValues[5]));
            propertiesGrid.Items.Refresh();
        }

        private void downloadsGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            dgScrollViewer.ScrollToVerticalOffset(dgScrollViewer.VerticalOffset - e.Delta / 3);
        }

        private void propertiesGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            propertiesScrollViewer.ScrollToVerticalOffset(propertiesScrollViewer.VerticalOffset - e.Delta / 3);
        }

        private void downloadsGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (DownloadManager.Instance.TotalDownloads == 0)
            {
                EnableDataGridMenuItems(false);
            }
            else
            {
                if (downloadsGrid.SelectedItems.Count == 1)
                {
                    EnableDataGridMenuItems(true);
                }
                else if (downloadsGrid.SelectedItems.Count > 1)
                {
                    EnableDataGridMenuItems(true);
                    cmOpenFile.IsEnabled = false;
                    cmOpenDownloadFolder.IsEnabled = false;
                    cmCopyURLtoClipboard.IsEnabled = false;
                }
                else
                {
                    EnableDataGridMenuItems(false);
                    cmStartAll.IsEnabled = true;
                    cmPauseAll.IsEnabled = true;
                    cmSelectAll.IsEnabled = true;
                }
            }
        }

        private void DownloadsList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (DownloadManager.Instance.TotalDownloads == 1)
            {
                EnableMenuItems(true);
                this.statusBarDownloads.Content = "1 Download";
            }
            else if (DownloadManager.Instance.TotalDownloads > 1)
            {
                EnableMenuItems(true);
                this.statusBarDownloads.Content = DownloadManager.Instance.TotalDownloads + " Downloads";
            }
            else
            {
                EnableMenuItems(false);
                this.statusBarDownloads.Content = "Ready";
            }
        }

        public void StatusChangedHandler(object sender, EventArgs e)
        {
            this.Dispatcher.Invoke(new EventHandler(StatusChanged), sender, e);
        }

        private void StatusChanged(object sender, EventArgs e)
        {
            // Start the first download in the queue, if it exists
            WebDownloadClient dl = (WebDownloadClient)sender;
            if (dl.Status == DownloadStatus.Paused || dl.Status == DownloadStatus.Completed
                || dl.Status == DownloadStatus.Deleted || dl.HasError)
            {
                foreach (WebDownloadClient d in DownloadManager.Instance.DownloadsList)
                {
                    if (d.Status == DownloadStatus.Queued)
                    {
                        d.Start();
                        break;
                    }
                }
            }

            foreach (WebDownloadClient d in DownloadManager.Instance.DownloadsList)
            {
                if (d.Status == DownloadStatus.Downloading)
                {
                    d.SpeedLimitChanged = true;
                }
            }

            int active = DownloadManager.Instance.ActiveDownloads;
            int completed = DownloadManager.Instance.CompletedDownloads;

            if (active > 0)
            {
                if (completed == 0)
                    this.statusBarActive.Content = " (" + active + " Active)";
                else
                    this.statusBarActive.Content = " (" + active + " Active, ";
            }
            else
                this.statusBarActive.Content = String.Empty;

            if (completed > 0)
            {
                if (active == 0)
                    this.statusBarCompleted.Content = " (" + completed + " Completed)";
                else
                    this.statusBarCompleted.Content = completed + " Completed)";
            }
            else
                this.statusBarCompleted.Content = String.Empty;
        }

        public void DownloadCompletedHandler(object sender, EventArgs e)
        {
            if (Settings.Default.ShowBalloonNotification)
            {
                WebDownloadClient download = (WebDownloadClient)sender;

                if (download.Status == DownloadStatus.Completed)
                {
                    string title = "Download Completed";
                    string text = download.FileName + " has finished downloading.";

                    XNotifyIcon.ShowBalloonTip(title, text, BalloonIcon.Info);
                }
            }
        }

        #endregion

        #region Click Event Handlers

        private void btnNewDownload_Click(object sender, RoutedEventArgs e)
        {
            NewDownload newDownloadDialog = new NewDownload(this);
            newDownloadDialog.ShowDialog();
        }

        private void btnBatchDownload_Click(object sender, RoutedEventArgs e)
        {
            BatchDownload batchDownloadDialog = new BatchDownload(this);
            batchDownloadDialog.ShowDialog();
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.SelectedItems.Count > 0)
            {
                MessageBoxResult result = MessageBoxResult.None;
                if (Settings.Default.ConfirmDelete)
                {
                    string message = "Are you sure you want to delete the selected download(s)?";
                    result = Xceed.Wpf.Toolkit.MessageBox.Show(message, "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                }

                if (result == MessageBoxResult.Yes || !Settings.Default.ConfirmDelete)
                {
                    var selectedDownloads = downloadsGrid.SelectedItems.Cast<WebDownloadClient>();
                    var downloadsToDelete = new List<WebDownloadClient>();

                    foreach (WebDownloadClient download in selectedDownloads)
                    {
                        if (download.HasError || download.Status == DownloadStatus.Paused || download.Status == DownloadStatus.Queued)
                        {
                            if (File.Exists(download.TempDownloadPath))
                            {
                                File.Delete(download.TempDownloadPath);
                            }
                            download.Status = DownloadStatus.Deleting;
                            downloadsToDelete.Add(download);
                        }
                        else if (download.Status == DownloadStatus.Completed)
                        {
                            download.Status = DownloadStatus.Deleting;
                            downloadsToDelete.Add(download);
                        }
                        else
                        {
                            download.Status = DownloadStatus.Deleting;
                            while (true)
                            {
                                if (download.DownloadThread.ThreadState == System.Threading.ThreadState.Stopped)
                                {
                                    if (File.Exists(download.TempDownloadPath))
                                    {
                                        File.Delete(download.TempDownloadPath);
                                    }
                                    downloadsToDelete.Add(download);
                                    break;
                                }
                            }
                        }
                    }

                    foreach (var download in downloadsToDelete)
                    {
                        download.Status = DownloadStatus.Deleted;
                        DownloadManager.Instance.DownloadsList.Remove(download);
                    }
                }
            }
        }

        private void btnClearCompleted_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadManager.Instance.TotalDownloads > 0)
            {
                var downloadsToClear = new List<WebDownloadClient>();

                foreach (var download in DownloadManager.Instance.DownloadsList)
                {
                    if (download.Status == DownloadStatus.Completed)
                    {
                        download.Status = DownloadStatus.Deleting;
                        downloadsToClear.Add(download);
                    }
                }

                foreach (var download in downloadsToClear)
                {
                    download.Status = DownloadStatus.Deleted;
                    DownloadManager.Instance.DownloadsList.Remove(download);
                }
            }
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.SelectedItems.Count > 0)
            {
                var selectedDownloads = downloadsGrid.SelectedItems.Cast<WebDownloadClient>();

                foreach (WebDownloadClient download in selectedDownloads)
                {
                    if (download.Status == DownloadStatus.Paused || download.HasError)
                    {
                        download.Start();
                    }
                }
            }
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.SelectedItems.Count > 0)
            {
                var selectedDownloads = downloadsGrid.SelectedItems.Cast<WebDownloadClient>();

                foreach (WebDownloadClient download in selectedDownloads)
                {
                    download.Pause();
                }
            }
        }

        private void btnAbout_Click(object sender, RoutedEventArgs e)
        {
            About about = new About();
            about.ShowDialog();
        }

        private void cmRestart_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.SelectedItems.Count > 0)
            {
                var selectedDownloads = downloadsGrid.SelectedItems.Cast<WebDownloadClient>();

                foreach (WebDownloadClient download in selectedDownloads)
                {
                    download.Restart();
                }
            }
        }

        private void cmOpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.SelectedItems.Count == 1)
            {
                var download = (WebDownloadClient)downloadsGrid.SelectedItem;
                if (download.Status == DownloadStatus.Completed && File.Exists(download.DownloadPath))
                {
                    Process.Start(@download.DownloadPath);
                }
            }
        }

        private void cmOpenDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.SelectedItems.Count == 1)
            {
                var download = (WebDownloadClient)downloadsGrid.SelectedItem;
                int lastIndex = download.DownloadPath.LastIndexOf("\\");
                string directory = download.DownloadPath.Remove(lastIndex + 1);
                if (Directory.Exists(directory))
                {
                    Process.Start(@directory);
                }
            }
        }

        private void cmStartAll_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.Items.Count > 0)
            {
                foreach (WebDownloadClient download in DownloadManager.Instance.DownloadsList)
                {
                    if (download.Status == DownloadStatus.Paused || download.HasError)
                    {
                        download.Start();
                    }
                }
            }
        }

        private void cmPauseAll_Click(object sender, RoutedEventArgs e)
        {
            PauseAllDownloads();
        }

        private void cmSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.Items.Count > 0)
            {
                if (downloadsGrid.SelectedItems.Count < downloadsGrid.Items.Count)
                {
                    downloadsGrid.SelectAll();
                }
            }
        }

        private void cmCopyURLtoClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (downloadsGrid.SelectedItems.Count == 1)
            {
                var download = (WebDownloadClient)downloadsGrid.SelectedItem;
                System.Windows.Clipboard.SetText(download.Url.ToString());
            }
        }

        private void tcmShowMainWindow_Click(object sender, RoutedEventArgs e)
        {
            this.ShowInTaskbar = true;
            this.Visibility = Visibility.Visible;
            this.WindowState = System.Windows.WindowState.Normal;
        }

        private void tcmExit_Click(object sender, RoutedEventArgs e)
        {
            // Close all windows
            trayExit = true;
            for (int intCounter = App.Current.Windows.Count - 1; intCounter >= 0; intCounter--)
                App.Current.Windows[intCounter].Close();
        }

        private void btnSetLimits_Click(object sender, RoutedEventArgs e)
        {
            Preferences limits = new Preferences(true);
            limits.ShowDialog();
        }

        private void btnPreferences_Click(object sender, RoutedEventArgs e)
        {
            Preferences preferences = new Preferences(false);
            preferences.ShowDialog();
        }

        private void cbShowGrid_Click(object sender, RoutedEventArgs e)
        {
            if (cbShowGrid.IsChecked.Value)
            {
                this.downloadsGrid.GridLinesVisibility = DataGridGridLinesVisibility.All;
            }
            else
            {
                this.downloadsGrid.GridLinesVisibility = DataGridGridLinesVisibility.None;
            }
            Settings.Default.ShowGrid = cbShowGrid.IsChecked.Value;
            Settings.Default.Save();
        }

        private void cbShowProperties_Click(object sender, RoutedEventArgs e)
        {
            if (cbShowProperties.IsChecked.Value)
            {
                this.propertiesSplitter.Visibility = Visibility.Visible;
                this.propertiesPanel.Visibility = Visibility.Visible;
            }
            else
            {
                this.propertiesSplitter.Visibility = Visibility.Collapsed;
                this.propertiesPanel.Visibility = Visibility.Collapsed;
            }
            Settings.Default.ShowProperties = cbShowProperties.IsChecked.Value;
            Settings.Default.Save();
        }

        private void cbShowStatusBar_Click(object sender, RoutedEventArgs e)
        {
            if (cbShowStatusBar.IsChecked.Value)
            {
                this.statusBar.Visibility = Visibility.Visible;
            }
            else
            {
                this.statusBar.Visibility = Visibility.Collapsed;
            }
            Settings.Default.ShowStatusBar = cbShowStatusBar.IsChecked.Value;
            Settings.Default.Save();
        }

        #endregion
    }
}
