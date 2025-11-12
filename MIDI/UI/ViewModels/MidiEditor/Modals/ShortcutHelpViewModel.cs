using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MIDI.UI.ViewModels.MidiEditor.Modals
{
    public class ShortcutHelpViewModel : ViewModelBase
    {
        public ObservableCollection<ShortcutCategory> ShortcutCategories { get; }

        public ShortcutHelpViewModel()
        {
            ShortcutCategories = new ObservableCollection<ShortcutCategory>
            {
                new ShortcutCategory("ファイル", new List<ShortcutItem>
                {
                    new ShortcutItem("Ctrl + N", "新規作成", "M19,13H15V11H19V13M19,17H15V15H19V17M15,21H19V19H15V21M5,3H17L21,7V19A2,2 0 0,1 19,21H5A2,2 0 0,1 3,19V5A2,2 0 0,1 5,3M14,8V4H6V8H14Z"),
                    new ShortcutItem("Ctrl + O", "MIDI 読み込み", "M20,6H12L10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6Z"),
                    new ShortcutItem("Ctrl + S", "MIDI 保存", "M15,9H5V5H15M12,19A3,3 0 0,1 9,16A3,3 0 0,1 12,13A3,3 0 0,1 15,16A3,3 0 0,1 12,19M17,3H5C3.89,3 3,3.9 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V7L17,3Z"),
                    new ShortcutItem("Ctrl + Shift + S", "MIDI 名前を付けて保存", "M17,3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V7L17,3M15,15H9V19H15V15M12,2A3,3 0 0,1 15,5H9A3,3 0 0,1 12,2Z"),
                    new ShortcutItem("Ctrl + E", "オーディオ書き出し", "M9,16V10H5L12,3L19,10H15V16H9M5,20V18H19V20H5Z")
                }),
                new ShortcutCategory("再生", new List<ShortcutItem>
                {
                    new ShortcutItem("Space", "再生/一時停止", "M8,5.14V19.14L19,12.14L8,5.14Z"),
                    new ShortcutItem("Alt + →", "次のフラグへ移動", "M16,18V6H18V18H16M6,18L14.5,12L6,6V18Z"),
                    new ShortcutItem("Alt + ←", "前のフラグへ移動", "M8,18V6H6V18H8M18,18V6L9.5,12L18,18Z")
                }),
                new ShortcutCategory("編集", new List<ShortcutItem>
                {
                    new ShortcutItem("Ctrl + Z", "元に戻す", "M13.5,2C9.91,2 6.89,4.43 5.94,7.86L4,7V13H10L8.03,11.03C8.88,8.58 11.23,7 14,7C17.87,7 21,10.13 21,14C21,17.87 17.87,21 14,21C11.1,21 9,19.39 8.12,17.15L6.3,17.94C7.45,20.9 10.45,23 14,23C19,23 23,19 23,14C23,9 19,2 13.5,2Z"),
                    new ShortcutItem("Ctrl + Y", "やり直し", "M10.5,2C6.91,2 3.89,4.43 2.94,7.86L1,7V13H7L5.03,11.03C5.88,8.58 8.23,7 11,7C14.87,7 18,10.13 18,14C18,17.87 14.87,21 11,21C8.1,21 6,19.39 5.12,17.15L3.3,17.94C4.45,20.9 7.45,23 11,23C16,23 20,19 20,14C20,9 16,2 10.5,2Z"),
                    new ShortcutItem("Ctrl + C", "コピー", "M19,21H8V7H19M19,5H8A2,2 0 0,0 6,7V21A2,2 0 0,0 8,23H19A2,2 0 0,0 21,21V7A2,2 0 0,0 19,5M16,1H4A2,2 0 0,0 2,3V17H4V3H16V1Z"),
                    new ShortcutItem("Ctrl + V", "貼り付け", "M19,21H8V7H19M19,5H8A2,2 0 0,0 6,7V21A2,2 0 0,0 8,23H19A2,2 0 0,0 21,21V7A2,2 0 0,0 19,5M16,1H4A2,2 0 0,0 2,3V17H4V3H16V1Z"),
                    new ShortcutItem("Delete", "削除", "M19,4H15.5L14.5,3H9.5L8.5,4H5V6H19M6,19A2,2 0 0,0 8,21H16A2,2 0 0,0 18,19V7H6V19Z"),
                    new ShortcutItem("Ctrl + A", "すべて選択", "M3,13H7V11H3M3,17H7V15H3M3,9H7V7H3M9,13H21V11H9M9,17H21V15H9M9,9H21V7H9M3,5H21V3H3V5Z")
                })
            };
        }
    }

    public class ShortcutCategory
    {
        public string CategoryName { get; }
        public List<ShortcutItem> Shortcuts { get; }

        public ShortcutCategory(string categoryName, List<ShortcutItem> shortcuts)
        {
            CategoryName = categoryName;
            Shortcuts = shortcuts;
        }
    }

    public class ShortcutItem
    {
        public string Key { get; }
        public string Description { get; }
        public string IconPath { get; }

        public ShortcutItem(string key, string description, string iconPath)
        {
            Key = key;
            Description = description;
            IconPath = iconPath;
        }
    }
}