using System.Text.RegularExpressions;
using Markdig;

namespace CloudNotes.Desktop.Services
{
    /// <summary>
    /// Сервис для конвертации Markdown в HTML с использованием Markdig.
    /// Поддерживает: headers, bold, italic, lists, spoiler.
    /// </summary>
    public class MarkdownConverter : IMarkdownConverter
    {
        private readonly MarkdownPipeline pipeline;

        // Regex для поиска spoiler синтаксиса ||текст||
        private static readonly Regex SpoilerRegex = new Regex(
            @"\|\|(.+?)\|\|",
            RegexOptions.Singleline | RegexOptions.Compiled);

        // Placeholders для защиты spoiler от Markdig
        private const string SpoilerStartPlaceholder = "%%SPOILER_START%%";
        private const string SpoilerEndPlaceholder = "%%SPOILER_END%%";

        // Regex для замены placeholders на HTML после Markdig
        private static readonly Regex PlaceholderRegex = new Regex(
            $@"{Regex.Escape(SpoilerStartPlaceholder)}(.+?){Regex.Escape(SpoilerEndPlaceholder)}",
            RegexOptions.Singleline | RegexOptions.Compiled);

        // CSS стили для spoiler эффекта
        private const string SpoilerStyles = "<style>.spoiler{background-color:#2a2a2a;color:#2a2a2a;border-radius:3px;padding:0 3px;cursor:pointer;}.spoiler:hover{background-color:#e0e0e0;color:#333;}</style>";

        public MarkdownConverter()
        {
            pipeline = new MarkdownPipelineBuilder()
                .Build();
        }

        /// <inheritdoc />
        public string ConvertToHtml(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return string.Empty;
            }

            // 1. Заменяем ||текст|| на placeholders (защита от Markdig)
            var withPlaceholders = SpoilerRegex.Replace(markdown, match =>
            {
                var text = match.Groups[1].Value;
                return $"{SpoilerStartPlaceholder}{text}{SpoilerEndPlaceholder}";
            });

            // 2. Конвертируем Markdown в HTML
            var html = Markdown.ToHtml(withPlaceholders, pipeline);

            // 3. Заменяем placeholders на HTML span
            html = PlaceholderRegex.Replace(html, match =>
            {
                var hiddenText = match.Groups[1].Value;
                return $"<span class=\"spoiler\">{hiddenText}</span>";
            });

            // 4. Добавляем стили если есть spoiler
            if (html.Contains("class=\"spoiler\""))
            {
                html = SpoilerStyles + html;
            }

            return html;
        }
    }
}

