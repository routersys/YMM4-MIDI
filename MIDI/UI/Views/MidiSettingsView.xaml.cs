using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MIDI.UI.ViewModels;
using MIDI.UI.ViewModels.Models;
using MIDI.UI.Views;

namespace MIDI
{
    public partial class MidiSettingsView : UserControl
    {
        private bool _wizardShown = false;

        public MidiSettingsView()
        {
            InitializeComponent();
            DataContext = new MidiSettingsViewModel();
            this.AllowDrop = true;
            this.Loaded += MidiSettingsView_Loaded;
        }

        private void MidiSettingsView_Loaded(object sender, RoutedEventArgs e)
        {

            if (!_wizardShown)
            {
                ShowWizardIfFirstLaunch();
            }
        }

        private void ShowWizardIfFirstLaunch()
        {
            var config = MidiConfiguration.Default;
            if (config.IsFirstLaunch)
            {
                _wizardShown = true;
                var wizardViewModel = new WizardViewModel(config);
                var wizardWindow = new WizardWindow(wizardViewModel)
                {
                    Owner = Window.GetWindow(this),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                wizardWindow.ShowDialog();


            }
        }


        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            if (DataContext is MidiSettingsViewModel vm)
            {
                vm.DragOverCommand.Execute(e);
            }
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);
            if (DataContext is MidiSettingsViewModel vm)
            {
                vm.DragOverCommand.Execute(e);
            }
        }

        protected override void OnDragLeave(DragEventArgs e)
        {
            base.OnDragLeave(e);
            if (DataContext is MidiSettingsViewModel vm)
            {
                vm.DragLeaveCommand.Execute(null);
            }
        }

        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);
            if (DataContext is MidiSettingsViewModel vm)
            {
                vm.DropCommand.Execute(e.Data);
            }
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.DataContext is SoundFontFileViewModel vm)
            {
                if (DataContext is MidiSettingsViewModel viewModel && viewModel.EditSoundFontRuleCommand.CanExecute(vm))
                {
                    viewModel.EditSoundFontRuleCommand.Execute(vm);
                }
            }
        }

        private T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t)
                {
                    return t;
                }
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void MidiSettingsView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var tabPanel = FindVisualChild<TabPanel>(MainTabControl);

            if (e.NewSize.Width < 750)
            {
                VisualStateManager.GoToState(this, "Compact", true);
                if (tabPanel != null)
                {
                    tabPanel.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                VisualStateManager.GoToState(this, "Wide", true);
                if (tabPanel != null)
                {
                    tabPanel.Visibility = Visibility.Visible;
                }
                MenuPopup.IsOpen = false;
            }
            UpdatePresetPanelLayout(e.NewSize.Width);
        }

        private void UpdatePresetPanelLayout(double currentWidth)
        {
            if (PresetActionsPanel == null || PresetGrid == null || PresetComboBox == null) return;

            bool isTwoRowLayout = Grid.GetRow(PresetActionsPanel) == 1;
            const double threshold = 550;

            if (currentWidth < threshold)
            {
                if (!isTwoRowLayout)
                {
                    PresetGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Auto);
                    Grid.SetColumnSpan(PresetComboBox, 2);
                    Grid.SetRow(PresetActionsPanel, 1);
                    Grid.SetColumn(PresetActionsPanel, 0);
                    Grid.SetColumnSpan(PresetActionsPanel, 2);
                    PresetActionsPanel.Margin = new Thickness(0, 5, 0, 0);
                }
            }
            else
            {
                if (isTwoRowLayout)
                {
                    PresetGrid.RowDefinitions[1].Height = new GridLength(0);
                    Grid.SetColumnSpan(PresetComboBox, 1);
                    Grid.SetRow(PresetActionsPanel, 0);
                    Grid.SetColumn(PresetActionsPanel, 1);
                    Grid.SetColumnSpan(PresetActionsPanel, 1);
                    PresetActionsPanel.Margin = new Thickness(0);
                }
            }
        }

        private void TabListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MenuPopup.IsOpen = false;
        }

        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.ScrollToEnd();
            }
        }
    }

    public class EnumDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            var type = value.GetType();
            if (!type.IsEnum) return value.ToString()!;

            var member = type.GetMember(value.ToString()!).FirstOrDefault();
            var description = member?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? value.ToString()!;
            return description;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class IntListToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<int> intList)
            {
                return string.Join(",", intList);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                var intList = str.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(s => int.TryParse(s.Trim(), out var i) ? i : (int?)null)
                                 .Where(i => i.HasValue)
                                 .Select(i => i!.Value);

                if (targetType == typeof(ObservableCollection<int>))
                {
                    return new ObservableCollection<int>(intList);
                }
                return new List<int>(intList);
            }

            if (targetType == typeof(ObservableCollection<int>))
            {
                return new ObservableCollection<int>();
            }
            return new List<int>();
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}