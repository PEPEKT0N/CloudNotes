using System;
using System.ComponentModel.DataAnnotations;

namespace CloudNotes.Desktop.Model
{
    /// <summary>
    /// Избранная комбинация тегов для быстрого запуска обучения.
    /// </summary>
    public class FavoriteTagCombo
    {
        /// <summary>
        /// Уникальный идентификатор.
        /// </summary>
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Название комбинации (например, "Math + Hard").
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// JSON массив идентификаторов тегов.
        /// Например: ["guid1", "guid2", "guid3"]
        /// </summary>
        [Required]
        public string TagIdsJson { get; set; } = "[]";

        /// <summary>
        /// Email пользователя (для привязки к пользователю).
        /// Пустая строка для гостевого режима.
        /// </summary>
        public string UserEmail { get; set; } = string.Empty;

        /// <summary>
        /// Дата создания.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
