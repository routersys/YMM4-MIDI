using MIDI.UI.ViewModels.MidiEditor;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MIDI.UI.Views.MidiEditor.Panel
{
    public partial class MetronomePanel : UserControl
    {
        private bool _isDraggingWeight = false;
        private Point _dragStartPoint;
        private double _initialWeightPosition;

        public MetronomePanel()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MetronomeViewModel oldVm)
            {
                oldVm.PropertyChanged -= OnViewModelPropertyChanged;
            }
            if (e.NewValue is MetronomeViewModel newVm)
            {
                newVm.PropertyChanged += OnViewModelPropertyChanged;
                UpdatePendulumAnimation(newVm);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (DataContext is MetronomeViewModel vm)
            {
                if (e.PropertyName == nameof(MetronomeViewModel.IsEnabled))
                {
                    UpdatePendulumAnimation(vm);
                }
                else if (e.PropertyName == nameof(MetronomeViewModel.PendulumDuration) && vm.IsEnabled)
                {
                    UpdatePendulumAnimation(vm);
                }
            }
        }

        private void UpdatePendulumAnimation(MetronomeViewModel vm)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (vm.IsEnabled)
                {
                    var animation = new DoubleAnimation
                    {
                        From = -30,
                        To = 30,
                        Duration = TimeSpan.FromSeconds(vm.PendulumDuration / 2),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever,
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                    };
                    PendulumRotation.BeginAnimation(RotateTransform.AngleProperty, animation);
                }
                else
                {
                    PendulumRotation.BeginAnimation(RotateTransform.AngleProperty, null);
                    PendulumRotation.Angle = 0;
                }
            }));
        }

        private void BPM_TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && DataContext is MetronomeViewModel vm)
            {
                vm.IsTempoInEditMode = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    BPM_TextBox.Focus();
                    BPM_TextBox.SelectAll();
                }));
            }
        }

        private void BPM_TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is MetronomeViewModel vm)
            {
                vm.IsTempoInEditMode = false;
            }
        }

        private void BPM_TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is MetronomeViewModel vm)
                {
                    var binding = (sender as TextBox)?.GetBindingExpression(TextBox.TextProperty);
                    binding?.UpdateSource();
                    vm.IsTempoInEditMode = false;
                }
            }
            else if (e.Key == Key.Escape)
            {
                if (DataContext is MetronomeViewModel vm)
                {
                    vm.IsTempoInEditMode = false;
                }
            }
        }

        private void PendulumWeight_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement weight && DataContext is MetronomeViewModel vm)
            {
                _isDraggingWeight = true;
                _dragStartPoint = e.GetPosition(this);
                _initialWeightPosition = vm.WeightPosition;
                weight.CaptureMouse();
                e.Handled = true;
            }
        }

        private void PendulumWeight_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingWeight && DataContext is MetronomeViewModel vm)
            {
                var currentPoint = e.GetPosition(this);
                var deltaY = currentPoint.Y - _dragStartPoint.Y;
                vm.WeightPosition = _initialWeightPosition + deltaY;
            }
        }

        private void PendulumWeight_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingWeight)
            {
                _isDraggingWeight = false;
                (sender as IInputElement)?.ReleaseMouseCapture();
            }
        }
    }
}