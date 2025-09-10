## Media Track Mixer Page (WinUI 3)
This provides a reuseable WinUI 3 page with an interface that allows for extracting/combining media tracks.

<img width="687" height="1017" alt="image" src="https://github.com/user-attachments/assets/0fc3222a-df96-4772-ae7a-d4f78697c404" />

# How to use
Include this library into your WinUI solution and reference it in your WinUI project. Then navigate to the **MediaTrackMixerMainPage** when the user requests for it, passing a **MixerProps** object as parameter.
The **MixerProps** object should contain the path to ffmpeg, the paths to the input media files, and optionally, the full name of the Page type to navigate back to when the user is done. If this last parameter is provided, you can get the path to the files that were generated on the Media Track Mixer page. If not, the user will be navigated back to whichever page called the Media Track Mixer page and there'll be no parameters. 
```
private void GoToMixer(){
  var ffmpegPath = Path.Join(Package.Current.InstalledLocation.Path, "Assets/ffmpeg.exe");
  var mediaPath = Path.Join(Package.Current.InstalledLocation.Path, "Assets/image.png");
  var secondMediaPath = Path.Join(Package.Current.InstalledLocation.Path, "Assets/video.mp4");
  Frame.Navigate(typeof(MediaTrackMixerMainPage), new TourProps { FfmpegPath = ffmpegPath, MediaPath = [mediaPath, secondMediaPath], TypeToNavigateTo = typeof(MainPage).FullName });
}

protected override void OnNavigatedTo(NavigationEventArgs e)
{
    //outputFiles is sent only if TypeToNavigateTo was specified in MixerProps.
    if (e.Parameter is List<string> outputFiles)
    {
        Console.WriteLine($"{outputFiles.Count} files were generated");
    }
}
```

You may check out [MediaTrackMixer](https://github.com/PeteJobi/MediaTrackMixer) to see a full application that uses this page.
