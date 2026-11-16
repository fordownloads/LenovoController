using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LenovoController.Features;

namespace LenovoController
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int DwmUseImmersiveDarkModeBefore20H1 = 19;
        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmWindowCornerPreference = 33;
        private const int DwmSystemBackdropType = 38;
        private const int DwmMicaEffect = 1029;
        private const int DwmSystemBackdropMainWindow = 2;
        private const int DwmCornerRound = 2;

        private readonly RadioButton[] _alwaysOnUsbButtons;
        private readonly AlwaysOnUsbFeature _alwaysOnUsbFeature = new AlwaysOnUsbFeature();
        private readonly RadioButton[] _batteryButtons;
        private readonly BatteryFeature _batteryFeature = new BatteryFeature();
        private readonly FnLockFeature _fnLockFeature = new FnLockFeature();
        private readonly OverDriveFeature _overDriveFeature = new OverDriveFeature();
        private readonly RadioButton[] _powerModeButtons;
        private readonly PowerModeFeature _powerModeFeature = new PowerModeFeature();
        private readonly TouchpadLockFeature _touchpadLockFeature = new TouchpadLockFeature();

        private bool _allowClose;
        private bool _isHiding;
        private bool _menuOpen;
        private bool _preparing;
        private bool _micaEnabled;
        private bool _wasActivated;
        private PowerModeState _powerModeState = PowerModeState.Balance;
        private DateTime _lastHiddenAt = DateTime.MinValue;
        private DateTime _shownAt = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();

            _powerModeButtons = new[] {radioQuiet, radioBalance, radioPerformance};
            _batteryButtons = new[] {radioConservation, radioNormalCharge, radioRapidCharge};
            _alwaysOnUsbButtons = new[] {radioAlwaysOnUsbOff, radioAlwaysOnUsbOnWhenSleeping, radioAlwaysOnUsbOnAlways};

            UpdateWindowIcon();
            Theme.Changed += (sender, args) => SyncTheme();
            Refresh();
        }

        internal DateTime LastHiddenAt => _lastHiddenAt;

        internal PowerModeState PowerModeState => _powerModeState;

        internal event EventHandler PowerModeChanged;

        private void SetPowerModeState(PowerModeState state)
        {
            if (_powerModeState == state)
                return;

            _powerModeState = state;
            PowerModeChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        ///     Sizes the window once while it is invisible, so the first popup opens without a flash.
        /// </summary>
        internal void PrepareForPopup()
        {
            _preparing = true;
            Opacity = 0;
            Show();
            UpdateLayout();
            HideImmediately();
            Opacity = 1;
            _lastHiddenAt = DateTime.MinValue;
            _preparing = false;
        }

        internal void RefreshFeatures() => Refresh();

        internal void ShowPopup()
        {
            if (IsVisible && !_isHiding)
            {
                Activate();
                ForceForeground();
                return;
            }

            if (_isHiding)
                ResetAnimations();

            _wasActivated = false;
            _shownAt = DateTime.UtcNow;

            PositionWindow();
            Show();
            Activate();
            ForceForeground();
            AnimateIn();
        }

        private void ForceForeground()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            var foreground = Native.GetForegroundWindow();
            var foregroundThread = foreground == IntPtr.Zero
                ? 0
                : Native.GetWindowThreadProcessId(foreground, IntPtr.Zero);
            var currentThread = Native.GetCurrentThreadId();

            var attached = foregroundThread != 0 && foregroundThread != currentThread &&
                           Native.AttachThreadInput(currentThread, foregroundThread, true);

            Native.SetForegroundWindow(hwnd);

            if (attached)
                Native.AttachThreadInput(currentThread, foregroundThread, false);
        }

        internal void HidePopup(bool animate = true)
        {
            if (!IsVisible)
                return;

            if (!animate || _isHiding)
            {
                HideImmediately();
                return;
            }

            _isHiding = true;

            var ease = new CubicEase {EasingMode = EasingMode.EaseIn};
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(110)) {EasingFunction = ease};
            fade.Completed += (sender, args) =>
            {
                HideImmediately();
            };

            ContentRoot.BeginAnimation(OpacityProperty, fade);
            ContentSlide.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(6, TimeSpan.FromMilliseconds(110)) {EasingFunction = ease});
            ContentScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(0.98, TimeSpan.FromMilliseconds(110)) {EasingFunction = ease});
            ContentScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(0.98, TimeSpan.FromMilliseconds(110)) {EasingFunction = ease});
        }

        private void HideImmediately()
        {
            Hide();
            _isHiding = false;
            _lastHiddenAt = DateTime.UtcNow;
            ResetAnimations();
        }

        private void ResetAnimations()
        {
            _isHiding = false;

            ContentRoot.BeginAnimation(OpacityProperty, null);
            ContentRoot.Opacity = 1;
            ContentSlide.BeginAnimation(TranslateTransform.YProperty, null);
            ContentSlide.Y = 0;
            ContentScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            ContentScale.ScaleX = 1;
            ContentScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            ContentScale.ScaleY = 1;
        }

        internal void AllowClose()
        {
            _allowClose = true;
            Close();
        }

        private void AnimateIn()
        {
            var ease = new CubicEase {EasingMode = EasingMode.EaseOut};
            var duration = TimeSpan.FromMilliseconds(180);

            ContentRoot.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
            ContentSlide.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(8, 0, duration) {EasingFunction = ease});
            ContentScale.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(0.97, 1, duration) {EasingFunction = ease});
            ContentScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(0.97, 1, duration) {EasingFunction = ease});
        }

        private void PositionWindow()
        {
            var cursor = System.Windows.Forms.Cursor.Position;
            var screen = System.Windows.Forms.Screen.FromPoint(cursor);
            var work = screen.WorkingArea;
            var bounds = screen.Bounds;
            var dpi = VisualTreeHelper.GetDpi(this);

            var width = (ActualWidth > 0 ? ActualWidth : Width) * dpi.DpiScaleX;
            var height = (ActualHeight > 0 ? ActualHeight : 320) * dpi.DpiScaleY;
            const double gap = 8;

            double x;
            double y;

            if (work.Width >= bounds.Width && work.Top > bounds.Top)
            {
                // Taskbar on top.
                x = cursor.X - width / 2;
                y = work.Top + gap;
            }
            else if (work.Width >= bounds.Width)
            {
                // Taskbar on the bottom (the common case).
                x = cursor.X - width / 2;
                y = work.Bottom - height - gap;
            }
            else if (work.Left > bounds.Left)
            {
                // Taskbar on the left.
                x = work.Left + gap;
                y = cursor.Y - height / 2;
            }
            else
            {
                // Taskbar on the right.
                x = work.Right - width - gap;
                y = cursor.Y - height / 2;
            }

            x = Math.Max(work.Left + gap, Math.Min(x, work.Right - width - gap));
            y = Math.Max(work.Top + gap, Math.Min(y, work.Bottom - height - gap));

            Left = x / dpi.DpiScaleX;
            Top = y / dpi.DpiScaleY;
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            ApplyWindowChrome();
        }

        private void ApplyWindowChrome()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            var darkMode = Theme.IsLight ? 0 : 1;
            if (Native.DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref darkMode, sizeof(int)) != 0)
                Native.DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkModeBefore20H1, ref darkMode, sizeof(int));

            var corner = DwmCornerRound;
            Native.DwmSetWindowAttribute(hwnd, DwmWindowCornerPreference, ref corner, sizeof(int));

            var backdrop = DwmSystemBackdropMainWindow;
            var result = Native.DwmSetWindowAttribute(hwnd, DwmSystemBackdropType, ref backdrop, sizeof(int));

            if (result != 0)
            {
                // Windows 11 21H2 exposes Mica through an earlier, undocumented attribute.
                var mica = 1;
                result = Native.DwmSetWindowAttribute(hwnd, DwmMicaEffect, ref mica, sizeof(int));
            }

            _micaEnabled = result == 0;

            if (_micaEnabled)
            {
                var margins = new Native.Margins {Left = -1, Right = -1, Top = -1, Bottom = -1};
                Native.DwmExtendFrameIntoClientArea(hwnd, ref margins);
                RootBorder.Background = Brushes.Transparent;
                RootBorder.CornerRadius = new CornerRadius(8);
            }
            else
            {
                RootBorder.Background = (Brush) FindResource("WindowFallbackBrush");
                RootBorder.CornerRadius = new CornerRadius(12);
            }
        }

        private void SyncTheme()
        {
            UpdateWindowIcon();

            if (new WindowInteropHelper(this).Handle != IntPtr.Zero)
                ApplyWindowChrome();
        }

        private void UpdateWindowIcon()
        {
            var theme = Theme.IsTaskbarLight ? "black" : "white";
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri($"pack://application:,,,/Resources/tray-{theme}.ico");
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            Icon = image;
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            _wasActivated = true;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (_preparing || _menuOpen)
                return;

            // A tray click can show the popup while Explorer keeps the foreground; ignore the
            // spurious deactivation that can arrive before the popup has actually been active.
            if (!_wasActivated ||
                DateTime.UtcNow - _shownAt < TimeSpan.FromMilliseconds(350))
                return;

            HidePopup();
        }

        private void Window_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            if (!IsVisible)
                return;

            Dispatcher.BeginInvoke(new Action(PositionWindow), DispatcherPriority.Loaded);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                HidePopup();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_allowClose)
                return;

            e.Cancel = true;
            HidePopup(false);
        }

        private class FeatureCheck
        {
            private readonly Action _check;
            private readonly Action _disable;

            internal FeatureCheck(Action check, Action disable)
            {
                _check = check;
                _disable = disable;
            }

            internal void Check() => _check();
            internal void Disable() => _disable();
        }

        private void Refresh()
        {
            var features = new[]
            {
                new FeatureCheck(
                    () =>
                    {
                        var state = _powerModeFeature.GetState();
                        _powerModeButtons[(int) state].IsChecked = true;
                        SetPowerModeState(state);
                    },
                    () => DisableControls(_powerModeButtons)),
                new FeatureCheck(
                    () => _batteryButtons[(int) _batteryFeature.GetState()].IsChecked = true,
                    () => DisableControls(_batteryButtons)),
                new FeatureCheck(
                    () => _alwaysOnUsbButtons[(int) _alwaysOnUsbFeature.GetState()].IsChecked = true,
                    () => DisableControls(_alwaysOnUsbButtons)),
                new FeatureCheck(
                    () => chkOverDrive.IsChecked = _overDriveFeature.GetState() == OverDriveState.On,
                    () => chkOverDrive.IsEnabled = false),
                new FeatureCheck(
                    () => chkTouchpadLock.IsChecked = _touchpadLockFeature.GetState() == TouchpadLockState.On,
                    () => chkTouchpadLock.IsEnabled = false),
                new FeatureCheck(
                    () => chkFnLock.IsChecked = _fnLockFeature.GetState() == FnLockState.On,
                    () => chkFnLock.IsEnabled = false)
            };

            foreach (var feature in features)
            {
                try
                {
                    feature.Check();
                }
                catch (Exception e)
                {
                    Trace.TraceInformation("Could not refresh feature: " + e);
                    feature.Disable();
                }
            }
        }

        private void DisableControls(Control[] buttons)
        {
            foreach (var btn in buttons)
                btn.IsEnabled = false;
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshRotation.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(450))
                {
                    EasingFunction = new CubicEase {EasingMode = EasingMode.EaseOut},
                    FillBehavior = FillBehavior.Stop
                });

            Refresh();
        }

        private void btnMenu_Click(object sender, RoutedEventArgs e)
        {
            const uint mfChecked = 0x00000008;
            const uint tpmRightAlign = 0x0008;
            const uint tpmNonotify = 0x0080;
            const uint tpmReturnCmd = 0x0100;

            var autostartEnabled = AutoStart.IsEnabled();
            var menu = Native.CreatePopupMenu();
            if (menu == IntPtr.Zero)
                return;

            _menuOpen = true;

            try
            {
                Native.AppendMenuW(menu, autostartEnabled ? mfChecked : 0, 1, Localization.Autostart);

                var origin = btnMenu.PointToScreen(
                    new Point(btnMenu.ActualWidth, btnMenu.ActualHeight + 4));

                var command = Native.TrackPopupMenuEx(
                    menu,
                    tpmReturnCmd | tpmNonotify | tpmRightAlign,
                    (int) origin.X,
                    (int) origin.Y,
                    new WindowInteropHelper(this).Handle,
                    IntPtr.Zero);

                if (command == 1 && !AutoStart.SetEnabled(!autostartEnabled))
                {
                    MessageBox.Show(this, Localization.AutostartError, "Lenovo Controller",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally
            {
                _menuOpen = false;
                Native.DestroyMenu(menu);
            }

            // Clicking outside the menu dismisses it; mirrors that by closing the popup too.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsActive)
                    HidePopup(false);
            }));
        }

        private void radioPowerMode_Checked(object sender, RoutedEventArgs e)
        {
            var state = (PowerModeState) Array.IndexOf(_powerModeButtons, sender);
            _powerModeFeature.SetState(state);
            SetPowerModeState(state);
        }

        private void radioBattery_Checked(object sender, RoutedEventArgs e)
        {
            _batteryFeature.SetState((BatteryState) Array.IndexOf(_batteryButtons, sender));
        }

        private void radioAlwaysOnUsb_Checked(object sender, RoutedEventArgs e)
        {
            _alwaysOnUsbFeature.SetState((AlwaysOnUsbState) Array.IndexOf(_alwaysOnUsbButtons, sender));
        }

        private void chkOverDrive_Checked(object sender, RoutedEventArgs e)
        {
            var state = chkOverDrive.IsChecked.GetValueOrDefault(false)
                ? OverDriveState.On
                : OverDriveState.Off;
            _overDriveFeature.SetState(state);
        }

        private void chkTouchpadLock_Checked(object sender, RoutedEventArgs e)
        {
            var state = chkTouchpadLock.IsChecked.GetValueOrDefault(false)
                ? TouchpadLockState.On
                : TouchpadLockState.Off;
            _touchpadLockFeature.SetState(state);
        }

        private void chkFnLock_Checked(object sender, RoutedEventArgs e)
        {
            var state = chkFnLock.IsChecked.GetValueOrDefault(false)
                ? FnLockState.On
                : FnLockState.Off;
            _fnLockFeature.SetState(state);
        }
    }
}
