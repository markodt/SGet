using Microsoft.Win32;
using SGet.Properties;
using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;

namespace SGet
{
    public partial class Preferences : Window
    {
        private int maxDownloads;
        private bool enableSpeedLimit;
        private int speedLimit;

        #region Constructor

        public Preferences(bool openLimitsTab)
        {
            InitializeComponent();

            if (openLimitsTab)
                tabControl.SelectedItem = (TabItem)tabControl.FindName("tiLimits");

            // Set the state of window controls based on settings from the .config file

            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (registryKey.GetValue("SGet") == null || !Settings.Default.StartOnSystemStartup)
            {
                cbStartOnSystemStartup.IsChecked = false;
            }
            else
            {
                cbStartOnSystemStartup.IsChecked = true;
            }

            cbShowWindowOnStartup.IsChecked = Settings.Default.ShowWindowOnStartup;
            cbStartDownloadsOnStartup.IsChecked = Settings.Default.StartDownloadsOnStartup;
            cbMinimizeToTray.IsChecked = Settings.Default.MinimizeToTray;
            cbCloseToTray.IsChecked = Settings.Default.CloseToTray;
            cbShowBalloonNotification.IsChecked = Settings.Default.ShowBalloonNotification;
            cbConfirmDelete.IsChecked = Settings.Default.ConfirmDelete;
            cbConfirmExit.IsChecked = Settings.Default.ConfirmExit;

            tbLocation.Text = Settings.Default.DownloadLocation;

            intMaxDownloads.Value = maxDownloads = Settings.Default.MaxDownloads;
            cbSpeedLimit.IsChecked = intSpeedLimit.IsEnabled = enableSpeedLimit = Settings.Default.EnableSpeedLimit;
            intSpeedLimit.Value = speedLimit = Settings.Default.SpeedLimit;
            intMemoryCacheSize.Value = Settings.Default.MemoryCacheSize;

            if (Settings.Default.ManualProxyConfig)
            {
                rbUseBrowserSettings.IsChecked = false;
                rbManualProxyConfig.IsChecked = true;
            }
            else
            {
                rbUseBrowserSettings.IsChecked = true;
                rbManualProxyConfig.IsChecked = false;
            }
            tbHttpProxy.Text = Settings.Default.HttpProxy;
            intProxyPort.Value = Settings.Default.ProxyPort;
            tbProxyUsername.Text = Settings.Default.ProxyUsername;
            tbProxyPassword.Password = Settings.Default.ProxyPassword;
        }

        #endregion

        #region Methods

        // Save the settings to the .config file
        private void SaveSettings()
        {
            Settings.Default.StartOnSystemStartup = cbStartOnSystemStartup.IsChecked.Value;

            // Path to the Registry key where Windows looks for startup applications
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (registryKey != null)
            {
                if (Settings.Default.StartOnSystemStartup)
                {
                    // Add a value to the Registry so the application starts on system startup
                    registryKey.SetValue("SGet", "\"" + Assembly.GetExecutingAssembly().Location + "\"");
                }
                else
                {
                    // Delete the value from the Registry to disable application start on system startup
                    registryKey.DeleteValue("SGet", false);
                }
            }

            Settings.Default.ShowWindowOnStartup = cbShowWindowOnStartup.IsChecked.Value;
            Settings.Default.StartDownloadsOnStartup = cbStartDownloadsOnStartup.IsChecked.Value;
            Settings.Default.MinimizeToTray = cbMinimizeToTray.IsChecked.Value;
            Settings.Default.CloseToTray = cbCloseToTray.IsChecked.Value;
            Settings.Default.ShowBalloonNotification = cbShowBalloonNotification.IsChecked.Value;
            Settings.Default.ConfirmDelete = cbConfirmDelete.IsChecked.Value;
            Settings.Default.ConfirmExit = cbConfirmExit.IsChecked.Value;

            Settings.Default.DownloadLocation = tbLocation.Text;

            Settings.Default.MaxDownloads = intMaxDownloads.Value.Value;
            Settings.Default.EnableSpeedLimit = cbSpeedLimit.IsChecked.Value;
            Settings.Default.SpeedLimit = intSpeedLimit.Value.Value;
            Settings.Default.MemoryCacheSize = intMemoryCacheSize.Value.Value;
            if (DownloadManager.Instance.TotalDownloads > 0)
            {
                // Set the speed limit for ongoing downloads
                if (enableSpeedLimit != Settings.Default.EnableSpeedLimit || speedLimit != Settings.Default.SpeedLimit)
                {
                    foreach (WebDownloadClient d in DownloadManager.Instance.DownloadsList)
                    {
                        if (d.Status == DownloadStatus.Downloading)
                        {
                            d.SpeedLimitChanged = true;
                        }
                    }
                }

                // Set the maximum number of active downloads
                if (maxDownloads != Settings.Default.MaxDownloads)
                {
                    foreach (WebDownloadClient d in DownloadManager.Instance.DownloadsList)
                    {
                        if (DownloadManager.Instance.ActiveDownloads < Settings.Default.MaxDownloads)
                        {
                            if (d.Status == DownloadStatus.Queued)
                            {
                                d.Start();
                            }
                        }
                    }
                    for (int i = DownloadManager.Instance.TotalDownloads - 1; i >= 0; i--)
                    {
                        if (DownloadManager.Instance.ActiveDownloads > Settings.Default.MaxDownloads)
                        {
                            if (DownloadManager.Instance.DownloadsList[i].Status == DownloadStatus.Waiting
                                || DownloadManager.Instance.DownloadsList[i].Status == DownloadStatus.Downloading)
                            {
                                DownloadManager.Instance.DownloadsList[i].Status = DownloadStatus.Queued;
                            }
                        }
                    }
                }
            }

            if (rbManualProxyConfig.IsChecked.Value)
            {
                Settings.Default.ManualProxyConfig = true;
            }
            else
            {
                Settings.Default.ManualProxyConfig = false;
            }
            Settings.Default.HttpProxy = tbHttpProxy.Text.Trim();
            Settings.Default.ProxyPort = intProxyPort.Value.Value;
            Settings.Default.ProxyUsername = tbProxyUsername.Text.Trim();
            Settings.Default.ProxyPassword = tbProxyPassword.Password.Trim();

            // Save settings
            Settings.Default.Save();
        }

        private void EnableProxyConfig(bool enabled)
        {
            tbHttpProxy.IsEnabled = enabled;
            intProxyPort.IsEnabled = enabled;
            tbProxyUsername.IsEnabled = enabled;
            tbProxyPassword.IsEnabled = enabled;
        }

        #endregion

        #region Event Handlers

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbDialog = new FolderBrowserDialog();
            fbDialog.Description = "Choose Default Download Location";
            fbDialog.ShowNewFolderButton = true;
            DialogResult result = fbDialog.ShowDialog();

            if (result.ToString().Equals("OK"))
            {
                string path = fbDialog.SelectedPath;
                if (path.EndsWith("\\") == false)
                    path += "\\";
                tbLocation.Text = path;
            }
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            this.Close();
        }

        private void cbSpeedLimit_Click(object sender, RoutedEventArgs e)
        {
            intSpeedLimit.IsEnabled = cbSpeedLimit.IsChecked.Value;
        }

        private void rbManualProxyConfig_Checked(object sender, RoutedEventArgs e)
        {
            EnableProxyConfig(true);
        }

        private void rbManualProxyConfig_Unchecked(object sender, RoutedEventArgs e)
        {
            EnableProxyConfig(false);
        }

        // Restore default settings for the current tab
        private void btnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (tabControl.SelectedItem == (TabItem)tabControl.FindName("tiGeneral"))
            {
                cbStartOnSystemStartup.IsChecked = false;
                cbShowWindowOnStartup.IsChecked = true;
                cbStartDownloadsOnStartup.IsChecked = false;
                cbMinimizeToTray.IsChecked = true;
                cbCloseToTray.IsChecked = false;
                cbShowBalloonNotification.IsChecked = true;
                cbConfirmDelete.IsChecked = true;
                cbConfirmExit.IsChecked = true;
            }
            else if (tabControl.SelectedItem == (TabItem)tabControl.FindName("tiLocation"))
            {
                tbLocation.Text = @"C:\Downloads\";
            }
            else if (tabControl.SelectedItem == (TabItem)tabControl.FindName("tiLimits"))
            {
                intMaxDownloads.Value = 5;
                cbSpeedLimit.IsChecked = false;
                intSpeedLimit.IsEnabled = false;
                intSpeedLimit.Value = 200;
                intMemoryCacheSize.Value = 1024;
            }
            else
            {
                rbUseBrowserSettings.IsChecked = true;
                rbManualProxyConfig.IsChecked = false;
                tbHttpProxy.IsEnabled = false;
                tbHttpProxy.Text = String.Empty;
                intProxyPort.IsEnabled = false;
                intProxyPort.Value = 0;
                tbProxyUsername.IsEnabled = false;
                tbProxyUsername.Text = String.Empty;
                tbProxyPassword.IsEnabled = false;
                tbProxyPassword.Password = String.Empty;
            }

            SaveSettings();
        }

        #endregion
    }
}
