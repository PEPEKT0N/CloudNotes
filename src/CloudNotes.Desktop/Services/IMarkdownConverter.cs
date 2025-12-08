namespace CloudNotes.Desktop.Services
{
    /// <summary>
    /// Интерфейс для конвертации Markdown в HTML.
    /// </summary>
    public interface IMarkdownConverter
    {
        /// <summary>
        /// Конвертирует Markdown-текст в HTML.
        /// </summary>
        /// <param name="markdown">Исходный текст в формате Markdown.</param>
        /// <returns>HTML-строка.</returns>
        string ConvertToHtml(string markdown);
    }
}

