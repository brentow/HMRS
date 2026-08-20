using System.Windows.Controls;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using HRMS.ViewModel;

namespace HRMS.View
{
    public partial class RecruitmentWindow : UserControl
    {
        private JobPostingRowVm? _editingPosting;

        public RecruitmentWindow()
        {
            InitializeComponent();
        }

        public async Task RefreshAsync()
        {
            var vm = DataContext as RecruitmentViewModel;
            if (vm == null && Content is FrameworkElement root)
            {
                vm = root.DataContext as RecruitmentViewModel;
            }

            if (vm != null)
            {
                await vm.RefreshAsync();
            }
        }

        private void NewPostingButton_OnClick(object sender, RoutedEventArgs e)
        {
            _editingPosting = null;
            EditPostingDialog.Visibility = Visibility.Collapsed;
            NewPostingDialog.Visibility = Visibility.Visible;
            PostingDialogOverlay.Visibility = Visibility.Visible;
        }

        private void ClosePostingDialogButton_OnClick(object sender, RoutedEventArgs e)
        {
            ClosePostingDialog();
        }

        private void CreatePostingButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is not RecruitmentViewModel vm ||
                string.IsNullOrWhiteSpace(vm.NewPostingCode) ||
                string.IsNullOrWhiteSpace(vm.NewPostingTitle))
            {
                return;
            }

            Dispatcher.BeginInvoke(new System.Action(ClosePostingDialog), DispatcherPriority.Background);
        }

        private void PostingActionsButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not JobPostingRowVm posting)
            {
                return;
            }

            var menu = CreateActionMenu();
            menu.Items.Add(CreatePostingMenuItem("View Applications", posting, ViewPostingApplications_OnClick));
            menu.Items.Add(CreatePostingMenuItem("Edit Posting", posting, EditPosting_OnClick));

            var closeItem = CreatePostingMenuItem("Close Posting", posting, ClosePosting_OnClick);
            closeItem.IsEnabled = !string.Equals(posting.Status, "CLOSED", System.StringComparison.OrdinalIgnoreCase) &&
                                  !string.Equals(posting.Status, "CANCELLED", System.StringComparison.OrdinalIgnoreCase);
            menu.Items.Add(closeItem);
            menu.Items.Add(new Separator());

            var deleteItem = CreatePostingMenuItem("Delete Posting", posting, DeletePosting_OnClick);
            deleteItem.Foreground = System.Windows.Media.Brushes.Firebrick;
            menu.Items.Add(deleteItem);

            OpenActionMenu(button, menu, e);
        }

        private static MenuItem CreatePostingMenuItem(
            string header,
            JobPostingRowVm posting,
            RoutedEventHandler clickHandler)
        {
            var item = new MenuItem
            {
                Header = header,
                Tag = posting,
                Padding = new Thickness(10, 6, 18, 6)
            };
            item.Click += clickHandler;
            return item;
        }

        private void ViewPostingApplications_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: JobPostingRowVm posting } ||
                DataContext is not RecruitmentViewModel vm)
            {
                return;
            }

            vm.ApplicationSearchText = posting.PostingCode;
            CandidatesTab.IsSelected = true;
            CandidateViewsTabControl.SelectedIndex = 1;
        }

        private void EditPosting_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: JobPostingRowVm posting } ||
                DataContext is not RecruitmentViewModel vm)
            {
                return;
            }

            _editingPosting = posting;
            EditPostingDialog.DataContext = posting;
            EditPostingEmploymentTypeComboBox.ItemsSource = vm.EmploymentTypes;
            EditPostingVacanciesComboBox.ItemsSource = vm.VacancyOptions;
            EditPostingStatusComboBox.ItemsSource = vm.PostingStatuses;
            NewPostingDialog.Visibility = Visibility.Collapsed;
            EditPostingDialog.Visibility = Visibility.Visible;
            PostingDialogOverlay.Visibility = Visibility.Visible;
        }

        private void SaveEditedPostingButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_editingPosting != null && DataContext is RecruitmentViewModel vm &&
                vm.SavePostingCommand.CanExecute(_editingPosting))
            {
                vm.SavePostingCommand.Execute(_editingPosting);
            }

            ClosePostingDialog();
        }

        private void ClosePosting_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: JobPostingRowVm posting } ||
                DataContext is not RecruitmentViewModel vm)
            {
                return;
            }

            posting.Status = "CLOSED";
            posting.CloseDate ??= System.DateTime.Today;
            if (vm.SavePostingCommand.CanExecute(posting))
            {
                vm.SavePostingCommand.Execute(posting);
            }
        }

        private void DeletePosting_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: JobPostingRowVm posting } ||
                DataContext is not RecruitmentViewModel vm)
            {
                return;
            }

            var confirmation = MessageBox.Show(
                $"Delete job posting {posting.PostingCode} - {posting.Title}?\n\nThis cannot be undone.",
                "Delete Job Posting",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmation == MessageBoxResult.Yes && vm.DeletePostingCommand.CanExecute(posting))
            {
                vm.DeletePostingCommand.Execute(posting);
            }
        }

        private void NewCandidateButton_OnClick(object sender, RoutedEventArgs e) => ShowRecruitmentDialog(NewCandidateDialog);
        private void NewApplicationButton_OnClick(object sender, RoutedEventArgs e) => ShowRecruitmentDialog(NewApplicationDialog);
        private void NewInterviewButton_OnClick(object sender, RoutedEventArgs e) => ShowRecruitmentDialog(NewInterviewDialog);
        private void NewOfferButton_OnClick(object sender, RoutedEventArgs e) => ShowRecruitmentDialog(NewOfferDialog);

        private void CloseRecruitmentDialogButton_OnClick(object sender, RoutedEventArgs e) => ClosePostingDialog();

        private void CreateCandidateButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is RecruitmentViewModel vm &&
                !string.IsNullOrWhiteSpace(vm.NewApplicantNo) &&
                !string.IsNullOrWhiteSpace(vm.NewApplicantFirstName) &&
                !string.IsNullOrWhiteSpace(vm.NewApplicantLastName))
            {
                Dispatcher.BeginInvoke(new System.Action(ClosePostingDialog), DispatcherPriority.Background);
            }
        }

        private void CreateApplicationButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is RecruitmentViewModel vm && vm.SelectedApplicationApplicantId.HasValue && vm.SelectedApplicationPostingId.HasValue)
            {
                Dispatcher.BeginInvoke(new System.Action(ClosePostingDialog), DispatcherPriority.Background);
            }
        }

        private void CreateInterviewButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is RecruitmentViewModel vm && vm.SelectedInterviewApplicationId.HasValue)
            {
                Dispatcher.BeginInvoke(new System.Action(ClosePostingDialog), DispatcherPriority.Background);
            }
        }

        private void CreateOfferButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is RecruitmentViewModel vm && vm.SelectedOfferApplicationId.HasValue)
            {
                Dispatcher.BeginInvoke(new System.Action(ClosePostingDialog), DispatcherPriority.Background);
            }
        }

        private void CandidateActionsButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ApplicantRowVm candidate } button)
            {
                return;
            }

            var menu = CreateActionMenu();
            menu.Items.Add(CreateActionMenuItem("View Applications", candidate, ViewCandidateApplications_OnClick));
            menu.Items.Add(CreateActionMenuItem("Edit Candidate", candidate, EditCandidate_OnClick));
            menu.Items.Add(new Separator());
            var delete = CreateActionMenuItem("Delete Candidate", candidate, DeleteCandidate_OnClick);
            delete.Foreground = System.Windows.Media.Brushes.Firebrick;
            menu.Items.Add(delete);
            OpenActionMenu(button, menu, e);
        }

        private void ApplicationActionsButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ApplicationRowVm application } button)
            {
                return;
            }

            var menu = CreateActionMenu();
            menu.Items.Add(CreateActionMenuItem("Edit Status & Notes", application, EditApplication_OnClick));
            menu.Items.Add(CreateActionMenuItem("Schedule Interview", application, ScheduleApplicationInterview_OnClick));
            menu.Items.Add(CreateActionMenuItem("Create Offer", application, CreateApplicationOffer_OnClick));
            menu.Items.Add(new Separator());
            var delete = CreateActionMenuItem("Delete Application", application, DeleteApplication_OnClick);
            delete.Foreground = System.Windows.Media.Brushes.Firebrick;
            menu.Items.Add(delete);
            OpenActionMenu(button, menu, e);
        }

        private void InterviewActionsButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: InterviewRowVm interview } button)
            {
                return;
            }

            var menu = CreateActionMenu();
            menu.Items.Add(CreateActionMenuItem("Edit Interview", interview, EditInterview_OnClick));
            var done = CreateActionMenuItem("Mark as Done", interview, MarkInterviewDone_OnClick);
            done.IsEnabled = !string.Equals(interview.Status, "DONE", System.StringComparison.OrdinalIgnoreCase);
            menu.Items.Add(done);
            menu.Items.Add(new Separator());
            var delete = CreateActionMenuItem("Delete Interview", interview, DeleteInterview_OnClick);
            delete.Foreground = System.Windows.Media.Brushes.Firebrick;
            menu.Items.Add(delete);
            OpenActionMenu(button, menu, e);
        }

        private void OfferActionsButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: OfferRowVm offer } button)
            {
                return;
            }

            var menu = CreateActionMenu();
            menu.Items.Add(CreateActionMenuItem("Edit Offer", offer, EditOffer_OnClick));
            var accepted = CreateActionMenuItem("Mark as Accepted", offer, MarkOfferAccepted_OnClick);
            accepted.IsEnabled = !string.Equals(offer.OfferStatus, "ACCEPTED", System.StringComparison.OrdinalIgnoreCase);
            menu.Items.Add(accepted);
            menu.Items.Add(new Separator());
            var delete = CreateActionMenuItem("Delete Offer", offer, DeleteOffer_OnClick);
            delete.Foreground = System.Windows.Media.Brushes.Firebrick;
            menu.Items.Add(delete);
            OpenActionMenu(button, menu, e);
        }

        private static MenuItem CreateActionMenuItem(string header, object record, RoutedEventHandler clickHandler)
        {
            var item = new MenuItem { Header = header, Tag = record, Padding = new Thickness(10, 6, 18, 6) };
            item.Click += clickHandler;
            return item;
        }

        private ContextMenu CreateActionMenu()
        {
            var menu = new ContextMenu
            {
                Style = (Style)FindResource("RecruitmentContextMenuStyle")
            };
            menu.Resources[typeof(MenuItem)] = FindResource("RecruitmentMenuItemStyle");
            menu.Resources[typeof(Separator)] = FindResource("RecruitmentMenuSeparatorStyle");
            return menu;
        }

        private static void OpenActionMenu(Button button, ContextMenu menu, RoutedEventArgs e)
        {
            button.ContextMenu = menu;
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Left;
            menu.HorizontalOffset = -6;
            menu.IsOpen = true;
            e.Handled = true;
        }

        private void ViewCandidateApplications_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: ApplicantRowVm candidate } && DataContext is RecruitmentViewModel vm)
            {
                vm.ApplicationSearchText = candidate.ApplicantNo;
                CandidateViewsTabControl.SelectedIndex = 1;
            }
        }

        private void EditCandidate_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: ApplicantRowVm candidate } || DataContext is not RecruitmentViewModel vm)
            {
                return;
            }

            var firstName = CreateEditTextBox(candidate.FirstName);
            var lastName = CreateEditTextBox(candidate.LastName);
            var middleName = CreateEditTextBox(candidate.MiddleName);
            var email = CreateEditTextBox(candidate.Email);
            var mobile = CreateEditTextBox(candidate.MobileNo);
            var address = CreateEditTextBox(candidate.Address);
            var birthDate = CreateEditDatePicker(candidate.BirthDate);
            if (!ShowRecordEditor("Edit Candidate", candidate.FullName,
                    ("First Name", firstName), ("Last Name", lastName), ("Middle Name", middleName),
                    ("Email", email), ("Mobile", mobile), ("Birth Date", birthDate), ("Address", address)))
            {
                return;
            }

            candidate.FirstName = firstName.Text.Trim();
            candidate.LastName = lastName.Text.Trim();
            candidate.MiddleName = middleName.Text.Trim();
            candidate.Email = email.Text.Trim();
            candidate.MobileNo = mobile.Text.Trim();
            candidate.BirthDate = birthDate.SelectedDate;
            candidate.Address = address.Text.Trim();
            ExecuteCommand(vm.SaveApplicantCommand, candidate);
        }

        private void EditApplication_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: ApplicationRowVm application } || DataContext is not RecruitmentViewModel vm)
            {
                return;
            }

            var status = CreateEditComboBox(vm.ApplicationStatuses, application.Status);
            var notes = CreateEditTextBox(application.Notes);
            if (!ShowRecordEditor("Edit Application", $"{application.ApplicantName} · {application.PostingCode}", ("Status", status), ("Notes", notes)))
            {
                return;
            }

            application.Status = status.SelectedItem?.ToString() ?? application.Status;
            application.Notes = notes.Text.Trim();
            ExecuteCommand(vm.SaveApplicationCommand, application);
        }

        private void EditInterview_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: InterviewRowVm interview } || DataContext is not RecruitmentViewModel vm)
            {
                return;
            }

            var date = CreateEditDatePicker(interview.InterviewDate);
            var time = CreateEditTextBox(interview.InterviewTimeText);
            var type = CreateEditComboBox(vm.InterviewTypes, interview.InterviewType);
            var interviewer = CreateEditComboBox(vm.EmployeeOptions, interview.InterviewerEmployeeId, "Label", "EmployeeId");
            var status = CreateEditComboBox(vm.InterviewStatuses, interview.Status);
            var location = CreateEditTextBox(interview.Location);
            var remarks = CreateEditTextBox(interview.Remarks);
            if (!ShowRecordEditor("Edit Interview", $"{interview.ApplicantName} · {interview.PostingCode}",
                    ("Date", date), ("Time (HH:mm)", time), ("Type", type), ("Interviewer", interviewer),
                    ("Status", status), ("Location", location), ("Remarks", remarks)))
            {
                return;
            }

            interview.InterviewDate = date.SelectedDate ?? interview.InterviewDate;
            interview.InterviewTimeText = time.Text.Trim();
            interview.InterviewType = type.SelectedItem?.ToString() ?? interview.InterviewType;
            interview.InterviewerEmployeeId = interviewer.SelectedValue is int employeeId ? employeeId : null;
            interview.Status = status.SelectedItem?.ToString() ?? interview.Status;
            interview.Location = location.Text.Trim();
            interview.Remarks = remarks.Text.Trim();
            ExecuteCommand(vm.SaveInterviewCommand, interview);
        }

        private void EditOffer_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: OfferRowVm offer } || DataContext is not RecruitmentViewModel vm)
            {
                return;
            }

            var status = CreateEditComboBox(vm.OfferStatuses, offer.OfferStatus);
            var salary = CreateEditTextBox(offer.SalaryOfferText);
            var startDate = CreateEditDatePicker(offer.StartDate);
            var remarks = CreateEditTextBox(offer.Remarks);
            if (!ShowRecordEditor("Edit Job Offer", $"{offer.ApplicantName} · {offer.PostingCode}",
                    ("Status", status), ("Salary Offer", salary), ("Start Date", startDate), ("Remarks", remarks)))
            {
                return;
            }

            offer.OfferStatus = status.SelectedItem?.ToString() ?? offer.OfferStatus;
            offer.SalaryOfferText = salary.Text.Trim();
            offer.StartDate = startDate.SelectedDate;
            offer.Remarks = remarks.Text.Trim();
            ExecuteCommand(vm.SaveOfferCommand, offer);
        }

        private void ScheduleApplicationInterview_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: ApplicationRowVm application } && DataContext is RecruitmentViewModel vm)
            {
                vm.SelectedInterviewApplicationId = application.JobApplicationId;
                InterviewsTab.IsSelected = true;
                ShowRecruitmentDialog(NewInterviewDialog);
            }
        }

        private void CreateApplicationOffer_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: ApplicationRowVm application } && DataContext is RecruitmentViewModel vm)
            {
                vm.SelectedOfferApplicationId = application.JobApplicationId;
                OffersTab.IsSelected = true;
                ShowRecruitmentDialog(NewOfferDialog);
            }
        }

        private void MarkInterviewDone_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: InterviewRowVm interview } && DataContext is RecruitmentViewModel vm)
            {
                interview.Status = "DONE";
                ExecuteCommand(vm.SaveInterviewCommand, interview);
            }
        }

        private void MarkOfferAccepted_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: OfferRowVm offer } && DataContext is RecruitmentViewModel vm)
            {
                offer.OfferStatus = "ACCEPTED";
                ExecuteCommand(vm.SaveOfferCommand, offer);
            }
        }

        private void DeleteCandidate_OnClick(object sender, RoutedEventArgs e) =>
            ConfirmAndExecuteDelete(sender, "candidate", "Delete Candidate", vm => vm.DeleteApplicantCommand);

        private void DeleteApplication_OnClick(object sender, RoutedEventArgs e) =>
            ConfirmAndExecuteDelete(sender, "application", "Delete Application", vm => vm.DeleteApplicationCommand);

        private void DeleteInterview_OnClick(object sender, RoutedEventArgs e) =>
            ConfirmAndExecuteDelete(sender, "interview", "Delete Interview", vm => vm.DeleteInterviewCommand);

        private void DeleteOffer_OnClick(object sender, RoutedEventArgs e) =>
            ConfirmAndExecuteDelete(sender, "offer", "Delete Offer", vm => vm.DeleteOfferCommand);

        private void ConfirmAndExecuteDelete(
            object sender,
            string recordLabel,
            string title,
            System.Func<RecruitmentViewModel, System.Windows.Input.ICommand> commandSelector)
        {
            if (sender is not MenuItem item || item.Tag == null || DataContext is not RecruitmentViewModel vm)
            {
                return;
            }

            var record = item.Tag;

            var result = MessageBox.Show($"Delete this {recordLabel}?\n\nThis cannot be undone.", title,
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                ExecuteCommand(commandSelector(vm), record);
            }
        }

        private void ShowRecruitmentDialog(Border dialog)
        {
            HideAllRecruitmentDialogs();
            dialog.Visibility = Visibility.Visible;
            PostingDialogOverlay.Visibility = Visibility.Visible;
        }

        private void HideAllRecruitmentDialogs()
        {
            NewPostingDialog.Visibility = Visibility.Collapsed;
            EditPostingDialog.Visibility = Visibility.Collapsed;
            NewCandidateDialog.Visibility = Visibility.Collapsed;
            NewApplicationDialog.Visibility = Visibility.Collapsed;
            NewInterviewDialog.Visibility = Visibility.Collapsed;
            NewOfferDialog.Visibility = Visibility.Collapsed;
        }

        private static void ExecuteCommand(System.Windows.Input.ICommand command, object parameter)
        {
            if (command.CanExecute(parameter))
            {
                command.Execute(parameter);
            }
        }

        private TextBox CreateEditTextBox(string? value) => new()
        {
            Text = value ?? string.Empty,
            Style = (Style)Resources["InputTextBoxStyle"]
        };

        private DatePicker CreateEditDatePicker(System.DateTime? value) => new()
        {
            SelectedDate = value,
            Style = (Style)Resources["InputDateStyle"],
            Height = 42,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 251, 255)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(199, 216, 238)),
            BorderThickness = new Thickness(1)
        };

        private ComboBox CreateEditComboBox(
            System.Collections.IEnumerable itemsSource,
            object? selected,
            string? displayMemberPath = null,
            string? selectedValuePath = null)
        {
            var comboBox = new ComboBox
            {
                ItemsSource = itemsSource,
                Style = (Style)Resources["InputComboStyle"]
            };

            if (!string.IsNullOrWhiteSpace(displayMemberPath))
            {
                comboBox.DisplayMemberPath = displayMemberPath;
            }

            if (!string.IsNullOrWhiteSpace(selectedValuePath))
            {
                comboBox.SelectedValuePath = selectedValuePath;
                comboBox.SelectedValue = selected;
            }
            else
            {
                comboBox.SelectedItem = selected;
            }

            return comboBox;
        }

        private bool ShowRecordEditor(
            string title,
            string subtitle,
            params (string Label, FrameworkElement Editor)[] fields)
        {
            var primary = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 67, 104));
            var muted = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(90, 106, 124));
            var borderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(216, 229, 243));
            var panelBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 251, 255));

            var window = new Window
            {
                Title = title,
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                SizeToContent = SizeToContent.Height,
                Width = 760,
                MaxHeight = 760,
                Background = System.Windows.Media.Brushes.Transparent,
                ShowInTaskbar = false
            };

            var root = new Grid { Width = 720 };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                Padding = new Thickness(24, 22, 20, 18)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var heading = new StackPanel();
            heading.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = primary
            });
            heading.Children.Add(new TextBlock
            {
                Text = subtitle,
                Margin = new Thickness(0, 4, 18, 0),
                Foreground = muted,
                TextWrapping = TextWrapping.Wrap
            });
            var close = new Button
            {
                Content = new TextBlock { Text = "×", FontSize = 22, FontWeight = FontWeights.SemiBold },
                Width = 38,
                Height = 38,
                Padding = new Thickness(0),
                Style = (Style)Resources["SecondaryButtonStyle"],
                IsCancel = true
            };
            close.Click += (_, _) => window.DialogResult = false;
            Grid.SetColumn(close, 1);
            headerGrid.Children.Add(heading);
            headerGrid.Children.Add(close);
            header.Child = headerGrid;
            root.Children.Add(header);

            var fieldsGrid = new Grid { Margin = new Thickness(16, 4, 16, 8) };
            fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var index = 0; index < fields.Length; index++)
            {
                var row = index / 2;
                var column = index % 2;
                if (column == 0)
                {
                    fieldsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                var field = new StackPanel
                {
                    Margin = column == 0
                        ? new Thickness(8, 8, 7, 6)
                        : new Thickness(7, 8, 8, 6)
                };
                var label = new TextBlock
                {
                    Text = fields[index].Label,
                    Margin = new Thickness(0, 0, 0, 6),
                    FontWeight = FontWeights.SemiBold,
                    Foreground = muted
                };
                var editor = fields[index].Editor;
                editor.Margin = new Thickness(0);
                editor.HorizontalAlignment = HorizontalAlignment.Stretch;
                field.Children.Add(label);
                field.Children.Add(editor);
                Grid.SetRow(field, row);
                Grid.SetColumn(field, column);
                fieldsGrid.Children.Add(field);
            }

            var fieldsPanel = new Border
            {
                Margin = new Thickness(16, 0, 16, 8),
                Padding = new Thickness(0, 8, 0, 8),
                Background = panelBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Child = new ScrollViewer
                {
                    Content = fieldsGrid,
                    MaxHeight = 480,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                }
            };
            Grid.SetRow(fieldsPanel, 1);
            root.Children.Add(fieldsPanel);

            var footer = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 249, 254)),
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(22, 15, 22, 18)
            };
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var cancel = new Button
            {
                Content = "Cancel",
                Width = 105,
                Height = 40,
                Margin = new Thickness(0, 0, 8, 0),
                Style = (Style)Resources["SecondaryButtonStyle"],
                IsCancel = true
            };
            var save = new Button
            {
                Content = "Save Changes",
                Width = 140,
                Height = 40,
                Style = (Style)Resources["PrimaryButtonStyle"],
                IsDefault = true
            };
            save.Click += (_, _) => window.DialogResult = true;
            actions.Children.Add(cancel);
            actions.Children.Add(save);
            footer.Child = actions;
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            var card = new Border
            {
                Margin = new Thickness(20),
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                ClipToBounds = true,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = System.Windows.Media.Color.FromRgb(16, 40, 63),
                    BlurRadius = 28,
                    ShadowDepth = 5,
                    Opacity = 0.28
                },
                Child = root
            };
            window.Content = card;
            return window.ShowDialog() == true;
        }

        private void ClosePostingDialog()
        {
            PostingDialogOverlay.Visibility = Visibility.Collapsed;
            HideAllRecruitmentDialogs();
            EditPostingDialog.DataContext = null;
            _editingPosting = null;
        }
    }
}
