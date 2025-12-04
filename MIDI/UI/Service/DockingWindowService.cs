using AvalonDock;
using AvalonDock.Layout;
using MIDI.UI.Interface;
using System;
using System.Reflection;
using System.Windows;

namespace MIDI.UI.Services
{
    public class DockingWindowService : IDockingWindowService
    {
        public void ShowWindow(UIElement content, string title, double width, double height)
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null) return;

            var dockingManagerField = mainWindow.GetType().GetField("docker", BindingFlags.Instance | BindingFlags.NonPublic);
            if (dockingManagerField != null && dockingManagerField.GetValue(mainWindow) is DockingManager dockingManager)
            {
                CreateFloating(dockingManager, content, title, width, height);
            }
            else
            {
                var win = new Window
                {
                    Title = title,
                    Content = content,
                    Width = width,
                    Height = height,
                    Owner = mainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                win.Show();
            }
        }

        private void CreateFloating(DockingManager manager, UIElement content, string title, double minWidth, double minHeight)
        {
            var layout = manager.Layout;
            var anchorable = new LayoutAnchorable
            {
                Title = title,
                Content = content
            };

            var pane = new LayoutAnchorablePane(anchorable)
            {
                DockMinHeight = minHeight,
                DockMinWidth = minWidth
            };

            var floatingWindow = new LayoutAnchorableFloatingWindow
            {
                RootPanel = new LayoutAnchorablePaneGroup(pane),
                Parent = layout
            };

            layout.FloatingWindows.Add(floatingWindow);

            manager.UpdateLayout();
            anchorable.Float();
            anchorable.IsActive = true;
        }
    }
}