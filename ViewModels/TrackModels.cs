using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WinUIShared.Helpers;

namespace MediaTrackMixerPage.ViewModels;

public class Track : INotifyPropertyChanged
{
    public string? FileName { get; set; }
    public string FullPath { get; set; }
    public TrackType Type { get; set; }
    public Colour Colour { get; set; }
    public int Index { get; set; }
    public string? CodecOrMimeType { get; set; }
    public BindingProxy BindingProxy { get; set; }
    public object Data { get; set; }
    public PassedTitle PassedTitle { get; set; }
    public PassedDefault PassedDefault { get; set; }
    private bool _ineditmode;
    public bool InEditMode
    {
        get => _ineditmode;
        set
        {
            _ineditmode = value;
            OnPropertyChanged();
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string IndexString => Type is TrackType.Chapters or TrackType.GlobalMetadata ? string.Empty : (Index + 1).ToString();
    public string? FileNameString => BindingProxy.OnSecondPage ? FileName : null;
    public string Icon => Type switch
    {
        TrackType.Video => "\uE714",
        TrackType.Audio => "\uE8D6",
        TrackType.Subtitle => "\uED1E",
        TrackType.Chapters => "\uE8F1",
        TrackType.Attachment => MimeIsFont(CodecOrMimeType) ? "\uE8D2" : "\uE723",
        TrackType.GlobalMetadata => "\uE946",
        _ => "\uE9CE"
    };
    public string ToolTip => Type switch
    {
        TrackType.Video => "Video",
        TrackType.Audio => "Audio",
        TrackType.Subtitle => "Subtitle",
        TrackType.Chapters => "Chapters",
        TrackType.Attachment => MimeIsFont(CodecOrMimeType) ? "Font" : "Attachment",
        TrackType.GlobalMetadata => "Global Metadata",
        _ => "Unknown"
    };
    public bool IsNotChapter => Type != TrackType.Chapters;

    public static TrackType ProcessorToModelTrackType(MediaTrackMixer.TrackType processorTrackType) => processorTrackType switch
    {
        MediaTrackMixer.TrackType.Video => TrackType.Video,
        MediaTrackMixer.TrackType.Audio => TrackType.Audio,
        MediaTrackMixer.TrackType.Subtitle => TrackType.Subtitle,
        _ => TrackType.Other
    };

    public static MediaTrackMixer.GeneralType ModelToProcessorGeneralType(TrackType modelTrackType) => modelTrackType switch
    {
        TrackType.Video or TrackType.Audio or TrackType.Subtitle => MediaTrackMixer.GeneralType.Track,
        TrackType.Chapters => MediaTrackMixer.GeneralType.Chapters,
        TrackType.Attachment => MediaTrackMixer.GeneralType.Attachment,
        TrackType.GlobalMetadata => MediaTrackMixer.GeneralType.GlobalMetadata,
        _ => MediaTrackMixer.GeneralType.None
    };

    private bool MimeIsFont(string? mimeType) => mimeType?.Contains("font") == true;
}

public class PassedTitle(TrackType type) : INotifyPropertyChanged
{
    private string? _title;
    public string? Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
    public TrackType Type { get; set; } = type;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, params string[] alsoNotify)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        foreach (var dep in alsoNotify) OnPropertyChanged(dep);
        return true;
    }
}

public class PassedDefault : INotifyPropertyChanged
{
    private bool _isdefault;
    public bool IsDefault
    {
        get => _isdefault;
        set => SetProperty(ref _isdefault, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, params string[] alsoNotify)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        foreach (var dep in alsoNotify) OnPropertyChanged(dep);
        return true;
    }
}

public class Colour
{
    public string Background { get; set; }
    public bool HasBlackForeground { get; set; }
    public string Foreground => HasBlackForeground ? "Black" : "White";
}

public class TrackGroup : List<Track>, INotifyPropertyChanged
{
    public TrackGroup(IEnumerable<Track> items) : base(items)
    {
    }
    private bool? _checked;
    public bool? Checked
    {
        get => _checked;
        set
        {
            _checked = value;
            OnPropertyChanged();
        }
    }
    public string FileName { get; set; }
    public string FullPath { get; set; }
    public Colour Colour { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class MetadataItem(string key, string value) : INotifyPropertyChanged
{
    private string _key = key;
    public string Key
    {
        get => _key;
        set => SetProperty(ref _key, value);
    }
    private string _value = value;
    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
    public bool CantEdit { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, params string[] alsoNotify)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        foreach (var dep in alsoNotify) OnPropertyChanged(dep);
        return true;
    }
}

public class DispositionItem: INotifyPropertyChanged
{
    private string _key;
    public string Key
    {
        get => _key;
        set => SetProperty(ref _key, value);
    }
    private bool _checked;
    public bool Checked
    {
        get => _checked;
        set => SetProperty(ref _checked, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, params string[] alsoNotify)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        foreach (var dep in alsoNotify) OnPropertyChanged(dep);
        return true;
    }
}

public class TrackEdit
{
    public MetadataEdit Metadata { get; set; }
    public ObservableCollection<DispositionItem> Dispositions { get; set; }
    public SyncEdit Sync { get; set; }
    public PassedDefault PassedDefault { get; set; }

    public TrackEdit(MetadataEdit metadata, ObservableCollection<DispositionItem> dispositions, PassedDefault passedDefault, SyncEdit sync)
    {
        Metadata = metadata;
        Dispositions = dispositions;
        PassedDefault = passedDefault;
        Sync = sync;

        SetIsDefault();
        foreach (var disposition in Dispositions)
        {
            disposition.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DispositionItem.Checked))
                {
                    SetIsDefault();
                }
            };
        }
    }

    public string DispositionString => string.Join(" • ", Dispositions.Where(d => d.Checked).Select(d => d.Key));
    public double DispositionLength => Dispositions.Count(d => d.Checked);

    private void SetIsDefault()
    {
        PassedDefault.IsDefault = Dispositions.FirstOrDefault(d => d.Key == "default")?.Checked == true;
    }
}

public class ChapterEdit(MetadataEdit metadata, PassedTitle passedTitle) : INotifyPropertyChanged
{
    public MetadataEdit Metadata { get; set; } = metadata;
    public PassedTitle PassedTitle { get; set; } = passedTitle;
    private TimeSpan _start;
    public TimeSpan Start
    {
        get => _start;
        set => SetProperty(ref _start, value);
    }
    private TimeSpan _end;
    public TimeSpan End
    {
        get => _end;
        set => SetProperty(ref _end, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, params string[] alsoNotify)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        foreach (var dep in alsoNotify) OnPropertyChanged(dep);
        return true;
    }
}

public class SyncEdit(TrackType type) : INotifyPropertyChanged
{
    public const string NoSync = "Keep sync";
    public const string Delay = "Delay";
    public const string Hasten = "Hasten";
    public ObservableCollection<string> Options = new([NoSync, Delay, Hasten]);
    private string _selectedoption = NoSync;
    public string SelectedOption
    {
        get => _selectedoption;
        set => SetProperty(ref _selectedoption, value, alsoNotify: nameof(EnableChangeTextBox));
    }
    private TimeSpan _change;
    public TimeSpan Change
    {
        get => _change;
        set => SetProperty(ref _change, value);
    }
    public TrackType Type { get; set; } = type;
    public bool EnableChangeTextBox => SelectedOption != NoSync;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, params string[] alsoNotify)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        foreach (var dep in alsoNotify) OnPropertyChanged(dep);
        return true;
    }
}

public class MetadataEdit : ObservableCollection<MetadataItem>
{
    public PassedTitle PassedTitle { get; set; }
    public ICommand DeleteItemCommand { get; }
    
    public MetadataEdit(IEnumerable<MetadataItem> list, PassedTitle passedTitle) : base(list)
    {
        PassedTitle = passedTitle;
        DeleteItemCommand = new RelayCommand<MetadataItem>(DeleteItem);

        passedTitle.PropertyChanged += (sender, args) =>
        {
            var pt = sender as PassedTitle;
            if(pt == null) return;
            if (args.PropertyName == nameof(ViewModels.PassedTitle.Title))
            {
                var curr = this.FirstOrDefault(m => m.Key == TitleKey);
                if (curr == null)
                {
                    if (pt.Title != null) Add(new MetadataItem(TitleKey, pt.Title!));
                }
                else if (curr.Value != pt.Title) curr.Value = pt.Title!;
            }
        };
        foreach (var metadataItem in Items)
        {
            ItemChanged(metadataItem);
            metadataItem.PropertyChanged += (s, e) => ItemChanged((MetadataItem)s);
        }
        CollectionChanged += (sender, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Remove)
            {
                if (Items.All(m => m.Key != TitleKey)) PassedTitle.Title = null;
                return;
            }
            if (args.NewItems == null) return;
            foreach (MetadataItem newItem in args.NewItems)
            {
                newItem.PropertyChanged += (s, e) => ItemChanged((MetadataItem)s);
            }
        };
    }

    private void ItemChanged(MetadataItem item)
    {
        switch (PassedTitle.Type, item.Key)
        {
            case (TrackType.Attachment, "filename"):
            case (_, "title"):
                if(PassedTitle.Title != item.Value) PassedTitle.Title = item.Value;
                return;
        }
        if (Items.All(m => m.Key != TitleKey)) PassedTitle.Title = null;
    }

    private void DeleteItem(MetadataItem? item)
    {
        if (item != null) Remove(item);
    }

    private string TitleKey => PassedTitle.Type == TrackType.Attachment ? "filename" : "title";
}

public class ChaptersEdit : ObservableCollection<ChapterEdit>
{
    public PassedTitle PassedTitle { get; set; }
    public ICommand DeleteItemCommand { get; }

    public ChaptersEdit(IEnumerable<ChapterEdit> list, PassedTitle passedTitle) : base(list)
    {
        PassedTitle = passedTitle;
        DeleteItemCommand = new RelayCommand<ChapterEdit>(DeleteItem);

        CollectionChanged += (sender, args) =>
        {
            PassedTitle.Title = $"{Count} chapters";
        };
        OnCollectionChanged(null);
    }

    private void DeleteItem(ChapterEdit? item)
    {
        if (item != null) Remove(item);
    }
}

public class BindingProxy : DependencyObject
{
    public bool OnSecondPage
    {
        get => (bool)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(OnSecondPage), typeof(object), typeof(BindingProxy), new PropertyMetadata(false));
}

public enum TrackType
{
    Other,
    Video,
    Audio,
    Subtitle,
    Chapters,
    Attachment,
    GlobalMetadata
}