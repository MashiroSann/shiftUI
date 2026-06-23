using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace shiftUI;

/// <summary>
/// 将 Markdown 文本转换为 WPF FlowDocument，支持基础语法
/// </summary>
public static class MarkdownParser
{
    private static readonly Color CodeBg = Color.FromRgb(0x31, 0x32, 0x44);
    private static readonly Color CodeFg = Color.FromRgb(0xF3, 0x8B, 0xA8);
    private static readonly SolidColorBrush TextBrush = new(Color.FromRgb(0xCD, 0xD6, 0xF4));
    private static readonly SolidColorBrush HeadingBrush = new(Color.FromRgb(0x89, 0xB4, 0xFA));
    private static readonly SolidColorBrush BoldBrush = new(Color.FromRgb(0xFB, 0xE3, 0x9F));
    private static readonly SolidColorBrush ItalicBrush = new(Color.FromRgb(0xA6, 0xE3, 0xA1));

    public static FlowDocument Parse(string? markdown)
    {
        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI, Microsoft YaHei, sans-serif"),
            FontSize = 14,
            Foreground = TextBrush,
            Background = Brushes.Transparent,
            PagePadding = new Thickness(16, 8, 16, 8),
        };

        if (string.IsNullOrEmpty(markdown))
        {
            doc.Blocks.Add(new Paragraph(new Run("（空文档）") { Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86)) }));
            return doc;
        }

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var paraLines = new List<string>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            // 空行 → 结束当前段落
            if (string.IsNullOrWhiteSpace(line))
            {
                if (paraLines.Count > 0)
                {
                    FlushParagraph(doc, paraLines);
                    paraLines.Clear();
                }
                continue;
            }

            // 标题行
            if (line.StartsWith("#"))
            {
                if (paraLines.Count > 0)
                {
                    FlushParagraph(doc, paraLines);
                    paraLines.Clear();
                }

                int level = 0;
                while (level < line.Length && level < 6 && line[level] == '#') level++;
                var headingText = line[level..].TrimStart();
                var heading = new Paragraph
                {
                    FontSize = 28 - level * 3,
                    FontWeight = FontWeights.Bold,
                    Foreground = HeadingBrush,
                    Margin = new Thickness(0, 6, 0, 2),
                };
                heading.Inlines.Add(new Run(headingText));
                doc.Blocks.Add(heading);
                continue;
            }

            // 普通行，收集到段落
            paraLines.Add(line);
        }

        // 最后一段
        if (paraLines.Count > 0)
            FlushParagraph(doc, paraLines);

        return doc;
    }

    private static void FlushParagraph(FlowDocument doc, List<string> lines)
    {
        var para = new Paragraph { Margin = new Thickness(0, 2, 0, 6), LineHeight = 1.5 };
        bool first = true;

        foreach (var line in lines)
        {
            if (!first)
                para.Inlines.Add(new LineBreak());
            first = false;
            ParseInline(para.Inlines, line);
        }

        doc.Blocks.Add(para);
    }

    private static void ParseInline(InlineCollection inlines, string text)
    {
        int i = 0;
        while (i < text.Length)
        {
            // 行内代码 `code`
            if (text[i] == '`')
            {
                int end = text.IndexOf('`', i + 1);
                if (end > i)
                {
                    var code = text.Substring(i + 1, end - i - 1);
                    inlines.Add(new Run(code)
                    {
                        FontFamily = new FontFamily("Consolas, Microsoft YaHei, monospace"),
                        Background = new SolidColorBrush(CodeBg),
                        Foreground = new SolidColorBrush(CodeFg),
                        FontSize = 13,
                    });
                    i = end + 1;
                    continue;
                }
            }

            // 加粗 **text**
            if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*')
            {
                int end = text.IndexOf("**", i + 2);
                if (end > i)
                {
                    var bold = text.Substring(i + 2, end - i - 2);
                    inlines.Add(new Run(bold) { FontWeight = FontWeights.Bold, Foreground = BoldBrush });
                    i = end + 2;
                    continue;
                }
            }

            // 斜体 *text*（不在行首/行尾的单词边界）
            if (text[i] == '*' && i + 1 < text.Length && text[i + 1] != '*')
            {
                int end = text.IndexOf('*', i + 1);
                if (end > i && end - i > 1)
                {
                    var italic = text.Substring(i + 1, end - i - 1);
                    inlines.Add(new Run(italic) { FontStyle = FontStyles.Italic, Foreground = ItalicBrush });
                    i = end + 1;
                    continue;
                }
            }

            // 普通字符
            inlines.Add(new Run(text[i].ToString()));
            i++;
        }
    }
}
