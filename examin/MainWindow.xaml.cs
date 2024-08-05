﻿using System.Windows;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Input;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;

namespace examin
{
    public partial class MainWindow : Window
    {
        private Config _config;
        private IEnumerable<Exam>? _exams;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var mainStackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new(20)
            };

            if (!File.Exists(Config.File))
            {
                var config = new Config();

                foreach (var property in typeof(Config).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var fieldName = new Label { Content = property.Name };
                    Grid.SetColumn(fieldName, 0);

                    Control fieldInput = property.Name.Equals("password", StringComparison.CurrentCultureIgnoreCase) ? new PasswordBox() : new TextBox { Text = (string?)property.GetValue(config) };
                    fieldInput.Name = property.Name;
                    fieldInput.MinWidth = 500;
                    Grid.SetColumn(fieldInput, 1);

                    mainStackPanel.Children.Add(new Grid
                    {
                        Children = { fieldName, fieldInput },
                        ColumnDefinitions =
                        {
                            new ColumnDefinition { Width = new(1, GridUnitType.Auto) },
                            new ColumnDefinition { Width = new(1, GridUnitType.Star) }
                        }
                    });
                }

                var generateConfigButton = new Button { Content = "Generate Config" };
                generateConfigButton.Click += OnGenerateConfig;

                mainStackPanel.Children.Add(generateConfigButton);
            }
            else
            {
                _config = Config.ReadFromFile();

                var dateTimeFrom = new TextBox
                {
                    Text = new DateOnly(DateTime.Now.Year - (DateTime.Now.Month <= 8 ? 1 : 0), 9, 1).ToString(_config.DateFormat),
                    MinWidth = 200,
                    TextAlignment = TextAlignment.Center
                };

                var dateTimeTo = new TextBox
                {
                    Text = new DateOnly(DateTime.Now.Year + (DateTime.Now.Month <= 8 ? 0 : 1), 7, 8).ToString(_config.DateFormat),
                    MinWidth = 200,
                    TextAlignment = TextAlignment.Center
                };

                var fetchExams = new Button { Content = "Fetch Exams" };
                fetchExams.Click += async (sender, e) => await OnFetchExams(dateTimeFrom, dateTimeTo, fetchExams);

                mainStackPanel.Children.Add(dateTimeFrom);
                mainStackPanel.Children.Add(dateTimeTo);
                mainStackPanel.Children.Add(fetchExams);
            }

            Main.Content = mainStackPanel;
        }

        private void OnGenerateConfig(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = Config.FromUIElementCollection(((StackPanel)Main.Content).Children);
                config.WriteToFile();

                MessageBox.Show("Config generated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                OnLoaded(this, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OnFetchExams(TextBox dateTimeFrom, TextBox dateTimeTo, Button fetchExams)
        {
            UIElement[] elementsToDisable = { dateTimeFrom, dateTimeTo, fetchExams };

            foreach (var element in elementsToDisable) { element.IsEnabled = false; }
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                var from = DateOnly.ParseExact(dateTimeFrom.Text, _config.DateFormat, CultureInfo.InvariantCulture);
                var to = DateOnly.ParseExact(dateTimeTo.Text, _config.DateFormat, CultureInfo.InvariantCulture);

                var session = new WebuntisAPI.Session(_config);
                await session.TryLogin();

                _exams = await session.TryGetExams(from, to);

                if (_exams.Any())
                {
                    var mainStackPanel = (StackPanel)Main.Content;
                    mainStackPanel.Children.Clear();
                    mainStackPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                    mainStackPanel.VerticalAlignment = VerticalAlignment.Stretch;

                    var saveExams = new Button { Content = "Save to File", Padding = new(12.5, 7.5, 12.5, 7.5), Margin = new(0, 0, 5, 0), HorizontalAlignment = HorizontalAlignment.Right };
                    Grid.SetColumn(saveExams, 0);
                    saveExams.Click += OnSaveExams;

                    var pushExams = new Button { Content = "Push to Google Calendar", Padding = new(12.5, 7.5, 12.5, 7.5), Margin = new(5, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = !string.IsNullOrEmpty(_config.CalendarID.Trim()) };
                    Grid.SetColumn(pushExams, 1);
                    pushExams.Click += OnPushExams;

                    mainStackPanel.Children.Add(new Grid
                    {
                        Children = { saveExams, pushExams },
                        ColumnDefinitions =
                        {
                            new ColumnDefinition { Width = new(1, GridUnitType.Star) },
                            new ColumnDefinition { Width = new(1, GridUnitType.Star) }
                        },
                        Margin = new(0, 0, 0, 20)
                    });

                    for (var i = 0; i < _exams.Count(); i++)
                    {
                        var exam = _exams.ElementAt(i);
                        exam.TranslateSubject();

                        var subject = new Label { Content = exam.Subject, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new(0) };
                        Grid.SetColumn(subject, 0);

                        var dateTime = new Grid
                        {
                            RowDefinitions =
                            {
                                new RowDefinition { Height = new(1, GridUnitType.Auto) },
                                new RowDefinition { Height = new(1, GridUnitType.Auto) }
                            },
                            Margin = new(0)
                        };
                        var date = new Label { Content = exam.Date.ToString("dd MMMM yyyy"), FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new(0) };
                        Grid.SetRow(date, 0);
                        var time = new Label { Content = $"{exam.Start:HH:mm} - {exam.End:HH:mm}", FontSize = 16.5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new(0) };
                        Grid.SetRow(time, 1);
                        dateTime.Children.Add(date);
                        dateTime.Children.Add(time);
                        Grid.SetColumn(dateTime, 1);

                        var toggleExam = new ToggleButton { Width = 50, HorizontalAlignment = HorizontalAlignment.Right, IsChecked = true };
                        toggleExam.Checked += (sender, e) => { subject.Foreground = date.Foreground = time.Foreground = Brushes.Black; };
                        toggleExam.Unchecked += (sender, e) => { subject.Foreground = date.Foreground = time.Foreground = Brushes.Gray; };
                        Grid.SetColumn(toggleExam, 2);

                        var grid = new Grid
                        {
                            Children = { subject, dateTime, toggleExam },
                            ColumnDefinitions =
                            {
                                new ColumnDefinition { Width = new(1, GridUnitType.Star) },
                                new ColumnDefinition { Width = new(1, GridUnitType.Star) },
                                new ColumnDefinition { Width = new(1, GridUnitType.Star) }
                            },
                            MinWidth = 400
                        };

                        mainStackPanel.Children.Add(new Border
                        {
                            Child = grid,
                            Margin = new(0, 10, 0, 10),
                            Padding = new(25, 17.5, 25, 17.5),
                            Background = i % 2 == 0 ? Brushes.White : Brushes.LightGray,
                            CornerRadius = new(5)
                        });
                    }
                }
                else
                {
                    MessageBox.Show("No exams found!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (FormatException)
            {
                MessageBox.Show("Invalid date format!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                foreach (var element in elementsToDisable) { element.IsEnabled = true; }
                Mouse.OverrideCursor = null;
            }
        }

        private void OnSaveExams(object sender, RoutedEventArgs e)
        {
            var examUIElements = ((StackPanel)Main.Content).Children.Cast<UIElement>().Skip(1);

            var saveFileDialog = new SaveFileDialog
            {
                FileName = "events.csv",
                DefaultExt = "csv",
                AddExtension = true,
                Filter = "CSV Files (*.csv)|*.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                using var writer = new StreamWriter(saveFileDialog.FileName);
                writer.WriteLine("Subject,Description,Start Date,Start Time,End Time");

                foreach (var examUIElement in examUIElements)
                {
                    var grid = (Grid)((Border)examUIElement).Child;
                    var isChecked = ((ToggleButton)grid.Children[2]).IsChecked;

                    if (isChecked!.Value)
                    {
                        var subject = (string)((Label)grid.Children[0]).Content;
                        var date = (string)((Label)((Grid)grid.Children[1]).Children[0]).Content;
                        var time = (string)((Label)((Grid)grid.Children[1]).Children[1]).Content;

                        foreach (var exam in _exams!)
                        {
                            if (exam.Subject == subject && exam.Date.ToString("dd MMMM yyyy") == date && $"{exam.Start:HH:mm} - {exam.End:HH:mm}" == time)
                            {
                                writer.WriteLine($"{exam.Subject},{exam.Description},{exam.Date.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)},{exam.StartTime:h:mm tt},{exam.EndTime:h:mm tt}");
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void OnPushExams(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Push to Google Calendar");
        }
    }
}