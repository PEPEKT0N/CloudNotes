using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CloudNotes.Desktop.Model;

namespace CloudNotes.Desktop.Views
{
    public partial class StudyDialog : Window
    {
        private readonly List<Flashcard> _flashcards;
        private int _currentIndex;
        private static readonly Random _random = new();

        public StudyDialog()
        {
            InitializeComponent();
            _flashcards = new List<Flashcard>();
        }

        public StudyDialog(List<Flashcard> flashcards) : this()
        {
            _flashcards = new List<Flashcard>(flashcards); // Копируем список
            ShuffleFlashcards(); // Перемешиваем
            _currentIndex = 0;
            ShowCurrentCard();
        }

        /// <summary>
        /// Перемешивает карточки в случайном порядке (Fisher-Yates shuffle).
        /// </summary>
        private void ShuffleFlashcards()
        {
            for (int i = _flashcards.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_flashcards[i], _flashcards[j]) = (_flashcards[j], _flashcards[i]);
            }
        }

        /// <summary>
        /// Показывает диалог с карточками для изучения.
        /// </summary>
        public static async System.Threading.Tasks.Task ShowDialogAsync(Window owner, List<Flashcard> flashcards)
        {
            if (flashcards.Count == 0)
            {
                // Нет карточек для изучения
                return;
            }

            var dialog = new StudyDialog(flashcards);
            await dialog.ShowDialog(owner);
        }

        private void ShowCurrentCard()
        {
            if (_currentIndex >= _flashcards.Count)
            {
                // Все карточки пройдены
                Close();
                return;
            }

            var card = _flashcards[_currentIndex];

            // Обновляем UI
            ProgressText.Text = $"Card {_currentIndex + 1} of {_flashcards.Count}";
            QuestionText.Text = card.Question;
            AnswerText.Text = card.Answer;

            // Скрываем ответ и панель оценки
            AnswerPanel.IsVisible = false;
            Separator.IsVisible = false;
            ShowAnswerButton.IsVisible = true;
            RatingPanel.IsVisible = false;
        }

        private void OnShowAnswerClick(object? sender, RoutedEventArgs e)
        {
            AnswerPanel.IsVisible = true;
            Separator.IsVisible = true;
            ShowAnswerButton.IsVisible = false;
            RatingPanel.IsVisible = true;
        }

        private void OnRatingClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string ratingStr)
            {
                var rating = int.Parse(ratingStr);
                // TODO: В будущем здесь будет логика SM-2 алгоритма
                System.Diagnostics.Debug.WriteLine($"Card rated: {rating}");

                // Переходим к следующей карточке или закрываем
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
