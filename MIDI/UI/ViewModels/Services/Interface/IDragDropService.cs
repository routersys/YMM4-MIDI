using System.Threading.Tasks;
using System.Windows;

namespace MIDI.UI.ViewModels.Services
{
    public interface IDragDropService
    {
        (bool, string) CanHandleDrop(DragEventArgs e);
        Task<bool> HandleDropAsync(IDataObject dataObject);
    }
}