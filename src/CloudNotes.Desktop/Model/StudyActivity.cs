using System;
using System.ComponentModel.DataAnnotations;

namespace CloudNotes.Desktop.Model
{
    /// <summary>
    /// Ежедневная активность пользователя для подсчёта streak и календаря активности.
    /// </summary>
    public class StudyActivity
    {
        /// <summary>
        /// Уникальный идентификатор записи.
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Email пользователя.
        /// Для гостевого режима — пустая строка.
        /// </summary>
        [Required]
        public string UserEmail { get; set; } = string.Empty;

        /// <summary>
        /// Дата активности (только дата, без времени).
        /// Хранится в UTC.
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Количество карточек, изученных в этот день.
        /// </summary>
        public int CardsStudied { get; set; } = 0;

        /// <summary>
        /// Количество правильных ответов в этот день.
        /// </summary>
        public int CorrectAnswers { get; set; } = 0;
    }
}
