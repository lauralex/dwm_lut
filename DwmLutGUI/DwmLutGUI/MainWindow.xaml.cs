using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using Microsoft.Win32;
using ContextMenu = System.Windows.Forms.ContextMenu;
using MenuItem = System.Windows.Forms.MenuItem;
using MessageBox = System.Windows.Forms.MessageBox;

namespace DwmLutGUI
{
    public partial class MainWindow
    {
        private readonly MainViewModel _viewModel;
        private bool _applyOnCooldown;

        private readonly MenuItem _statusItem;
        private readonly MenuItem _applyItem;
        private readonly MenuItem _disableItem;
        private readonly MenuItem _disableAndExitItem;

        public MainWindow()
        {
            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                MessageBox.Show("Already running!");
                Close();
                return;
            }

            InitializeComponent();
            _viewModel = (MainViewModel)DataContext;
            _applyOnCooldown = false;

            var args = Environment.GetCommandLineArgs().ToList();
            args.RemoveAt(0);

            if (args.Contains("-apply"))
            {
                Apply_Click(null, null);
            }
            else if (args.Contains("-disable"))
            {
                Disable_Click(null, null);
            }

            if (args.Contains("-minimize"))
            {
                WindowState = WindowState.Minimized;
                Hide();
            }
            else if (args.Contains("-exit"))
            {
                Close();
                return;
            }

            var notifyIcon = new NotifyIcon();
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DwmLutGUI.smile.ico");
            notifyIcon.Icon = new Icon(stream);
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick +=
                delegate
                {
                    Show();
                    WindowState = WindowState.Normal;
                };

            var contextMenu = new ContextMenu();

            _statusItem = new MenuItem();
            contextMenu.MenuItems.Add(_statusItem);
            _statusItem.Enabled = false;

            contextMenu.MenuItems.Add("-");

            _applyItem = new MenuItem();
            contextMenu.MenuItems.Add(_applyItem);
            _applyItem.Text = "Apply";
            _applyItem.Click += delegate { Apply_Click(null, null); };

            _disableItem = new MenuItem();
            contextMenu.MenuItems.Add(_disableItem);
            _disableItem.Text = "Disable";
            _disableItem.Click += delegate { Disable_Click(null, null); };

            contextMenu.MenuItems.Add("-");

            _disableAndExitItem = new MenuItem();
            contextMenu.MenuItems.Add(_disableAndExitItem);
            _disableAndExitItem.Text = "Disable and exit";
            _disableAndExitItem.Click += delegate
            {
                Disable_Click(null, null);
                Close();
            };

            var exitItem = new MenuItem();
            contextMenu.MenuItems.Add(exitItem);
            exitItem.Text = "Exit";
            exitItem.Click += delegate { Close(); };

            contextMenu.Popup += delegate { UpdateContextMenu(); };

            notifyIcon.ContextMenu = contextMenu;

            notifyIcon.Text = Assembly.GetEntryAssembly().GetName().Name;

            Closed += delegate { notifyIcon.Dispose(); };

            SystemEvents.DisplaySettingsChanged += _viewModel.OnDisplaySettingsChanged;
            App.KListener.KeyDown += MonitorLutToggle;
            var keys = Enum.GetValues(typeof(Key)).Cast<Key>().ToList();
            ToggleKeyCombo.ItemsSource = keys;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }

            base.OnStateChanged(e);
        }

        private void UpdateContextMenu()
        {
            _statusItem.Text = "Status: " + _viewModel.ActiveText;

            var canDisable = _viewModel.IsActive && !Injector.NoDebug;

            _applyItem.Enabled = _viewModel.CanApply;
            _disableItem.Enabled = canDisable;
            _disableAndExitItem.Enabled = canDisable;
        }

        private static string BrowseLuts(string folder)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "LUT Files|*.cube;*.txt",
                InitialDirectory = folder
            };

            var result = dlg.ShowDialog();

            return result == true ? dlg.FileName : null;
        }

        private void AboutButton_Click(object sender, RoutedEventArgs o)
        {
            var window = new AboutWindow
            {
                Owner = this
            };
            window.ShowDialog();
        }

        private void SdrLutBrowse_Click(object sender, RoutedEventArgs e)
        {
            var folder = Path.GetDirectoryName(_viewModel.SdrLutPath);
            var lutPath = BrowseLuts(folder);
            if (!string.IsNullOrEmpty(lutPath))
            {
                _viewModel.SdrLutPath = lutPath;
            }
        }

        private void SdrLutClear_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SdrLutPath = "None";
        }

        private void HdrLutBrowse_Click(object sender, RoutedEventArgs e)
        {
            var folder = Path.GetDirectoryName(_viewModel.HdrLutPath);
            var lutPath = BrowseLuts(folder);
            if (!string.IsNullOrEmpty(lutPath))
            {
                _viewModel.HdrLutPath = lutPath;
            }
        }

        private void HdrLutClear_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.HdrLutPath = "None";
        }

        private void Disable_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel.Uninject();
                RedrawScreens();
            }
            catch (Exception x)
            {
                MessageBox.Show(x.Message);
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (_applyOnCooldown) return;
            _applyOnCooldown = true;

            try
            {
                _viewModel.ReInject();
                RedrawScreens();
            }
            catch (Exception x)
            {
                MessageBox.Show(x.Message);
            }

            Task.Run(() =>
            {
                Thread.Sleep(100);
                _applyOnCooldown = false;
            });
        }

        private static void RedrawScreens()
        {
            var rect = Screen.AllScreens.Select(x => x.Bounds).Aggregate(Rectangle.Union);
            var overlay = new OverlayWindow
            {
                Left = rect.Left,
                Top = rect.Top,
                Height = rect.Height,
                Width = rect.Width,
            };

            overlay.Show();
            Thread.Sleep(50);
            overlay.Close();
        }

        private void RemoveSdrLut_Click(object sender, RoutedEventArgs e)
        {
            var monitor = _viewModel.SelectedMonitor;
            if (monitor == null) return;
            monitor.SdrLuts.Remove(monitor.SdrLutPath);
            var anySdrLut = monitor.SdrLuts.FirstOrDefault();
            monitor.SdrLutPath = anySdrLut ?? "None";
        }

        private void MonitorLutToggle(object sender, RawKeyEventArgs e)
        {
            if (e.Key != (Key)ToggleKeyCombo.SelectedItem) return;
            var monitor = _viewModel.SelectedMonitor;
            if (monitor == null) return;
            if (monitor.SdrLutFilename != "None")
            {
                _viewModel.SdrLutPath =
                    monitor.SdrLuts[(monitor.SdrLuts.IndexOf(monitor.SdrLutPath) + 1) % monitor.SdrLuts.Count];
            }
            else
            {
                _viewModel.HdrLutPath = monitor.HdrLuts[(monitor.HdrLuts.IndexOf(monitor.HdrLutPath) + 1) % monitor.HdrLuts.Count];
            }

            if (_viewModel.IsActive)
            {
                Disable_Click(null, null);
                Apply_Click(null, null);
            }
        }

        private void RemoveHdrLut_Click(object sender, RoutedEventArgs e)
        {
            var monitor = _viewModel.SelectedMonitor;
            if (monitor == null) return;
            monitor.HdrLuts.Remove(monitor.HdrLutPath);
            monitor.HdrLutPath = monitor.HdrLuts.FirstOrDefault() ?? "None";
        }
    }
}