using MediaTrackMixerPage.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Storage.Pickers;
using WinUIShared.Enums;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MediaTrackMixerPage;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MediaTrackMixerProcessingPage : Page
{
    private ProcessingPageModel viewModel;
    private MediaTrackMixer mixer;
    private string? outputFile;
    private List<MediaTrackMixer.TrackGroup> mixerTracks;
    private BindingProxy globalProxy;

    public MediaTrackMixerProcessingPage()
    {
        InitializeComponent();
        viewModel = new ProcessingPageModel { Tracks = [] };
        globalProxy = (BindingProxy)Application.Current.Resources["GlobalBindingProxy"];
        globalProxy.OnSecondPage = true;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        dynamic? obj = e.Parameter;
        mixer = new MediaTrackMixer(obj.FfmpegPath);
        viewModel = new ProcessingPageModel
        {
            Tracks = new ObservableCollection<Track>((List<Track>)obj.Tracks)
        };
        mixerTracks = obj.MixerTracks;
        base.OnNavigatedTo(e);
    }

    private (string, PickerLocationId, IEnumerable<string>) GetSaveParameters()
    {
        var containsVideo = false;
        var containsChapters = false;
        var containsAudio = false;
        var containsSubtitles = false;
        var containsAttachments = false;
        var subtitleExtension = string.Empty;
        var attachmentName = string.Empty;
        var attachmentExtension = string.Empty;
        foreach (var track in viewModel.Tracks)
        {
            if(track.Type == TrackType.GlobalMetadata) continue;
            switch (track.Type)
            {
                case TrackType.Video:
                    containsVideo = true;
                    break;
                case TrackType.Audio:
                    containsAudio = true;
                    break;
                case TrackType.Subtitle:
                    containsSubtitles = true;
                    subtitleExtension = track.CodecOrMimeType == "ass" ? ".ass" : ".srt";
                    break;
                case TrackType.Chapters:
                    containsChapters = true;
                    break;
                case TrackType.Attachment:
                    containsAttachments = true;
                    (attachmentName, attachmentExtension) = track.PassedTitle.Title?.Contains('.') == true 
                        ? mixer.GetFileNameAndExtension(track.PassedTitle.Title)
                            : !string.IsNullOrWhiteSpace(track.PassedTitle.Title)
                                ? (track.PassedTitle.Title, ".file") 
                                : ("New attachment", ".file");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (containsChapters || ((containsVideo || containsAudio) && (containsSubtitles || containsAttachments)))
        {
            return ("New video", PickerLocationId.VideosLibrary, FileTypeChoices(".mkv"));
        }

        if (containsVideo)
        {
            return ("New video", PickerLocationId.VideosLibrary, FileTypeChoices(".mp4"));
        }

        if (viewModel.Tracks.Count > 1)
        {
            if (containsAudio)
            {
                return ("New multi-track audio", PickerLocationId.VideosLibrary, FileTypeChoices(".mp4"));
            }

            if (containsSubtitles)
            {
                return ("Empty video with subtitles", PickerLocationId.VideosLibrary, FileTypeChoices(".mp4"));
            }

            if (containsAttachments)
            {
                return ("Empty video with attachments", PickerLocationId.VideosLibrary, FileTypeChoices(".mkv"));
            }
        }

        if (containsAudio)
        {
            return ("New audio", PickerLocationId.MusicLibrary, FileTypeChoices(".mp3"));
        }

        if (containsSubtitles)
        {
            return ("New subtitles", PickerLocationId.DocumentsLibrary, FileTypeChoices(subtitleExtension));
        }

        if (containsAttachments)
        {
            return (attachmentName, PickerLocationId.DocumentsLibrary, FileTypeChoices(attachmentExtension));
        }

        return ("New file", PickerLocationId.Downloads, FileTypeChoices(".mp4"));

        IEnumerable<string> FileTypeChoices(string type) => MediaTrackMixerMainPage.AllSupportedTypes.Where(t => t != type).Prepend(type);
    }

    private List<MediaTrackMixer.TrackMap> GetTrackMaps() => viewModel.Tracks.Where(tr => tr.Type is not (TrackType.Chapters or TrackType.GlobalMetadata))
        .Select(tr =>
    {
        var inputIndex = mixerTracks.FindIndex(tg => tg.Path == tr.FullPath);
        var trackIndex = tr.Index;
        var type = Track.ModelToProcessorGeneralType(tr.Type);
        var trackEdit = (TrackEdit)tr.Data;
        var metadata = trackEdit.Metadata.Select(m => new KeyValuePair<string, string>(m.Key, m.Value)).ToList();
        var dispositions = trackEdit.Dispositions.Where(d => d.Checked).Select(d => d.Key).ToList();
        var syncType = trackEdit.Sync.SelectedOption switch
        {
            SyncEdit.NoSync => MediaTrackMixer.SyncType.None,
            SyncEdit.Delay => MediaTrackMixer.SyncType.Delay,
            SyncEdit.Hasten => MediaTrackMixer.SyncType.Hasten,
            _ => throw new ArgumentOutOfRangeException()
        };
        var syncChange = trackEdit.Sync.Change;
        return new MediaTrackMixer.TrackMap(tr.FullPath, trackIndex, type, metadata, dispositions, syncType, syncChange);
    }).ToList();

    private (List<KeyValuePair<string, string>> globalMetadata, List<MediaTrackMixer.Chapter> chapters) GetGlobalData()
    {
        var globalMetadataEdit = viewModel.Tracks.Where(t => t.Type == TrackType.GlobalMetadata).Select(t => (MetadataEdit)t.Data);
        var globalMetadata = globalMetadataEdit
            .SelectMany(g => g.Select(m => new KeyValuePair<string, string>(m.Key, m.Value))).ToList();
        List<MediaTrackMixer.Chapter> chapters = [];
        var chaptersEdit = viewModel.Tracks.Where(t => t.Type == TrackType.Chapters).Select(t => (ChaptersEdit)t.Data);
        chapters = chaptersEdit.SelectMany(c => c.Select(ch => new MediaTrackMixer.Chapter
        {
            Start = ch.Start,
            End = ch.End,
            Metadata = ch.Metadata.Select(m => new KeyValuePair<string, string>(m.Key, m.Value)).ToList()
        })).ToList();
        return (globalMetadata, chapters);
    }

    private async void Save(object sender, RoutedEventArgs e)
    {
        var fileSaver = new FileSavePicker();
        var (suggestedFileName, suggestedStartLocation, fileTypeChoices) = GetSaveParameters();
        fileSaver.SuggestedFileName = suggestedFileName;
        fileSaver.SuggestedStartLocation = suggestedStartLocation;
        fileSaver.FileTypeChoices.Add("Media files", fileTypeChoices.ToList());
        var windowId = XamlRoot?.ContentIslandEnvironment?.AppWindowId;
        var hwnd = Win32Interop.GetWindowFromWindowId(windowId.Value);
        //var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Window);
        WinRT.Interop.InitializeWithWindow.Initialize(fileSaver, hwnd);
        var file = await fileSaver.PickSaveFileAsync();
        if (file == null) return;


        var trackMaps = GetTrackMaps();
        var (globalMetadata, chapters) = GetGlobalData();
        var isExtractingAttachment = viewModel.Tracks is [{ Type: TrackType.Attachment }]; //Meaning: viewModel.Tracks.Count == 1 && viewModel.Tracks[0].Type == TrackType.Attachment;
        outputFile = null;
        var processTask = isExtractingAttachment
            ? mixer.ExtractAttachment(trackMaps[0].Path, trackMaps[0].TrackIndex, file.Path)
            : mixer.Mix(file.Path, globalMetadata, chapters, trackMaps);
        outputFile = await ProcessManager.StartProcess(processTask);
        //outputFile = await ProcessManager.StartProcess(mixer.Mix(mixerTracks, file.Path, globalMetadata, chapters, trackMaps, isExtractingAttachment));
    }

    private async void GoBack(object sender, RoutedEventArgs e)
    {
        await mixer.Cancel();
        globalProxy.OnSecondPage = false;
        var transition = new SlideNavigationTransitionInfo
        {
            Effect = SlideNavigationTransitionEffect.FromLeft
        };
        Frame.NavigateToType(typeof(MediaTrackMixerMainPage), outputFile, new FrameNavigationOptions { IsNavigationStackEnabled = false, TransitionInfoOverride = transition });
    }
}
