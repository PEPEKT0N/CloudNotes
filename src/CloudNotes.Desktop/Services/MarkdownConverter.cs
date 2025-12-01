using Markdig;

namespace CloudNotes.Desktop.Services
{
    /// <summary>
    /// Сервис для конвертации Markdown в HTML с использованием Markdig.
    /// Поддерживает: headers, bold, italic, lists.
    /// </summary>
    public class MarkdownConverter : IMarkdownConverter
    {
        private readonly MarkdownPipeline pipeline;

        public MarkdownConverter()
        {
            // Базовый пайплайн Markdig уже включает поддержку:
            // - Headers (# ## ### и т.д.)
            // - Bold (**text** или __text__)
            // - Italic (*text* или _text_)
            // - Lists (ordered и unordered)
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

            return Markdown.ToHtml(markdown, pipeline);
        }
    }
}

