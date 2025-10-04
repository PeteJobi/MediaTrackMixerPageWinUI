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
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Storage.Pickers;

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

    public MediaTrackMixerProcessingPage()
    {
        InitializeComponent();
        viewModel = new ProcessingPageModel { Tracks = [] };
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
        foreach (var track in viewModel.Tracks)
        {
            track.OnSecondPage = true;
        }
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
                    (attachmentName, attachmentExtension) = track.Title?.Contains('.') == true 
                        ? mixer.GetFileNameAndExtension(track.Title)
                            : !string.IsNullOrWhiteSpace(track.Title)
                                ? (track.Title, ".file") 
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

    private List<MediaTrackMixer.Map> GetMaps() => viewModel.Tracks.Select(tr =>
    {
        var inputIndex = mixerTracks.FindIndex(tg => tg.Path == tr.FullPath);
        var trackIndex = tr.Type == TrackType.Chapters ? 0 : tr.Index;
        var type = Track.ModelToProcessorGeneralType(tr.Type);
        var metadataReplacements = tr.Type switch
        {
            TrackType.Attachment => new Dictionary<string, string>
            {
                { "filename", tr.Title ?? string.Empty },
                { "mimetype", tr.CodecOrMimeType ?? string.Empty }
            },
            _ => new Dictionary<string, string>
            {
                { "title", tr.Title ?? string.Empty }
            },
        };
        return new MediaTrackMixer.Map(inputIndex, trackIndex, type, metadataReplacements);
    }).ToList();

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
        viewModel.OperationVisibility = OperationVisibility.ShowProgress;
        var maps = GetMaps();
        var progress = new Progress<double>(progress =>
        {
            ProgressBar.Value = progress;
            ProgressText.Text = $"{Math.Round(progress, 2)}%";
        });
        bool success;
        try
        {
            var isExtractingAttachment = viewModel.Tracks is [{ Type: TrackType.Attachment }]; //viewModel.Tracks.Count == 1 && viewModel.Tracks[0].Type == TrackType.Attachment;
            await mixer.Mix(mixerTracks, file.Path, maps, null, isExtractingAttachment, progress);
            success = true;
            outputFile = file.Path;
        }
        catch (Exception)
        {
            success = false;
        }
        viewModel.OperationVisibility = OperationVisibility.ShowOnlyBack;
        viewModel.ShowInfoBar = true;
        viewModel.InfoBarSeverity = success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        viewModel.InfoBarMessage = success ? "Track mix completed successfully" : "Track mix was not successful";
    }

    private void GoBack(object sender, RoutedEventArgs e)
    {
        var transition = new SlideNavigationTransitionInfo
        {
            Effect = SlideNavigationTransitionEffect.FromLeft
        };
        Frame.NavigateToType(typeof(MediaTrackMixerMainPage), outputFile, new FrameNavigationOptions { IsNavigationStackEnabled = false, TransitionInfoOverride = transition });
    }
}
