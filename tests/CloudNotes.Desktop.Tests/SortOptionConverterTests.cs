using System.Globalization;
using Xunit;
using CloudNotes.Desktop.Converters;
using CloudNotes.Desktop.Model;

namespace CloudNotes.Desktop.Tests;

public class SortOptionConverterTests
{
    private readonly SortOptionConverter _converter = SortOptionConverter.Instance;

    [Theory]
    [InlineData(SortOption.TitleAsc, "A - Z")]
    [InlineData(SortOption.TitleDesc, "Z - A")]
    [InlineData(SortOption.UpdatedAsc, "Update asc")]
    [InlineData(SortOption.UpdatedDesc, "Update desc")]
    public void Convert_ReturnsCorrectDisplayName(SortOption option, string expected)
    {
        var result = _converter.Convert(option, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_WithNullValue_ReturnsNull()
    {
        var result = _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Null(result);
    }

    [Fact]
    public void Convert_WithNonSortOptionValue_ReturnsToString()
    {
        var result = _converter.Convert("SomeString", typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("SomeString", result);
    }

    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        var instance1 = SortOptionConverter.Instance;
        var instance2 = SortOptionConverter.Instance;

        Assert.Same(instance1, instance2);
    }
}
