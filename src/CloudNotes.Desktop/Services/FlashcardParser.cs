using System.Collections.Generic;
using System.Text.RegularExpressions;
using CloudNotes.Desktop.Model;

namespace CloudNotes.Desktop.Services
{
    /// <summary>
    /// Парсер для извлечения карточек (flashcards) из текста заметки.
    /// Синтаксис: ??вопрос::ответ??
    /// </summary>
    public static class FlashcardParser
    {
        // Regex для поиска карточек: ??вопрос::ответ??
        // Поддерживает многострочный текст
        private static readonly Regex FlashcardRegex = new Regex(
            @"\?\?(.+?)::(.+?)\?\?",
            RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// Извлекает все карточки из текста.
        /// </summary>
        /// <param name="text">Текст заметки в формате Markdown.</param>
        /// <returns>Список найденных карточек.</returns>
        public static List<Flashcard> Parse(string text)
        {
            var flashcards = new List<Flashcard>();

            if (string.IsNullOrEmpty(text))
                return flashcards;

            var matches = FlashcardRegex.Matches(text);

            foreach (Match match in matches)
            {
                var question = match.Groups[1].Value.Trim();
                var answer = match.Groups[2].Value.Trim();

                if (!string.IsNullOrEmpty(question) && !string.IsNullOrEmpty(answer))
                {
                    flashcards.Add(new Flashcard
                    {
                        Question = question,
                        Answer = answer,
                        StartIndex = match.Index,
                        EndIndex = match.Index + match.Length
                    });
                }
            }

            return flashcards;
        }

        /// <summary>
        /// Проверяет, содержит ли текст карточки.
        /// </summary>
        public static bool HasFlashcards(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            return FlashcardRegex.IsMatch(text);
        }

        /// <summary>
        /// Возвращает количество карточек в тексте.
        /// </summary>
        public static int CountFlashcards(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            return FlashcardRegex.Matches(text).Count;
        }
    }
}
