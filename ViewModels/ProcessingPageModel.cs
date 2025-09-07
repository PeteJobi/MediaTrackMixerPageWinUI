using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace MediaTrackMixerPage.ViewModels
{
    public class ProcessingPageModel: INotifyPropertyChanged
    {
        private ObservableCollection<Track> _tracks;
        public ObservableCollection<Track> Tracks
        {
            get => _tracks;
            set
            {
                _tracks = value;
                OnPropertyChanged();
            }
        }
        private bool _showinfobar;
        public bool ShowInfoBar
        {
            get => _showinfobar;
            set
            {
                _showinfobar = value;
                OnPropertyChanged();
            }
        }
        private string _infobarmessage;
        public string InfoBarMessage
        {
            get => _infobarmessage;
            set
            {
                _infobarmessage = value;
                OnPropertyChanged();
            }
        }
        private InfoBarSeverity _infobarseverity;
        public InfoBarSeverity InfoBarSeverity
        {
            get => _infobarseverity;
            set
            {
                _infobarseverity = value;
                OnPropertyChanged();
            }
        }
        private OperationVisibility _operationvisibility;
        public OperationVisibility OperationVisibility
        {
            get => _operationvisibility;
            set
            {
                _operationvisibility = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowBackMixStack));
                OnPropertyChanged(nameof(ShowMixBut));
                OnPropertyChanged(nameof(ShowProgressPanel));
            }
        }
        private double _operationprogress;
        public double OperationProgress
        {
            get => _operationprogress;
            set
            {
                _operationprogress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OperationProgressString));
            }
        }

        public bool ShowBackMixStack => OperationVisibility != OperationVisibility.ShowProgress;
        public bool ShowMixBut => OperationVisibility != OperationVisibility.ShowOnlyBack;
        public bool ShowProgressPanel => OperationVisibility == OperationVisibility.ShowProgress;
        public string OperationProgressString => $"{Math.Round(OperationProgress, 2)}%";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum OperationVisibility { ShowBackAndMix, ShowProgress, ShowOnlyBack }
}
