namespace CloudNotes.Desktop.Model;

/// <summary>
/// Варианты сортировки заметок.
/// </summary>
public enum SortOption
{
    /// <summary>
    /// По названию (A-Z).
    /// </summary>
    TitleAsc,

    /// <summary>
    /// По названию (Z-A).
    /// </summary>
    TitleDesc,

    /// <summary>
    /// По дате создания (новые сначала).
    /// </summary>
    CreatedDesc,

    /// <summary>
    /// По дате создания (старые сначала).
    /// </summary>
    CreatedAsc,

    /// <summary>
    /// По дате изменения (старые сначала).
    /// </summary>
    UpdatedAsc,

    /// <summary>
    /// По дате изменения (новые сначала).
    /// </summary>
    UpdatedDesc
}
