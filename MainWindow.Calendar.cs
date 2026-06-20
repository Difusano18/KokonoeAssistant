using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using KokonoeAssistant.Services;
using Microsoft.Win32;
using Newtonsoft.Json;
using SkiaSharp;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TgUpdate   = Telegram.Bot.Types.Update;
using WMsgBox    = System.Windows.MessageBox;
using WButton    = System.Windows.Controls.Button;
using WKeyArgs   = System.Windows.Input.KeyEventArgs;
using WDragArgs  = System.Windows.DragEventArgs;
using WClipboard = System.Windows.Clipboard;
using WDataFmts  = System.Windows.DataFormats;
using WTextBox   = System.Windows.Controls.TextBox;
using MediaBrush   = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor   = System.Windows.Media.Color;
using WpfRect      = System.Windows.Shapes.Rectangle;
using WpfFF        = System.Windows.Media.FontFamily;
using WpfSz        = System.Windows.Size;
using WpfOri       = System.Windows.Controls.Orientation;
using WinForms     = System.Windows.Forms;

namespace KokonoeAssistant
{
    public partial class MainWindow
    {
        // ------------------------------------------------------------
        // HEALTH TAB
        // ------------------------------------------------------------

        // ------------------------------------------------------------
        // CALENDAR TAB
        // ------------------------------------------------------------

        private DateTime _calViewDate    = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        private DateTime _calSelectedDate = DateTime.Today;

        private void LoadCalendarTab()
        {
            RefreshCalendar();
            RefreshUpcoming();
        }

        private void CalPrev_Click(object sender, RoutedEventArgs e)
        {
            _calViewDate = _calViewDate.AddMonths(-1);
            RefreshCalendar();
        }

        private void CalNext_Click(object sender, RoutedEventArgs e)
        {
            _calViewDate = _calViewDate.AddMonths(1);
            RefreshCalendar();
        }

        private void RefreshCalendar()
        {
            var cal  = ServiceContainer.Calendar;
            var year = _calViewDate.Year;
            var mon  = _calViewDate.Month;

            CalMonthLabel.Text = _calViewDate.ToString("MMMM yyyy").ToUpper();

            // ---- Weekday header ----
            CalWeekHeader.Children.Clear();
            CalWeekHeader.ColumnDefinitions.Clear();
            var days = new[] { "ПН", "ВТ", "СР", "ЧТ", "ПТ", "СБ", "НД" };
            for (int i = 0; i < 7; i++)
            {
                CalWeekHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var tb = new TextBlock
                {
                    Text = days[i],
                    Foreground = MakeBrush(i >= 5 ? "#1A4A28" : "#2A6040"),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize   = 10,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                Grid.SetColumn(tb, i);
                CalWeekHeader.Children.Add(tb);
            }

            // ---- Day grid ----
            CalDaysGrid.Children.Clear();
            CalDaysGrid.ColumnDefinitions.Clear();
            CalDaysGrid.RowDefinitions.Clear();

            for (int i = 0; i < 7; i++)
                CalDaysGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 6; i++)
                CalDaysGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });

            // First day of month — what weekday? (Mon=0)
            var firstDay  = new DateTime(year, mon, 1);
            var startCol  = ((int)firstDay.DayOfWeek + 6) % 7; // Mon=0
            var daysInMon = DateTime.DaysInMonth(year, mon);

            int col = startCol, row = 0;
            for (int d = 1; d <= daysInMon; d++)
            {
                var date    = new DateTime(year, mon, d);
                var isToday = date == DateTime.Today;
                var isSel   = date == _calSelectedDate.Date;
                var hasEvts = cal.HasEventsOnDay(date);
                var isWknd  = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

                var cellDate = date; // capture for lambda
                var cell = new Border
                {
                    Margin          = new Thickness(2),
                    CornerRadius    = new CornerRadius(6),
                    Background      = isSel   ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentCalSel"] :
                                      isToday ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentCalToday"] : MakeBrush("Transparent"),
                    BorderBrush     = isToday ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBase"] :
                                      isSel   ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentCalBorder"] : MakeBrush("Transparent"),
                    BorderThickness = new Thickness(isToday ? 1 : 0),
                    Cursor          = System.Windows.Input.Cursors.Hand
                };

                var inner = new StackPanel
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                };
                inner.Children.Add(new TextBlock
                {
                    Text       = d.ToString(),
                    FontSize   = 12,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    Foreground = isToday ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBase"] :
                                 isWknd  ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentCalWnd"] : (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["Brush_6A9878"],
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                });
                if (hasEvts)
                {
                    inner.Children.Add(new System.Windows.Shapes.Ellipse
                    {
                        Width  = 4, Height = 4,
                        Fill   = (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBase"],
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        Margin = new Thickness(0, 2, 0, 0)
                    });
                }
                cell.Child = inner;
                cell.MouseLeftButtonUp += (s, e) => SelectCalDay(cellDate);

                Grid.SetColumn(cell, col);
                Grid.SetRow(cell, row);
                CalDaysGrid.Children.Add(cell);

                col++;
                if (col == 7) { col = 0; row++; }
            }

            // Refresh selected day panel
            SelectCalDay(_calSelectedDate);
        }

        private void SelectCalDay(DateTime date)
        {
            _calSelectedDate = date;
            var cal    = ServiceContainer.Calendar;
            var events = cal.GetForDay(date);

            CalSelectedLabel.Text   = date.ToString("dd MMMM yyyy, dddd").ToLower();
            CalSelectedPanel.Visibility = Visibility.Visible;

            CalEventsList.ItemsSource = events.Select(ev => new CalEventVm
            {
                Id      = ev.Id,
                Title   = ev.Title,
                TimeStr = ev.EventAt.ToString("HH:mm"),
                DateStr = ev.EventAt.ToString("dd.MM HH:mm")
            }).ToList();

            // Highlight selected cell (re-draw)
            RefreshCalendarHighlight();
        }

        private void RefreshCalendarHighlight()
        {
            var year = _calViewDate.Year;
            var mon  = _calViewDate.Month;
            // Quick redraw without full rebuild
            foreach (Border cell in CalDaysGrid.Children.OfType<Border>())
            {
                if (cell.Child is StackPanel sp &&
                    sp.Children.Count > 0 &&
                    sp.Children[0] is TextBlock tb &&
                    int.TryParse(tb.Text, out int d))
                {
                    var date   = new DateTime(year, mon, d);
                    var isSel  = date == _calSelectedDate.Date;
                    var isToday = date == DateTime.Today;
                    cell.Background     = isSel   ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentCalSel"] :
                                          isToday ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentCalToday"] : MakeBrush("Transparent");
                    cell.BorderBrush    = isToday ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentBase"] :
                                          isSel   ? (System.Windows.Media.SolidColorBrush)System.Windows.Application.Current.Resources["AccentCalBorder"] : MakeBrush("Transparent");
                    cell.BorderThickness = new Thickness(isToday || isSel ? 1 : 0);
                }
            }
        }

        private void AddEvent_Click(object sender, RoutedEventArgs e)
        {
            var title = EventTitleBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(title)) return;

            var timeStr = EventTimeBox.Text.Trim();
            var time    = TimeSpan.Zero;
            if (TimeSpan.TryParse(timeStr, out var t)) time = t;

            var ev = new Services.CalendarEvent
            {
                Title       = title,
                EventAt     = _calSelectedDate.Date + time,
                Description = EventDescBox.Text.Trim()
            };
            ServiceContainer.Calendar.Add(ev);

            EventTitleBox.Text = "";
            EventDescBox.Text  = "";
            EventTimeBox.Text  = "17:00";

            RefreshCalendar();
            RefreshUpcoming();
        }

        private void DeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string id)
            {
                ServiceContainer.Calendar.Delete(id);
                RefreshCalendar();
                RefreshUpcoming();
            }
        }

        private void RefreshUpcoming()
        {
            var upcoming = ServiceContainer.Calendar.GetUpcoming(60);
            UpcomingEventsList.ItemsSource = upcoming.Select(ev => new CalEventVm
            {
                Id      = ev.Id,
                Title   = ev.Title,
                DateStr = ev.EventAt.ToString("dd.MM · HH:mm"),
            }).ToList();
        }
    }
}
