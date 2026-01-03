using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaTrackMixerPage.ViewModels;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace MediaTrackMixer.Resources
{
    public sealed partial class Template: ResourceDictionary
    {
        public BindingProxy Proxy { get; } = new BindingProxy();
        public Template()
        {
            InitializeComponent();
        }

        private void TitleEdit(object sender, RoutedEventArgs e)
        {
            var button = sender as ToggleButton;
            var track = button.DataContext as Track;
            track.InEditMode = !track.InEditMode;
        }

        private void AddMetadata(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var metadataEdit = (MetadataEdit)button.DataContext;
            metadataEdit.Add(new MetadataItem(string.Empty, string.Empty));
        }

        private void AddChapter(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var chaptersEdit = (ChaptersEdit)button.DataContext;
            var individualTitle = new PassedTitle(TrackType.Chapters);
            chaptersEdit.Add(new ChapterEdit(new MetadataEdit([new MetadataItem("title", $"Chapter {chaptersEdit.Count + 1}")], individualTitle), individualTitle)
            {
                Start = chaptersEdit.LastOrDefault()?.End ?? TimeSpan.Zero,
                End = chaptersEdit.LastOrDefault()?.End ?? TimeSpan.Zero,
            });
        }
    }

    public class TrackDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate TrackEditTemplate { get; set; }
        public DataTemplate MetadataEditTemplate { get; set; }
        public DataTemplate ChaptersEditTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return item switch
            {
                TrackEdit => TrackEditTemplate,
                MetadataEdit => MetadataEditTemplate,
                ChaptersEdit => ChaptersEditTemplate,
                _ => base.SelectTemplateCore(item)
            };
        }
    }
}
