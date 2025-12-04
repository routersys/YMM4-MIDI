using System.Windows.Controls;
using MIDI.UI.Core;

namespace MIDI.UI.Views.MidiEditor.Panel
{
    [LayoutContent("controlChangeEditor", "コントロールチェンジ")]
    public partial class ControlChangeEditorPanel : UserControl
    {
        public ControlChangeEditorPanel()
        {
            InitializeComponent();
        }
    }
}