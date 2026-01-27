using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using CloudNotes.Desktop.Services;
using CloudNotes.Services;

namespace CloudNotes.Desktop.Views;

public partial class StatisticsDialog : Window
{
    private readonly string _userEmail;
    private readonly SpacedRepetitionService _srService;

    public StatisticsDialog() : this(string.Empty)
    {
    }

    public StatisticsDialog(string userEmail)
    {
        InitializeComponent();
        _userEmail = userEmail;

        var context = DbContextProvider.GetContext();
        _srService = new SpacedRepetitionService(context, userEmail);

        Loaded += async (_, _) => await LoadStatisticsAsync();
    }

    private async Task LoadStatisticsAsync()
    {
        try
        {
            // Load streak
            var streak = await _srService.GetStreakAsync();
            StreakCount.Text = streak.ToString();
            StreakMessage.Text = GetStreakMessage(streak);

            // Load today's stats
            var (cardsStudiedToday, correctToday, successRateToday) = await _srService.GetTodayStatsAsync();
            CardsReviewedToday.Text = cardsStudiedToday.ToString();
            TodaySuccessRate.Text = $"{successRateToday:F0}%";

            // Load total cards studied
            var totalStudied = await _srService.GetTotalCardsStudiedAsync();
            TotalCardsStudied.Text = totalStudied.ToString();

            // Load due cards count
            var dueCount = await _srService.GetDueCardsCountAsync();
            DueForReview.Text = dueCount.ToString();

            // Load and render calendar
            var calendar = await _srService.GetActivityCalendarAsync(35);
            RenderCalendar(calendar);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading statistics: {ex.Message}");
        }
    }

    private static string GetStreakMessage(int streak)
    {
        return streak switch
        {
            0 => "Start learning today!",
            1 => "Great start! Keep going!",
            < 7 => "You're building a habit!",
            < 30 => "Amazing consistency!",
            < 100 => "You're on fire!",
            _ => "Legendary dedication!"
        };
    }

    private void RenderCalendar(Dictionary<DateTime, int> activityData)
    {
        CalendarGrid.Children.Clear();
        CalendarGrid.RowDefinitions.Clear();
        CalendarGrid.ColumnDefinitions.Clear();

        // 5 weeks (rows) + 7 days (columns) + 1 column for week labels
        for (int i = 0; i < 5; i++)
        {
            CalendarGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        // Week label column
        CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(30)));

        // 7 day columns
        for (int i = 0; i < 7; i++)
        {
            CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        // Find the maximum activity for color scaling
        int maxActivity = 1;
        foreach (var kvp in activityData)
        {
            if (kvp.Value > maxActivity) maxActivity = kvp.Value;
        }

        // Get the start date (35 days ago, aligned to Monday)
        var today = DateTime.UtcNow.Date;
        var startDate = today.AddDays(-34);

        // Align to Monday
        while (startDate.DayOfWeek != DayOfWeek.Monday)
        {
            startDate = startDate.AddDays(-1);
        }

        // Render weeks
        for (int week = 0; week < 5; week++)
        {
            // Week label
            var weekStart = startDate.AddDays(week * 7);
            var weekLabel = new TextBlock
            {
                Text = weekStart.ToString("dd"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#999999")),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            Grid.SetRow(weekLabel, week);
            Grid.SetColumn(weekLabel, 0);
            CalendarGrid.Children.Add(weekLabel);

            // Day cells
            for (int day = 0; day < 7; day++)
            {
                var cellDate = startDate.AddDays(week * 7 + day);
                var activity = activityData.GetValueOrDefault(cellDate, 0);

                var cell = new Border
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(2),
                    Background = GetActivityColor(activity, maxActivity, cellDate > today)
                };
                ToolTip.SetTip(cell, $"{cellDate:MMM dd}: {activity} cards");

                Grid.SetRow(cell, week);
                Grid.SetColumn(cell, day + 1);
                CalendarGrid.Children.Add(cell);
            }
        }
    }

    private static IBrush GetActivityColor(int activity, int maxActivity, bool isFuture)
    {
        if (isFuture)
        {
            return new SolidColorBrush(Color.Parse("#EEEEEE"));
        }

        if (activity == 0)
        {
            return new SolidColorBrush(Color.Parse("#E0E0E0"));
        }

        // Scale from light green to dark green based on activity
        var intensity = Math.Min(1.0, (double)activity / Math.Max(maxActivity, 1));

        // Interpolate between #C8E6C9 (light green) and #1B5E20 (dark green)
        var r = (int)(200 - intensity * 173);  // 200 -> 27
        var g = (int)(230 - intensity * 136);  // 230 -> 94
        var b = (int)(201 - intensity * 169);  // 201 -> 32

        return new SolidColorBrush(Color.FromRgb((byte)r, (byte)g, (byte)b));
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    public static async Task ShowDialogAsync(Window owner, string userEmail)
    {
        var dialog = new StatisticsDialog(userEmail);
        await dialog.ShowDialog(owner);
    }
}
