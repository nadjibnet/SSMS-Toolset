using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SsmsToolset.UI
{
    /// <summary>
    /// Attached behaviour that renders <see cref="SourceTextProperty"/> into a
    /// <see cref="TextBlock"/>, giving every case-insensitive occurrence of
    /// <see cref="TermProperty"/> a yellow "selection" background. Used by the
    /// Columns/Params column so its header search box highlights (rather than
    /// filters) matching text in every cell.
    /// </summary>
    public static class TextHighlighter
    {
        public static readonly DependencyProperty SourceTextProperty =
            DependencyProperty.RegisterAttached(
                "SourceText", typeof(string), typeof(TextHighlighter),
                new PropertyMetadata(null, OnChanged));

        public static readonly DependencyProperty TermProperty =
            DependencyProperty.RegisterAttached(
                "Term", typeof(string), typeof(TextHighlighter),
                new PropertyMetadata(null, OnChanged));

        public static string GetSourceText(DependencyObject o) => (string)o.GetValue(SourceTextProperty);
        public static void SetSourceText(DependencyObject o, string v) => o.SetValue(SourceTextProperty, v);
        public static string GetTerm(DependencyObject o) => (string)o.GetValue(TermProperty);
        public static void SetTerm(DependencyObject o, string v) => o.SetValue(TermProperty, v);

        // Soft yellow "selection" swatch with black text so it reads in both themes.
        private static readonly Brush HighlightBackground = MakeFrozen(Color.FromRgb(0xFF, 0xE1, 0x6A));
        private static readonly Brush HighlightForeground = Brushes.Black;

        private static Brush MakeFrozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }
            return brush;
        }

        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is TextBlock block))
            {
                return;
            }

            string text = GetSourceText(block) ?? string.Empty;
            string term = GetTerm(block);

            block.Inlines.Clear();

            if (string.IsNullOrEmpty(term))
            {
                block.Inlines.Add(new Run(text));
                return;
            }

            int start = 0;
            while (start <= text.Length)
            {
                int match = text.IndexOf(term, start, StringComparison.OrdinalIgnoreCase);
                if (match < 0)
                {
                    block.Inlines.Add(new Run(text.Substring(start)));
                    break;
                }

                if (match > start)
                {
                    block.Inlines.Add(new Run(text.Substring(start, match - start)));
                }

                block.Inlines.Add(new Run(text.Substring(match, term.Length))
                {
                    Background = HighlightBackground,
                    Foreground = HighlightForeground
                });

                start = match + term.Length;
            }
        }
    }
}
