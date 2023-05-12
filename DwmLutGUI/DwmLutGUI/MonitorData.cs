using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;

namespace DwmLutGUI
{
    public class MonitorData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public static event PropertyChangedEventHandler StaticPropertyChanged;

        private string _sdrLutPath;
        private string _hdrLutPath;

        public MonitorData(string devicePath, uint sourceId, string name, string connector, string position,
            string sdrLutPath, string hdrLutPath)
        {
            SdrLuts = new ObservableCollection<string>();
            HdrLuts = new ObservableCollection<string>();
            DevicePath = devicePath;
            SourceId = sourceId;
            Name = name;
            Connector = connector;
            Position = position;
            SdrLutPath = sdrLutPath;
            HdrLutPath = hdrLutPath;
        }

        public MonitorData(string devicePath, string sdrLutPath, string hdrLutPath)
        {
            SdrLuts = new ObservableCollection<string>();
            HdrLuts = new ObservableCollection<string>();
            DevicePath = devicePath;
            SdrLutPath = sdrLutPath;
            HdrLutPath = hdrLutPath;
        }

        public string DevicePath { get; }
        public uint SourceId { get; }
        public string Name { get; }
        public string Connector { get; }
        public string Position { get; }

        public ObservableCollection<string> SdrLuts { get; set; }
        public ObservableCollection<string> HdrLuts { get; set; }


        public string SdrLutPath
        {
            set
            {
                if (value == _sdrLutPath) return;
                if (value == null) return;
                if (value != "None" && !SdrLuts.Contains(value))
                    SdrLuts.Add(value);
                _sdrLutPath = value != "None" ? value : null;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SdrLutFilename)));
                StaticPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SdrLutFilename)));
            }
            get => _sdrLutPath;
        }

        public string HdrLutPath
        {
            set
            {
                if (value == _hdrLutPath) return;
                if (value != "None" && !HdrLuts.Contains(value))
                {
                    HdrLuts.Add(value);
                }
                _hdrLutPath = value != "None" ? value : null;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HdrLutFilename)));
                StaticPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HdrLutFilename)));
            }
            get => _hdrLutPath;
        }

        public string SdrLutFilename => Path.GetFileName(SdrLutPath) ?? "None";

        public string HdrLutFilename => Path.GetFileName(HdrLutPath) ?? "None";
    }
}