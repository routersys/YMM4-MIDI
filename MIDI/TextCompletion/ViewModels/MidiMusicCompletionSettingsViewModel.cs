using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using MIDI.TextCompletion.Models;
using MIDI.TextCompletion.Services;
using MIDI.TextCompletion;
using YukkuriMovieMaker.Controls;

namespace MIDI.TextCompletion.ViewModels
{
    public class MidiMusicCompletionSettingsViewModel : ViewModelBase
    {
        public MidiMusicCompletionSettings Settings { get; }
        private readonly ITerminologyPersistence _persistenceService;

        private readonly ObservableCollection<MusicTerm> MasterTermsList;
        public ObservableCollection<MusicTerm> FilteredTerms { get; }

        private MusicTerm? selectedTerm;
        public MusicTerm? SelectedTerm
        {
            get => selectedTerm;
            set
            {
                if (Set(ref selectedTerm, value))
                {
                    DeleteTermCommand.RaiseCanExecuteChanged();
                    RaisePropertyChanged(nameof(IsTermSelected));
                    RaisePropertyChanged(nameof(IsTermNotSelected));
                }
            }
        }

        public bool IsTermSelected => SelectedTerm != null;
        public bool IsTermNotSelected => SelectedTerm == null;

        private string searchTerm = string.Empty;
        public string SearchTerm
        {
            get => searchTerm;
            set
            {
                if (Set(ref searchTerm, value))
                {
                    FilterTerms();
                }
            }
        }

        public DelegateCommand AddTermCommand { get; }
        public DelegateCommand DeleteTermCommand { get; }
        public DelegateCommand ResetTermsCommand { get; }
        public DelegateCommand ImportTermsCommand { get; }
        public DelegateCommand ExportTermsCommand { get; }
        public DelegateCommand ClearSearchCommand { get; }

        public MidiMusicCompletionSettingsViewModel(MidiMusicCompletionSettings settings)
        {
            Settings = settings;
            _persistenceService = new JsonTerminologyPersistence();

            MasterTermsList = Settings.TermsList;
            FilteredTerms = new ObservableCollection<MusicTerm>();

            MasterTermsList.CollectionChanged += (s, e) => FilterTerms();
            FilterTerms();

            AddTermCommand = new DelegateCommand(() =>
            {
                var newTerm = new MusicTerm { JapaneseName = "新規用語" };
                MasterTermsList.Add(newTerm);
                Settings.SaveTermsListToFile();
                SelectedTerm = newTerm;
            });

            DeleteTermCommand = new DelegateCommand(() =>
            {
                if (SelectedTerm != null)
                {
                    if (MessageBox.Show($"「{SelectedTerm.JapaneseName}」を削除しますか？", "削除の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        MasterTermsList.Remove(SelectedTerm);
                        Settings.SaveTermsListToFile();
                        SelectedTerm = null;
                    }
                }
            },
            () => SelectedTerm != null);

            ResetTermsCommand = new DelegateCommand(() =>
            {
                if (MessageBox.Show("用語集をデフォルト（初期状態）に戻します。\n現在の編集内容は失われますが、よろしいですか？", "リセットの確認", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    var defaultTerms = TerminologyData.GetDefaultTerms();
                    MasterTermsList.Clear();
                    foreach (var term in defaultTerms)
                    {
                        MasterTermsList.Add(term);
                    }
                    Settings.SaveTermsListToFile();
                }
            });

            ImportTermsCommand = new DelegateCommand(() =>
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "JSONファイル (*.json)|*.json|すべてのファイル (*.*)|*.*",
                    Title = "用語集のインポート"
                };
                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        var importedTerms = _persistenceService.Load(dialog.FileName);
                        if (importedTerms != null)
                        {
                            if (MessageBox.Show($"既存の用語集をクリアして、{importedTerms.Count}件の用語をインポートしますか？\n（既存の用語集に追加する場合は「いいえ」を選択してください）", "インポートの確認", MessageBoxButton.YesNoCancel, MessageBoxImage.Question) is MessageBoxResult result && result != MessageBoxResult.Cancel)
                            {
                                if (result == MessageBoxResult.Yes)
                                {
                                    MasterTermsList.Clear();
                                }
                                foreach (var term in importedTerms)
                                {
                                    MasterTermsList.Add(term);
                                }
                                Settings.SaveTermsListToFile();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"インポートに失敗しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            });

            ExportTermsCommand = new DelegateCommand(() =>
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "JSONファイル (*.json)|*.json",
                    Title = "用語集のエクスポート",
                    FileName = "MidiMusicTerminology.json"
                };
                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        _persistenceService.Save(dialog.FileName, MasterTermsList.ToList());
                        MessageBox.Show("用語集をエクスポートしました。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"エクスポートに失敗しました。\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            });

            ClearSearchCommand = new DelegateCommand(() => SearchTerm = string.Empty);
        }

        private void FilterTerms()
        {
            if (MasterTermsList == null) return;

            var selectedItem = SelectedTerm;
            FilteredTerms.Clear();
            var query = SearchTerm.ToLower();

            var filtered = string.IsNullOrWhiteSpace(query)
                ? MasterTermsList
                : MasterTermsList.Where(t =>
                    t.JapaneseName.ToLower().Contains(query) ||
                    t.EnglishName.ToLower().Contains(query) ||
                    t.Description.ToLower().Contains(query) ||
                    t.AliasText.ToLower().Contains(query));

            foreach (var term in filtered.OrderBy(t => t.JapaneseName))
            {
                FilteredTerms.Add(term);
            }

            if (selectedItem != null && FilteredTerms.Contains(selectedItem))
            {
                SelectedTerm = selectedItem;
            }
        }
    }
}