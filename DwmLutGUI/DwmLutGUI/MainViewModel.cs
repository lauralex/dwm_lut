using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Xml;
using System.Xml.Linq;

namespace DwmLutGUI
{
    internal class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _activeText;
        private MonitorData _selectedMonitor;
        private bool _isActive;

        private readonly string _configPath;

        private bool _configChanged;
        private XElement _lastConfig;
        private XElement _activeConfig;

        public MainViewModel()
        {
            UpdateActiveStatus();
            var dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();

            _configPath = AppDomain.CurrentDomain.BaseDirectory + "config.xml";

            _allMonitors = new List<MonitorData>();
            Monitors = new ObservableCollection<MonitorData>();
            UpdateMonitors();

            CanApply = !Injector.NoDebug;
            MonitorData.StaticPropertyChanged += MonitorDataOnStaticPropertyChanged;
        }

        private void MonitorDataOnStaticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(SdrLutPath));
            OnPropertyChanged(nameof(HdrLutPath));
            SaveConfig();
        }

        public string ActiveText
        {
            private set
            {
                if (value == _activeText) return;
                _activeText = value;
                OnPropertyChanged();
            }
            get => _activeText;
        }

        public MonitorData SelectedMonitor
        {
            set
            {
                if (value == _selectedMonitor) return;
                _selectedMonitor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SdrLutPath));
                OnPropertyChanged(nameof(HdrLutPath));
            }
            get => _selectedMonitor;
        }

        private void UpdateConfigChanged()
        {
            _configChanged = _lastConfig != _activeConfig && !XNode.DeepEquals(_lastConfig, _activeConfig);
        }

        private void SaveConfig()
        {
            var xElem = new XElement("monitors",
                _allMonitors.Select(x =>
                    new XElement("monitor", new XAttribute("path", x.DevicePath),
                        x.SdrLutPath != null ? new XAttribute("sdr_lut", x.SdrLutPath) : null,
                        x.HdrLutPath != null ? new XAttribute("hdr_lut", x.HdrLutPath) : null,
                        x.SdrLuts != null ? new XElement("sdr_luts", x.SdrLuts.Select(s => new XElement("sdr_lut", s))) : null)));

            xElem.Save(_configPath);

            _lastConfig = xElem;
            UpdateConfigChanged();
            UpdateActiveStatus();
        }

        public string SdrLutPath
        {
            set
            {
                if (SelectedMonitor == null || SelectedMonitor.SdrLutPath == value) return;
                SelectedMonitor.SdrLutPath = value;
                OnPropertyChanged();

                SaveConfig();
            }
            get => SelectedMonitor?.SdrLutPath;
        }

        public string HdrLutPath
        {
            set
            {
                if (SelectedMonitor == null || SelectedMonitor.HdrLutPath == value) return;
                SelectedMonitor.HdrLutPath = value;
                OnPropertyChanged();

                SaveConfig();
            }
            get => SelectedMonitor?.HdrLutPath;
        }

        public bool IsActive
        {
            set
            {
                if (value == _isActive) return;
                _isActive = value;
                OnPropertyChanged();
            }
            get => _isActive;
        }

        public bool CanApply { get; }

        private List<MonitorData> _allMonitors { get; }
        public ObservableCollection<MonitorData> Monitors { get; }

        public void UpdateMonitors()
        {
            var selectedPath = SelectedMonitor?.DevicePath;
            _allMonitors.Clear();
            Monitors.Clear();
            List<XElement> config = null;
            if (File.Exists(_configPath))
            {
                config = XElement.Load(_configPath).Descendants("monitor").ToList();
            }

            var paths = WindowsDisplayAPI.DisplayConfig.PathInfo.GetActivePaths();
            foreach (var path in paths)
            {
                if (path.IsCloneMember) continue;
                var targetInfo = path.TargetsInfo[0];
                var deviceId = targetInfo.DisplayTarget.TargetId;
                var devicePath = targetInfo.DisplayTarget.DevicePath;
                var name = targetInfo.DisplayTarget.FriendlyName;
                if (string.IsNullOrEmpty(name))
                {
                    name = "???";
                }

                var connector = targetInfo.OutputTechnology.ToString();
                if (connector == "DisplayPortExternal")
                {
                    connector = "DisplayPort";
                }

                var position = path.Position.X + "," + path.Position.Y;

                string sdrLutPath = null;
                string hdrLutPath = null;

                var settings = config?.FirstOrDefault(x => (uint?)x.Attribute("id") == deviceId) ??
                               config?.FirstOrDefault(x => (string)x.Attribute("path") == devicePath);

                if (settings != null)
                {
                    sdrLutPath = (string)settings.Attribute("sdr_lut");
                    hdrLutPath = (string)settings.Attribute("hdr_lut");
                }
                var sdrLutPaths = settings?.Element("sdr_luts")?.Elements("sdr_lut").Select(x => (string)x).ToList();
                var hdrLutPaths = settings?.Element("hdr_luts")?.Elements("hdr_lut").Select(x => (string)x).ToList();
                var monitor = new MonitorData(devicePath, path.DisplaySource.SourceId + 1, name, connector, position,
                    sdrLutPath, hdrLutPath);
                if (sdrLutPaths != null) monitor.SdrLuts = new ObservableCollection<string>(sdrLutPaths);
                if (hdrLutPaths != null) monitor.HdrLuts = new ObservableCollection<string>(hdrLutPaths);
                _allMonitors.Add(monitor);
                Monitors.Add(monitor);
            }

            if (config != null)
            {
                foreach (var monitor in config)
                {
                    var path = (string)monitor.Attribute("path");
                    if (path == null || Monitors.Any(x => x.DevicePath == path)) continue;

                    var sdrLutPath = (string)monitor.Attribute("sdr_lut");
                    var hdrLutPath = (string)monitor.Attribute("hdr_lut");

                    var sdrLutPaths = monitor.Element("sdr_luts")?.Elements("sdr_lut").Select(x => (string)x).ToList();
                    var hdrLutPaths = monitor.Element("hdr_luts")?.Elements("hdr_lut").Select(x => (string)x).ToList();
                    var newMonitorData = new MonitorData(path, sdrLutPath, hdrLutPath)
                    {
                        SdrLuts = new ObservableCollection<string>(sdrLutPaths),
                        HdrLuts = new ObservableCollection<string>(hdrLutPaths)
                    };
                    _allMonitors.Add(newMonitorData);
                }
            }

            if (selectedPath == null) return;

            var previous = Monitors.FirstOrDefault(monitor => monitor.DevicePath == selectedPath);
            if (previous != null)
            {
                SelectedMonitor = previous;
            }
        }

        public void ReInject()
        {
            Injector.Uninject();
            if (!Monitors.All(monitor =>
                    string.IsNullOrEmpty(monitor.SdrLutPath) && string.IsNullOrEmpty(monitor.HdrLutPath)))
            {
                Injector.Inject(Monitors);
            }

            _activeConfig = _lastConfig;
            UpdateConfigChanged();

            UpdateActiveStatus();
        }

        public void Uninject()
        {
            Injector.Uninject();
            UpdateActiveStatus();
        }

        private void UpdateActiveStatus()
        {
            var status = Injector.GetStatus();
            if (status != null)
            {
                IsActive = (bool)status;
                if (status == true)
                {
                    ActiveText = "Active" + (_configChanged ? " (changed)" : "");
                }
                else
                {
                    ActiveText = "Inactive";
                }
            }
            else
            {
                IsActive = false;
                ActiveText = "???";
            }
        }

        public void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            UpdateMonitors();
            if (!_configChanged)
            {
                ReInject();
            }
        }

        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            UpdateActiveStatus();
        }

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}