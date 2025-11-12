using AvalonDock;
using AvalonDock.Layout;
using MIDI.UI.Views.MidiEditor.Panel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MIDI.UI.Core
{
    public static class LayoutInitializer
    {
        public static void EnsurePanelsExist(DockingManager dockingManager)
        {
            var requiredPanels = new Dictionary<string, (string Title, object Content)>
            {
                { "noteEditor", ("編集", new NoteEditorPanel()) },
                { "quantizeSettings", ("クオンタイズ設定", new QuantizeSettingsPanel()) },
                { "playbackSettings", ("再生設定", new PlaybackSettingsPanel()) },
                { "metronome", ("メトロノーム", new MetronomePanel()) },
                { "tempoEditor", ("テンポ", new TempoEditorPanel()) },
                { "controlChangeEditor", ("コントロールチェンジ", new ControlChangeEditorPanel()) }
            };

            var layout = dockingManager.Layout;
            if (layout?.RootPanel == null) return;

            var currentPanels = new HashSet<string>(layout.Descendents().OfType<LayoutAnchorable>().Select(a => a.ContentId));

            var mainLayoutPanel = layout.RootPanel.Children.OfType<LayoutPanel>().FirstOrDefault();
            if (mainLayoutPanel == null) return;

            var targetGroup = mainLayoutPanel.Children.OfType<LayoutAnchorablePaneGroup>().FirstOrDefault(g => g.Orientation == Orientation.Vertical);
            if (targetGroup == null)
            {
                targetGroup = new LayoutAnchorablePaneGroup { Orientation = Orientation.Vertical, DockWidth = new GridLength(250) };

                var existingPanes = mainLayoutPanel.Children.OfType<LayoutAnchorablePane>().ToList();
                if (existingPanes.Any())
                {
                    var parent = existingPanes.First().Parent as ILayoutContainer;
                    parent?.RemoveChild(existingPanes.First());
                    targetGroup.Children.Add(existingPanes.First());
                }

                mainLayoutPanel.Children.Add(targetGroup);
            }

            foreach (var panelInfo in requiredPanels)
            {
                if (!currentPanels.Contains(panelInfo.Key))
                {
                    var content = panelInfo.Value.Content;
                    if (content == null)
                    {
                        var existingContent = layout.Descendents().OfType<LayoutContent>().FirstOrDefault(lc => lc.ContentId == panelInfo.Key);
                        if (existingContent != null)
                        {
                            content = existingContent.Content;
                        }
                    }

                    if (content == null && (panelInfo.Key == "pianoKeys" || panelInfo.Key == "pianoRoll"))
                    {
                        continue;
                    }

                    var newAnchorable = new LayoutAnchorable
                    {
                        ContentId = panelInfo.Key,
                        Title = panelInfo.Value.Title,
                        Content = content
                    };

                    var targetPane = targetGroup.Children.OfType<LayoutAnchorablePane>().LastOrDefault();
                    if (targetPane == null)
                    {
                        targetPane = new LayoutAnchorablePane();
                        targetGroup.Children.Add(targetPane);
                    }
                    targetPane.Children.Add(newAnchorable);

                    newAnchorable.Hide();
                }
            }
        }
    }
}