using System;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;
using MIDI.Voice.ViewModels;

namespace MIDI.Voice.Views
{
    public partial class NoteNotationHelpControl : UserControl, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public NoteNotationHelpControl()
        {
            InitializeComponent();
            DataContextChanged += NoteNotationHelpControl_DataContextChanged;
        }

        private void NoteNotationHelpControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is NoteNotationHelpViewModel oldVm)
            {
                oldVm.BeginEdit -= Vm_BeginEdit;
                oldVm.EndEdit -= Vm_EndEdit;
            }
            if (e.NewValue is NoteNotationHelpViewModel newVm)
            {
                newVm.BeginEdit += Vm_BeginEdit;
                newVm.EndEdit += Vm_EndEdit;
            }
        }

        private void Vm_BeginEdit(object? sender, EventArgs e)
        {
            BeginEdit?.Invoke(this, e);
        }

        private void Vm_EndEdit(object? sender, EventArgs e)
        {
            EndEdit?.Invoke(this, e);
        }
    }
}