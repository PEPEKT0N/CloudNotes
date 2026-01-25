using System;
using System.Collections.Generic;
using System.Linq;
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
        private const int SESSION_LIMIT = 10;

        private readonly List<(Guid NoteId, Flashcard Card)> _allCards;
        private readonly List<(Guid NoteId, Flashcard Card)> _sessionCards;
        private readonly SpacedRepetitionService _srService;
        private int _currentIndex;
        private int _totalReviewed;
        private int _sessionStartIndex;
        private static readonly Random _random = new();

        public StudyDialog()
        {
            InitializeComponent();
            _allCards = new List<(Guid, Flashcard)>();
            _sessionCards = new List<(Guid, Flashcard)>();
            var context = DbContextProvider.GetContext();
            _srService = new SpacedRepetitionService(context);
        }

        /// <summary>
        /// Конструктор для изучения карточек из одной заметки.
        /// </summary>
        public StudyDialog(List<Flashcard> flashcards, Guid noteId, string? userEmail = null) : this()
        {
            var context = DbContextProvider.GetContext();
            _srService = new SpacedRepetitionService(context, userEmail);

            // Преобразуем в кортежи
            _allCards = flashcards.Select(c => (noteId, c)).ToList();
            _currentIndex = 0;
            _totalReviewed = 0;

            InitializeAsync();
        }

        /// <summary>
        /// Конструктор для изучения карточек из нескольких заметок (Study by Tags).
        /// </summary>
        public StudyDialog(List<(Guid NoteId, Flashcard Card)> cards, string? userEmail = null) : this()
        {
            var context = DbContextProvider.GetContext();
            _srService = new SpacedRepetitionService(context, userEmail);

            _allCards = new List<(Guid, Flashcard)>(cards);
            _currentIndex = 0;
            _totalReviewed = 0;

            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            // Сортируем все карточки по приоритету
            var sorted = await SortCardsByPriorityAsync(_allCards);
            _allCards.Clear();
            _allCards.AddRange(sorted);

            // Загружаем первую сессию
            LoadNextSession();
        }

        private async System.Threading.Tasks.Task<List<(Guid NoteId, Flashcard Card)>> SortCardsByPriorityAsync(
            List<(Guid NoteId, Flashcard Card)> cards)
        {
            var cardPriorities = new List<((Guid NoteId, Flashcard Card) Card, int Priority)>();

            foreach (var card in cards)
            {
                var priority = await _srService.GetCardPriorityAsync(card.NoteId, card.Card.Question);
                cardPriorities.Add((card, priority));
            }

            // Группируем по приоритету, перемешиваем внутри группы
            var sorted = cardPriorities
                .GroupBy(cp => cp.Priority)
                .OrderBy(g => g.Key)
                .SelectMany(g => Shuffle(g.Select(cp => cp.Card).ToList()))
                .ToList();

            return sorted;
        }

        private static List<T> Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        private void LoadNextSession()
        {
            _sessionCards.Clear();
            _sessionStartIndex = _totalReviewed;

            // Берём следующие SESSION_LIMIT карточек
            var remaining = _allCards.Skip(_sessionStartIndex).Take(SESSION_LIMIT).ToList();
            _sessionCards.AddRange(remaining);
            _currentIndex = 0;

            // Скрываем панель завершения сессии
            SessionCompletePanel.IsVisible = false;

            if (_sessionCards.Count > 0)
            {
                ShowCurrentCard();
            }
            else
            {
                Close();
            }
        }

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

        /// <summary>
        /// Показывает диалог для изучения карточек по тегам.
        /// </summary>
        public static async System.Threading.Tasks.Task ShowDialogByTagsAsync(
            Window owner,
            List<(Guid NoteId, Flashcard Card)> cards,
            string? userEmail = null)
        {
            if (cards.Count == 0)
            {
                return;
            }

            var dialog = new StudyDialog(cards, userEmail);
            await dialog.ShowDialog(owner);
        }

        private async void ShowCurrentCard()
        {
            if (_currentIndex >= _sessionCards.Count)
            {
                // Сессия завершена
                ShowSessionComplete();
                return;
            }

            var (noteId, card) = _sessionCards[_currentIndex];

            var totalProgress = _totalReviewed + _currentIndex + 1;
            ProgressText.Text = $"Card {totalProgress} of {_allCards.Count}";
            QuestionText.Text = card.Question;
            AnswerText.Text = card.Answer;

            await UpdateCardStatusAsync(noteId, card);

            AnswerPanel.IsVisible = false;
            Separator.IsVisible = false;
            ShowAnswerButton.IsVisible = true;
            RatingPanel.IsVisible = false;
            SessionCompletePanel.IsVisible = false;
        }

        private void ShowSessionComplete()
        {
            var reviewedInSession = _currentIndex;
            _totalReviewed += reviewedInSession;

            var remaining = _allCards.Count - _totalReviewed;

            if (remaining > 0)
            {
                SessionStatsText.Text = $"Session complete! {reviewedInSession} cards reviewed.\n{remaining} cards remaining.";
            }
            else
            {
                SessionStatsText.Text = $"All done! {_totalReviewed} cards reviewed.";
            }

            // Скрываем элементы карточки
            ShowAnswerButton.IsVisible = false;
            RatingPanel.IsVisible = false;

            // Показываем панель завершения
            SessionCompletePanel.IsVisible = true;
        }

        private async System.Threading.Tasks.Task UpdateCardStatusAsync(Guid noteId, Flashcard card)
        {
            var status = await _srService.GetCardStatusAsync(noteId, card.Question);
            StatusText.Text = status;

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
                var (noteId, card) = _sessionCards[_currentIndex];

                var stats = await _srService.ProcessAnswerAsync(noteId, card.Question, rating);
                var nextReview = SpacedRepetitionService.GetNextReviewDescription(stats);

                Console.WriteLine(
                    $"[SM-2] Q: \"{card.Question}\" | " +
                    $"Rating: {rating}, " +
                    $"Rep: {stats.RepetitionCount}, " +
                    $"EF: {stats.EaseFactor:F2}, " +
                    $"Interval: {stats.IntervalDays}d, " +
                    $"Next: {nextReview}");

                _currentIndex++;
                ShowCurrentCard();
            }
        }

        private void OnContinueClick(object? sender, RoutedEventArgs e)
        {
            LoadNextSession();
        }

        private void OnFinishClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
