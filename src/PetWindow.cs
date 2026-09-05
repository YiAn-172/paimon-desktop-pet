using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace GenshinDesktopPet
{
    public sealed class NormalizedXChangedEventArgs : EventArgs
    {
        public string CharacterId { get; private set; }
        public double Value { get; private set; }

        public NormalizedXChangedEventArgs(string characterId, double value)
        {
            CharacterId = characterId;
            Value = value;
        }
    }

    public sealed class ScalePercentChangedEventArgs : EventArgs
    {
        public string CharacterId { get; private set; }
        public int Percent { get; private set; }

        public ScalePercentChangedEventArgs(string characterId, int percent)
        {
            CharacterId = characterId;
            Percent = percent;
        }
    }

    public sealed class ChatHistoryRequestedEventArgs : EventArgs
    {
        public int Delta { get; private set; }

        public ChatHistoryRequestedEventArgs(int delta)
        {
            Delta = delta;
        }
    }

    public sealed class HotkeyZoneChangedEventArgs : EventArgs
    {
        public bool IsInside { get; private set; }

        public HotkeyZoneChangedEventArgs(bool isInside)
        {
            IsInside = isInside;
        }
    }

    public sealed class PetWindow : Window
    {
        private const double BaseSize = 160.0;
        private readonly CharacterDefinition character;
        private readonly string assetFolder;
        private readonly Grid inputSurface;
        private readonly Image sprite;
        private readonly DispatcherTimer frameTimer;
        private readonly DispatcherTimer scheduleTimer;
        private readonly DispatcherTimer motionTimer;
        private readonly DispatcherTimer hoverTimer;
        private readonly DispatcherTimer clickTimer;
        private readonly DispatcherTimer speechTimer;
        private readonly Popup speechPopup;
        private readonly TextBlock speechText;
        private readonly Random random;
        private readonly Dictionary<string, BitmapImage> bitmapCache;
        private List<string> currentFrames;
        private int frameIndex;
        private bool currentLoop;
        private Action animationCompleted;
        private bool paused;
        private bool closingPermanently;
        private bool clickThrough;
        private bool mouseInside;
        private bool pointerDown;
        private bool dragging;
        private Point pointerDownScreen;
        private Point pointerDownWindow;
        private Point pointerDownLocal;
        private DateTime lastClickAt;
        private DateTime lastWheelAt;
        private int pendingClickCount;
        private double pendingClickNormalizedY;
        private string state;
        private DateTime nextMoveAt;
        private DateTime nextGestureAt;
        private DateTime nextClimbAt;
        private DateTime emotionEndsAt;
        private double normalizedX;
        private double motionStartX;
        private double motionStartY;
        private double motionTargetX;
        private double motionTargetY;
        private DateTime motionStartedAt;
        private double motionDurationSeconds;
        private MotionKind motionKind;
        private bool climbWillFall;
        private bool climbDescending;
        private bool dropShouldCry;
        private int currentScalePercent;
        private MenuItem comfortMenuItem;

        public event EventHandler<NormalizedXChangedEventArgs> NormalizedXChanged;
        public event EventHandler<ScalePercentChangedEventArgs> ScalePercentChanged;
        public event EventHandler HideRequested;
        public event EventHandler ExitRequested;
        public event EventHandler ChatRequested;
        public event EventHandler<ChatHistoryRequestedEventArgs> ChatHistoryRequested;
        public event EventHandler QuickChatToggleRequested;
        public event EventHandler<HotkeyZoneChangedEventArgs> HotkeyZoneChanged;

        public string CharacterId
        {
            get { return character.id; }
        }

        private enum MotionKind
        {
            None,
            Walk,
            Climb,
            Drop
        }

        public PetWindow(CharacterDefinition character, string assetFolder, int scalePercent, double normalizedX)
        {
            this.character = character;
            this.assetFolder = assetFolder;
            this.normalizedX = Math.Max(0.0, Math.Min(1.0, normalizedX));
            random = new Random(unchecked(Environment.TickCount * 31 + character.id.GetHashCode()));
            bitmapCache = new Dictionary<string, BitmapImage>(StringComparer.OrdinalIgnoreCase);
            currentFrames = new List<string>();
            state = "idle";

            Title = character.displayName + "桌宠";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            SizeToContent = SizeToContent.Manual;
            Focusable = true;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            inputSurface = new Grid();
            inputSurface.Background = Brushes.Transparent;
            inputSurface.IsHitTestVisible = true;

            sprite = new Image();
            sprite.Stretch = Stretch.Uniform;
            sprite.HorizontalAlignment = HorizontalAlignment.Stretch;
            sprite.VerticalAlignment = VerticalAlignment.Stretch;
            sprite.SnapsToDevicePixels = true;
            sprite.UseLayoutRounding = true;
            sprite.IsHitTestVisible = false;
            RenderOptions.SetBitmapScalingMode(sprite, BitmapScalingMode.HighQuality);
            inputSurface.Children.Add(sprite);
            Content = inputSurface;

            speechText = new TextBlock();
            speechText.Foreground = new SolidColorBrush(Color.FromRgb(45, 54, 78));
            speechText.FontSize = 13;
            speechText.FontWeight = FontWeights.SemiBold;
            speechText.TextWrapping = TextWrapping.Wrap;
            speechText.TextAlignment = TextAlignment.Center;
            speechText.MaxWidth = 190;
            Border speechBubble = new Border();
            speechBubble.Background = new SolidColorBrush(Color.FromArgb(242, 255, 255, 255));
            speechBubble.BorderBrush = new SolidColorBrush(Color.FromRgb(115, 139, 190));
            speechBubble.BorderThickness = new Thickness(1.5);
            speechBubble.CornerRadius = new CornerRadius(10);
            speechBubble.Padding = new Thickness(9, 5, 9, 5);
            speechBubble.Child = speechText;
            speechPopup = new Popup();
            speechPopup.PlacementTarget = inputSurface;
            speechPopup.Placement = PlacementMode.Top;
            speechPopup.VerticalOffset = -5;
            speechPopup.AllowsTransparency = true;
            speechPopup.PopupAnimation = PopupAnimation.Fade;
            speechPopup.StaysOpen = true;
            speechPopup.IsHitTestVisible = false;
            speechPopup.Child = speechBubble;

            ContextMenu = CreatePetContextMenu();

            frameTimer = new DispatcherTimer(DispatcherPriority.Render);
            frameTimer.Interval = TimeSpan.FromMilliseconds(140);
            frameTimer.Tick += OnFrameTick;

            scheduleTimer = new DispatcherTimer(DispatcherPriority.Background);
            scheduleTimer.Interval = TimeSpan.FromSeconds(1);
            scheduleTimer.Tick += OnScheduleTick;

            motionTimer = new DispatcherTimer(DispatcherPriority.Render);
            motionTimer.Interval = TimeSpan.FromMilliseconds(33);
            motionTimer.Tick += OnMotionTick;

            hoverTimer = new DispatcherTimer(DispatcherPriority.Background);
            hoverTimer.Interval = TimeSpan.FromSeconds(1);
            hoverTimer.Tick += OnHoverTimer;

            clickTimer = new DispatcherTimer(DispatcherPriority.Input);
            clickTimer.Interval = TimeSpan.FromMilliseconds(360);
            clickTimer.Tick += OnClickTimer;

            speechTimer = new DispatcherTimer(DispatcherPriority.Background);
            speechTimer.Interval = TimeSpan.FromMilliseconds(1700);
            speechTimer.Tick += delegate
            {
                speechTimer.Stop();
                speechPopup.IsOpen = false;
            };

            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
            Closing += OnClosing;
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;
            AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnPreviewMouseDown), true);
            AddHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler(OnMouseMove), true);
            AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler(OnPreviewMouseUp), true);
            AddHandler(Mouse.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnMouseWheel), true);

            SetScalePercent(scalePercent);
            ResetSchedule();
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            SetClickThrough(clickThrough);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SetNormalizedX(normalizedX);
            PlayLoop("idle", PrefixFor("idle"), 180);
            if (!paused)
            {
                scheduleTimer.Start();
            }
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!closingPermanently)
            {
                e.Cancel = true;
                Hide();
            }
        }

        public void ClosePermanently()
        {
            closingPermanently = true;
            StopAllTimers();
            Close();
        }

        public void ShowPet()
        {
            if (!IsVisible)
            {
                Show();
            }
            SetNormalizedX(normalizedX);
        }

        public void HidePet()
        {
            speechPopup.IsOpen = false;
            Hide();
        }

        public void SetScalePercent(int percent)
        {
            if (percent != 100 && percent != 125 && percent != 150)
            {
                percent = 100;
            }
            currentScalePercent = percent;
            double size = BaseSize * percent / 100.0;
            Width = size;
            Height = size;
            sprite.Width = size;
            sprite.Height = size;
            Dispatcher.BeginInvoke(new Action(delegate { SetNormalizedX(normalizedX); }), DispatcherPriority.Loaded);
        }

        private ContextMenu CreatePetContextMenu()
        {
            ContextMenu menu = new ContextMenu();
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            MenuItem title = new MenuItem();
            title.Header = character.displayName + " · 互动";
            title.IsEnabled = false;
            menu.Items.Add(title);
            MenuItem chatItem = new MenuItem();
            chatItem.Header = "派蒙聊天";
            MenuItem fullChatItem = new MenuItem();
            fullChatItem.Header = "打开完整聊天窗口";
            fullChatItem.Click += delegate
            {
                EventHandler handler = ChatRequested;
                if (handler != null) handler(this, EventArgs.Empty);
            };
            chatItem.Items.Add(fullChatItem);
            MenuItem quickChatItem = new MenuItem();
            quickChatItem.Header = "显示/隐藏快捷输入行（Ctrl+L）";
            quickChatItem.Click += delegate
            {
                EventHandler handler = QuickChatToggleRequested;
                if (handler != null) handler(this, EventArgs.Empty);
            };
            chatItem.Items.Add(quickChatItem);
            menu.Items.Add(chatItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateActionMenuItem("打个招呼", "wave"));
            menu.Items.Add(CreateActionMenuItem("跳一下", "jump"));
            menu.Items.Add(CreateActionMenuItem("好奇地看看", "curious"));
            menu.Items.Add(CreateActionMenuItem("装可怜", "cry"));

            MenuItem feedItem = new MenuItem();
            feedItem.Header = "投喂小零食";
            feedItem.Click += delegate { RunFeedInteraction(); };
            menu.Items.Add(feedItem);

            MenuItem spinItem = new MenuItem();
            spinItem.Header = "转圈逗她";
            spinItem.Click += delegate { RunSpinInteraction(); };
            menu.Items.Add(spinItem);

            comfortMenuItem = new MenuItem();
            comfortMenuItem.Header = "安慰一下";
            comfortMenuItem.Click += delegate { Comfort(); };
            menu.Items.Add(comfortMenuItem);

            MenuItem climbItem = new MenuItem();
            climbItem.Header = "去爬墙";
            climbItem.Click += delegate { RunManualClimb(); };
            menu.Items.Add(climbItem);

            MenuItem personality = new MenuItem();
            personality.Header = "角色专属动作";
            AddPersonalityMenuItems(personality);
            menu.Items.Add(personality);

            MenuItem interactionHelp = new MenuItem();
            interactionHelp.Header = "点击互动说明";
            interactionHelp.Items.Add(CreateDisabledMenuItem("点头部：摸摸头"));
            interactionHelp.Items.Add(CreateDisabledMenuItem("点身体：戳一下"));
            interactionHelp.Items.Add(CreateDisabledMenuItem("双击：抱高高"));
            interactionHelp.Items.Add(CreateDisabledMenuItem("三连击：连续戳派蒙"));
            interactionHelp.Items.Add(CreateDisabledMenuItem("中键：投喂零食"));
            interactionHelp.Items.Add(CreateDisabledMenuItem("滚轮：让派蒙转圈"));
            interactionHelp.Items.Add(CreateDisabledMenuItem("Ctrl+滚轮：翻看历史会话"));
            menu.Items.Add(interactionHelp);
            menu.Items.Add(new Separator());

            MenuItem scale = new MenuItem();
            scale.Header = "缩放";
            foreach (int percent in new int[] { 100, 125, 150 })
            {
                int capturedPercent = percent;
                MenuItem item = new MenuItem();
                item.Header = percent + "%";
                item.Click += delegate
                {
                    SetScalePercent(capturedPercent);
                    EventHandler<ScalePercentChangedEventArgs> handler = ScalePercentChanged;
                    if (handler != null)
                    {
                        handler(this, new ScalePercentChangedEventArgs(character.id, capturedPercent));
                    }
                };
                scale.Items.Add(item);
            }
            menu.Items.Add(scale);

            MenuItem returnItem = new MenuItem();
            returnItem.Header = "回到任务栏";
            returnItem.Click += delegate { ReturnToTaskbar(); };
            menu.Items.Add(returnItem);

            MenuItem hideItem = new MenuItem();
            hideItem.Header = "隐藏这个角色";
            hideItem.Click += delegate
            {
                EventHandler handler = HideRequested;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            };
            menu.Items.Add(hideItem);

            MenuItem exitItem = new MenuItem();
            exitItem.Header = "退出桌宠";
            exitItem.Click += delegate
            {
                EventHandler handler = ExitRequested;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            };
            menu.Items.Add(exitItem);

            menu.Opened += delegate
            {
                comfortMenuItem.IsEnabled = state == "cry";
                foreach (object raw in scale.Items)
                {
                    MenuItem item = raw as MenuItem;
                    if (item != null)
                    {
                        item.IsChecked = string.Equals(Convert.ToString(item.Header), currentScalePercent + "%", StringComparison.Ordinal);
                    }
                }
            };
            return menu;
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                OnMouseLeftButtonDown(sender, e);
            }
        }

        private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                OpenPetContextMenu();
                e.Handled = true;
                return;
            }
            if (e.ChangedButton == MouseButton.Middle)
            {
                RunFeedInteraction();
                e.Handled = true;
                return;
            }
            if (e.ChangedButton == MouseButton.Left)
            {
                OnMouseLeftButtonUp(sender, e);
            }
        }

        private void OpenPetContextMenu()
        {
            if (clickThrough || ContextMenu == null)
            {
                return;
            }
            ContextMenu.PlacementTarget = inputSurface;
            ContextMenu.IsOpen = true;
        }

        internal bool RunInputPipelineSelfTest(out string details)
        {
            if (!IsVisible || clickThrough || !Focusable || !inputSurface.IsHitTestVisible || sprite.IsHitTestVisible)
            {
                details = "precondition=false;visible=" + IsVisible + ";clickThrough=" + clickThrough + ";focusable=" + Focusable + ";surface=" + inputSurface.IsHitTestVisible + ";sprite=" + sprite.IsHitTestVisible;
                return false;
            }

            MouseButtonEventArgs leftDown = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left);
            leftDown.RoutedEvent = Mouse.PreviewMouseDownEvent;
            string stateBeforeClick = state;
            RaiseEvent(leftDown);
            bool leftDownHandled = pointerDown && leftDown.Handled;

            MouseButtonEventArgs leftUp = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left);
            leftUp.RoutedEvent = Mouse.PreviewMouseUpEvent;
            RaiseEvent(leftUp);
            bool pointerReleased = !pointerDown;
            bool leftUpEventHandled = leftUp.Handled;
            bool clickQueued = pendingClickCount > 0;
            bool immediateInteraction = !string.Equals(stateBeforeClick, state, StringComparison.Ordinal);
            bool leftUpHandled = pointerReleased && leftUpEventHandled && (clickQueued || immediateInteraction);

            MouseButtonEventArgs rightUp = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Right);
            rightUp.RoutedEvent = Mouse.PreviewMouseUpEvent;
            RaiseEvent(rightUp);
            bool rightHandled = rightUp.Handled && ContextMenu != null && ContextMenu.IsOpen;

            clickTimer.Stop();
            pendingClickCount = 0;
            if (ContextMenu != null)
            {
                ContextMenu.IsOpen = false;
            }

            lastClickAt = DateTime.MinValue;
            ReturnToIdle();
            RegisterClick(new Point(Width * 0.5, Height * 0.25));
            OnClickTimer(this, EventArgs.Empty);
            bool headInteraction = state == "happy" && speechPopup.IsOpen;

            lastClickAt = DateTime.MinValue;
            pendingClickCount = 0;
            ReturnToIdle();
            RegisterClick(new Point(Width * 0.5, Height * 0.8));
            OnClickTimer(this, EventArgs.Empty);
            bool bodyInteraction = state == "special";

            lastClickAt = DateTime.MinValue;
            pendingClickCount = 0;
            ReturnToIdle();
            RegisterClick(new Point(Width * 0.5, Height * 0.4));
            RegisterClick(new Point(Width * 0.5, Height * 0.4));
            OnClickTimer(this, EventArgs.Empty);
            bool doubleInteraction = state == "bounce";

            MouseButtonEventArgs middleUp = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Middle);
            middleUp.RoutedEvent = Mouse.PreviewMouseUpEvent;
            RaiseEvent(middleUp);
            bool middleHandled = middleUp.Handled && state == "snack";

            lastWheelAt = DateTime.MinValue;
            MouseWheelEventArgs wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, 120);
            wheel.RoutedEvent = Mouse.PreviewMouseWheelEvent;
            RaiseEvent(wheel);
            bool wheelHandled = wheel.Handled && state == "spin";

            speechTimer.Stop();
            speechPopup.IsOpen = false;
            details = "leftDown=" + leftDownHandled + ";leftUp=" + leftUpHandled + ";pointerReleased=" + pointerReleased + ";leftUpEventHandled=" + leftUpEventHandled + ";clickQueued=" + clickQueued + ";immediateInteraction=" + immediateInteraction + ";stateBefore=" + stateBeforeClick + ";stateAfter=" + state + ";right=" + rightHandled + ";head=" + headInteraction + ";body=" + bodyInteraction + ";double=" + doubleInteraction + ";middle=" + middleHandled + ";wheel=" + wheelHandled;
            return leftDownHandled && leftUpHandled && rightHandled && headInteraction && bodyInteraction && doubleInteraction && middleHandled && wheelHandled;
        }

        private static MenuItem CreateDisabledMenuItem(string header)
        {
            MenuItem item = new MenuItem();
            item.Header = header;
            item.IsEnabled = false;
            return item;
        }

        private MenuItem CreateActionMenuItem(string header, string action)
        {
            MenuItem item = new MenuItem();
            item.Header = header;
            item.Click += delegate { RunManualAction(action); };
            return item;
        }

        private void AddPersonalityMenuItems(MenuItem parent)
        {
            parent.Items.Add(CreateSequenceMenuItem("馋嘴找零食", "snack", "happy"));
            parent.Items.Add(CreateSequenceMenuItem("跺脚抗议", "special", "cry"));
            parent.Items.Add(CreateSequenceMenuItem("开心转圈", "spin", "happy"));
        }

        private MenuItem CreateSequenceMenuItem(string header, string firstAction, string secondAction)
        {
            MenuItem item = new MenuItem();
            item.Header = header;
            item.Click += delegate { RunPersonalitySequence(firstAction, secondAction); };
            return item;
        }

        private void RunPersonalitySequence(string firstAction, string secondAction)
        {
            if (paused)
            {
                return;
            }
            CancelMotionAndSnap();
            PlayOnce(firstAction, PrefixFor(firstAction), MillisecondsFor(firstAction), delegate
            {
                PlayOnce(secondAction, PrefixFor(secondAction), MillisecondsFor(secondAction), ReturnToIdle);
            });
        }

        private void RunFeedInteraction()
        {
            if (paused || clickThrough)
            {
                return;
            }
            ShowSpeech("好吃！再来一点嘛～");
            CancelMotionAndSnap();
            PlayOnce("snack", PrefixFor("snack"), MillisecondsFor("snack"), delegate
            {
                PlayOnce("happy", PrefixFor("happy"), MillisecondsFor("happy"), ReturnToIdle);
            });
        }

        private void RunSpinInteraction()
        {
            if (paused || clickThrough)
            {
                return;
            }
            ShowSpeech("要转晕啦！");
            CancelMotionAndSnap();
            PlayOnce("spin", PrefixFor("spin"), MillisecondsFor("spin"), delegate
            {
                PlayOnce("curious", PrefixFor("curious"), MillisecondsFor("curious"), ReturnToIdle);
            });
        }

        private void ShowSpeech(string text)
        {
            ShowSpeech(text, 1700);
        }

        private void ShowSpeech(string text, int durationMilliseconds)
        {
            if (!IsVisible || paused)
            {
                return;
            }
            speechText.Text = text;
            speechPopup.IsOpen = false;
            speechPopup.IsOpen = true;
            speechTimer.Stop();
            speechTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1200, durationMilliseconds));
            speechTimer.Start();
        }

        public void ShowChatBubble(string text, bool history)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            string compact = Regex.Replace(text.Trim(), "\\s+", " ");
            if (compact.Length > 220) compact = compact.Substring(0, 217) + "…";
            if (history) compact = "历史 · " + compact;
            int duration = Math.Min(12000, Math.Max(4500, 2600 + compact.Length * 38));
            ShowSpeech(compact, duration);
        }

        public void ShowChatProgress(string text)
        {
            if (paused || string.IsNullOrWhiteSpace(text)) return;
            ShowChatBubble(text, false);
        }

        public void ReactToChatOpened()
        {
            if (paused) return;
            ShowSpeech("旅行者，来和派蒙聊聊天吧！");
            CancelMotionAndSnap();
            PlayOnce("wave", PrefixFor("wave"), MillisecondsFor("wave"), ReturnToIdle);
        }

        public void ReactToChatThinking()
        {
            if (paused) return;
            ShowSpeech("派蒙想想……");
            CancelMotionAndSnap();
            PlayOnce("curious", PrefixFor("curious"), MillisecondsFor("curious"), ReturnToIdle);
        }

        public void ReactToChatReply(string reply, bool isError)
        {
            if (paused) return;
            ShowChatBubble(reply, false);
            CancelMotionAndSnap();
            string action = isError ? "cry" : "happy";
            PlayOnce(action, PrefixFor(action), MillisecondsFor(isError ? "wronged" : "happy"), ReturnToIdle);
        }

        private void RunManualAction(string action)
        {
            if (paused)
            {
                return;
            }
            CancelMotionAndSnap();
            if (action == "cry")
            {
                BeginCry();
            }
            else
            {
                PlayOnce(action, PrefixFor(action), MillisecondsFor(action), ReturnToIdle);
            }
        }

        private void RunManualClimb()
        {
            if (paused)
            {
                return;
            }
            CancelMotionAndSnap();
            BeginClimb();
        }

        private void ReturnToTaskbar()
        {
            if (paused)
            {
                return;
            }
            motionTimer.Stop();
            motionKind = MotionKind.None;
            BeginDropFromCurrentPosition(false);
        }

        private void CancelMotionAndSnap()
        {
            motionTimer.Stop();
            motionKind = MotionKind.None;
            sprite.RenderTransform = Transform.Identity;
            Rect area = SystemParameters.WorkArea;
            Left = Math.Round(Math.Max(area.Left, Math.Min(area.Right - Width, Left)));
            Top = Math.Round(area.Bottom - Height);
            UpdateNormalizedX();
        }

        public void SetNormalizedX(double value)
        {
            normalizedX = Math.Max(0.0, Math.Min(1.0, value));
            Rect area = SystemParameters.WorkArea;
            Left = Math.Round(area.Left + normalizedX * Math.Max(0.0, area.Width - Width));
            Top = Math.Round(area.Bottom - Height);
        }

        public void SetClickThrough(bool enabled)
        {
            clickThrough = enabled;
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                NativeMethods.SetClickThrough(handle, enabled);
            }
        }

        public void SetPaused(bool value)
        {
            if (paused == value)
            {
                return;
            }
            paused = value;
            if (paused)
            {
                PreserveMotionForPause();
                StopAllTimers();
            }
            else
            {
                ResetSchedule();
                if (currentFrames.Count == 0)
                {
                    PlayLoop("idle", PrefixFor("idle"), 180);
                }
                else
                {
                    frameTimer.Start();
                }
                if (motionKind != MotionKind.None)
                {
                    motionStartedAt = DateTime.UtcNow;
                    motionTimer.Start();
                }
                scheduleTimer.Start();
            }
        }

        private void PreserveMotionForPause()
        {
            if (motionKind == MotionKind.None)
            {
                return;
            }
            double progress = (DateTime.UtcNow - motionStartedAt).TotalSeconds / Math.Max(0.1, motionDurationSeconds);
            progress = Math.Max(0.0, Math.Min(1.0, progress));
            motionStartX = Left;
            motionStartY = Top;
            motionDurationSeconds = Math.Max(0.1, motionDurationSeconds * (1.0 - progress));
        }

        private void StopAllTimers()
        {
            frameTimer.Stop();
            scheduleTimer.Stop();
            motionTimer.Stop();
            hoverTimer.Stop();
            clickTimer.Stop();
            speechTimer.Stop();
            speechPopup.IsOpen = false;
        }

        private void ResetSchedule()
        {
            DateTime now = DateTime.UtcNow;
            nextMoveAt = now.AddSeconds(random.Next(120, 241));
            nextGestureAt = now.AddSeconds(random.Next(480, 901));
            nextClimbAt = now.AddSeconds(random.Next(600, 1201));
        }

        private void OnScheduleTick(object sender, EventArgs e)
        {
            if (paused || dragging || motionKind != MotionKind.None)
            {
                return;
            }
            DateTime now = DateTime.UtcNow;
            if (state == "cry" && now >= emotionEndsAt)
            {
                ReturnToIdle();
                return;
            }
            if (state != "idle")
            {
                return;
            }
            if (now >= nextClimbAt)
            {
                BeginClimb();
                nextClimbAt = now.AddSeconds(random.Next(600, 1201));
                return;
            }
            if (now >= nextMoveAt)
            {
                BeginWalk();
                nextMoveAt = now.AddSeconds(random.Next(120, 241));
                return;
            }
            if (now >= nextGestureAt)
            {
                PlayPersonalityGesture();
                nextGestureAt = now.AddSeconds(random.Next(480, 901));
            }
        }

        private void BeginWalk()
        {
            Rect area = SystemParameters.WorkArea;
            bool currentlyLeft = Left + Width / 2.0 < area.Left + area.Width / 2.0;
            double zoneMin = currentlyLeft ? 0.76 : 0.03;
            double zoneMax = currentlyLeft ? 0.94 : 0.24;
            double destinationNormalized = zoneMin + random.NextDouble() * (zoneMax - zoneMin);
            double targetX = area.Left + destinationNormalized * Math.Max(0.0, area.Width - Width);
            double distance = Math.Abs(targetX - Left);
            bool movingRight = targetX > Left;
            sprite.RenderTransformOrigin = new Point(0.5, 0.5);
            // 派蒙的 move_ 帧朝向屏幕左侧；向右漂浮时镜像。
            sprite.RenderTransform = new ScaleTransform(movingRight ? -1.0 : 1.0, 1.0);
            PlayLoop("walk", PrefixFor("walk"), 145);
            StartMotion(MotionKind.Walk, targetX, area.Bottom - Height, Math.Max(1.5, distance / 85.0));
        }

        private void BeginClimb()
        {
            Rect area = SystemParameters.WorkArea;
            bool leftEdge = Left + Width / 2.0 < area.Left + area.Width / 2.0;
            Left = leftEdge ? area.Left : area.Right - Width;
            Top = area.Bottom - Height;
            sprite.RenderTransformOrigin = new Point(0.5, 0.5);
            // 攀爬使用专用的手脚贴墙姿势，不再把行走动画旋转 90 度。
            sprite.RenderTransform = new ScaleTransform(leftEdge ? 1.0 : -1.0, 1.0);
            // 透明帧左侧约保留 14/384 画布宽度，让手掌恰好贴住屏幕边缘。
            double wallInset = Width * (14.0 / 384.0);
            Left = leftEdge ? area.Left - wallInset : area.Right - Width + wallInset;
            climbWillFall = random.NextDouble() < 0.20;
            climbDescending = false;
            PlayLoop("climb", PrefixFor(leftEdge ? "climbLeft" : "climbRight"), 150);
            double targetY = Math.Max(area.Top + Height * 0.25, area.Bottom - Height - Math.Min(360.0, area.Height * 0.42));
            StartMotion(MotionKind.Climb, Left, targetY, Math.Max(3.0, Math.Abs(targetY - Top) / 48.0));
        }

        private void BeginDropFromCurrentPosition(bool shouldCry)
        {
            Rect area = SystemParameters.WorkArea;
            dropShouldCry = shouldCry;
            sprite.RenderTransform = Transform.Identity;
            PlayLoop("fall", PrefixFor("fall"), 120);
            double landingX = Math.Max(area.Left, Math.Min(area.Right - Width, Left));
            StartMotion(MotionKind.Drop, landingX, area.Bottom - Height, Math.Max(0.45, Math.Abs((area.Bottom - Height) - Top) / 300.0));
        }

        private void StartMotion(MotionKind kind, double targetX, double targetY, double durationSeconds)
        {
            motionKind = kind;
            motionStartX = Left;
            motionStartY = Top;
            motionTargetX = targetX;
            motionTargetY = targetY;
            motionStartedAt = DateTime.UtcNow;
            motionDurationSeconds = Math.Max(0.1, durationSeconds);
            motionTimer.Start();
        }

        private void OnMotionTick(object sender, EventArgs e)
        {
            if (paused)
            {
                return;
            }
            double progress = (DateTime.UtcNow - motionStartedAt).TotalSeconds / motionDurationSeconds;
            progress = Math.Max(0.0, Math.Min(1.0, progress));
            double eased = motionKind == MotionKind.Drop ? progress * progress : SmoothStep(progress);
            Left = Math.Round(motionStartX + (motionTargetX - motionStartX) * eased);
            Top = Math.Round(motionStartY + (motionTargetY - motionStartY) * eased);
            if (progress < 1.0)
            {
                return;
            }
            MotionKind completed = motionKind;
            motionKind = MotionKind.None;
            motionTimer.Stop();
            if (completed == MotionKind.Walk)
            {
                UpdateNormalizedX();
                ReturnToIdle();
            }
            else if (completed == MotionKind.Climb)
            {
                if (!climbDescending && climbWillFall)
                {
                    BeginDropFromCurrentPosition(true);
                }
                else if (!climbDescending)
                {
                    climbDescending = true;
                    Rect area = SystemParameters.WorkArea;
                    PlayLoop("climbDown", PrefixFor("climbDown"), 165);
                    StartMotion(MotionKind.Climb, Left, area.Bottom - Height, Math.Max(2.8, Math.Abs((area.Bottom - Height) - Top) / 48.0));
                }
                else
                {
                    Rect area = SystemParameters.WorkArea;
                    Left = Math.Max(area.Left, Math.Min(area.Right - Width, Left));
                    UpdateNormalizedX();
                    ReturnToIdle();
                }
            }
            else if (completed == MotionKind.Drop)
            {
                UpdateNormalizedX();
                if (dropShouldCry)
                {
                    BeginCry();
                }
                else
                {
                    PlayOnce("happy", PrefixFor("happy"), MillisecondsFor("happy"), ReturnToIdle);
                }
            }
        }

        private static double SmoothStep(double value)
        {
            return value * value * (3.0 - 2.0 * value);
        }

        private void BeginCry()
        {
            PlayLoop("cry", PrefixFor("cry"), 160);
            emotionEndsAt = DateTime.UtcNow.AddSeconds(random.Next(5, 9));
        }

        private void Comfort()
        {
            ShowSpeech("嘿嘿，有旅行者真好！");
            PlayOnce("happy", PrefixFor("happy"), MillisecondsFor("happy"), ReturnToIdle);
        }

        private void PlayPersonalityGesture()
        {
            string[,] sequences = new string[,] { { "snack", "happy" }, { "special", "wave" }, { "spin", "happy" }, { "curious", "wave" } };
            int selected = random.Next(sequences.GetLength(0));
            string first = sequences[selected, 0];
            string second = sequences[selected, 1];
            PlayOnce(first, PrefixFor(first), MillisecondsFor(first), delegate
            {
                PlayOnce(second, PrefixFor(second), MillisecondsFor(second), ReturnToIdle);
            });
        }

        private void ReturnToIdle()
        {
            sprite.RenderTransform = Transform.Identity;
            Rect area = SystemParameters.WorkArea;
            Left = Math.Round(Math.Max(area.Left, Math.Min(area.Right - Width, Left)));
            Top = area.Bottom - Height;
            PlayLoop("idle", PrefixFor("idle"), 180);
        }

        private string PrefixFor(string action)
        {
            switch (action)
            {
                case "idle": return "idle_";
                case "walk": return "move_";
                case "jump": return "bounce_";
                case "wave": return "wave_";
                case "climbLeft": return "climb_";
                case "climbRight": return "climb_";
                case "climbDown": return "climb_down_";
                case "fall": return "fall_";
                case "cry": return "cry_";
                case "happy": return "happy_";
                case "sleep": return "sleep_";
                case "curious": return "curious_";
                case "snack": return "snack_";
                case "spin": return "spin_";
                case "wronged": return "cry_";
                case "special": return "special_";
                default: return "idle_";
            }
        }

        private int MillisecondsFor(string action)
        {
            switch (action)
            {
                case "sleep": return 210;
                case "climbDown": return 165;
                case "spin": return 125;
                case "wronged": return 165;
                case "curious": return 150;
                case "wave": return 150;
                case "jump": return 135;
                case "happy": return 145;
                case "snack": return 155;
                case "special": return 150;
                default: return 145;
            }
        }

        private void PlayLoop(string newState, string prefix, int milliseconds)
        {
            Play(newState, prefix, milliseconds, true, null);
        }

        private void PlayOnce(string newState, string prefix, int milliseconds, Action completed)
        {
            Play(newState, prefix, milliseconds, false, completed);
        }

        private void Play(string newState, string prefix, int milliseconds, bool loop, Action completed)
        {
            List<string> frames = FindFrames(prefix);
            if (frames.Count == 0 && !string.Equals(prefix, "idle_", StringComparison.OrdinalIgnoreCase))
            {
                frames = FindFrames("idle_");
            }
            currentFrames = frames;
            frameIndex = 0;
            currentLoop = loop;
            animationCompleted = completed;
            state = newState;
            frameTimer.Interval = TimeSpan.FromMilliseconds(milliseconds);
            RenderCurrentFrame();
            if (!paused && currentFrames.Count > 1)
            {
                frameTimer.Start();
            }
        }

        private List<string> FindFrames(string prefix)
        {
            if (!Directory.Exists(assetFolder))
            {
                return new List<string>();
            }
            string[] files = Directory.GetFiles(assetFolder, prefix + "*.png", SearchOption.TopDirectoryOnly);
            return files.OrderBy(NumericSuffix).ThenBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static int NumericSuffix(string path)
        {
            Match match = Regex.Match(Path.GetFileNameWithoutExtension(path), "(\\d+)$");
            int value;
            return match.Success && int.TryParse(match.Groups[1].Value, out value) ? value : int.MaxValue;
        }

        private void OnFrameTick(object sender, EventArgs e)
        {
            if (paused || currentFrames.Count == 0)
            {
                return;
            }
            frameIndex++;
            if (frameIndex >= currentFrames.Count)
            {
                if (currentLoop)
                {
                    frameIndex = 0;
                }
                else
                {
                    frameTimer.Stop();
                    frameIndex = currentFrames.Count - 1;
                    Action completed = animationCompleted;
                    animationCompleted = null;
                    if (completed != null)
                    {
                        completed();
                    }
                    return;
                }
            }
            RenderCurrentFrame();
        }

        private void RenderCurrentFrame()
        {
            if (currentFrames.Count == 0)
            {
                return;
            }
            string path = currentFrames[Math.Max(0, Math.Min(frameIndex, currentFrames.Count - 1))];
            BitmapImage bitmap;
            if (!bitmapCache.TryGetValue(path, out bitmap))
            {
                bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                bitmapCache[path] = bitmap;
                if (bitmapCache.Count > 100)
                {
                    string keep = path;
                    bitmapCache.Clear();
                    bitmapCache[keep] = bitmap;
                }
            }
            sprite.Source = bitmap;
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            mouseInside = true;
            EventHandler<HotkeyZoneChangedEventArgs> hotkeyHandler = HotkeyZoneChanged;
            if (hotkeyHandler != null) hotkeyHandler(this, new HotkeyZoneChangedEventArgs(true));
            if (!paused && state == "idle" && !clickThrough)
            {
                hoverTimer.Stop();
                hoverTimer.Start();
            }
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            mouseInside = false;
            EventHandler<HotkeyZoneChangedEventArgs> hotkeyHandler = HotkeyZoneChanged;
            if (hotkeyHandler != null) hotkeyHandler(this, new HotkeyZoneChangedEventArgs(false));
            hoverTimer.Stop();
        }

        private void OnHoverTimer(object sender, EventArgs e)
        {
            hoverTimer.Stop();
            if (mouseInside && !paused && state == "idle")
            {
                ShowSpeech("旅行者，在看什么呢？");
                PlayOnce("curious", PrefixFor("curious"), MillisecondsFor("curious"), ReturnToIdle);
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (paused || clickThrough)
            {
                return;
            }
            pointerDown = true;
            dragging = false;
            pointerDownScreen = GetCursorScreenPoint();
            pointerDownWindow = new Point(Left, Top);
            pointerDownLocal = e.GetPosition(this);
            CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!pointerDown || clickThrough)
            {
                return;
            }
            Point current = GetCursorScreenPoint();
            Vector delta = current - pointerDownScreen;
            if (!dragging && delta.Length > 5.0)
            {
                dragging = true;
                frameTimer.Stop();
                motionTimer.Stop();
                motionKind = MotionKind.None;
                ShowSpeech("抓稳一点呀！");
                PlayLoop("held", PrefixFor("jump"), 120);
            }
            if (dragging)
            {
                Rect area = SystemParameters.WorkArea;
                Point target = CalculateDragPosition(pointerDownScreen, pointerDownWindow, current);
                Left = Math.Max(area.Left, Math.Min(area.Right - Width, target.X));
                Top = Math.Max(area.Top, Math.Min(area.Bottom - Height, target.Y));
            }
        }

        private Point GetCursorScreenPoint()
        {
            System.Drawing.Point cursor = WinForms.Cursor.Position;
            Point devicePoint = new Point(cursor.X, cursor.Y);
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                return source.CompositionTarget.TransformFromDevice.Transform(devicePoint);
            }
            return devicePoint;
        }

        internal static Point CalculateDragPosition(Point cursorStart, Point windowStart, Point cursorNow)
        {
            Vector delta = cursorNow - cursorStart;
            return new Point(windowStart.X + delta.X, windowStart.Y + delta.Y);
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!pointerDown)
            {
                return;
            }
            pointerDown = false;
            ReleaseMouseCapture();
            if (dragging)
            {
                dragging = false;
                ShowSpeech("放我下来啦！");
                BeginDropFromCurrentPosition(false);
            }
            else
            {
                RegisterClick(pointerDownLocal);
            }
            e.Handled = true;
        }

        private void RegisterClick(Point localPoint)
        {
            if (state == "cry")
            {
                pendingClickCount = 0;
                clickTimer.Stop();
                Comfort();
                return;
            }
            DateTime now = DateTime.UtcNow;
            pendingClickNormalizedY = Height <= 0 ? 0.5 : Math.Max(0.0, Math.Min(1.0, localPoint.Y / Height));
            if ((now - lastClickAt).TotalMilliseconds <= 420)
            {
                pendingClickCount++;
            }
            else
            {
                pendingClickCount = 1;
            }
            lastClickAt = now;
            if (pendingClickCount >= 3)
            {
                pendingClickCount = 0;
                clickTimer.Stop();
                ShowSpeech("旅行者——别一直戳啦！");
                CancelMotionAndSnap();
                PlayOnce("special", PrefixFor("special"), MillisecondsFor("special"), delegate
                {
                    PlayOnce("cry", PrefixFor("cry"), MillisecondsFor("wronged"), ReturnToIdle);
                });
            }
            else
            {
                clickTimer.Stop();
                clickTimer.Start();
            }
        }

        private void OnClickTimer(object sender, EventArgs e)
        {
            clickTimer.Stop();
            int clicks = pendingClickCount;
            pendingClickCount = 0;
            if (state == "cry")
            {
                return;
            }
            if (clicks == 1)
            {
                CancelMotionAndSnap();
                if (pendingClickNormalizedY <= 0.55)
                {
                    ShowSpeech("嘿嘿，摸摸头～");
                    PlayOnce("happy", PrefixFor("happy"), MillisecondsFor("happy"), ReturnToIdle);
                }
                else
                {
                    ShowSpeech("别戳肚子啦！");
                    PlayOnce("special", PrefixFor("special"), MillisecondsFor("special"), ReturnToIdle);
                }
            }
            else if (clicks == 2)
            {
                ShowSpeech("派蒙飞起来啦！");
                RunPersonalitySequence("bounce", "happy");
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (paused || clickThrough)
            {
                return;
            }
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                EventHandler<ChatHistoryRequestedEventArgs> historyHandler = ChatHistoryRequested;
                if (historyHandler != null) historyHandler(this, new ChatHistoryRequestedEventArgs(e.Delta));
                e.Handled = true;
                return;
            }
            DateTime now = DateTime.UtcNow;
            if ((now - lastWheelAt).TotalMilliseconds < 650)
            {
                e.Handled = true;
                return;
            }
            lastWheelAt = now;
            RunSpinInteraction();
            e.Handled = true;
        }

        private void UpdateNormalizedX()
        {
            Rect area = SystemParameters.WorkArea;
            double width = Math.Max(1.0, area.Width - Width);
            normalizedX = Math.Max(0.0, Math.Min(1.0, (Left - area.Left) / width));
            EventHandler<NormalizedXChangedEventArgs> handler = NormalizedXChanged;
            if (handler != null)
            {
                handler(this, new NormalizedXChangedEventArgs(character.id, normalizedX));
            }
        }
    }
}
