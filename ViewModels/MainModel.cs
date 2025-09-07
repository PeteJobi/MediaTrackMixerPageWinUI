using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace MediaTrackMixerPage.ViewModels
{
    public class MainModel: INotifyPropertyChanged
    {
        private ObservableCollection<TrackGroup> _trackGroups;
        public ObservableCollection<TrackGroup> TrackGroups
        {
            get => _trackGroups;
            set
            {
                _trackGroups = value;
                _trackGroups.CollectionChanged += _trackGroups_CollectionChanged;
                OnPropertyChanged();
                _trackGroups_CollectionChanged(null, null);
            }
        }
        private bool _hasselectedtracks;
        public bool HasSelectedTracks
        {
            get => _hasselectedtracks;
            set
            {
                _hasselectedtracks = value;
                OnPropertyChanged();
            }
        }

        private void _trackGroups_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasTracks));
            OnPropertyChanged(nameof(HasNoTracks));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool HasTracks => _trackGroups.Any();
        public bool HasNoTracks => !HasTracks;
    }
}
