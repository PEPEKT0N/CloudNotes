using Xunit;
using CloudNotes.Desktop.Services;

namespace CloudNotes.Desktop.Tests
{
    public class MarkdownConverterTests
    {
        private readonly IMarkdownConverter _converter;

        public MarkdownConverterTests()
        {
            _converter = new MarkdownConverter();
        }

        // Тесты для Headers
        public class HeaderTests : MarkdownConverterTests
        {
            [Fact]
            public void ConvertToHtml_H1Header_ReturnsH1Tag()
            {
                var markdown = "# Заголовок первого уровня";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<h1>", result);
                Assert.Contains("Заголовок первого уровня", result);
                Assert.Contains("</h1>", result);
            }

            [Fact]
            public void ConvertToHtml_H2Header_ReturnsH2Tag()
            {
                var markdown = "## Заголовок второго уровня";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<h2>", result);
                Assert.Contains("Заголовок второго уровня", result);
                Assert.Contains("</h2>", result);
            }

            [Fact]
            public void ConvertToHtml_H3Header_ReturnsH3Tag()
            {
                var markdown = "### Заголовок третьего уровня";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<h3>", result);
                Assert.Contains("Заголовок третьего уровня", result);
                Assert.Contains("</h3>", result);
            }

            [Fact]
            public void ConvertToHtml_MultipleHeaders_ReturnsAllHeaders()
            {
                var markdown = "# H1\n## H2\n### H3";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<h1>", result);
                Assert.Contains("<h2>", result);
                Assert.Contains("<h3>", result);
            }
        }

        // Тесты для Bold
        public class BoldTests : MarkdownConverterTests
        {
            [Fact]
            public void ConvertToHtml_BoldWithAsterisks_ReturnsStrongTag()
            {
                var markdown = "Это **жирный** текст";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<strong>", result);
                Assert.Contains("жирный", result);
                Assert.Contains("</strong>", result);
            }

            [Fact]
            public void ConvertToHtml_BoldWithUnderscores_ReturnsStrongTag()
            {
                var markdown = "Это __жирный__ текст";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<strong>", result);
                Assert.Contains("жирный", result);
                Assert.Contains("</strong>", result);
            }

            [Fact]
            public void ConvertToHtml_MultipleBoldWords_ReturnsMultipleStrongTags()
            {
                var markdown = "**Первый** и **второй**";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("Первый", result);
                Assert.Contains("второй", result);
                // Проверяем что есть минимум 2 strong тега
                var strongCount = result.Split("<strong>").Length - 1;
                Assert.True(strongCount >= 2);
            }
        }

        // Тесты для Italic
        public class ItalicTests : MarkdownConverterTests
        {
            [Fact]
            public void ConvertToHtml_ItalicWithAsterisk_ReturnsEmTag()
            {
                var markdown = "Это *курсив* текст";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<em>", result);
                Assert.Contains("курсив", result);
                Assert.Contains("</em>", result);
            }

            [Fact]
            public void ConvertToHtml_ItalicWithUnderscore_ReturnsEmTag()
            {
                var markdown = "Это _курсив_ текст";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<em>", result);
                Assert.Contains("курсив", result);
                Assert.Contains("</em>", result);
            }

            [Fact]
            public void ConvertToHtml_BoldAndItalicCombined_ReturnsBothTags()
            {
                var markdown = "Это **жирный** и *курсив* текст";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<strong>", result);
                Assert.Contains("<em>", result);
            }
        }

        // Тесты для Lists
        public class ListTests : MarkdownConverterTests
        {
            [Fact]
            public void ConvertToHtml_UnorderedListWithDash_ReturnsUlTag()
            {
                var markdown = "- Первый\n- Второй\n- Третий";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<ul>", result);
                Assert.Contains("<li>", result);
                Assert.Contains("Первый", result);
                Assert.Contains("Второй", result);
                Assert.Contains("Третий", result);
                Assert.Contains("</ul>", result);
            }

            [Fact]
            public void ConvertToHtml_UnorderedListWithAsterisk_ReturnsUlTag()
            {
                var markdown = "* Первый\n* Второй";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<ul>", result);
                Assert.Contains("<li>", result);
            }

            [Fact]
            public void ConvertToHtml_OrderedList_ReturnsOlTag()
            {
                var markdown = "1. Первый\n2. Второй\n3. Третий";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<ol>", result);
                Assert.Contains("<li>", result);
                Assert.Contains("Первый", result);
                Assert.Contains("Второй", result);
                Assert.Contains("Третий", result);
                Assert.Contains("</ol>", result);
            }

            [Fact]
            public void ConvertToHtml_ListWithFormattedItems_ReturnsFormattedList()
            {
                var markdown = "- **Жирный** элемент\n- *Курсивный* элемент";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<ul>", result);
                Assert.Contains("<strong>", result);
                Assert.Contains("<em>", result);
            }
        }

        // Тесты для Spoiler
        public class SpoilerTests : MarkdownConverterTests
        {
            [Fact]
            public void ConvertToHtml_SimpleSpoiler_ReturnsSpoilerSpan()
            {
                var markdown = "Это ||секрет|| текст";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("class=\"spoiler\"", result);
                Assert.Contains("секрет", result);
            }

            [Fact]
            public void ConvertToHtml_SpoilerContainsText()
            {
                var markdown = "||test||";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<span class=\"spoiler\">test</span>", result);
            }

            [Fact]
            public void ConvertToHtml_MultipleSpoilers_ReturnsAllSpoilers()
            {
                var markdown = "||first|| и ||second||";

                var result = _converter.ConvertToHtml(markdown);

                // Два разных spoiler блока
                var spoilerCount = result.Split("class=\"spoiler\"").Length - 1;
                Assert.Equal(2, spoilerCount);
                Assert.Contains("first", result);
                Assert.Contains("second", result);
            }

            [Fact]
            public void ConvertToHtml_SpoilerInHeader_WorksCorrectly()
            {
                var markdown = "# Заголовок с ||секретом||";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<h1>", result);
                Assert.Contains("class=\"spoiler\"", result);
                Assert.Contains("секретом", result);
            }

            [Fact]
            public void ConvertToHtml_SpoilerInList_WorksCorrectly()
            {
                var markdown = "- Элемент с ||секретом||\n- Обычный элемент";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<ul>", result);
                Assert.Contains("class=\"spoiler\"", result);
            }

            [Fact]
            public void ConvertToHtml_EmptySpoiler_HandlesGracefully()
            {
                var markdown = "||  ||";

                var result = _converter.ConvertToHtml(markdown);

                // Пустой или пробельный spoiler тоже должен работать
                Assert.Contains("class=\"spoiler\"", result);
            }

            [Fact]
            public void ConvertToHtml_WithSpoiler_IncludesStyles()
            {
                var markdown = "||секрет||";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<style>", result);
                Assert.Contains(".spoiler", result);
            }

            [Fact]
            public void ConvertToHtml_WithoutSpoiler_NoStyles()
            {
                var markdown = "Обычный текст без споилера";

                var result = _converter.ConvertToHtml(markdown);

                Assert.DoesNotContain("<style>", result);
            }

            [Fact]
            public void ConvertToHtml_SpoilerWithBoldInside_WorksCorrectly()
            {
                var markdown = "||**жирный секрет**||";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("class=\"spoiler\"", result);
                Assert.Contains("<strong>", result);
            }
        }

        // Тесты для Edge Cases
        public class EdgeCaseTests : MarkdownConverterTests
        {
            [Fact]
            public void ConvertToHtml_EmptyString_ReturnsEmptyString()
            {
                var result = _converter.ConvertToHtml(string.Empty);

                Assert.Equal(string.Empty, result);
            }

            [Fact]
            public void ConvertToHtml_NullString_ReturnsEmptyString()
            {
                var result = _converter.ConvertToHtml(null!);

                Assert.Equal(string.Empty, result);
            }

            [Fact]
            public void ConvertToHtml_PlainText_ReturnsParagraph()
            {
                var markdown = "Просто текст без форматирования";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<p>", result);
                Assert.Contains("Просто текст без форматирования", result);
            }

            [Fact]
            public void ConvertToHtml_ComplexDocument_ReturnsValidHtml()
            {
                var markdown = @"# Заголовок

Параграф с **жирным** и *курсивом*.

## Список покупок

- Молоко
- Хлеб
- Яйца

1. Первый шаг
2. Второй шаг";

                var result = _converter.ConvertToHtml(markdown);

                Assert.Contains("<h1>", result);
                Assert.Contains("<h2>", result);
                Assert.Contains("<strong>", result);
                Assert.Contains("<em>", result);
                Assert.Contains("<ul>", result);
                Assert.Contains("<ol>", result);
            }
        }
    }
}

