using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.EmphasisExtras;

namespace CloudNotes.Desktop.Services
{
    /// <summary>
    /// Сервис для конвертации Markdown в HTML с использованием Markdig.
    /// Поддерживает: headers, bold, italic, lists, spoiler, tables.
    /// Поддерживает: headers, bold, italic, lists, spoiler, flashcards.
    /// Локальные изображения file:// в превью подменяются на data URI для отображения.
    /// </summary>
    public class MarkdownConverter : IMarkdownConverter
    {
        private readonly MarkdownPipeline pipeline;

        // Regex для подстановки локальных изображений ![alt](file:///path) в data URI для превью
        private static readonly Regex FileImageRegex = new Regex(
            @"!\[([^\]]*)\]\((file:///[^)]+)\)",
            RegexOptions.Compiled);

        // Regex для поиска spoiler синтаксиса ||текст||
        // Не захватывает таблицы - spoiler должен быть в одной строке или не содержать структуру таблицы
        // Ограничиваем spoiler одной строкой или небольшим блоком без переносов строк, начинающихся с |
        private static readonly Regex SpoilerRegex = new Regex(
            @"\|\|([^\r\n]+?)\|\|",
            RegexOptions.Compiled);

        // Regex для поиска flashcard синтаксиса ??вопрос::ответ??
        private static readonly Regex FlashcardRegex = new Regex(
            @"\?\?(.+?)::(.+?)\?\?",
            RegexOptions.Singleline | RegexOptions.Compiled);

        // Placeholders для защиты от Markdig
        private const string SpoilerStartPlaceholder = "%%SPOILER_START%%";
        private const string SpoilerEndPlaceholder = "%%SPOILER_END%%";
        private const string FlashcardPlaceholder = "%%FLASHCARD_{0}_{1}%%";

        // Regex для замены placeholders на HTML после Markdig
        private static readonly Regex SpoilerPlaceholderRegex = new Regex(
            $@"{Regex.Escape(SpoilerStartPlaceholder)}(.+?){Regex.Escape(SpoilerEndPlaceholder)}",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex FlashcardPlaceholderRegex = new Regex(
            @"%%FLASHCARD_(.+?)_(.+?)%%",
            RegexOptions.Singleline | RegexOptions.Compiled);

        // CSS стили
        private const string Styles = @"<style>
.spoiler{background-color:#2a2a2a;color:#2a2a2a;border-radius:3px;padding:0 3px;cursor:pointer;}
.spoiler:hover{background-color:#e0e0e0;color:#333;}
.flashcard{border:1px solid #ccc;border-radius:8px;padding:16px 20px;margin:20px auto 25px auto;background:#f9f9f9;width:50%;text-align:center;}
.flashcard-q{font-weight:bold;color:#333;margin-bottom:10px;font-size:1.1em;}
.flashcard-q::before{content:'Q: ';color:#666;}
.flashcard-divider{border-top:1px solid #ddd;margin:10px 0;}
.flashcard-a{color:#333;}
.flashcard-a::before{content:'A: ';color:#666;font-weight:bold;}
.flashcard-a .spoiler{background-color:#2a2a2a;color:#2a2a2a;}
.flashcard-a .spoiler:hover{background-color:#e0e0e0;color:#2E7D32;}
</style>";

        public MarkdownConverter()
        {
            pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UsePipeTables()  // Tables support
                .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)  // ~~strikethrough~~
                .UseTaskLists()  // - [ ] and - [x] task lists
                .Build();
        }

        /// <inheritdoc />
        public string ConvertToHtml(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return string.Empty;
            }

            // 0. Локальные изображения file:// -> data URI, чтобы превью их показывало (WebView не грузит file://)
            markdown = FileImageRegex.Replace(markdown, match =>
            {
                var alt = match.Groups[1].Value;
                var fileUrl = match.Groups[2].Value;
                try
                {
                    var localPath = new Uri(fileUrl).LocalPath;
                    if (!System.IO.File.Exists(localPath))
                        return match.Value;
                    var bytes = System.IO.File.ReadAllBytes(localPath);
                    var base64 = Convert.ToBase64String(bytes);
                    var ext = System.IO.Path.GetExtension(localPath).ToLowerInvariant();
                    var mime = ext switch
                    {
                        ".png" => "image/png",
                        ".jpg" or ".jpeg" => "image/jpeg",
                        ".gif" => "image/gif",
                        ".bmp" => "image/bmp",
                        ".webp" => "image/webp",
                        _ => "image/png"
                    };
                    return $"![{alt}](data:{mime};base64,{base64})";
                }
                catch
                {
                    return match.Value;
                }
            });

            // 1. Заменяем ??вопрос::ответ?? на placeholders
            var withPlaceholders = FlashcardRegex.Replace(markdown, match =>
            {
                var question = match.Groups[1].Value.Trim();
                var answer = match.Groups[2].Value.Trim();
                // Кодируем специальные символы для безопасной передачи через placeholder
                var encodedQ = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(question));
                var encodedA = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(answer));
                return $"%%FLASHCARD_{encodedQ}_{encodedA}%%";
            });

            // 2. Заменяем ||текст|| на placeholders
            withPlaceholders = SpoilerRegex.Replace(withPlaceholders, match =>
            {
                var text = match.Groups[1].Value;
                return $"{SpoilerStartPlaceholder}{text}{SpoilerEndPlaceholder}";
            });

            // 3. Конвертируем Markdown в HTML
            var html = Markdown.ToHtml(withPlaceholders, pipeline);

            // 4. Заменяем flashcard placeholders на HTML
            html = FlashcardPlaceholderRegex.Replace(html, match =>
            {
                var question = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(match.Groups[1].Value));
                var answer = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(match.Groups[2].Value));
                return $"<div class=\"flashcard\"><div class=\"flashcard-q\">{System.Net.WebUtility.HtmlEncode(question)}</div><div class=\"flashcard-divider\"></div><div class=\"flashcard-a\"><span class=\"spoiler\">{System.Net.WebUtility.HtmlEncode(answer)}</span></div></div>";
            });

            // 5. Заменяем spoiler placeholders на HTML span
            html = SpoilerPlaceholderRegex.Replace(html, match =>
            {
                var hiddenText = match.Groups[1].Value;
                return $"<span class=\"spoiler\">{hiddenText}</span>";
            });

            // 4. Добавляем стили если есть spoiler, таблицы или изображения
            var styles = string.Empty;
            if (html.Contains("class=\"spoiler\"") || html.Contains("class=\"flashcard\""))
            {
                styles += Styles;
            }
            if (html.Contains("<table>") || html.Contains("<table "))
            {
                styles += "<style>table{border-collapse:collapse;width:100%;margin:10px 0;}th,td{border:1px solid #ddd;padding:8px;text-align:left;}th{background-color:#f2f2f2;font-weight:bold;}</style>";
            }
            if (html.Contains("<img") || html.Contains("data:image"))
            {
                styles += "<style>img{max-width:100%;height:auto;display:block;margin:10px 0;}</style>";
            }
            if (!string.IsNullOrEmpty(styles))
            {
                html = styles + html;
            }

            return html;
        }
    }
}

