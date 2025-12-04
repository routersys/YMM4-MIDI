using System.Windows;

namespace MIDI.UI.Interface
{
    public interface IDockingWindowService
    {
        void ShowWindow(UIElement content, string title, double width, double height);
    }
}