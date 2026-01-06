using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WinUIShared.Helpers;

namespace MediaTrackMixerPage
{
    public class MediaTrackMixer(string ffmpegPath) : Processor(ffmpegPath)
    {
        public async Task<List<TrackGroup>> GetTracks(string[] inputs)
        {
            var trackGroups = new List<TrackGroup>();
            var outputLines = new List<string>();
            var inputStartsAt = -1;
            var inputArgs = string.Join(" ", inputs.Select(inp => $"-i \"{inp}\""));
            await StartFfmpegProcess(inputArgs, (sender, args) =>
            {
                if (string.IsNullOrWhiteSpace(args.Data)) return;
                Debug.WriteLine(args.Data);
                if (args.Data.StartsWith(nameof(FfOutputEnum.Input)) && inputStartsAt == -1)
                    inputStartsAt = outputLines.Count;
                if (args.Data.StartsWith(' ') || args.Data.StartsWith(nameof(FfOutputEnum.Input)))
                    outputLines.Add(args.Data);
            });
            if (inputStartsAt == -1) return trackGroups;
            outputLines.RemoveRange(0, inputStartsAt);
            var parseResult = ParseOutputLines(FfOutputTree, 0, 0, out _);
            if (parseResult == null) return trackGroups;
            trackGroups = parseResult.Select(r =>
            {
                var path = (string)r.Value;
                var globalMetadata = r.Children.FirstOrDefault(c => c.First().Name == FfOutputEnum.Metadata);
                var duration = r.Children.First(c => c.First().Name == FfOutputEnum.Duration)[0];
                var chapters = r.Children.FirstOrDefault(c => c.First().Name == FfOutputEnum.Chapters) ??
                               [new FfOutputValueLeaf(FfOutputEnum.Chapters)];
                var streams = r.Children.First(c => c.First().Name == FfOutputEnum.Stream);
                return new TrackGroup(path)
                {
                    GlobalMetadata = globalMetadata == null ? new List<KeyValuePair<string, string>>()
                        : globalMetadata[0].Children[0].Select(kv => (KeyValuePair<string, string>)kv.Value).ToList(),
                    Duration = (TimeSpan)duration.Value,
                    Chapters = chapters[0].Children.SelectMany(chapterList => chapterList).Select(ch =>
                    {
                        var chapter = (Chapter)ch.Value;
                        if (ch.Children.Count > 0)
                            chapter.Metadata = ch.Children[0][0].Children.SelectMany(s => s)
                                .Select(kv => (KeyValuePair<string, string>)kv.Value).ToList();
                        return chapter;
                    }).ToList(),
                    Tracks = streams.Where(c => c.Value is Track).Select(tr =>
                    {
                        var track = (Track)tr.Value;
                        if (tr.Children.Count > 0)
                            track.Metadata = tr.Children[0][0].Children.SelectMany(s => s)
                                .Select(kv => (KeyValuePair<string, string>)kv.Value).ToList();
                        return track;
                    }).ToList(),
                    Attachments = streams.Where(c => c.Value is Attachment).Select(tr =>
                    {
                        var attachment = (Attachment)tr.Value;
                        attachment.Metadata = tr.Children[0][0].Children.SelectMany(s => s)
                            .Select(kv => (KeyValuePair<string, string>)kv.Value).ToList();
                        return attachment;
                    }).ToList()
                };
            }).ToList();
            return trackGroups;

            List<FfOutputValueLeaf>? ParseOutputLines(FfOutputLeaf currentLeaf, int currentLine, int depth,
                out int newCurrentLine)
            {
                newCurrentLine = currentLine;
                if (currentLine >= outputLines.Count) return null;
                var line = outputLines[currentLine];
                const string indent = "  ";
                if (!line.StartsWith(string.Concat(Enumerable.Repeat(indent, depth)))) return null;
                line = line[(depth * indent.Length)..];
                if (currentLeaf.Name != FfOutputEnum.MetadataEntry &&
                    !line.StartsWith(GetLeafName(currentLeaf.Name))) return null;

                MatchCollection matchCollection;
                var result = new FfOutputValueLeaf(currentLeaf.Name);
                switch (currentLeaf.Name)
                {
                    case FfOutputEnum.Input:
                        matchCollection = Regex.Matches(line, @"Input #\d+,.+?from '(.+?)'");
                        if (matchCollection.Count == 0) return null;
                        result.Value = matchCollection[0].Groups[1].Value;
                        break;
                    case FfOutputEnum.Duration:
                        matchCollection = Regex.Matches(line, @"Duration:\s(?:(\d{2}:\d{2}:\d{2}\.\d{2})|N/A).+");
                        if (matchCollection.Count == 0) return null;
                        TimeSpan timeSpan;
                        if (!matchCollection[0].Groups[1].Success) timeSpan = TimeSpan.MinValue;
                        else if (!TimeSpan.TryParse(matchCollection[0].Groups[1].Value, out timeSpan)) return null;
                        result.Value = timeSpan;
                        break;
                    case FfOutputEnum.Chapter:
                        matchCollection = Regex.Matches(line,
                            @"Chapter #\d+:\d+: start (\d+?\.?\d*?), end (\d+?\.?\d*?)$");
                        if (matchCollection.Count == 0) return null;
                        result.Value = new Chapter
                        {
                            Start = TimeSpan.FromSeconds(double.Parse(matchCollection[0].Groups[1].Value)),
                            End = TimeSpan.FromSeconds(double.Parse(matchCollection[0].Groups[2].Value))
                        };
                        break;
                    case FfOutputEnum.Stream:
                        matchCollection = Regex.Matches(line, @"Stream #(\d+):(\d+).*?: (\w+): (\w+).+?( \(\w+\))*$");
                        if (matchCollection.Count == 0) return null;
                        var streamType = matchCollection[0].Groups[3].Value;
                        var dispositions = matchCollection[0].Groups[5].Captures
                            .Select(c => c.Value[" (".Length..^")".Length]).ToList(); //Strip out " (" and ")"
                        if (streamType == "Attachment")
                        {
                            result.Value = new Attachment(int.Parse(matchCollection[0].Groups[2].Value))
                                { Dispositions = dispositions };
                        }
                        else
                        {
                            var trackType = GetTrackType(streamType);
                            var trackCodec = matchCollection[0].Groups[4].Value;
                            result.Value = new Track(int.Parse(matchCollection[0].Groups[2].Value), trackType,
                                    trackCodec)
                                { Dispositions = dispositions };
                        }

                        break;
                    case FfOutputEnum.MetadataEntry:
                        matchCollection = Regex.Matches(line, @"(.+?)\s*?: (.+)");
                        if (matchCollection.Count == 0) return null;
                        result.Value = new KeyValuePair<string, string>(matchCollection[0].Groups[1].Value,
                            matchCollection[0].Groups[2].Value);
                        break;
                }

                if (currentLeaf.Children != null)
                {
                    foreach (var child in currentLeaf.Children)
                    {
                        var res = ParseOutputLines(child, newCurrentLine + 1, depth + 1, out var ncl);
                        if (res != null)
                        {
                            result.Children.Add(res);
                            newCurrentLine = ncl;
                        }
                    }
                }

                if (currentLeaf.Multiple)
                {
                    var multiRes = ParseOutputLines(currentLeaf, newCurrentLine + 1, depth, out var ncl);
                    if (multiRes == null) return [result];

                    newCurrentLine = ncl;
                    multiRes.Insert(0, result);
                    return multiRes;
                }

                return [result];
            }

            string GetLeafName(FfOutputEnum name) => name switch
            {
                FfOutputEnum.SideData => "Side data",
                _ => name.ToString()
            };
        }

        public async Task Mix(string output, List<KeyValuePair<string, string>> globalMetadata, List<Chapter> chapters, List<TrackMap> maps)
        {
            var pathAndSync = new List<(string path, SyncType syncType, TimeSpan syncChange)>();
            var trackToInputIndex = new Dictionary<TrackMap, int>();
            foreach (var trackMap in maps)
            {
                var ps = (trackMap.Path, trackMap.SyncType,
                    trackMap.SyncType == SyncType.None ? TimeSpan.Zero : trackMap.SyncChange);
                var psIndex = pathAndSync.IndexOf(ps);
                if (psIndex == -1)
                {
                    trackToInputIndex.Add(trackMap, pathAndSync.Count);
                    pathAndSync.Add(ps);
                }
                else trackToInputIndex.Add(trackMap, psIndex);
            }

            var inputArgs = string.Join(' ', pathAndSync.Select(ps =>
            {
                var offsetString = string.Empty;
                if (ps.syncType != SyncType.None)
                {
                    var syncChange = ps.syncType == SyncType.Delay ? ps.syncChange : -ps.syncChange;
                    offsetString = $"-itsoffset {syncChange} ";
                }

                return $"{offsetString}-i \"{ps.path}\"";
            }));

            var metadataFileContent = CreateMetadataFile();
            var metadataFileIndex = pathAndSync.Count;
            const string metadataFileArgs = "-f ffmetadata -i -";

            var mapArgs = string.Join(' ',
                maps.Select(trackMap => $"-map {trackToInputIndex[trackMap]}:{trackMap.TrackIndex}"));

            //var disableDefaultMappingFromFirstInput = "-map_metadata -1 -map_chapters -1"; //By default, ffmpeg maps the global metadata and chapters from the first input if there is no metadata file. These arguments disable that.
            var globalDataMapArgs =
                "-map_metadata 1 -map_chapters 1"; //Copy globalmetadata and chapters from the metadata file.
            var metadataMapArgs =
                string.Join(' ', maps.Select((_, i) => $"-map_metadata:s:{i} {metadataFileIndex}:s:{i}"));

            var dispositionArgs = string.Join(' ', maps.Select((trackMap, i) =>
            {
                var dispParams = "0";
                if (trackMap.Dispositions.Count > 0) dispParams = string.Join('+', trackMap.Dispositions);
                return $"-disposition:{i} {dispParams}";
            }));

            var outputExtension = Path.GetExtension(output);
            var subtitleEncode = "-c:s copy";
            var audioEncode = "-c:a copy";
            switch (outputExtension)
            {
                case ".mp4":
                    subtitleEncode = "-c:s mov_text";
                    break;
                case ".mp3":
                    audioEncode = "-c:a libmp3lame";
                    break;
            }

            var totalDuration = TimeSpan.MinValue;
            var durationsFound = 0;
            MatchCollection matchCollection;

            File.Delete(output);
            await StartFfmpegProcess($"{inputArgs} {metadataFileArgs} {audioEncode} {subtitleEncode} {globalDataMapArgs} {mapArgs} {metadataMapArgs} {dispositionArgs} -max_interleave_delta 0 -c:v copy \"{output}\"",
                (sender, args) =>
                {
                    if (string.IsNullOrWhiteSpace(args.Data)) return;
                    Debug.WriteLine(args.Data);
                    if (HasError(args.Data)) return;
                    if (CheckFileNameLongError(args.Data)) return;
                    if (durationsFound < maps.Count && args.Data.StartsWith("  Duration:"))
                    {
                        matchCollection = Regex.Matches(args.Data, @"\s*Duration:\s(\d{2}:\d{2}:\d{2}\.\d{2}).+");
                        if (matchCollection.Count != 0)
                        {
                            durationsFound++;
                            if (TimeSpan.TryParse(matchCollection[0].Groups[1].Value, out var inputDuration) &&
                                totalDuration < inputDuration)
                            {
                                totalDuration = inputDuration;
                            }
                        }
                    }

                    if (!args.Data.StartsWith("frame") && !args.Data.StartsWith("size")) return;
                    if (CheckNoSpaceDuringProcess(args.Data)) return;
                    matchCollection = Regex.Matches(args.Data,
                        @"^(?:frame|size)=\s*.+?time=(\d{2}:\d{2}:\d{2}\.\d{2}).+");
                    if (matchCollection.Count == 0) return;
                    var progressPercent = TimeSpan.Parse(matchCollection[0].Groups[1].Value) / totalDuration * ProgressMax;
                    progressPrimary.Report(progressPercent);
                    centerTextPrimary.Report($"{Math.Round(progressPercent, 2)} %");
                }, async process =>
                {
                    //Pipe the metadata file through stdin. With this, no need to create a .txt file on disk
                    await process.StandardInput.WriteAsync(metadataFileContent);
                    await process.StandardInput.FlushAsync();
                });
            progressPrimary.Report(100);
            centerTextPrimary.Report("100 %");
            outputFile = output;

            string CreateMetadataFile()
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(";FFMETADATA1");
                AppendMetadata(globalMetadata);
                stringBuilder.AppendLine();

                foreach (var chapter in chapters)
                {
                    stringBuilder.AppendLine("[CHAPTER]");
                    stringBuilder.AppendLine("TIMEBASE=1/1000");
                    stringBuilder.AppendLine($"START={Math.Round(chapter.Start.TotalMilliseconds)}");
                    stringBuilder.AppendLine($"END={Math.Round(chapter.End.TotalMilliseconds)}");
                    AppendMetadata(chapter.Metadata);
                }

                stringBuilder.AppendLine();

                foreach (var trackMap in maps)
                {
                    stringBuilder.AppendLine("[STREAM]");
                    AppendMetadata(trackMap.Metadata);
                }

                return stringBuilder.ToString();

                void AppendMetadata(List<KeyValuePair<string, string>> metadata)
                {
                    foreach (var kvp in metadata)
                    {
                        stringBuilder.AppendLine($"{Escape(kvp.Key)}={Escape(kvp.Value)}");
                    }
                }
            }

            string Escape(string keyOrValue)
            {
                return Regex.Replace(keyOrValue, @"[=;#\\]", $"\\$1");
            }
        }

        public async Task ExtractAttachment(string input, int attachmentTrackIndex, string output)
        {
            rightTextPrimary.Report("Mixing...");
            File.Delete(output);
            await StartFfmpegProcess($"-dump_attachment:{attachmentTrackIndex} \"{output}\" -i \"{input}\"", (sender, args) =>
            {
                if (string.IsNullOrWhiteSpace(args.Data)) return;
                if (CheckFileNameLongError(args.Data)) return;
                Debug.WriteLine(args.Data);
                HasError(args.Data);
            });
            progressPrimary.Report(100);
            centerTextPrimary.Report("100 %");
            outputFile = output;
        }

        public (string name, string ext) GetFileNameAndExtension(string fileNameWithExtension)
        {
            var ext = Path.GetExtension(fileNameWithExtension);
            var name = Path.GetFileNameWithoutExtension(fileNameWithExtension);
            return (name, ext);
        }

        private bool HasError(string line)
        {
            if(line == "Conversion failed!"
                    || line.StartsWith("Error initializing the muxer")
                    || line.StartsWith("Error opening output file"))
            {
                error($"An error occurred while extracting attachment\n\n{line}");
                return true;
            }
            return false;
        }

        private static TrackType GetTrackType(string type) => type switch
        {
            "Video" => TrackType.Video,
            "Audio" => TrackType.Audio,
            "Subtitle" => TrackType.Subtitle,
            _ => TrackType.Other
        };

        public enum TrackType{ Other, Video, Audio, Subtitle }
        public enum GeneralType{ None, Track, Chapters, Attachment, GlobalMetadata }
        public enum SyncType{ None, Delay, Hasten }
        public class Track(int index, TrackType type, string codec)
        {
            public int Index { get; set; } = index;
            public TrackType Type { get; set; } = type;
            public string Codec { get; set; } = codec;
            public List<KeyValuePair<string, string>> Metadata { get; set; } = [];
            public List<string> Dispositions { get; set; } = [];
        }

        public class Chapter
        {
            public TimeSpan Start { get; set; }
            public TimeSpan End { get; set; }
            public List<KeyValuePair<string, string>> Metadata { get; set; } = [];
        }

        public class Attachment(int index)
        {
            public int Index { get; set; } = index;
            public List<KeyValuePair<string, string>> Metadata { get; set; } = [];
            public List<string> Dispositions { get; set; } = [];
        }

        public class TrackGroup(string path)
        {
            public string Path { get; set; } = path;
            public List<KeyValuePair<string, string>> GlobalMetadata { get; set; } = [];
            public TimeSpan Duration { get; set; }
            public List<Track> Tracks { get; set; } = [];
            public List<Chapter> Chapters { get; set; } = [];
            public List<Attachment> Attachments { get; set; } = [];
        }
        public class TrackMap(string path, int trackIndex, GeneralType type, List<KeyValuePair<string, string>> metadata, List<string> dispositions, SyncType syncType, TimeSpan syncChange)
        {
            public string Path { get; set; } = path;
            public int TrackIndex { get; set; } = trackIndex;
            public GeneralType Type { get; set; } = type;
            public List<KeyValuePair<string, string>> Metadata { get; set; } = metadata;
            public List<string> Dispositions { get; set; } = dispositions;
            public SyncType SyncType { get; set; } = syncType;
            public TimeSpan SyncChange { get; set; } = syncChange;
        }

        public enum FfOutputEnum { Input, Duration, Chapters, Chapter, Stream, Metadata, MetadataEntry, SideData}
        public class FfOutputLeaf
        {
            public FfOutputEnum Name { get; set; }
            public bool Multiple { get; set; }
            public List<FfOutputLeaf>? Children { get; set; }
            public override string ToString() => Name.ToString();
        }
        public class FfOutputValueLeaf(FfOutputEnum name)
        {
            public FfOutputEnum Name { get; set; } = name;
            public object Value { get; set; }
            public List<List<FfOutputValueLeaf>> Children { get; set; } = [];
            public override string ToString() => $"{Name}: {Value}";
        }

        private static readonly FfOutputLeaf MetadataLeaf = new() { Name = FfOutputEnum.Metadata, Children = [new FfOutputLeaf { Name = FfOutputEnum.MetadataEntry, Multiple = true }] };
        private static readonly FfOutputLeaf FfOutputTree = new()
        {
            Name = FfOutputEnum.Input,
            Multiple = true,
            Children =
            [
                MetadataLeaf,
                new FfOutputLeaf{ Name = FfOutputEnum.Duration },
                new FfOutputLeaf
                {
                    Name = FfOutputEnum.Chapters,
                    Children = [
                        new FfOutputLeaf
                        {
                            Name = FfOutputEnum.Chapter,
                            Multiple = true,
                            Children = [ MetadataLeaf ]
                        }
                    ]
                },
                new FfOutputLeaf
                {
                    Name = FfOutputEnum.Stream,
                    Multiple = true,
                    Children = [ 
                        MetadataLeaf,
                        new FfOutputLeaf{ Name = FfOutputEnum.SideData, Children = [new FfOutputLeaf { Name = FfOutputEnum.MetadataEntry, Multiple = true }] }
                    ]
                }
            ]
        };
    }
}
