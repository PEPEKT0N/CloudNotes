using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace CloudNotes.Desktop.Converters
{
    /// <summary>
    /// Конвертер для отображения текста кнопки Preview/Edit в зависимости от режима.
    /// </summary>
    public class PreviewButtonConverter : IValueConverter
    {
        public static readonly PreviewButtonConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isPreviewMode)
            {
                return isPreviewMode ? "Edit" : "Preview";
            }
            return "Preview";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
