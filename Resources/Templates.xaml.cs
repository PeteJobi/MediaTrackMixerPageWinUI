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
    }
}
