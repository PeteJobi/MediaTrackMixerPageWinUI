using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MediaTrackMixerPage
{
    public class MediaTrackMixer(string ffmpegPath)
    {
        public MediaTrackMixer() : this("ffmpeg.exe")
        {
        }

        public async Task<List<TrackGroup>> GetTracks(string[] inputs)
        {
            var trackGroups = new List<TrackGroup>();
            var inputArgs = string.Join(" ", inputs.Select(inp => $"-i \"{inp}\""));
            var currentInputIndex = -1;
            var currentTrackIndex = -1;
            var trackDictionary = new Dictionary<int, Track>();
            var chapterDictionary = new Dictionary<int, Chapter>();
            var attachmentDictionary = new Dictionary<int, Attachment>();
            var currentlyProcessed = GeneralType.None;

            await StartProcess(ffmpegPath, inputArgs, null, (sender, args) =>
            {
                if (string.IsNullOrWhiteSpace(args.Data)) return;
                Debug.WriteLine(args.Data);
                var matchCollection = Regex.Matches(args.Data, @"\s*Input #(\d+).*");
                if (matchCollection.Count != 0)
                {
                    var inputIndex = currentInputIndex = int.Parse(matchCollection[0].Groups[1].Value);
                    trackGroups.Add(new TrackGroup(inputs[inputIndex]));
                    currentTrackIndex = -1;
                    currentlyProcessed = GeneralType.GlobalMetadata;
                    trackDictionary.Clear();
                    chapterDictionary.Clear();
                    attachmentDictionary.Clear();
                }
                else
                {
                    matchCollection = Regex.Matches(args.Data, @"\s*Stream #(\d+):(\d+).*?: (\w+): (\w+).*");
                    if (matchCollection.Count != 0)
                    {
                        var inputIndex = currentInputIndex = int.Parse(matchCollection[0].Groups[1].Value);
                        var trackIndex = currentTrackIndex = int.Parse(matchCollection[0].Groups[2].Value);
                        var streamType = matchCollection[0].Groups[3].Value;
                        if (streamType == "Attachment")
                        {
                            var attachment = new Attachment(trackIndex);
                            trackGroups[inputIndex].Attachments.Add(attachment);
                            attachmentDictionary.Add(trackIndex, attachment);
                            currentlyProcessed = GeneralType.Attachment;
                        }
                        else
                        {
                            var trackType = GetTrackType(streamType);
                            var trackCodec = matchCollection[0].Groups[4].Value;
                            var track = new Track(trackIndex, trackType, trackCodec);
                            trackGroups[inputIndex].Tracks.Add(track);
                            trackDictionary.Add(trackIndex, track);
                            currentlyProcessed = GeneralType.Track;
                        }
                    }
                    else
                    {
                        matchCollection = Regex.Matches(args.Data, @"\s*Chapter #(\d+):(\d+).+");
                        if (matchCollection.Count != 0)
                        {
                            var inputIndex = currentInputIndex = int.Parse(matchCollection[0].Groups[1].Value);
                            var chapterIndex = currentTrackIndex = int.Parse(matchCollection[0].Groups[2].Value);
                            var chapter = new Chapter(chapterIndex);
                            trackGroups[inputIndex].Chapters.Add(chapter);
                            chapterDictionary.Add(chapterIndex, chapter);
                            currentlyProcessed = GeneralType.Chapters;
                        }
                    }
                }

                if (currentInputIndex < 0) return;

                if (currentlyProcessed == GeneralType.GlobalMetadata)
                {
                    matchCollection = Regex.Matches(args.Data, @"\s*Duration:\s(\d{2}:\d{2}:\d{2}\.\d{2}).+");
                    if (matchCollection.Count != 0 && TimeSpan.TryParse(matchCollection[0].Groups[1].Value, out var timeSpan))
                    {
                        trackGroups[currentInputIndex].Duration = timeSpan;
                    }
                    matchCollection = Regex.Matches(args.Data, @"\s*\btitle\s*:\s*(.+)\s*");
                    if (matchCollection.Count != 0)
                    {
                        trackGroups[currentInputIndex].GlobalMetadataTitle = matchCollection[0].Groups[1].Value;
                    }
                }
                else
                {
                    if (currentTrackIndex < 0) return;

                    if (currentlyProcessed == GeneralType.Attachment)
                    {
                        matchCollection = Regex.Matches(args.Data, @"\s*\bfilename\s*:\s*(.+)\s*");
                        if (matchCollection.Count != 0)
                        {
                            attachmentDictionary[currentTrackIndex].Name = matchCollection[0].Groups[1].Value;
                        }
                        else
                        {
                            matchCollection = Regex.Matches(args.Data, @"\s*\bmimetype\s*:\s*(.+)\s*");
                            if (matchCollection.Count != 0)
                            {
                                var mimeType = matchCollection[0].Groups[1].Value;
                                attachmentDictionary[currentTrackIndex].MimeType = mimeType;
                            }
                        }

                        if (attachmentDictionary[currentTrackIndex].Name != null
                            && attachmentDictionary[currentTrackIndex].MimeType != null
                            && trackGroups[currentInputIndex].Duration != TimeSpan.MinValue)
                            currentInputIndex = currentTrackIndex = -1;
                    }
                    else
                    {
                        matchCollection = Regex.Matches(args.Data, @"\s*\btitle\s*:\s*(.+)\s*");
                        if (matchCollection.Count != 0)
                        {
                            var title = matchCollection[0].Groups[1].Value;
                            if (currentlyProcessed == GeneralType.Chapters) chapterDictionary[currentTrackIndex].Title = title;
                            else trackDictionary[currentTrackIndex].Title = title;
                        }

                        if (((currentlyProcessed == GeneralType.Track && trackDictionary[currentTrackIndex].Title != null)
                             || (currentlyProcessed == GeneralType.Chapters && chapterDictionary[currentTrackIndex].Title != null))
                            && trackGroups[currentInputIndex].Duration != TimeSpan.MinValue)
                            currentInputIndex = currentTrackIndex = -1;
                    }
                }
            });

            return trackGroups;
        }

        public async Task Mix(List<TrackGroup> tracks, string output, List<Map> maps, int? globalMetadataInputIndex = null, bool isExtractingAttachment = false, IProgress<double>? progress = null)
        {
            var failure = false;
            if (isExtractingAttachment)
            {
                File.Delete(output);
                await StartProcess(ffmpegPath, $"-dump_attachment:{maps[0].TrackIndex} \"{output}\" -i \"{tracks[0].Path}\"", null, (sender, args) =>
                {
                    if (string.IsNullOrWhiteSpace(args.Data)) return;
                    Debug.WriteLine(args.Data);
                    if (args.Data == "Conversion failed!")
                    {
                        failure = true;
                    }
                });
                if (failure) throw new Exception("The operation failed");
                progress?.Report(100);
                return;
            }


            var inputArgs = string.Join(" ", tracks.Select(tr => $"-i \"{tr.Path}\""));
            var mapTrackArgs = string.Join(" ", maps.Select(mp => mp.Type switch
            {
                GeneralType.Chapters => $"-map_chapters {mp.InputIndex}",
                GeneralType.Track or GeneralType.Attachment => $"-map {mp.InputIndex}:{mp.TrackIndex}",
                GeneralType.GlobalMetadata => $"-map_metadata {mp.InputIndex}",
                _ => throw new ArgumentOutOfRangeException()
            }));
            int c = 0, s = 0, g = 0;
            var mapMetadataArgs = string.Join(" ", maps.Select(mp =>
            {
                if (mp.Type == GeneralType.Chapters) return null;
                var specifier = mp.Type switch
                {
                    GeneralType.Track or GeneralType.Attachment => $"s:{s++}",
                    GeneralType.GlobalMetadata => $"g:{g++}",
                    _ => throw new ArgumentOutOfRangeException()
                };
                var args = mp.ReplaceMetadataValues?.Select(kvp => $"-metadata:{specifier} {kvp.Key}=\"{kvp.Value}\"");
                return args == null ? null : string.Join(' ', args);
            }).Where(arg => arg != null));
            var disableDefaultMappingFromFirstInput = "-map_metadata -1 -map_chapters -1"; //By default, ffmpeg maps the global metadata and chapters from the first input. These arguments disable that.
            var outputExtension = Path.GetExtension(output);
            var subtitleEncode = "-c:s mov_text";
            var audioEncode = "-c:a copy";
            switch (outputExtension)
            {
                case ".mkv":
                case ".srt":
                    subtitleEncode = "-c:s copy";
                    break;
                case ".mp3":
                    audioEncode = string.Empty;
                    break;
            }
            var totalDuration = TimeSpan.MinValue;
            foreach (var map in maps)
            {
                if (tracks.Count <= map.InputIndex)
                    throw new ArgumentException(
                        $"You mapped to a track that does not exist. Input index: {map.InputIndex}");
                var trackDuration = tracks[map.InputIndex].Duration;
                if (totalDuration < trackDuration) totalDuration = trackDuration;
            }

            File.Delete(output);
            await StartProcess(ffmpegPath, $"{inputArgs} -c:v copy {audioEncode} {subtitleEncode} {disableDefaultMappingFromFirstInput} {mapTrackArgs} {mapMetadataArgs} -max_interleave_delta 0 \"{output}\"", null, (sender, args) =>
            {
                if (string.IsNullOrWhiteSpace(args.Data)) return;
                Debug.WriteLine(args.Data);
                if (args.Data == "Conversion failed!")
                {
                    failure = true;
                    return;
                }
                if (progress == null) return;
                var matchCollection = Regex.Matches(args.Data, @"^(?:frame|size)=\s*.+?time=(\d{2}:\d{2}:\d{2}\.\d{2}).+");
                if (matchCollection.Count == 0) return;
                progress.Report(TimeSpan.Parse(matchCollection[0].Groups[1].Value) / totalDuration * 100);
            });
            if (failure) throw new Exception("The operation failed");
            progress?.Report(100);
        }

        public (string name, string ext) GetFileNameAndExtension(string fileNameWithExtension)
        {
            var ext = Path.GetExtension(fileNameWithExtension);
            var name = Path.GetFileNameWithoutExtension(fileNameWithExtension);
            return (name, ext);
        }

        private static TrackType GetTrackType(string type) => type switch
        {
            "Video" => TrackType.Video,
            "Audio" => TrackType.Audio,
            "Subtitle" => TrackType.Subtitle,
            _ => TrackType.Other
        };

        private static async Task StartProcess(string processFileName, string arguments, DataReceivedEventHandler? outputEventHandler, DataReceivedEventHandler? errorEventHandler)
        {
            Process ffmpeg = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = processFileName,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                },
                EnableRaisingEvents = true
            };
            ffmpeg.OutputDataReceived += outputEventHandler;
            ffmpeg.ErrorDataReceived += errorEventHandler;
            ffmpeg.Start();
            ffmpeg.BeginErrorReadLine();
            ffmpeg.BeginOutputReadLine();
            await ffmpeg.WaitForExitAsync();
            ffmpeg.Dispose();
        }

        public enum TrackType{ Other, Video, Audio, Subtitle }
        public enum GeneralType{ None, Track, Chapters, Attachment, GlobalMetadata }
        public class Track(int index, TrackType type, string codec)
        {
            public int Index { get; set; } = index;
            public TrackType Type { get; set; } = type;
            public string Codec { get; set; } = codec;
            public string? Title { get; set; }
        }

        public class Chapter(int index)
        {
            public int Index { get; set; } = index;
            public string? Title { get; set; }
        }

        public class Attachment(int index)
        {
            public int Index { get; set; } = index;
            public string? Name { get; set; }
            public string? MimeType { get; set; }
        }

        public class TrackGroup(string path)
        {
            public string Path { get; set; } = path;
            public string? GlobalMetadataTitle { get; set; }
            public TimeSpan Duration { get; set; }
            public List<Track> Tracks { get; set; } = [];
            public List<Chapter> Chapters { get; set; } = [];
            public List<Attachment> Attachments { get; set; } = [];
        }
        public class Map(int inputIndex, int trackIndex, GeneralType type, Dictionary<string, string>? replaceMetadataValues = null)
        {
            public int InputIndex { get; set; } = inputIndex;
            public int TrackIndex { get; set; } = trackIndex;
            public GeneralType Type { get; set; } = type;
            public Dictionary<string, string>? ReplaceMetadataValues { get; set; } = replaceMetadataValues;
        }
    }
}
