using System.Collections.ObjectModel;
using MIDI.UI.ViewModels.MidiEditor;
using System.Windows;

namespace MIDI.Voice.ViewModels
{
    public class HelpTopicViewModel : ViewModelBase
    {
        private string _title = "";
        public string Title { get => _title; set => SetField(ref _title, value); }

        private int _indentLevel = 0;
        public int IndentLevel { get => _indentLevel; set => SetField(ref _indentLevel, value); }

        public Thickness IndentMargin => new Thickness(IndentLevel * 20, 0, 0, 0);
    }

    public class HelpWindowViewModel : ViewModelBase
    {
        public enum HelpTopic
        {
            EMEL,
            SUSL
        }

        private int _selectedTabIndex = 0;
        public int SelectedTabIndex { get => _selectedTabIndex; set => SetField(ref _selectedTabIndex, value); }

        public ObservableCollection<HelpTopicViewModel> EmelTopics { get; } = new();
        public ObservableCollection<HelpTopicViewModel> SuslTopics { get; } = new();

        private HelpTopicViewModel? _selectedTopic;
        public HelpTopicViewModel? SelectedTopic
        {
            get => _selectedTopic;
            set => SetField(ref _selectedTopic, value);
        }

        public HelpWindowViewModel(HelpTopic initialTopic)
        {
            LoadEmelTopics();
            LoadSuslTopics();

            if (initialTopic == HelpTopic.EMEL)
            {
                SelectedTabIndex = 0;
                SelectedTopic = EmelTopics.Count > 0 ? EmelTopics[0] : null;
            }
            else
            {
                SelectedTabIndex = 1;
                SelectedTopic = SuslTopics.Count > 0 ? SuslTopics[0] : null;
            }
        }

        private void LoadEmelTopics()
        {
            EmelTopics.Clear();
            EmelTopics.Add(new HelpTopicViewModel { Title = "EMEL 概要" });
            EmelTopics.Add(new HelpTopicViewModel { Title = "基本構文" });
            EmelTopics.Add(new HelpTopicViewModel { Title = "  Note / Rest", IndentLevel = 1 });
            EmelTopics.Add(new HelpTopicViewModel { Title = "  NoteEx (詳細指定)", IndentLevel = 1 });
            EmelTopics.Add(new HelpTopicViewModel { Title = "  制御コマンド", IndentLevel = 1 });
            EmelTopics.Add(new HelpTopicViewModel { Title = "  Chord (和音)", IndentLevel = 1 });
            EmelTopics.Add(new HelpTopicViewModel { Title = "制御構文" });
            EmelTopics.Add(new HelpTopicViewModel { Title = "  変数 (let / assign)", IndentLevel = 1 });
            EmelTopics.Add(new HelpTopicViewModel { Title = "  繰り返し (repeat)", IndentLevel = 1 });
            EmelTopics.Add(new HelpTopicViewModel { Title = "  条件分岐 (if / else)", IndentLevel = 1 });
            EmelTopics.Add(new HelpTopicViewModel { Title = "  関数 (func)", IndentLevel = 1 });
            EmelTopics.Add(new HelpTopicViewModel { Title = "演算子" });
            EmelTopics.Add(new HelpTopicViewModel { Title = "データ型" });
        }

        private void LoadSuslTopics()
        {
            SuslTopics.Clear();
            SuslTopics.Add(new HelpTopicViewModel { Title = "SUSL 概要" });
            SuslTopics.Add(new HelpTopicViewModel { Title = "Config セクション", IndentLevel = 1 });
            SuslTopics.Add(new HelpTopicViewModel { Title = "Default セクション", IndentLevel = 1 });
            SuslTopics.Add(new HelpTopicViewModel { Title = "Sequence セクション", IndentLevel = 1 });
            SuslTopics.Add(new HelpTopicViewModel { Title = "  Note (ノート)", IndentLevel = 2 });
            SuslTopics.Add(new HelpTopicViewModel { Title = "  Rest (休符)", IndentLevel = 2 });
            SuslTopics.Add(new HelpTopicViewModel { Title = "  制御コマンド", IndentLevel = 2 });
            SuslTopics.Add(new HelpTopicViewModel { Title = "  Parameters ブロック", IndentLevel = 2 });
            SuslTopics.Add(new HelpTopicViewModel { Title = "    Vibrato", IndentLevel = 3 });
            SuslTopics.Add(new HelpTopicViewModel { Title = "    PitchCurve", IndentLevel = 3 });
        }
    }
}