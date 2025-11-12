using MidiPlugin.Services;
using MidiPlugin.ViewModels;
using System.Windows;

namespace MidiPlugin
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var configService = new ConfigService();
            configService.RegisterCurrentExecutable();

            if (e.Args.Length > 0)
            {
                var fileAssociationService = new FileAssociationService();
                string arg = e.Args[0].ToLower();

                if (arg == "/register")
                {
                    fileAssociationService.Associate();
                    Application.Current.Shutdown();
                    return;
                }
                else if (arg == "/unregister")
                {
                    fileAssociationService.Disassociate();
                    Application.Current.Shutdown();
                    return;
                }
            }

            var mainWindow = new MainWindow
            {
                DataContext = new MainViewModel(e.Args)
            };

            mainWindow.Show();
        }
    }
}