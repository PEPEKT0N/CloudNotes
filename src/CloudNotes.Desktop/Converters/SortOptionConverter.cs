using System;
using System.Globalization;
using Avalonia.Data.Converters;
using CloudNotes.Desktop.Model;

namespace CloudNotes.Desktop.Converters;

/// <summary>
/// Конвертер для отображения понятных названий сортировки.
/// </summary>
public class SortOptionConverter : IValueConverter
{
    public static readonly SortOptionConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SortOption option)
        {
            return option switch
            {
                SortOption.TitleAsc => "A - Z",
                SortOption.TitleDesc => "Z - A",
                SortOption.CreatedDesc => "Latest created",
                SortOption.CreatedAsc => "Earliest created",
                SortOption.UpdatedAsc => "Update asc",
                SortOption.UpdatedDesc => "Update desc",
                _ => option.ToString()
            };
        }
        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
