using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Microsoft.Win32;
using LenovoController.Features;

namespace LenovoController
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private MainWindow _mainWindow;
        private Forms.NotifyIcon _trayIcon;
        private Icon _trayIconImage;
        private DateTime _lastTrayActionAt = DateTime.MinValue;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Theme.Refresh();

            _mainWindow = new MainWindow();
            _mainWindow.PrepareForPopup();

            CreateTrayIcon();

            _mainWindow.PowerModeChanged += (sender, args) => UpdateTrayIcon();

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        private void CreateTrayIcon()
        {
            _trayIcon = new Forms.NotifyIcon
            {
                Text = "Lenovo Controller",
                Visible = false
            };
            _trayIcon.MouseUp += TrayIcon_MouseUp;
            _trayIcon.MouseClick += TrayIcon_LeftClick;

            UpdateTrayIcon();
            _trayIcon.Visible = true;
        }

        private void TrayIcon_MouseUp(object sender, Forms.MouseEventArgs e)
        {
            if (e.Button != Forms.MouseButtons.Right)
                return;

            ShowTrayMenu();
        }

        private void ShowTrayMenu()
        {
            const uint mfSeparator = 0x00000800;
            const uint tpmRightButton = 0x0002;
            const uint tpmNonotify = 0x0080;
            const uint tpmReturnCmd = 0x0100;

            var menu = Native.CreatePopupMenu();
            if (menu == IntPtr.Zero)
                return;

            try
            {
                Native.AppendMenuW(menu, 0, 1, Localization.MenuOpen);
                Native.AppendMenuW(menu, 0, 2, Localization.MenuRefresh);
                Native.AppendMenuW(menu, mfSeparator, 0, null);
                Native.AppendMenuW(menu, 0, 3, Localization.MenuExit);

                var hwnd = new System.Windows.Interop.WindowInteropHelper(_mainWindow).Handle;
                var position = Forms.Cursor.Position;

                Native.SetForegroundWindow(hwnd);

                var command = Native.TrackPopupMenuEx(
                    menu,
                    tpmReturnCmd | tpmNonotify | tpmRightButton,
                    position.X,
                    position.Y,
                    hwnd,
                    IntPtr.Zero);

                Native.PostMessage(hwnd, 0, IntPtr.Zero, IntPtr.Zero);

                switch (command)
                {
                    case 1:
                        ShowPopup();
                        break;
                    case 2:
                        _mainWindow.RefreshFeatures();
                        break;
                    case 3:
                        ExitApplication();
                        break;
                }
            }
            finally
            {
                Native.DestroyMenu(menu);
            }
        }

        private void TrayIcon_LeftClick(object sender, Forms.MouseEventArgs e)
        {
            if (e.Button != Forms.MouseButtons.Left)
                return;

            // MouseUp and MouseClick both fire for a single physical click.
            var now = DateTime.UtcNow;
            if (now - _lastTrayActionAt < TimeSpan.FromMilliseconds(300))
                return;
            _lastTrayActionAt = now;

            if (_mainWindow.IsVisible)
            {
                _mainWindow.HidePopup(false);
                return;
            }

            // The click that dismissed the popup also deactivates it; don't instantly reopen.
            if (now - _mainWindow.LastHiddenAt < TimeSpan.FromMilliseconds(300))
                return;

            ShowPopup();
        }

        private void ShowPopup()
        {
            Theme.Refresh();
            UpdateTrayIcon();
            _mainWindow.ShowPopup();
        }

        private void UpdateTrayIcon()
        {
            if (_trayIcon == null)
                return;

            string mode;
            string modeLabel;
            switch (_mainWindow?.PowerModeState ?? PowerModeState.Balance)
            {
                case PowerModeState.Quiet:
                    mode = "quiet";
                    modeLabel = Localization.PowerModeQuiet;
                    break;
                case PowerModeState.Performance:
                    mode = "performance";
                    modeLabel = Localization.PowerModePerformance;
                    break;
                default:
                    mode = "balanced";
                    modeLabel = Localization.PowerModeBalanced;
                    break;
            }

            _trayIcon.Text = string.Format(Localization.TrayTooltipFormat, modeLabel);

            var theme = Theme.IsTaskbarLight ? "black" : "white";
            var resource = GetResourceStream(new Uri($"pack://application:,,,/Resources/tray-{mode}-{theme}.ico"));
            if (resource == null)
                return;

            using (var stream = resource.Stream)
            {
                var icon = new Icon(stream, Forms.SystemInformation.SmallIconSize);
                _trayIcon.Icon = icon;
                _trayIconImage?.Dispose();
                _trayIconImage = icon;
            }
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General && e.Category != UserPreferenceCategory.Color)
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                Theme.Refresh();
                UpdateTrayIcon();
            }));
        }

        private void ExitApplication()
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            _trayIconImage?.Dispose();
            _trayIconImage = null;

            _mainWindow.AllowClose();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            _trayIconImage?.Dispose();
            _trayIconImage = null;

            base.OnExit(e);
        }

        private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var errorText = e.Exception.ToString();
            Trace.TraceError(errorText);
            MessageBox.Show(errorText, Localization.ErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(-1);
        }
    }
}
