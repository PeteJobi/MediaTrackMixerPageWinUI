using MediaTrackMixer;
using MediaTrackMixerPage.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media;
using Windows.Media.Core;
using Windows.Storage.Pickers;

namespace MediaTrackMixerPage;

public sealed partial class MediaTrackMixerMainPage : Page
{
    private MainModel _mainModel;
    private MediaTrackMixer mixer;
    private List<MediaTrackMixer.TrackGroup> mixerTracks;
    private string? navigateTo;
    private string ffmpegPath;
    private List<string> outputFiles = [];
    public static List<string> AllSupportedTypes = [ ".mkv", ".mp4", ".png", ".jpg", ".jpeg", ".mp3", ".wav", ".srt", ".ass" ];
    (string, bool)[] colours =
    [
        ("Magenta", true),
        ("Yellow", true),
        ("Cyan", true),
        ("Green", false),
        ("Blue", false),
        ("DarkCyan", false),
        ("Red", false),
        ("DarkMagenta", false),
    ];
    public MediaTrackMixerMainPage()
    {
        InitializeComponent();
        _mainModel = new MainModel{ TrackGroups = new ObservableCollection<TrackGroup>() };
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is MixerProps props)
        {
            navigateTo = props.TypeToNavigateTo;
            ffmpegPath = props.FfmpegPath;
            mixer = new MediaTrackMixer(ffmpegPath);
            await AddMedia(props.MediaPaths.ToArray());
        }

        if (e.Parameter is string output && !outputFiles.Contains(output)) outputFiles.Add(output);
    }

    private async void ShowFilePicker(object sender, RoutedEventArgs e)
    {
        var filePicker = new FileOpenPicker();
        filePicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
        AllSupportedTypes.ForEach(t => filePicker.FileTypeFilter.Add(t));
        var windowId = XamlRoot?.ContentIslandEnvironment?.AppWindowId;
        var hwnd = Win32Interop.GetWindowFromWindowId(windowId.Value);
        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);
        var files = await filePicker.PickMultipleFilesAsync();
        await AddMedia(files.Select(f => f.Path).ToArray());
    }

    private async Task AddMedia(string[] inputs)
    {
        mixerTracks = await mixer.GetTracks(inputs);
        var i = 0;
        _mainModel.TrackGroups = new ObservableCollection<TrackGroup>(mixerTracks.Select(t =>
        {
            var (background, hasBlackForeground) = colours[i++ % colours.Length];
            var colour = new Colour { Background = background, HasBlackForeground = hasBlackForeground };
            var tracks = t.Tracks.Select(s => new Track
            {
                FullPath = t.Path,
                Title = s.Title,
                CodecOrMimeType = s.Codec,
                Index = s.Index,
                Type = Track.ProcessorToModelTrackType(s.Type),
                Colour = colour,
                FileName = Path.GetFileName(t.Path)
            }).Concat(t.Attachments.Select(s => new Track
            {
                FullPath = t.Path,
                Title = s.Name,
                CodecOrMimeType = s.MimeType,
                Index = s.Index,
                Type = TrackType.Attachment,
                Colour = colour,
                FileName = Path.GetFileName(t.Path)
            })).ToList();
            if(t.Chapters.Count > 0) tracks.Add(new Track
            {
                Type = TrackType.Chapters,
                Title = $"{t.Chapters.Count} chapters",
                Colour = colour,
                FileName = Path.GetFileName(t.Path),
                FullPath = t.Path
            });
            tracks.Add(new Track
            {
                Title = t.GlobalMetadataTitle,
                Type = TrackType.GlobalMetadata,
                Colour = colour,
                FileName = Path.GetFileName(t.Path),
                FullPath = t.Path
            });
            return new TrackGroup(tracks)
            {
                FileName = Path.GetFileName(t.Path),
                FullPath = t.Path,
                Colour = tracks.First().Colour,
                Checked = false
            };
        }));
        CollectionViewSourcee.Source = _mainModel.TrackGroups;
        ListVieww.ItemsSource = CollectionViewSourcee.View;

    }

    private void TrackSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (var trackGroup in _mainModel.TrackGroups)
        {
            var checkedd = false;
            var notAll = false;
            foreach (var track in trackGroup)
            {
                if (ListVieww.SelectedItems.Contains(track)) checkedd = true;
                else notAll = true;
            }

            trackGroup.Checked = checkedd switch
            {
                true when notAll => null,
                false when notAll => false,
                _ => true
            };
        }

        _mainModel.HasSelectedTracks = ListVieww.SelectedItems.Any();
    }

    private int GetTrackGroupIndex(TrackGroup trackGroup)
    {
        return _mainModel.TrackGroups.TakeWhile(tg => tg != trackGroup).Sum(tg => tg.Count);
    }

    private void TrackGroupChecked(object sender, RoutedEventArgs e)
    {
        var checkbox = sender as CheckBox;
        if (checkbox == null) return;
        var trackGroup = checkbox.DataContext as TrackGroup;
        if (trackGroup == null) return;
        var index = GetTrackGroupIndex(trackGroup);
        if (checkbox.IsChecked == true)
        {
            ListVieww.SelectRange(new ItemIndexRange(index, (uint)trackGroup.Count));
        }
        else
        {
            ListVieww.DeselectRange(new ItemIndexRange(index, (uint)trackGroup.Count));
        }
    }

    private void TrackGroupDeleted(object sender, RoutedEventArgs e)
    {
        _mainModel.TrackGroups.Remove((sender as MenuFlyoutItem).DataContext as TrackGroup);
    }

    private async void MainPage_OnDrop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            await AddMedia(_mainModel.TrackGroups.Select(t => t.FullPath).Concat(items.Select(i => i.Path)).ToArray());
        }
    }

    private void MainPage_OnDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private void SelectVideoAndAudio(object sender, RoutedEventArgs e)
    {
        var checkbox = sender as MenuFlyoutItem;
        if (checkbox == null) return;
        var trackGroup = checkbox.DataContext as TrackGroup;
        if (trackGroup == null) return;
        trackGroup.Checked = false;
        var index = GetTrackGroupIndex(trackGroup);
        var count = trackGroup.Count(t => t.Type is TrackType.Video or TrackType.Audio);
        ListVieww.SelectRange(new ItemIndexRange(index, (uint)count));
    }

    private void SelectSubtitles(object sender, RoutedEventArgs e)
    {
        var checkbox = sender as MenuFlyoutItem;
        if (checkbox == null) return;
        var trackGroup = checkbox.DataContext as TrackGroup;
        if (trackGroup == null) return;
        trackGroup.Checked = false;
        var index = GetTrackGroupIndex(trackGroup);
        index += trackGroup.Count(t => t.Type is TrackType.Video or TrackType.Audio);
        var count = trackGroup.Count(t => t.Type == TrackType.Subtitle);
        ListVieww.SelectRange(new ItemIndexRange(index, (uint)count));
    }

    private void RemoveAllMedia(object sender, RoutedEventArgs e)
    {
        _mainModel.TrackGroups.Clear();
    }

    private void GoToNextPage(object sender, RoutedEventArgs e)
    {
        var transition = new SlideNavigationTransitionInfo();
        transition.Effect = SlideNavigationTransitionEffect.FromRight;
        var selectedTracks = ListVieww.SelectedItems.Cast<Track>().ToList();
        Frame.Navigate(typeof(MediaTrackMixerProcessingPage), new
        {
            Tracks = selectedTracks.Where(t => t.Type != TrackType.Other).ToList(),
            MixerTracks = mixerTracks.Where(mt => selectedTracks.Any(t => t.FullPath == mt.Path)).ToList(),
            FfmpegPath = ffmpegPath,
        }, transition);
    }

    private void GoBack(object sender, RoutedEventArgs e)
    {
        if (navigateTo == null) Frame.GoBack();
        else Frame.NavigateToType(Type.GetType(navigateTo), outputFiles, new FrameNavigationOptions { IsNavigationStackEnabled = false });
    }
}

public class MixerProps
{
    public string FfmpegPath { get; set; }
    public IEnumerable<string> MediaPaths { get; set; }
    public string? TypeToNavigateTo { get; set; }
}
