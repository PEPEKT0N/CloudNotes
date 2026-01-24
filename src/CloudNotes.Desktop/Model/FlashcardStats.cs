using System;
using System.ComponentModel.DataAnnotations;

namespace CloudNotes.Desktop.Model
{
    /// <summary>
    /// Статистика карточки для алгоритма интервального повторения SM-2.
    /// </summary>
    public class FlashcardStats
    {
        /// <summary>
        /// Уникальный идентификатор статистики.
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Email пользователя (для привязки статистики к пользователю).
        /// Для гостевого режима — пустая строка.
        /// </summary>
        [Required]
        public string UserEmail { get; set; } = string.Empty;

        /// <summary>
        /// ID заметки, к которой принадлежит карточка.
        /// </summary>
        public Guid NoteId { get; set; }

        /// <summary>
        /// Хеш вопроса для идентификации карточки.
        /// Используется для связи с карточкой в тексте заметки.
        /// </summary>
        public string QuestionHash { get; set; } = string.Empty;

        /// <summary>
        /// Фактор лёгкости (EaseFactor) — отражает насколько легко запоминается карточка.
        /// Начальное значение: 2.5. Минимум: 1.3.
        /// </summary>
        public double EaseFactor { get; set; } = 2.5;

        /// <summary>
        /// Текущий интервал повторения в днях.
        /// </summary>
        public int IntervalDays { get; set; } = 0;

        /// <summary>
        /// Количество успешных повторений подряд.
        /// Сбрасывается при неправильном ответе (оценка < 3).
        /// </summary>
        public int RepetitionCount { get; set; } = 0;

        /// <summary>
        /// Дата следующего повторения.
        /// </summary>
        public DateTime NextReviewDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Дата последнего повторения.
        /// </summary>
        public DateTime? LastReviewDate { get; set; }

        /// <summary>
        /// Общее количество повторений (включая неудачные).
        /// </summary>
        public int TotalReviews { get; set; } = 0;

        /// <summary>
        /// Количество правильных ответов (оценка >= 3).
        /// </summary>
        public int CorrectAnswers { get; set; } = 0;

        /// <summary>
        /// Проверяет, нужно ли повторять карточку сегодня.
        /// </summary>
        public bool IsDueForReview => NextReviewDate.Date <= DateTime.UtcNow.Date;

        /// <summary>
        /// Процент правильных ответов.
        /// </summary>
        public double SuccessRate => TotalReviews > 0 ? (double)CorrectAnswers / TotalReviews * 100 : 0;
    }
}
