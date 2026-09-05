using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GenshinDesktopPet
{
    public sealed class ChatSettingsWindow : Window
    {
        private readonly ChatSettingsStore store;
        private readonly ComboBox providerBox;
        private readonly TextBox baseUrlBox;
        private readonly TextBox modelBox;
        private readonly TextBox tokenBox;
        private readonly PasswordBox apiKeyBox;
        private readonly TextBlock keyStatus;
        private readonly TextBlock operationStatus;
        private readonly Button testButton;
        private ChatSettings settings;
        private bool loading;

        public ChatSettingsWindow(ChatSettingsStore store)
        {
            this.store = store;
            settings = store.Load();
            Title = "派蒙聊天 · 模型设置";
            Width = 470;
            Height = 520;
            MinWidth = 430;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            Background = new SolidColorBrush(Color.FromRgb(247, 249, 253));
            FontFamily = new FontFamily("Microsoft YaHei UI");

            Grid root = new Grid();
            root.Margin = new Thickness(22);
            for (int index = 0; index < 9; index++) root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            TextBlock heading = new TextBlock();
            heading.Text = "大模型 API";
            heading.FontSize = 21;
            heading.FontWeight = FontWeights.SemiBold;
            heading.Foreground = new SolidColorBrush(Color.FromRgb(45, 55, 76));
            root.Children.Add(heading);

            TextBlock lockNotice = new TextBlock();
            lockNotice.Text = "派蒙的性格与《原神》剧情背景已内置锁定，聊天内容和记忆摘要都不能修改。";
            lockNotice.TextWrapping = TextWrapping.Wrap;
            lockNotice.Margin = new Thickness(0, 8, 0, 16);
            lockNotice.Foreground = new SolidColorBrush(Color.FromRgb(102, 82, 42));
            Grid.SetRow(lockNotice, 1);
            root.Children.Add(lockNotice);

            providerBox = new ComboBox();
            providerBox.Items.Add("DeepSeek");
            providerBox.Items.Add("OpenAI");
            providerBox.Items.Add("MiMo");
            providerBox.Items.Add("自定义");
            AddField(root, 2, "服务商", providerBox);

            baseUrlBox = new TextBox();
            AddField(root, 3, "Base URL", baseUrlBox);

            modelBox = new TextBox();
            AddField(root, 4, "模型名称", modelBox);

            tokenBox = new TextBox();
            AddField(root, 5, "快速回答上限", tokenBox);

            StackPanel keyPanel = new StackPanel();
            apiKeyBox = new PasswordBox();
            apiKeyBox.MinWidth = 240;
            keyPanel.Children.Add(apiKeyBox);
            keyStatus = new TextBlock();
            keyStatus.Margin = new Thickness(0, 4, 0, 0);
            keyStatus.FontSize = 11;
            keyStatus.Foreground = Brushes.DimGray;
            keyPanel.Children.Add(keyStatus);
            AddField(root, 6, "API Key", keyPanel);

            TextBlock hint = new TextBlock();
            hint.Text = "已启用日常快速模式：流式显示、短回答、精简随请求发送的上下文；完整记忆仍保存在本机。密钥只保存在当前 Windows 用户的本机加密区。";
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 10, 0, 8);
            hint.Foreground = Brushes.DimGray;
            Grid.SetRow(hint, 7);
            root.Children.Add(hint);

            operationStatus = new TextBlock();
            operationStatus.TextWrapping = TextWrapping.Wrap;
            operationStatus.Margin = new Thickness(0, 4, 0, 10);
            operationStatus.Foreground = new SolidColorBrush(Color.FromRgb(76, 92, 120));
            Grid.SetRow(operationStatus, 8);
            root.Children.Add(operationStatus);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetRow(buttons, 10);
            root.Children.Add(buttons);

            Button clearButton = CreateButton("清除密钥");
            clearButton.Click += delegate { ClearKey(); };
            buttons.Children.Add(clearButton);
            testButton = CreateButton("测试连接");
            testButton.Click += delegate { TestConnection(); };
            buttons.Children.Add(testButton);
            Button saveButton = CreateButton("保存");
            saveButton.IsDefault = true;
            saveButton.Click += delegate { SaveAndClose(); };
            buttons.Children.Add(saveButton);
            Button cancelButton = CreateButton("取消");
            cancelButton.IsCancel = true;
            buttons.Children.Add(cancelButton);

            providerBox.SelectionChanged += delegate
            {
                if (!loading) ApplyProviderPreset(Convert.ToString(providerBox.SelectedItem));
            };
            LoadValues();
        }

        private static void AddField(Grid root, int row, string label, UIElement control)
        {
            Grid line = new Grid();
            line.Margin = new Thickness(0, 5, 0, 5);
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock text = new TextBlock();
            text.Text = label;
            text.VerticalAlignment = VerticalAlignment.Center;
            text.Foreground = new SolidColorBrush(Color.FromRgb(55, 64, 82));
            line.Children.Add(text);
            Grid.SetColumn(control, 1);
            line.Children.Add(control);
            Grid.SetRow(line, row);
            root.Children.Add(line);
        }

        private static Button CreateButton(string text)
        {
            Button button = new Button();
            button.Content = text;
            button.MinWidth = 76;
            button.Margin = new Thickness(6, 0, 0, 0);
            button.Padding = new Thickness(10, 5, 10, 5);
            return button;
        }

        private void LoadValues()
        {
            loading = true;
            providerBox.SelectedItem = settings.Provider;
            if (providerBox.SelectedIndex < 0) providerBox.SelectedItem = "自定义";
            baseUrlBox.Text = settings.BaseUrl;
            modelBox.Text = settings.Model;
            tokenBox.Text = settings.MaxOutputTokens.ToString();
            UpdateKeyStatus();
            loading = false;
        }

        private void UpdateKeyStatus()
        {
            keyStatus.Text = string.IsNullOrWhiteSpace(store.GetApiKey(settings)) ? "尚未保存密钥" : "已保存密钥（Windows DPAPI 加密）";
        }

        private void ApplyProviderPreset(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider) || string.Equals(provider, "自定义", StringComparison.Ordinal)) return;
            ChatSettings preset = ChatSettings.ForProvider(provider);
            baseUrlBox.Text = preset.BaseUrl;
            modelBox.Text = preset.Model;
        }

        private ChatSettings ReadAndValidate()
        {
            string provider = Convert.ToString(providerBox.SelectedItem);
            if (string.IsNullOrWhiteSpace(provider)) provider = "自定义";
            string endpoint = ChatProviderClient.NormalizeEndpoint(baseUrlBox.Text);
            int maxTokens;
            if (!int.TryParse(tokenBox.Text.Trim(), out maxTokens) || maxTokens < 128 || maxTokens > 1024)
            {
                throw new InvalidOperationException("快速回答上限请输入 128–1024 之间的整数。");
            }
            if (string.IsNullOrWhiteSpace(modelBox.Text)) throw new InvalidOperationException("模型名称不能为空。");
            ChatSettings result = new ChatSettings();
            result.Provider = provider;
            result.BaseUrl = endpoint.Substring(0, endpoint.Length - "/chat/completions".Length);
            result.Model = modelBox.Text.Trim();
            result.MaxOutputTokens = maxTokens;
            result.EncryptedApiKey = settings.EncryptedApiKey;
            return result;
        }

        private void SaveCurrent(bool close)
        {
            ChatSettings next = ReadAndValidate();
            store.Save(next, apiKeyBox.Password);
            settings = store.Load();
            apiKeyBox.Clear();
            UpdateKeyStatus();
            operationStatus.Text = "设置已保存。";
            if (close)
            {
                DialogResult = true;
                Close();
            }
        }

        private void SaveAndClose()
        {
            try { SaveCurrent(true); }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, "无法保存", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void ClearKey()
        {
            if (MessageBox.Show(this, "确定清除本机保存的 API Key？", "清除密钥", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            store.ClearApiKey(settings);
            settings = store.Load();
            apiKeyBox.Clear();
            UpdateKeyStatus();
            operationStatus.Text = "密钥已清除。";
        }

        private void TestConnection()
        {
            try { SaveCurrent(false); }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "设置有误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string key = store.GetApiKey(settings);
            if (string.IsNullOrWhiteSpace(key))
            {
                MessageBox.Show(this, "请先填写并保存 API Key。", "缺少密钥", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            testButton.IsEnabled = false;
            operationStatus.Text = "正在测试连接……";
            Task.Factory.StartNew(delegate
            {
                ChatProviderClient client = new ChatProviderClient();
                List<ChatRequestMessage> messages = new List<ChatRequestMessage>();
                messages.Add(new ChatRequestMessage { role = "system", content = "这是连接测试。" });
                messages.Add(new ChatRequestMessage { role = "user", content = "只回复：连接成功" });
                return client.Send(settings, key, messages, 64);
            }).ContinueWith(task => Dispatcher.BeginInvoke(new Action(delegate
            {
                testButton.IsEnabled = true;
                operationStatus.Text = task.IsFaulted ? "连接失败：" + GetError(task.Exception) : "连接成功：" + task.Result;
            })));
        }

        private static string GetError(Exception exception)
        {
            while (exception != null && exception.InnerException != null) exception = exception.InnerException;
            return exception == null ? "未知错误" : exception.Message;
        }
    }
}
