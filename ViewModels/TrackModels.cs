using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MediaTrackMixerPage.ViewModels;

public class Track : INotifyPropertyChanged
{
    public bool OnSecondPage { get; set; }
    public string? FileName { get; set; }
    public string FullPath { get; set; }
    public TrackType Type { get; set; }
    public Colour Colour { get; set; }
    public int Index { get; set; }
    public string? CodecOrMimeType { get; set; }

    private string? _Title;
    public string? Title
    {
        get => _Title;
        set
        {
            _Title = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasTitle));
            OnPropertyChanged(nameof(HasNoTitle));
        }
    }
    private bool _ineditmode;
    public bool InEditMode
    {
        get => _ineditmode;
        set
        {
            _ineditmode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NotInEditMode));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string IndexString => Type is TrackType.Chapters or TrackType.GlobalMetadata ? string.Empty : (Index + 1).ToString();
    public string? FileNameString => OnSecondPage ? FileName : null;
    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);
    public bool HasNoTitle => !HasTitle;
    public bool NotInEditMode => !InEditMode;
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
    public bool NotOnSecondPage => !OnSecondPage;
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