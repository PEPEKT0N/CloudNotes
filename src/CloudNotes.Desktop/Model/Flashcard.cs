namespace CloudNotes.Desktop.Model
{
    /// <summary>
    /// Карточка для интервального повторения (вопрос-ответ).
    /// Извлекается из текста заметки по синтаксису ??вопрос::ответ??
    /// </summary>
    public class Flashcard
    {
        /// <summary>
        /// Вопрос карточки.
        /// </summary>
        public string Question { get; set; } = string.Empty;

        /// <summary>
        /// Ответ на вопрос.
        /// </summary>
        public string Answer { get; set; } = string.Empty;

        /// <summary>
        /// Позиция начала карточки в исходном тексте (для редактирования).
        /// </summary>
        public int StartIndex { get; set; }

        /// <summary>
        /// Позиция конца карточки в исходном тексте.
        /// </summary>
        public int EndIndex { get; set; }

        public Flashcard() { }

        public Flashcard(string question, string answer)
        {
            Question = question;
            Answer = answer;
        }
    }
}
