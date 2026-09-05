using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace GenshinDesktopPet
{
    public sealed class QuickChatWindow : Window
    {
        private const string Placeholder = "和派蒙说点什么……";
        private readonly ChatSettingsStore settingsStore;
        private readonly PaimonChatEngine engine;
        private readonly TextBox inputBox;
        private readonly Button sendButton;
        private PetWindow anchorPet;
        private bool showingPlaceholder;
        private bool closingPermanently;
        private bool busy;

        public event EventHandler MessageSent;
        public event EventHandler<ChatReplyEventArgs> ReplyProgress;
        public event EventHandler<ChatReplyEventArgs> ReplyReceived;
        public event EventHandler SettingsRequested;

        public QuickChatWindow(ChatSettingsStore settingsStore, PaimonChatEngine engine)
        {
            this.settingsStore = settingsStore;
            this.engine = engine;
            Title = "派蒙快捷聊天";
            Width = 300;
            Height = 42;
            MinWidth = 240;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            SizeToContent = SizeToContent.Manual;
            FontFamily = new FontFamily("Microsoft YaHei UI");

            Border shell = new Border();
            shell.Background = new SolidColorBrush(Color.FromArgb(246, 255, 255, 255));
            shell.BorderBrush = new SolidColorBrush(Color.FromRgb(105, 132, 188));
            shell.BorderThickness = new Thickness(1.5);
            shell.CornerRadius = new CornerRadius(12);
            shell.Padding = new Thickness(10, 4, 4, 4);
            shell.Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Opacity = 0.22 };

            Grid line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inputBox = new TextBox();
            inputBox.BorderThickness = new Thickness(0);
            inputBox.Background = Brushes.Transparent;
            inputBox.VerticalContentAlignment = VerticalAlignment.Center;
            inputBox.FontSize = 13;
            inputBox.Padding = new Thickness(1, 0, 4, 0);
            inputBox.PreviewKeyDown += OnInputKeyDown;
            inputBox.GotKeyboardFocus += delegate { ClearPlaceholder(); };
            inputBox.LostKeyboardFocus += delegate { RestorePlaceholder(); };
            line.Children.Add(inputBox);

            sendButton = new Button();
            sendButton.Content = "发送";
            sendButton.MinWidth = 52;
            sendButton.Padding = new Thickness(7, 3, 7, 3);
            sendButton.BorderThickness = new Thickness(0);
            sendButton.Background = new SolidColorBrush(Color.FromRgb(82, 110, 166));
            sendButton.Foreground = Brushes.White;
            sendButton.Click += delegate { SendCurrent(); };
            Grid.SetColumn(sendButton, 1);
            line.Children.Add(sendButton);
            shell.Child = line;
            Content = shell;

            PreviewMouseDown += delegate { Activate(); };
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            {
                if (!closingPermanently)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
            RestorePlaceholder();
        }

        public void AttachToPet(PetWindow pet)
        {
            if (ReferenceEquals(anchorPet, pet))
            {
                Reposition();
                return;
            }
            if (anchorPet != null)
            {
                anchorPet.LocationChanged -= OnPetBoundsChanged;
                anchorPet.SizeChanged -= OnPetBoundsChanged;
            }
            anchorPet = pet;
            if (anchorPet != null)
            {
                anchorPet.LocationChanged += OnPetBoundsChanged;
                anchorPet.SizeChanged += OnPetBoundsChanged;
            }
            Reposition();
        }

        private void OnPetBoundsChanged(object sender, EventArgs e)
        {
            Reposition();
        }

        public void ShowQuick()
        {
            if (anchorPet == null || !anchorPet.IsVisible) return;
            if (!IsVisible) Show();
            Reposition();
        }

        public void FocusInput()
        {
            ShowQuick();
            if (!IsVisible) return;
            Activate();
            inputBox.Focus();
            Keyboard.Focus(inputBox);
            ClearPlaceholder();
        }

        public void Reposition()
        {
            if (anchorPet == null) return;
            Rect workArea = SystemParameters.WorkArea;
            Rect petBounds = new Rect(anchorPet.Left, anchorPet.Top, anchorPet.ActualWidth > 0 ? anchorPet.ActualWidth : anchorPet.Width, anchorPet.ActualHeight > 0 ? anchorPet.ActualHeight : anchorPet.Height);
            Point point = CalculateAnchorPosition(workArea, petBounds, new Size(Width, Height));
            Left = Math.Round(point.X);
            Top = Math.Round(point.Y);
        }

        internal static Point CalculateAnchorPosition(Rect workArea, Rect petBounds, Size barSize)
        {
            const double gap = 8.0;
            double bottomAligned = Math.Max(workArea.Top, Math.Min(workArea.Bottom - barSize.Height, petBounds.Bottom - barSize.Height));
            double right = petBounds.Right + gap;
            if (right + barSize.Width <= workArea.Right) return new Point(right, bottomAligned);
            double left = petBounds.Left - gap - barSize.Width;
            if (left >= workArea.Left) return new Point(left, bottomAligned);
            double centered = Math.Max(workArea.Left, Math.Min(workArea.Right - barSize.Width, petBounds.Left + (petBounds.Width - barSize.Width) / 2.0));
            double above = Math.Max(workArea.Top, petBounds.Top - gap - barSize.Height);
            return new Point(centered, above);
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SendCurrent();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Keyboard.ClearFocus();
            }
        }

        private void SendCurrent()
        {
            if (busy || showingPlaceholder) return;
            string text = inputBox.Text.Trim();
            if (text.Length == 0) return;
            ChatSettings settings = settingsStore.Load();
            if (!engine.CanReplyLocally(text) && string.IsNullOrWhiteSpace(settingsStore.GetApiKey(settings)))
            {
                EventHandler handler = SettingsRequested;
                if (handler != null) handler(this, EventArgs.Empty);
                return;
            }
            inputBox.Clear();
            SetBusy(true);
            EventHandler sent = MessageSent;
            if (sent != null) sent(this, EventArgs.Empty);
            DateTime lastProgressAt = DateTime.MinValue;
            engine.SendStreamingAsync(text, delegate(string partial)
            {
                DateTime now = DateTime.UtcNow;
                if ((now - lastProgressAt).TotalMilliseconds < 100) return;
                lastProgressAt = now;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    EventHandler<ChatReplyEventArgs> progress = ReplyProgress;
                    if (progress != null) progress(this, new ChatReplyEventArgs(partial));
                }));
            }).ContinueWith(task => Dispatcher.BeginInvoke(new Action(delegate
            {
                SetBusy(false);
                EventHandler<ChatReplyEventArgs> replied = ReplyReceived;
                if (replied != null)
                {
                    bool isError = task.IsFaulted;
                    string reply = isError ? "唔，连接失败了：" + GetError(task.Exception) : task.Result.Reply;
                    replied(this, new ChatReplyEventArgs(reply, isError));
                }
                RestorePlaceholder();
            })));
        }

        private void SetBusy(bool value)
        {
            busy = value;
            inputBox.IsEnabled = !value;
            sendButton.IsEnabled = !value;
            sendButton.Content = value ? "……" : "发送";
        }

        private void ClearPlaceholder()
        {
            if (!showingPlaceholder) return;
            showingPlaceholder = false;
            inputBox.Text = string.Empty;
            inputBox.Foreground = new SolidColorBrush(Color.FromRgb(40, 49, 67));
        }

        private void RestorePlaceholder()
        {
            if (busy || inputBox.Text.Length > 0) return;
            showingPlaceholder = true;
            inputBox.Text = Placeholder;
            inputBox.Foreground = Brushes.Gray;
        }

        private static string GetError(Exception exception)
        {
            while (exception != null && exception.InnerException != null) exception = exception.InnerException;
            return exception == null ? "未知错误" : exception.Message;
        }

        internal bool RunUiSelfTest(out string details)
        {
            Point right = CalculateAnchorPosition(new Rect(0, 0, 1920, 1040), new Rect(20, 880, 160, 160), new Size(300, 42));
            Point left = CalculateAnchorPosition(new Rect(0, 0, 1920, 1040), new Rect(1760, 880, 160, 160), new Size(300, 42));
            bool ok = inputBox != null && sendButton != null && right.X > 180 && left.X < 1760;
            details = ok ? "quick-chat-ui=ok" : "quick-chat-ui=failed";
            return ok;
        }

        public void ClosePermanently()
        {
            closingPermanently = true;
            AttachToPet(null);
            Close();
        }
    }
}
