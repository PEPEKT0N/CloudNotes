using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CloudNotes.Desktop.Data;
using CloudNotes.Desktop.Model;
using Microsoft.EntityFrameworkCore;

namespace CloudNotes.Desktop.Services
{
    /// <summary>
    /// Сервис интервального повторения на основе алгоритма SM-2 (SuperMemo 2).
    /// Сохраняет статистику в локальной БД SQLite.
    /// </summary>
    public class SpacedRepetitionService
    {
        private readonly AppDbContext _context;
        private readonly string _userEmail;

        /// <summary>
        /// Минимальное значение EaseFactor.
        /// </summary>
        private const double MinEaseFactor = 1.3;

        /// <summary>
        /// Начальное значение EaseFactor для новых карточек.
        /// </summary>
        private const double DefaultEaseFactor = 2.5;

        public SpacedRepetitionService(AppDbContext context, string? userEmail = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userEmail = userEmail ?? string.Empty; // Пустая строка для гостевого режима
        }

        /// <summary>
        /// Обрабатывает ответ пользователя и обновляет статистику карточки.
        /// </summary>
        public async Task<FlashcardStats> ProcessAnswerAsync(Guid noteId, string question, int quality)
        {
            // Преобразуем оценку 1-5 в шкалу SM-2 (0-5)
            int sm2Quality = quality switch
            {
                1 => 0,
                2 => 2,
                3 => 3,
                4 => 4,
                5 => 5,
                _ => 3
            };

            var stats = await GetOrCreateStatsAsync(noteId, question);
            ApplySM2Algorithm(stats, sm2Quality);
            await _context.SaveChangesAsync();

            // Записываем активность
            bool isCorrect = sm2Quality >= 3;
            await RecordActivityAsync(isCorrect);

            return stats;
        }

        /// <summary>
        /// Получает статистику карточки из БД или создаёт новую.
        /// </summary>
        public async Task<FlashcardStats> GetOrCreateStatsAsync(Guid noteId, string question)
        {
            var hash = ComputeQuestionHash(noteId, question);

            var stats = await _context.FlashcardStats
                .FirstOrDefaultAsync(fs => fs.UserEmail == _userEmail && fs.QuestionHash == hash);

            if (stats != null)
            {
                return stats;
            }

            stats = new FlashcardStats
            {
                UserEmail = _userEmail,
                NoteId = noteId,
                QuestionHash = hash,
                EaseFactor = DefaultEaseFactor,
                IntervalDays = 0,
                RepetitionCount = 0,
                NextReviewDate = DateTime.UtcNow
            };

            _context.FlashcardStats.Add(stats);
            return stats;
        }

        /// <summary>
        /// Получает количество карточек для повторения сегодня.
        /// </summary>
        public async Task<int> GetDueCardsCountAsync()
        {
            var today = DateTime.UtcNow.Date;
            return await _context.FlashcardStats
                .Where(fs => fs.UserEmail == _userEmail && fs.NextReviewDate.Date <= today)
                .CountAsync();
        }

        /// <summary>
        /// Получает приоритет карточки для сортировки.
        /// Приоритет: 0 = overdue, 1 = due today, 2 = new (no stats), 3 = scheduled for later
        /// </summary>
        public async Task<int> GetCardPriorityAsync(Guid noteId, string question)
        {
            var hash = ComputeQuestionHash(noteId, question);
            var stats = await _context.FlashcardStats
                .FirstOrDefaultAsync(fs => fs.UserEmail == _userEmail && fs.QuestionHash == hash);

            if (stats == null)
            {
                // Новая карточка — высокий приоритет
                return 2;
            }

            var today = DateTime.UtcNow.Date;
            var reviewDate = stats.NextReviewDate.Date;

            if (reviewDate < today)
            {
                return 0; // Overdue — наивысший приоритет
            }
            else if (reviewDate == today)
            {
                return 1; // Due today
            }
            else
            {
                return 3; // Scheduled for later — низший приоритет
            }
        }

        /// <summary>
        /// Получает статус карточки для отображения в UI.
        /// </summary>
        public async Task<string> GetCardStatusAsync(Guid noteId, string question)
        {
            var hash = ComputeQuestionHash(noteId, question);
            var stats = await _context.FlashcardStats
                .FirstOrDefaultAsync(fs => fs.UserEmail == _userEmail && fs.QuestionHash == hash);

            if (stats == null)
            {
                return "New";
            }

            var today = DateTime.UtcNow.Date;
            var reviewDate = stats.NextReviewDate.Date;

            if (reviewDate < today)
            {
                var daysOverdue = (today - reviewDate).Days;
                return $"Overdue ({daysOverdue}d)";
            }
            else if (reviewDate == today)
            {
                return "Due today";
            }
            else
            {
                var daysUntil = (reviewDate - today).Days;
                return $"In {daysUntil}d";
            }
        }

        /// <summary>
        /// Сортирует карточки по приоритету: overdue → due today → new → later.
        /// Внутри каждой группы — случайный порядок.
        /// </summary>
        public async Task<List<Flashcard>> SortByPriorityAsync(List<Flashcard> flashcards, Guid noteId)
        {
            var cardPriorities = new List<(Flashcard Card, int Priority)>();

            foreach (var card in flashcards)
            {
                var priority = await GetCardPriorityAsync(noteId, card.Question);
                cardPriorities.Add((card, priority));
            }

            // Группируем по приоритету, перемешиваем внутри группы, сортируем
            var sorted = cardPriorities
                .GroupBy(cp => cp.Priority)
                .OrderBy(g => g.Key)
                .SelectMany(g => Shuffle(g.Select(cp => cp.Card).ToList()))
                .ToList();

            return sorted;
        }

        /// <summary>
        /// Перемешивает список (Fisher-Yates).
        /// </summary>
        private static List<T> Shuffle<T>(List<T> list)
        {
            var random = new Random();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        /// <summary>
        /// Получает общую статистику пользователя.
        /// </summary>
        public async Task<(int TotalCards, int DueToday, int TotalReviews, double AvgSuccessRate)> GetUserStatsAsync()
        {
            var today = DateTime.UtcNow.Date;
            var allStats = await _context.FlashcardStats
                .Where(fs => fs.UserEmail == _userEmail)
                .ToListAsync();

            if (allStats.Count == 0)
            {
                return (0, 0, 0, 0);
            }

            var totalCards = allStats.Count;
            var dueToday = allStats.Count(s => s.NextReviewDate.Date <= today);
            var totalReviews = allStats.Sum(s => s.TotalReviews);
            var avgSuccessRate = allStats.Average(s => s.SuccessRate);

            return (totalCards, dueToday, totalReviews, avgSuccessRate);
        }

        /// <summary>
        /// Применяет алгоритм SM-2 к статистике карточки.
        /// </summary>
        private void ApplySM2Algorithm(FlashcardStats stats, int quality)
        {
            stats.TotalReviews++;
            stats.LastReviewDate = DateTime.UtcNow;

            if (quality >= 3)
            {
                stats.CorrectAnswers++;

                if (stats.RepetitionCount == 0)
                {
                    stats.IntervalDays = 1;
                }
                else if (stats.RepetitionCount == 1)
                {
                    stats.IntervalDays = 6;
                }
                else
                {
                    stats.IntervalDays = (int)Math.Round(stats.IntervalDays * stats.EaseFactor);
                }

                stats.RepetitionCount++;
            }
            else
            {
                stats.RepetitionCount = 0;
                stats.IntervalDays = 1;
            }

            double easeDelta = 0.1 - (5 - quality) * (0.08 + (5 - quality) * 0.02);
            stats.EaseFactor = Math.Max(MinEaseFactor, stats.EaseFactor + easeDelta);
            stats.NextReviewDate = DateTime.UtcNow.AddDays(stats.IntervalDays);
        }

        /// <summary>
        /// Вычисляет хеш для идентификации карточки.
        /// </summary>
        private static string ComputeQuestionHash(Guid noteId, string question)
        {
            var input = $"{noteId}:{question.Trim().ToLowerInvariant()}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes)[..16];
        }

        /// <summary>
        /// Возвращает описание следующего повторения для UI.
        /// </summary>
        public static string GetNextReviewDescription(FlashcardStats stats)
        {
            var days = (stats.NextReviewDate.Date - DateTime.UtcNow.Date).Days;

            return days switch
            {
                < 0 => "Overdue",
                0 => "Today",
                1 => "Tomorrow",
                _ => $"In {days} days"
            };
        }

        /// <summary>
        /// Записывает активность пользователя за сегодня.
        /// Вызывается при каждом ответе на карточку.
        /// </summary>
        public async Task RecordActivityAsync(bool isCorrect)
        {
            var today = DateTime.UtcNow.Date;

            var activity = await _context.StudyActivities
                .FirstOrDefaultAsync(a => a.UserEmail == _userEmail && a.Date == today);

            if (activity == null)
            {
                activity = new StudyActivity
                {
                    UserEmail = _userEmail,
                    Date = today,
                    CardsStudied = 0,
                    CorrectAnswers = 0
                };
                _context.StudyActivities.Add(activity);
            }

            activity.CardsStudied++;
            if (isCorrect)
            {
                activity.CorrectAnswers++;
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Получает текущий streak (количество дней подряд с активностью).
        /// </summary>
        public async Task<int> GetStreakAsync()
        {
            var activities = await _context.StudyActivities
                .Where(a => a.UserEmail == _userEmail)
                .OrderByDescending(a => a.Date)
                .Select(a => a.Date)
                .ToListAsync();

            if (activities.Count == 0)
            {
                return 0;
            }

            var today = DateTime.UtcNow.Date;
            var streak = 0;
            var expectedDate = today;

            // Если сегодня нет активности, проверяем со вчера
            if (activities.Count == 0 || activities[0] != today)
            {
                expectedDate = today.AddDays(-1);
            }

            foreach (var date in activities)
            {
                if (date == expectedDate)
                {
                    streak++;
                    expectedDate = expectedDate.AddDays(-1);
                }
                else if (date < expectedDate)
                {
                    // Пропущен день — streak прерван
                    break;
                }
            }

            return streak;
        }

        /// <summary>
        /// Получает данные для календаря активности за последние N дней.
        /// Возвращает словарь: дата → количество изученных карточек.
        /// </summary>
        public async Task<Dictionary<DateTime, int>> GetActivityCalendarAsync(int days = 35)
        {
            var startDate = DateTime.UtcNow.Date.AddDays(-days + 1);

            var activities = await _context.StudyActivities
                .Where(a => a.UserEmail == _userEmail && a.Date >= startDate)
                .ToDictionaryAsync(a => a.Date, a => a.CardsStudied);

            // Заполняем все дни, включая дни без активности
            var result = new Dictionary<DateTime, int>();
            for (var date = startDate; date <= DateTime.UtcNow.Date; date = date.AddDays(1))
            {
                result[date] = activities.GetValueOrDefault(date, 0);
            }

            return result;
        }

        /// <summary>
        /// Получает статистику за сегодня.
        /// </summary>
        public async Task<(int CardsStudied, int CorrectAnswers, double SuccessRate)> GetTodayStatsAsync()
        {
            var today = DateTime.UtcNow.Date;

            var activity = await _context.StudyActivities
                .FirstOrDefaultAsync(a => a.UserEmail == _userEmail && a.Date == today);

            if (activity == null || activity.CardsStudied == 0)
            {
                return (0, 0, 0);
            }

            var successRate = (double)activity.CorrectAnswers / activity.CardsStudied * 100;
            return (activity.CardsStudied, activity.CorrectAnswers, successRate);
        }

        /// <summary>
        /// Получает общее количество изученных карточек за всё время.
        /// </summary>
        public async Task<int> GetTotalCardsStudiedAsync()
        {
            return await _context.StudyActivities
                .Where(a => a.UserEmail == _userEmail)
                .SumAsync(a => a.CardsStudied);
        }
    }
}
