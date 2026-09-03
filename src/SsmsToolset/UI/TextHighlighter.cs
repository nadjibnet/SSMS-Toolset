using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SsmsToolset.Data;

namespace SsmsToolset.UI
{
    /// <summary>
    /// Attached behaviour that renders a Columns/Params <see cref="TokensProperty"/>
    /// list into a <see cref="TextBlock"/>:
    ///  - primary-key columns get a small <c>[pk]</c> marker and a bold name;
    ///  - foreign-key columns get a small <c>[fk]</c> marker;
    ///  - every case-insensitive occurrence of <see cref="TermProperty"/> gets a
    ///    yellow "selection" background (highlight, never a filter).
    /// Parameters (procedures/functions) carry no key flags, so they render plainly.
    /// </summary>
    public static class TextHighlighter
    {
        public static readonly DependencyProperty TokensProperty =
            DependencyProperty.RegisterAttached(
                "Tokens", typeof(IEnumerable<ColumnToken>), typeof(TextHighlighter),
                new PropertyMetadata(null, OnChanged));

        public static readonly DependencyProperty TermProperty =
            DependencyProperty.RegisterAttached(
                "Term", typeof(string), typeof(TextHighlighter),
                new PropertyMetadata(null, OnChanged));

        public static IEnumerable<ColumnToken> GetTokens(DependencyObject o) => (IEnumerable<ColumnToken>)o.GetValue(TokensProperty);
        public static void SetTokens(DependencyObject o, IEnumerable<ColumnToken> v) => o.SetValue(TokensProperty, v);
        public static string GetTerm(DependencyObject o) => (string)o.GetValue(TermProperty);
        public static void SetTerm(DependencyObject o, string v) => o.SetValue(TermProperty, v);

        // Soft yellow "selection" swatch with black text so it reads in both themes.
        private static readonly Brush HighlightBackground = MakeFrozen(Color.FromRgb(0xFF, 0xE1, 0x6A));
        private static readonly Brush HighlightForeground = Brushes.Black;

        // Key markers: amber for [pk], blue for [fk] — legible on dark and light.
        private static readonly Brush PrimaryKeyBrush = MakeFrozen(Color.FromRgb(0xE0, 0xA0, 0x30));
        private static readonly Brush ForeignKeyBrush = MakeFrozen(Color.FromRgb(0x4E, 0xA0, 0xD0));

        private const double MarkerFontSize = 9.0;

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

            var tokens = GetTokens(block);
            string term = GetTerm(block);

            block.Inlines.Clear();
            if (tokens == null)
            {
                return;
            }

            bool first = true;
            foreach (var token in tokens)
            {
                if (!first)
                {
                    block.Inlines.Add(new Run(", "));
                }
                first = false;

                if (token.IsPrimaryKey)
                {
                    block.Inlines.Add(Marker("[pk]", PrimaryKeyBrush));
                    block.Inlines.Add(new Run(" ")); // small space after the marker
                }
                else if (token.IsForeignKey)
                {
                    block.Inlines.Add(Marker("[fk]", ForeignKeyBrush));
                    block.Inlines.Add(new Run(" "));
                }

                AppendHighlighted(block, token.Name ?? string.Empty, term, token.IsPrimaryKey);
            }
        }

        private static Run Marker(string text, Brush brush) =>
            new Run(text) { Foreground = brush, FontSize = MarkerFontSize };

        /// <summary>
        /// Appends <paramref name="text"/> to the block, bolding it when
        /// <paramref name="bold"/> and giving each case-insensitive match of
        /// <paramref name="term"/> the yellow highlight.
        /// </summary>
        private static void AppendHighlighted(TextBlock block, string text, string term, bool bold)
        {
            FontWeight weight = bold ? FontWeights.Bold : FontWeights.Normal;

            if (string.IsNullOrEmpty(term))
            {
                block.Inlines.Add(new Run(text) { FontWeight = weight });
                return;
            }

            int start = 0;
            while (start <= text.Length)
            {
                int match = text.IndexOf(term, start, StringComparison.OrdinalIgnoreCase);
                if (match < 0)
                {
                    block.Inlines.Add(new Run(text.Substring(start)) { FontWeight = weight });
                    break;
                }

                if (match > start)
                {
                    block.Inlines.Add(new Run(text.Substring(start, match - start)) { FontWeight = weight });
                }

                block.Inlines.Add(new Run(text.Substring(match, term.Length))
                {
                    FontWeight = weight,
                    Background = HighlightBackground,
                    Foreground = HighlightForeground
                });

                start = match + term.Length;
            }
        }
    }
}
