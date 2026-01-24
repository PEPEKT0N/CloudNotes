using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CloudNotes.Desktop.Data;
using CloudNotes.Desktop.Model;
using CloudNotes.Desktop.Services;
using CloudNotes.Services;

namespace CloudNotes.Desktop.Views
{
    public partial class StudyDialog : Window
    {
        private readonly List<Flashcard> _flashcards;
        private readonly Guid _noteId;
        private readonly SpacedRepetitionService _srService;
        private int _currentIndex;
        private static readonly Random _random = new();

        public StudyDialog()
        {
            InitializeComponent();
            _flashcards = new List<Flashcard>();
            // Создаём сервис с контекстом БД
            var context = DbContextProvider.GetContext();
            _srService = new SpacedRepetitionService(context);
        }

        public StudyDialog(List<Flashcard> flashcards, Guid noteId, string? userEmail = null) : this()
        {
            _noteId = noteId;

            // Пересоздаём сервис с правильным userEmail
            var context = DbContextProvider.GetContext();
            _srService = new SpacedRepetitionService(context, userEmail);

            // Инициализируем карточки (сортировка будет async)
            _flashcards = new List<Flashcard>(flashcards);
            _currentIndex = 0;

            // Запускаем async инициализацию
            InitializeAsync(flashcards);
        }

        /// <summary>
        /// Асинхронная инициализация — сортировка по приоритету.
        /// </summary>
        private async void InitializeAsync(List<Flashcard> flashcards)
        {
            // Сортируем карточки по приоритету
            var sorted = await _srService.SortByPriorityAsync(flashcards, _noteId);
            _flashcards.Clear();
            _flashcards.AddRange(sorted);
            _currentIndex = 0;
            ShowCurrentCard();
        }

        /// <summary>
        /// Показывает диалог с карточками для изучения.
        /// </summary>
        public static async System.Threading.Tasks.Task ShowDialogAsync(
            Window owner,
            List<Flashcard> flashcards,
            Guid noteId,
            string? userEmail = null)
        {
            if (flashcards.Count == 0)
            {
                return;
            }

            var dialog = new StudyDialog(flashcards, noteId, userEmail);
            await dialog.ShowDialog(owner);
        }

        private async void ShowCurrentCard()
        {
            if (_currentIndex >= _flashcards.Count)
            {
                Close();
                return;
            }

            var card = _flashcards[_currentIndex];

            ProgressText.Text = $"Card {_currentIndex + 1} of {_flashcards.Count}";
            QuestionText.Text = card.Question;
            AnswerText.Text = card.Answer;

            // Показываем статус карточки
            await UpdateCardStatusAsync(card);

            AnswerPanel.IsVisible = false;
            Separator.IsVisible = false;
            ShowAnswerButton.IsVisible = true;
            RatingPanel.IsVisible = false;
        }

        /// <summary>
        /// Обновляет бейдж статуса карточки.
        /// </summary>
        private async System.Threading.Tasks.Task UpdateCardStatusAsync(Flashcard card)
        {
            var status = await _srService.GetCardStatusAsync(_noteId, card.Question);
            StatusText.Text = status;

            // Устанавливаем цвет в зависимости от статуса
            if (status.StartsWith("Overdue"))
            {
                StatusBadge.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFEBEE"));
                StatusText.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#C62828"));
            }
            else if (status == "Due today")
            {
                StatusBadge.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#FFF8E1"));
                StatusText.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F57F17"));
            }
            else if (status == "New")
            {
                StatusBadge.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E3F2FD"));
                StatusText.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1565C0"));
            }
            else
            {
                // Scheduled for later
                StatusBadge.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#F5F5F5"));
                StatusText.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#757575"));
            }
        }

        private void OnShowAnswerClick(object? sender, RoutedEventArgs e)
        {
            AnswerPanel.IsVisible = true;
            Separator.IsVisible = true;
            ShowAnswerButton.IsVisible = false;
            RatingPanel.IsVisible = true;
        }

        private async void OnRatingClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string ratingStr)
            {
                var rating = int.Parse(ratingStr);
                var card = _flashcards[_currentIndex];

                // Применяем алгоритм SM-2 и сохраняем в БД
                var stats = await _srService.ProcessAnswerAsync(_noteId, card.Question, rating);
                var nextReview = SpacedRepetitionService.GetNextReviewDescription(stats);

                Console.WriteLine(
                    $"[SM-2] Q: \"{card.Question}\" | " +
                    $"Rating: {rating}, " +
                    $"Rep: {stats.RepetitionCount}, " +
                    $"EF: {stats.EaseFactor:F2}, " +
                    $"Interval: {stats.IntervalDays}d, " +
                    $"Next: {nextReview}");

                _currentIndex++;
                if (_currentIndex >= _flashcards.Count)
                {
                    Close();
                }
                else
                {
                    ShowCurrentCard();
                }
            }
        }
    }
}
