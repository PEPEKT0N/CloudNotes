using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using CloudNotes.Desktop.Services;

namespace CloudNotes.Desktop.Views
{
    public partial class HelpDialog : Window
    {
        private readonly IMarkdownConverter _markdownConverter;
        private string _currentLanguage = "EN";

        // Active/inactive button colors
        private static readonly IBrush ActiveBackground = new SolidColorBrush(Color.Parse("#1976D2"));
        private static readonly IBrush ActiveForeground = new SolidColorBrush(Colors.White);
        private static readonly IBrush InactiveBackground = new SolidColorBrush(Color.Parse("#E0E0E0"));
        private static readonly IBrush InactiveForeground = new SolidColorBrush(Color.Parse("#333333"));

        public HelpDialog()
        {
            InitializeComponent();
            _markdownConverter = new MarkdownConverter();
            
            // Load default language (English)
            LoadHelpContent("EN");
            UpdateButtonStyles();
        }

        /// <summary>
        /// Shows the help dialog.
        /// </summary>
        public static async Task ShowDialogAsync(Window owner)
        {
            var dialog = new HelpDialog();
            await dialog.ShowDialog(owner);
        }

        private void OnEnglishClick(object? sender, RoutedEventArgs e)
        {
            if (_currentLanguage != "EN")
            {
                _currentLanguage = "EN";
                LoadHelpContent("EN");
                UpdateButtonStyles();
            }
        }

        private void OnRussianClick(object? sender, RoutedEventArgs e)
        {
            if (_currentLanguage != "RU")
            {
                _currentLanguage = "RU";
                LoadHelpContent("RU");
                UpdateButtonStyles();
            }
        }

        private void UpdateButtonStyles()
        {
            if (_currentLanguage == "EN")
            {
                EnglishButton.Background = ActiveBackground;
                EnglishButton.Foreground = ActiveForeground;
                RussianButton.Background = InactiveBackground;
                RussianButton.Foreground = InactiveForeground;
            }
            else
            {
                EnglishButton.Background = InactiveBackground;
                EnglishButton.Foreground = InactiveForeground;
                RussianButton.Background = ActiveBackground;
                RussianButton.Foreground = ActiveForeground;
            }
        }

        private void LoadHelpContent(string language)
        {
            try
            {
                var markdown = LoadHelpMarkdown(language);
                var html = _markdownConverter.ConvertToHtml(markdown);
                
                // Add additional styles for better help display
                var styledHtml = GetHelpStyles() + html;
                HelpHtmlPanel.Text = styledHtml;
            }
            catch (Exception ex)
            {
                HelpHtmlPanel.Text = $"<p style='color:red;'>Error loading help content: {ex.Message}</p>";
                System.Diagnostics.Debug.WriteLine($"Error loading help: {ex}");
            }
        }

        private static string LoadHelpMarkdown(string language)
        {
            // Try to load from embedded resource first
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"CloudNotes.Desktop.Assets.Help_{language}.md";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }

            // Fallback: try to load from file system (for development)
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var filePath = Path.Combine(basePath, "Assets", $"Help_{language}.md");
            
            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
            }

            // Try relative path from executable
            var exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (exePath != null)
            {
                filePath = Path.Combine(exePath, "Assets", $"Help_{language}.md");
                if (File.Exists(filePath))
                {
                    return File.ReadAllText(filePath);
                }
            }

            throw new FileNotFoundException($"Help file not found for language: {language}");
        }

        private static string GetHelpStyles()
        {
            return @"<style>
body { 
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif; 
    line-height: 1.6; 
    color: #333;
    padding: 10px;
}
h1 { 
    color: #1976D2; 
    border-bottom: 2px solid #1976D2; 
    padding-bottom: 10px;
    margin-top: 0;
}
h2 { 
    color: #333; 
    border-bottom: 1px solid #E0E0E0; 
    padding-bottom: 8px;
    margin-top: 25px;
}
h3 {
    color: #555;
    margin-top: 20px;
}
table { 
    border-collapse: collapse; 
    width: 100%; 
    margin: 15px 0;
}
th, td { 
    border: 1px solid #ddd; 
    padding: 10px 12px; 
    text-align: left; 
}
th { 
    background-color: #f5f5f5; 
    font-weight: 600;
}
tr:nth-child(even) { 
    background-color: #fafafa; 
}
code { 
    background-color: #f5f5f5; 
    padding: 2px 6px; 
    border-radius: 3px; 
    font-family: 'Consolas', 'Monaco', monospace;
    font-size: 0.9em;
}
pre { 
    background-color: #f5f5f5; 
    padding: 15px; 
    border-radius: 5px; 
    overflow-x: auto;
    border: 1px solid #e0e0e0;
}
pre code {
    background: none;
    padding: 0;
}
hr { 
    border: none; 
    border-top: 1px solid #E0E0E0; 
    margin: 25px 0; 
}
strong { 
    color: #1976D2; 
}
kbd {
    background-color: #f5f5f5;
    border: 1px solid #ccc;
    border-radius: 3px;
    padding: 2px 5px;
    font-family: 'Consolas', 'Monaco', monospace;
    font-size: 0.85em;
}
ul, ol {
    padding-left: 25px;
}
li {
    margin: 5px 0;
}
</style>";
        }
    }
}

