using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CloudNotes.Desktop.Data;
using CloudNotes.Desktop.Model;
using CloudNotes.Services;
using Microsoft.EntityFrameworkCore;

namespace CloudNotes.Desktop.Views
{
    public partial class TagSelectionDialog : Window
    {
        private readonly AppDbContext _context;
        private readonly string _userEmail;
        private readonly List<Tag> _allTags = new();
        private readonly HashSet<Guid> _selectedTagIds = new();
        private List<FavoriteTagCombo> _favorites = new();

        public List<Guid> SelectedTagIds => _selectedTagIds.ToList();
        public bool IsConfirmed { get; private set; }

        public TagSelectionDialog()
        {
            InitializeComponent();
            _context = DbContextProvider.GetContext();
            _userEmail = string.Empty;
        }

        public TagSelectionDialog(string? userEmail) : this()
        {
            _userEmail = userEmail ?? string.Empty;
            LoadDataAsync();
        }

        private async void LoadDataAsync()
        {
            await LoadTagsAsync();
            await LoadFavoritesAsync();
        }

        private async Task LoadTagsAsync()
        {
            _allTags.Clear();
            _allTags.AddRange(await _context.Tags.ToListAsync());

            TagsPanel.Children.Clear();

            if (_allTags.Count == 0)
            {
                TagsPanel.Children.Add(new TextBlock
                {
                    Text = "No tags available. Create tags on your notes first.",
                    Foreground = new SolidColorBrush(Color.Parse("#888888")),
                    FontStyle = FontStyle.Italic
                });
                return;
            }

            foreach (var tag in _allTags.OrderBy(t => t.Name))
            {
                var checkBox = new CheckBox
                {
                    Content = tag.Name,
                    Tag = tag.Id,
                    Margin = new Avalonia.Thickness(0, 2)
                };
                checkBox.IsCheckedChanged += OnTagCheckChanged;
                TagsPanel.Children.Add(checkBox);
            }
        }

        private async Task LoadFavoritesAsync()
        {
            _favorites = await _context.FavoriteTagCombos
                .Where(f => f.UserEmail == _userEmail)
                .OrderBy(f => f.Name)
                .ToListAsync();

            FavoritesComboBox.ItemsSource = _favorites.Select(f => f.Name).ToList();
        }

        private void OnTagCheckChanged(object? sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.Tag is Guid tagId)
            {
                if (checkBox.IsChecked == true)
                {
                    _selectedTagIds.Add(tagId);
                }
                else
                {
                    _selectedTagIds.Remove(tagId);
                }

                UpdateSelectedTagsDisplay();
                UpdateStartButtonState();
            }
        }

        private void UpdateSelectedTagsDisplay()
        {
            var items = new List<Border>();

            foreach (var tagId in _selectedTagIds)
            {
                var tag = _allTags.FirstOrDefault(t => t.Id == tagId);
                if (tag == null) continue;

                var panel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 5
                };

                panel.Children.Add(new TextBlock
                {
                    Text = tag.Name,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var removeButton = new Button
                {
                    Content = "×",
                    FontSize = 14,
                    Padding = new Avalonia.Thickness(4, 0),
                    Background = Brushes.Transparent,
                    Tag = tagId
                };
                removeButton.Click += OnRemoveTagClick;
                panel.Children.Add(removeButton);

                var chip = new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#BBDEFB")),
                    CornerRadius = new Avalonia.CornerRadius(12),
                    Padding = new Avalonia.Thickness(10, 4),
                    Margin = new Avalonia.Thickness(0, 0, 5, 5),
                    Child = panel
                };

                items.Add(chip);
            }

            SelectedTagsControl.ItemsSource = items;
        }

        private void OnRemoveTagClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Guid tagId)
            {
                _selectedTagIds.Remove(tagId);

                // Снимаем галочку с чекбокса
                foreach (var child in TagsPanel.Children)
                {
                    if (child is CheckBox cb && cb.Tag is Guid cbTagId && cbTagId == tagId)
                    {
                        cb.IsChecked = false;
                        break;
                    }
                }

                UpdateSelectedTagsDisplay();
                UpdateStartButtonState();
            }
        }

        private void UpdateStartButtonState()
        {
            StartStudyButton.IsEnabled = _selectedTagIds.Count > 0;
        }

        private void OnFavoriteSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (FavoritesComboBox.SelectedIndex < 0 || FavoritesComboBox.SelectedIndex >= _favorites.Count)
            {
                DeleteFavoriteButton.IsEnabled = false;
                return;
            }

            var favorite = _favorites[FavoritesComboBox.SelectedIndex];
            DeleteFavoriteButton.IsEnabled = true;

            // Загружаем теги из избранного
            try
            {
                var tagIds = JsonSerializer.Deserialize<List<Guid>>(favorite.TagIdsJson) ?? new List<Guid>();

                // Сбрасываем все чекбоксы
                foreach (var child in TagsPanel.Children)
                {
                    if (child is CheckBox cb)
                    {
                        cb.IsChecked = false;
                    }
                }

                _selectedTagIds.Clear();

                // Устанавливаем выбранные теги
                foreach (var tagId in tagIds)
                {
                    if (_allTags.Any(t => t.Id == tagId))
                    {
                        _selectedTagIds.Add(tagId);

                        foreach (var child in TagsPanel.Children)
                        {
                            if (child is CheckBox cb && cb.Tag is Guid cbTagId && cbTagId == tagId)
                            {
                                cb.IsChecked = true;
                                break;
                            }
                        }
                    }
                }

                UpdateSelectedTagsDisplay();
                UpdateStartButtonState();
            }
            catch
            {
                // Игнорируем ошибки парсинга JSON
            }
        }

        private async void OnSaveFavoriteClick(object? sender, RoutedEventArgs e)
        {
            var name = FavoriteNameTextBox.Text?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                name = string.Join(" + ", _selectedTagIds
                    .Select(id => _allTags.FirstOrDefault(t => t.Id == id)?.Name)
                    .Where(n => n != null));
            }

            if (string.IsNullOrEmpty(name) || _selectedTagIds.Count == 0)
            {
                return;
            }

            var favorite = new FavoriteTagCombo
            {
                Name = name,
                TagIdsJson = JsonSerializer.Serialize(_selectedTagIds.ToList()),
                UserEmail = _userEmail
            };

            _context.FavoriteTagCombos.Add(favorite);
            await _context.SaveChangesAsync();

            FavoriteNameTextBox.Text = string.Empty;
            await LoadFavoritesAsync();
        }

        private async void OnDeleteFavoriteClick(object? sender, RoutedEventArgs e)
        {
            if (FavoritesComboBox.SelectedIndex < 0 || FavoritesComboBox.SelectedIndex >= _favorites.Count)
            {
                return;
            }

            var favorite = _favorites[FavoritesComboBox.SelectedIndex];
            _context.FavoriteTagCombos.Remove(favorite);
            await _context.SaveChangesAsync();

            FavoritesComboBox.SelectedIndex = -1;
            DeleteFavoriteButton.IsEnabled = false;
            await LoadFavoritesAsync();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }

        private void OnStartStudyClick(object? sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            Close();
        }

        public static async Task<(bool Confirmed, List<Guid> TagIds)> ShowDialogAsync(Window owner, string? userEmail)
        {
            var dialog = new TagSelectionDialog(userEmail);
            await dialog.ShowDialog(owner);
            return (dialog.IsConfirmed, dialog.SelectedTagIds);
        }
    }
}
