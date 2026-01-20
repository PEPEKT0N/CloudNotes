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

        public StudyDialog()
        {
            InitializeComponent();
            _flashcards = new List<Flashcard>();
        }

        public StudyDialog(List<Flashcard> flashcards) : this()
        {
            _flashcards = flashcards;
            _currentIndex = 0;
            ShowCurrentCard();
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

            // Скрываем ответ
            AnswerPanel.IsVisible = false;
            Separator.IsVisible = false;
            ShowAnswerButton.IsVisible = true;
            NavigationPanel.IsVisible = false;

            // Настраиваем кнопку Next/Finish
            NextButton.IsVisible = _currentIndex < _flashcards.Count - 1;
            FinishButton.IsVisible = true;
        }

        private void OnShowAnswerClick(object? sender, RoutedEventArgs e)
        {
            AnswerPanel.IsVisible = true;
            Separator.IsVisible = true;
            ShowAnswerButton.IsVisible = false;
            NavigationPanel.IsVisible = true;
        }

        private void OnNextClick(object? sender, RoutedEventArgs e)
        {
            _currentIndex++;
            ShowCurrentCard();
        }

        private void OnFinishClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
