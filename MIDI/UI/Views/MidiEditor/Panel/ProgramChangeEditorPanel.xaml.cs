using System.Windows.Controls;
using MIDI.UI.Core;

namespace MIDI.UI.Views.MidiEditor.Panel
{
    [LayoutContent("programChangeEditor", "音色 (プログラムチェンジ)")]
    public partial class ProgramChangeEditorPanel : UserControl
    {
        public ProgramChangeEditorPanel()
        {
            InitializeComponent();
        }
    }
}