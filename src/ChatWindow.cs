using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GenshinDesktopPet
{
    public sealed class ChatWindow : Window
    {
        private readonly ChatSettingsStore settingsStore;
        private readonly PaimonChatEngine engine;
        private readonly StackPanel transcript;
        private readonly ScrollViewer scrollViewer;
        private readonly TextBox inputBox;
        private readonly Button sendButton;
        private readonly Button compactButton;
        private readonly TextBlock statusText;
        private bool allowClose;
        private bool busy;

        public event EventHandler UserMessageSent;
        public event EventHandler<ChatReplyEventArgs> PaimonReplyProgress;
        public event EventHandler<ChatReplyEventArgs> PaimonReplied;

        public ChatWindow(ChatSettingsStore settingsStore, ChatMemoryStore memoryStore)
            : this(settingsStore, new PaimonChatEngine(settingsStore, memoryStore))
        {
        }

        public ChatWindow(ChatSettingsStore settingsStore, PaimonChatEngine engine)
        {
            this.settingsStore = settingsStore;
            this.engine = engine;
            Title = "和派蒙聊天";
            Width = 500;
            Height = 650;
            MinWidth = 400;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(245, 248, 253));
            FontFamily = new FontFamily("Microsoft YaHei UI");

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            Grid header = new Grid();
            header.Background = new SolidColorBrush(Color.FromRgb(70, 91, 133));
            header.Margin = new Thickness(0);
            TextBlock title = new TextBlock();
            title.Text = "派蒙 · 提瓦特最好的向导";
            title.Foreground = Brushes.White;
            title.FontSize = 18;
            title.FontWeight = FontWeights.SemiBold;
            title.Margin = new Thickness(18, 13, 18, 13);
            header.Children.Add(title);
            root.Children.Add(header);

            transcript = new StackPanel();
            transcript.Margin = new Thickness(12);
            scrollViewer = new ScrollViewer();
            scrollViewer.Content = transcript;
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            Grid.SetRow(scrollViewer, 1);
            root.Children.Add(scrollViewer);

            Grid composer = new Grid();
            composer.Margin = new Thickness(12, 4, 12, 8);
            composer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            composer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inputBox = new TextBox();
            inputBox.AcceptsReturn = true;
            inputBox.TextWrapping = TextWrapping.Wrap;
            inputBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            inputBox.MinHeight = 72;
            inputBox.MaxHeight = 130;
            inputBox.Padding = new Thickness(8);
            inputBox.ToolTip = "输入消息，Ctrl+Enter 发送";
            inputBox.PreviewKeyDown += OnInputKeyDown;
            composer.Children.Add(inputBox);
            sendButton = new Button();
            sendButton.Content = "发送";
            sendButton.Width = 72;
            sendButton.Margin = new Thickness(8, 0, 0, 0);
            sendButton.Click += delegate { SendCurrent(); };
            Grid.SetColumn(sendButton, 1);
            composer.Children.Add(sendButton);
            Grid.SetRow(composer, 2);
            root.Children.Add(composer);

            Grid footer = new Grid();
            footer.Margin = new Thickness(12, 0, 12, 12);
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            statusText = new TextBlock();
            statusText.VerticalAlignment = VerticalAlignment.Center;
            statusText.FontSize = 11;
            statusText.Foreground = Brushes.DimGray;
            footer.Children.Add(statusText);
            compactButton = CreateFooterButton("整理记忆");
            compactButton.Click += delegate { CompactMemory(); };
            Grid.SetColumn(compactButton, 1);
            footer.Children.Add(compactButton);
            Button settingsButton = CreateFooterButton("模型设置");
            settingsButton.Click += delegate { OpenSettings(); };
            Grid.SetColumn(settingsButton, 2);
            footer.Children.Add(settingsButton);
            Button clearButton = CreateFooterButton("清空当前对话");
            clearButton.Click += delegate { ClearConversation(); };
            Grid.SetColumn(clearButton, 3);
            footer.Children.Add(clearButton);
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            {
                if (!allowClose)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
            Loaded += delegate { inputBox.Focus(); ScrollToBottom(); };
            Activated += delegate { RefreshTranscript(); };
            RefreshTranscript();
        }

        private static Button CreateFooterButton(string content)
        {
            Button button = new Button();
            button.Content = content;
            button.Margin = new Thickness(6, 0, 0, 0);
            button.Padding = new Thickness(8, 4, 8, 4);
            return button;
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true;
                SendCurrent();
            }
        }

        private void RefreshTranscript()
        {
            transcript.Children.Clear();
            foreach (ChatMessageRecord message in engine.GetRecentDisplayMessages())
            {
                AddBubble(message.Role, message.Content);
            }
            if (transcript.Children.Count == 0)
            {
                AddBubble("assistant", "旅行者，你来啦！想和派蒙聊些什么？");
            }
            statusText.Text = engine.GetMemoryStatus();
            ScrollToBottom();
        }

        public void RefreshFromMemory()
        {
            RefreshTranscript();
        }

        private TextBlock AddBubble(string role, string content)
        {
            bool isPaimon = string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase);
            Border bubble = new Border();
            bubble.Background = new SolidColorBrush(isPaimon ? Color.FromRgb(237, 240, 250) : Color.FromRgb(220, 238, 255));
            bubble.CornerRadius = new CornerRadius(10);
            bubble.Padding = new Thickness(11, 8, 11, 8);
            bubble.Margin = new Thickness(isPaimon ? 0 : 58, 4, isPaimon ? 58 : 0, 4);
            bubble.HorizontalAlignment = isPaimon ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            TextBlock text = new TextBlock();
            text.Text = (isPaimon ? "派蒙：" : "旅行者：") + content;
            text.TextWrapping = TextWrapping.Wrap;
            text.MaxWidth = 355;
            text.Foreground = new SolidColorBrush(Color.FromRgb(39, 49, 68));
            bubble.Child = text;
            transcript.Children.Add(bubble);
            return text;
        }

        private void SendCurrent()
        {
            if (busy) return;
            string text = inputBox.Text.Trim();
            if (text.Length == 0) return;
            ChatSettings settings = settingsStore.Load();
            bool localReply = engine.CanReplyLocally(text);
            if (!localReply && string.IsNullOrWhiteSpace(settingsStore.GetApiKey(settings)))
            {
                MessageBox.Show(this, "先在“模型设置”里选择服务商并填写 API Key，派蒙才能回复。", "尚未配置模型", MessageBoxButton.OK, MessageBoxImage.Information);
                OpenSettings();
                return;
            }
            inputBox.Clear();
            AddBubble("user", text);
            TextBlock pendingReply = AddBubble("assistant", "派蒙想想……");
            ScrollToBottom();
            SetBusy(true, localReply ? "正在使用本地快速回复……" : "正在请求 " + settings.Provider + " / " + settings.Model + "……");
            EventHandler sent = UserMessageSent;
            if (sent != null) sent(this, EventArgs.Empty);
            DateTime lastProgressAt = DateTime.MinValue;
            engine.SendStreamingAsync(text, delegate(string partial)
            {
                DateTime now = DateTime.UtcNow;
                if ((now - lastProgressAt).TotalMilliseconds < 100) return;
                lastProgressAt = now;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    pendingReply.Text = "派蒙：" + partial;
                    statusText.Text = "派蒙正在回答……";
                    EventHandler<ChatReplyEventArgs> progress = PaimonReplyProgress;
                    if (progress != null) progress(this, new ChatReplyEventArgs(partial));
                    ScrollToBottom();
                }));
            }).ContinueWith(task => Dispatcher.BeginInvoke(new Action(delegate
            {
                if (task.IsFaulted)
                {
                    string error = GetError(task.Exception);
                    pendingReply.Text = "派蒙：唔，连接没有成功：" + error;
                    statusText.Text = "发送失败；消息未得到回复。";
                }
                else
                {
                    pendingReply.Text = "派蒙：" + task.Result.Reply;
                    statusText.Text = engine.GetMemoryStatus();
                    if (!string.IsNullOrWhiteSpace(task.Result.CompactionStatus) && task.Result.CompactionStatus.IndexOf("暂无", StringComparison.Ordinal) < 0)
                        statusText.Text += " · " + task.Result.CompactionStatus;
                    EventHandler<ChatReplyEventArgs> replied = PaimonReplied;
                    if (replied != null) replied(this, new ChatReplyEventArgs(task.Result.Reply));
                }
                SetBusy(false, statusText.Text);
                ScrollToBottom();
                inputBox.Focus();
            })));
        }

        private void CompactMemory()
        {
            if (busy) return;
            ChatSettings settings = settingsStore.Load();
            if (string.IsNullOrWhiteSpace(settingsStore.GetApiKey(settings)))
            {
                MessageBox.Show(this, "整理摘要需要先配置 API Key。", "尚未配置模型", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SetBusy(true, "正在整理记忆……");
            engine.CompactNowAsync().ContinueWith(task => Dispatcher.BeginInvoke(new Action(delegate
            {
                string text = task.IsFaulted ? "整理失败：" + GetError(task.Exception) : task.Result;
                SetBusy(false, text + " · " + engine.GetMemoryStatus());
            })));
        }

        public void OpenSettings()
        {
            ChatSettingsWindow window = new ChatSettingsWindow(settingsStore);
            window.Owner = this;
            window.ShowDialog();
            statusText.Text = engine.GetMemoryStatus();
        }

        private void ClearConversation()
        {
            if (busy) return;
            MessageBoxResult result = MessageBox.Show(this, "清空当前未归档对话？已经生成的周摘要和月摘要会保留。", "清空当前对话", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            engine.ClearRecent();
            RefreshTranscript();
        }

        private void SetBusy(bool value, string status)
        {
            busy = value;
            inputBox.IsEnabled = !value;
            sendButton.IsEnabled = !value;
            compactButton.IsEnabled = !value;
            statusText.Text = status;
        }

        private void ScrollToBottom()
        {
            Dispatcher.BeginInvoke(new Action(delegate { scrollViewer.ScrollToEnd(); }));
        }

        private static string GetError(Exception exception)
        {
            while (exception != null && exception.InnerException != null) exception = exception.InnerException;
            return exception == null ? "未知错误" : exception.Message;
        }

        internal bool RunUiSelfTest(out string details)
        {
            bool ok = inputBox != null && sendButton != null && compactButton != null && transcript != null && PaimonPersona.HardSystemPrompt.IndexOf("不可变更", StringComparison.Ordinal) >= 0;
            details = ok ? "chat-ui=ok" : "chat-ui=missing-control-or-persona-lock";
            return ok;
        }

        public void ClosePermanently()
        {
            allowClose = true;
            Close();
        }
    }
}
