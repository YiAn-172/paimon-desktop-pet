using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace GenshinDesktopPet
{
    public sealed class AppController : IDisposable
    {
        private readonly Application application;
        private readonly string baseDirectory;
        private readonly CharacterCatalog catalog;
        private readonly SettingsStore settingsStore;
        private readonly ChatSettingsStore chatSettingsStore;
        private readonly ChatMemoryStore chatMemoryStore;
        private readonly PaimonChatEngine chatEngine;
        private readonly Dictionary<string, PetWindow> windows;
        private readonly DispatcherTimer fullscreenTimer;
        private AppSettings settings;
        private WinForms.NotifyIcon trayIcon;
        private WinForms.ToolStripMenuItem characterMenu;
        private WinForms.ToolStripMenuItem scaleMenu;
        private WinForms.ToolStripMenuItem topmostMenuItem;
        private WinForms.ToolStripMenuItem clickThroughMenuItem;
        private WinForms.ToolStripMenuItem pauseMenuItem;
        private WinForms.ToolStripMenuItem quickChatMenuItem;
        private ChatWindow chatWindow;
        private QuickChatWindow quickChatWindow;
        private GlobalHotkeyWindow hotkeyWindow;
        private bool fullscreen;
        private bool chatWasVisibleBeforeFullscreen;
        private int historyMessageIndex = -1;
        private bool disposed;

        public AppController(Application application, string baseDirectory, CharacterCatalog catalog)
        {
            this.application = application;
            this.baseDirectory = baseDirectory;
            this.catalog = catalog;
            settingsStore = new SettingsStore();
            chatSettingsStore = new ChatSettingsStore();
            chatMemoryStore = new ChatMemoryStore();
            chatEngine = new PaimonChatEngine(chatSettingsStore, chatMemoryStore);
            settings = settingsStore.Load();
            settings.Normalize(catalog.characters);
            windows = new Dictionary<string, PetWindow>(StringComparer.OrdinalIgnoreCase);
            fullscreenTimer = new DispatcherTimer(DispatcherPriority.Background);
            fullscreenTimer.Interval = TimeSpan.FromSeconds(2);
            fullscreenTimer.Tick += delegate { UpdateFullscreenState(); };
        }

        public void Start()
        {
            CreateTrayIcon();
            hotkeyWindow = new GlobalHotkeyWindow();
            hotkeyWindow.ToggleQuickChatRequested += delegate
            {
                application.Dispatcher.BeginInvoke(new Action(delegate { ToggleQuickChat(true); }));
            };
            ReconcileWindows();
            fullscreenTimer.Start();
            UpdateFullscreenState();
        }

        internal bool RunInputPipelineSelfTest(out string details)
        {
            PetWindow window = windows.Values.FirstOrDefault();
            if (window == null)
            {
                details = "window=null";
                return false;
            }
            window.NormalizedXChanged -= OnNormalizedXChanged;
            window.ScalePercentChanged -= OnScalePercentChanged;
            try { return window.RunInputPipelineSelfTest(out details); }
            finally
            {
                window.NormalizedXChanged += OnNormalizedXChanged;
                window.ScalePercentChanged += OnScalePercentChanged;
            }
        }

        internal bool RunChatUiSelfTest(out string details)
        {
            ChatWindow testWindow = new ChatWindow(chatSettingsStore, chatEngine);
            QuickChatWindow quickWindow = new QuickChatWindow(chatSettingsStore, chatEngine);
            try
            {
                string fullDetails;
                string quickDetails;
                bool fullOk = testWindow.RunUiSelfTest(out fullDetails);
                bool quickOk = quickWindow.RunUiSelfTest(out quickDetails);
                bool hotkeyOk = hotkeyWindow != null && hotkeyWindow.RunRegistrationSelfTest() && !hotkeyWindow.Registered;
                bool ok = fullOk && quickOk && hotkeyOk;
                details = fullDetails + ";" + quickDetails + ";ctrl-l-hover-only=" + hotkeyOk;
                return ok;
            }
            finally
            {
                testWindow.ClosePermanently();
                quickWindow.ClosePermanently();
            }
        }

        private void CreateTrayIcon()
        {
            trayIcon = new WinForms.NotifyIcon();
            string iconPath = Path.Combine(baseDirectory, "assets", "icon.ico");
            trayIcon.Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
            trayIcon.Text = "派蒙桌宠";
            trayIcon.Visible = true;

            WinForms.ContextMenuStrip menu = new WinForms.ContextMenuStrip();
            WinForms.ToolStripMenuItem chatItem = new WinForms.ToolStripMenuItem("派蒙聊天");
            WinForms.ToolStripMenuItem fullChatItem = new WinForms.ToolStripMenuItem("打开完整聊天窗口");
            fullChatItem.Click += delegate { OpenChat(); };
            chatItem.DropDownItems.Add(fullChatItem);
            quickChatMenuItem = new WinForms.ToolStripMenuItem("显示快捷输入行（Ctrl+L）");
            quickChatMenuItem.CheckOnClick = true;
            quickChatMenuItem.Checked = settings.QuickChatBarMode == 1;
            quickChatMenuItem.Click += delegate
            {
                SetQuickChatVisible(quickChatMenuItem.Checked, quickChatMenuItem.Checked);
            };
            chatItem.DropDownItems.Add(quickChatMenuItem);
            menu.Items.Add(chatItem);
            menu.Items.Add(new WinForms.ToolStripSeparator());
            characterMenu = new WinForms.ToolStripMenuItem("显示派蒙");
            scaleMenu = new WinForms.ToolStripMenuItem("角色缩放");
            menu.Items.Add(characterMenu);
            menu.Items.Add(scaleMenu);
            menu.Items.Add(new WinForms.ToolStripSeparator());

            topmostMenuItem = new WinForms.ToolStripMenuItem("始终置顶");
            topmostMenuItem.Checked = settings.AlwaysOnTop;
            topmostMenuItem.CheckOnClick = true;
            topmostMenuItem.Click += delegate
            {
                settings.AlwaysOnTop = topmostMenuItem.Checked;
                foreach (PetWindow window in windows.Values)
                {
                    window.Topmost = settings.AlwaysOnTop;
                }
                if (chatWindow != null) chatWindow.Topmost = settings.AlwaysOnTop;
                if (quickChatWindow != null) quickChatWindow.Topmost = settings.AlwaysOnTop;
                SaveSettings();
            };
            menu.Items.Add(topmostMenuItem);

            clickThroughMenuItem = new WinForms.ToolStripMenuItem("鼠标穿透（开启后无法点击桌宠）");
            clickThroughMenuItem.Checked = settings.ClickThrough;
            clickThroughMenuItem.CheckOnClick = true;
            clickThroughMenuItem.Click += delegate
            {
                settings.ClickThrough = clickThroughMenuItem.Checked;
                foreach (PetWindow window in windows.Values)
                {
                    window.SetClickThrough(settings.ClickThrough);
                }
                if (hotkeyWindow != null)
                {
                    PetWindow hoverPet = windows.Values.FirstOrDefault();
                    hotkeyWindow.SetEnabled(!settings.ClickThrough && hoverPet != null && hoverPet.IsMouseOver && !fullscreen);
                }
                SaveSettings();
            };
            menu.Items.Add(clickThroughMenuItem);

            pauseMenuItem = new WinForms.ToolStripMenuItem("暂停活动");
            pauseMenuItem.CheckOnClick = true;
            pauseMenuItem.Click += delegate
            {
                foreach (PetWindow window in windows.Values)
                {
                    window.SetPaused(pauseMenuItem.Checked || fullscreen);
                }
            };
            menu.Items.Add(pauseMenuItem);

            WinForms.ToolStripMenuItem hideAllItem = new WinForms.ToolStripMenuItem("隐藏全部");
            hideAllItem.Click += delegate
            {
                settings.ActiveCharacterIds.Clear();
                ReconcileWindows();
                SaveSettings();
                RefreshTrayChecks();
            };
            menu.Items.Add(hideAllItem);

            WinForms.ToolStripMenuItem resetItem = new WinForms.ToolStripMenuItem("重置位置");
            resetItem.Click += delegate
            {
                ResetPositions();
            };
            menu.Items.Add(resetItem);
            menu.Items.Add(new WinForms.ToolStripSeparator());

            WinForms.ToolStripMenuItem exitItem = new WinForms.ToolStripMenuItem("退出");
            exitItem.Click += delegate { Exit(); };
            menu.Items.Add(exitItem);

            trayIcon.ContextMenuStrip = menu;
            trayIcon.DoubleClick += delegate
            {
                if (settings.ActiveCharacterIds.Count == 0)
                {
                    settings.ActiveCharacterIds.Add("paimon");
                    ReconcileWindows();
                    RefreshTrayChecks();
                    SaveSettings();
                }
            };

            foreach (CharacterDefinition character in catalog.characters)
            {
                CharacterDefinition captured = character;
                WinForms.ToolStripMenuItem item = new WinForms.ToolStripMenuItem(character.displayName);
                item.Name = "character_" + character.id;
                item.CheckOnClick = true;
                item.Checked = settings.ActiveCharacterIds.Contains(character.id, StringComparer.OrdinalIgnoreCase);
                item.Click += delegate
                {
                    ToggleCharacter(captured, item.Checked);
                };
                characterMenu.DropDownItems.Add(item);

                WinForms.ToolStripMenuItem characterScaleItem = new WinForms.ToolStripMenuItem(character.displayName);
                foreach (int percent in new int[] { 100, 125, 150 })
                {
                    int capturedPercent = percent;
                    WinForms.ToolStripMenuItem scaleItem = new WinForms.ToolStripMenuItem(percent + "%");
                    scaleItem.Name = "scale_" + character.id + "_" + percent;
                    scaleItem.Checked = settings.CharacterScalePercent[character.id] == percent;
                    scaleItem.Click += delegate
                    {
                        SetCharacterScale(captured, capturedPercent);
                    };
                    characterScaleItem.DropDownItems.Add(scaleItem);
                }
                scaleMenu.DropDownItems.Add(characterScaleItem);
            }
        }

        private void ToggleCharacter(CharacterDefinition character, bool requestedVisible)
        {
            bool currentlyVisible = settings.ActiveCharacterIds.Contains(character.id, StringComparer.OrdinalIgnoreCase);
            if (requestedVisible && !currentlyVisible)
            {
                if (settings.ActiveCharacterIds.Count >= 3)
                {
                    WinForms.MessageBox.Show("最多可同时显示 3 个桌宠。", "桌宠", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                }
                else
                {
                    settings.ActiveCharacterIds.Add(character.id);
                }
            }
            else if (!requestedVisible && currentlyVisible)
            {
                settings.ActiveCharacterIds.RemoveAll(id => string.Equals(id, character.id, StringComparison.OrdinalIgnoreCase));
            }
            ReconcileWindows();
            RefreshTrayChecks();
            SaveSettings();
        }

        private void SetCharacterScale(CharacterDefinition character, int percent)
        {
            settings.CharacterScalePercent[character.id] = percent;
            PetWindow window;
            if (windows.TryGetValue(character.id, out window))
            {
                window.SetScalePercent(percent);
            }
            RefreshTrayChecks();
            SaveSettings();
        }

        private void RefreshTrayChecks()
        {
            foreach (WinForms.ToolStripItem rawItem in characterMenu.DropDownItems)
            {
                WinForms.ToolStripMenuItem item = rawItem as WinForms.ToolStripMenuItem;
                if (item == null || !item.Name.StartsWith("character_", StringComparison.Ordinal))
                {
                    continue;
                }
                string id = item.Name.Substring("character_".Length);
                item.Checked = settings.ActiveCharacterIds.Contains(id, StringComparer.OrdinalIgnoreCase);
            }
            foreach (WinForms.ToolStripItem rawCharacter in scaleMenu.DropDownItems)
            {
                WinForms.ToolStripMenuItem characterItem = rawCharacter as WinForms.ToolStripMenuItem;
                if (characterItem == null)
                {
                    continue;
                }
                foreach (WinForms.ToolStripItem rawScale in characterItem.DropDownItems)
                {
                    WinForms.ToolStripMenuItem scaleItem = rawScale as WinForms.ToolStripMenuItem;
                    if (scaleItem == null || !scaleItem.Name.StartsWith("scale_", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    string[] parts = scaleItem.Name.Split('_');
                    if (parts.Length == 3)
                    {
                        int percent;
                        if (int.TryParse(parts[2], out percent))
                        {
                            scaleItem.Checked = settings.CharacterScalePercent.ContainsKey(parts[1]) && settings.CharacterScalePercent[parts[1]] == percent;
                        }
                    }
                }
            }
        }

        private void ReconcileWindows()
        {
            foreach (CharacterDefinition character in catalog.characters)
            {
                bool active = settings.ActiveCharacterIds.Contains(character.id, StringComparer.OrdinalIgnoreCase);
                PetWindow window;
                if (active)
                {
                    if (!windows.TryGetValue(character.id, out window))
                    {
                        string assetFolder = Path.Combine(baseDirectory, "assets", "characters", character.folder);
                        window = new PetWindow(character, assetFolder, settings.CharacterScalePercent[character.id], settings.CharacterNormalizedX[character.id]);
                        window.Topmost = settings.AlwaysOnTop;
                        window.NormalizedXChanged += OnNormalizedXChanged;
                        window.ScalePercentChanged += OnScalePercentChanged;
                        window.HideRequested += OnHideRequested;
                        window.ExitRequested += OnExitRequested;
                        window.ChatRequested += OnChatRequested;
                        window.ChatHistoryRequested += OnChatHistoryRequested;
                        window.QuickChatToggleRequested += OnQuickChatToggleRequested;
                        window.HotkeyZoneChanged += OnHotkeyZoneChanged;
                        window.Show();
                        window.SetClickThrough(settings.ClickThrough);
                        windows.Add(character.id, window);
                    }
                    if (!fullscreen)
                    {
                        window.SetPaused(pauseMenuItem != null && pauseMenuItem.Checked);
                        window.ShowPet();
                    }
                }
                else if (windows.TryGetValue(character.id, out window))
                {
                    window.NormalizedXChanged -= OnNormalizedXChanged;
                    window.ScalePercentChanged -= OnScalePercentChanged;
                    window.HideRequested -= OnHideRequested;
                    window.ExitRequested -= OnExitRequested;
                    window.ChatRequested -= OnChatRequested;
                    window.ChatHistoryRequested -= OnChatHistoryRequested;
                    window.QuickChatToggleRequested -= OnQuickChatToggleRequested;
                    window.HotkeyZoneChanged -= OnHotkeyZoneChanged;
                    if (hotkeyWindow != null) hotkeyWindow.SetEnabled(false);
                    window.ClosePermanently();
                    windows.Remove(character.id);
                }
            }
            UpdateQuickChatBar();
        }

        private void OnNormalizedXChanged(object sender, NormalizedXChangedEventArgs e)
        {
            settings.CharacterNormalizedX[e.CharacterId] = e.Value;
            SaveSettings();
        }

        private void OnScalePercentChanged(object sender, ScalePercentChangedEventArgs e)
        {
            settings.CharacterScalePercent[e.CharacterId] = e.Percent;
            RefreshTrayChecks();
            SaveSettings();
        }

        private void OnHideRequested(object sender, EventArgs e)
        {
            PetWindow window = sender as PetWindow;
            if (window == null)
            {
                return;
            }
            CharacterDefinition character = catalog.characters.FirstOrDefault(c => string.Equals(c.id, window.CharacterId, StringComparison.OrdinalIgnoreCase));
            if (character != null)
            {
                settings.ActiveCharacterIds.RemoveAll(id => string.Equals(id, character.id, StringComparison.OrdinalIgnoreCase));
                ReconcileWindows();
                RefreshTrayChecks();
                SaveSettings();
            }
        }

        private void OnExitRequested(object sender, EventArgs e)
        {
            Exit();
        }

        private void OnChatRequested(object sender, EventArgs e)
        {
            OpenChat();
        }

        private void OnQuickChatToggleRequested(object sender, EventArgs e)
        {
            ToggleQuickChat(true);
        }

        private void OnHotkeyZoneChanged(object sender, HotkeyZoneChangedEventArgs e)
        {
            if (hotkeyWindow != null)
            {
                hotkeyWindow.SetEnabled(e.IsInside && !fullscreen && !settings.ClickThrough);
            }
        }

        private void OpenChat()
        {
            if (fullscreen) return;
            if (chatWindow == null)
            {
                chatWindow = new ChatWindow(chatSettingsStore, chatEngine);
                chatWindow.Topmost = settings.AlwaysOnTop;
                chatWindow.UserMessageSent += delegate { ReactFirstPet(delegate(PetWindow pet) { pet.ReactToChatThinking(); }); };
                chatWindow.PaimonReplyProgress += delegate(object sender, ChatReplyEventArgs e)
                {
                    ReactFirstPet(delegate(PetWindow pet) { pet.ShowChatProgress(e.Reply); });
                };
                chatWindow.PaimonReplied += delegate(object sender, ChatReplyEventArgs e)
                {
                    historyMessageIndex = -1;
                    ReactFirstPet(delegate(PetWindow pet) { pet.ReactToChatReply(e.Reply, e.IsError); });
                };
            }
            if (!chatWindow.IsVisible) chatWindow.Show();
            if (chatWindow.WindowState == WindowState.Minimized) chatWindow.WindowState = WindowState.Normal;
            chatWindow.Activate();
            ReactFirstPet(delegate(PetWindow pet) { pet.ReactToChatOpened(); });
        }

        private void EnsureQuickChatWindow()
        {
            if (quickChatWindow != null) return;
            quickChatWindow = new QuickChatWindow(chatSettingsStore, chatEngine);
            quickChatWindow.Topmost = settings.AlwaysOnTop;
            quickChatWindow.MessageSent += delegate
            {
                historyMessageIndex = -1;
                ReactFirstPet(delegate(PetWindow pet) { pet.ReactToChatThinking(); });
            };
            quickChatWindow.ReplyProgress += delegate(object sender, ChatReplyEventArgs e)
            {
                ReactFirstPet(delegate(PetWindow pet) { pet.ShowChatProgress(e.Reply); });
            };
            quickChatWindow.ReplyReceived += delegate(object sender, ChatReplyEventArgs e)
            {
                historyMessageIndex = -1;
                ReactFirstPet(delegate(PetWindow pet) { pet.ReactToChatReply(e.Reply, e.IsError); });
                if (chatWindow != null && chatWindow.IsVisible) chatWindow.RefreshFromMemory();
            };
            quickChatWindow.SettingsRequested += delegate
            {
                OpenChat();
                if (chatWindow != null) chatWindow.OpenSettings();
            };
        }

        private void UpdateQuickChatBar()
        {
            if (disposed) return;
            PetWindow pet = windows.Values.FirstOrDefault();
            if (pet == null || fullscreen || settings.QuickChatBarMode != 1)
            {
                if (quickChatWindow != null)
                {
                    quickChatWindow.Hide();
                    if (pet == null || settings.QuickChatBarMode != 1) quickChatWindow.AttachToPet(null);
                }
                return;
            }
            EnsureQuickChatWindow();
            quickChatWindow.AttachToPet(pet);
            quickChatWindow.ShowQuick();
        }

        private void ToggleQuickChat(bool focusWhenShown)
        {
            SetQuickChatVisible(settings.QuickChatBarMode != 1, focusWhenShown);
        }

        private void SetQuickChatVisible(bool visible, bool focusWhenShown)
        {
            settings.QuickChatBarMode = visible ? 1 : 2;
            if (quickChatMenuItem != null) quickChatMenuItem.Checked = visible;
            UpdateQuickChatBar();
            SaveSettings();
            if (visible && focusWhenShown && quickChatWindow != null && !fullscreen) quickChatWindow.FocusInput();
        }

        private void OnChatHistoryRequested(object sender, ChatHistoryRequestedEventArgs e)
        {
            PetWindow pet = sender as PetWindow;
            if (pet == null) return;
            IList<ChatMessageRecord> messages = chatEngine.GetHistoryDisplayMessages();
            if (messages.Count == 0)
            {
                pet.ShowChatBubble("还没有历史会话呢，先和派蒙说句话吧！", true);
                return;
            }
            if (e.Delta > 0)
            {
                historyMessageIndex = historyMessageIndex < 0 ? messages.Count - 1 : Math.Max(0, historyMessageIndex - 1);
            }
            else
            {
                if (historyMessageIndex < 0) historyMessageIndex = messages.Count - 1;
                else if (historyMessageIndex < messages.Count - 1) historyMessageIndex++;
                else
                {
                    historyMessageIndex = -1;
                    pet.ShowChatBubble("已经回到最新会话啦。", true);
                    return;
                }
            }
            ChatMessageRecord message = messages[historyMessageIndex];
            string speaker = string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? "派蒙：" :
                string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase) ? "旅行者：" : "记忆：";
            pet.ShowChatBubble((historyMessageIndex + 1) + "/" + messages.Count + " · " + speaker + message.Content, true);
        }

        private void ReactFirstPet(Action<PetWindow> action)
        {
            PetWindow pet = windows.Values.FirstOrDefault();
            if (pet != null) action(pet);
        }

        private void UpdateFullscreenState()
        {
            bool detected = NativeMethods.IsPrimaryScreenFullscreen();
            if (detected == fullscreen)
            {
                return;
            }
            fullscreen = detected;
            if (fullscreen)
            {
                if (hotkeyWindow != null) hotkeyWindow.SetEnabled(false);
                chatWasVisibleBeforeFullscreen = chatWindow != null && chatWindow.IsVisible;
                if (chatWasVisibleBeforeFullscreen) chatWindow.Hide();
                if (quickChatWindow != null) quickChatWindow.Hide();
            }
            else if (chatWasVisibleBeforeFullscreen && chatWindow != null)
            {
                chatWindow.Show();
                chatWasVisibleBeforeFullscreen = false;
            }
            foreach (PetWindow window in windows.Values)
            {
                if (fullscreen)
                {
                    window.SetPaused(true);
                    window.HidePet();
                }
                else
                {
                    window.SetPaused(pauseMenuItem != null && pauseMenuItem.Checked);
                    window.ShowPet();
                }
            }
            if (!fullscreen) UpdateQuickChatBar();
        }

        private void ResetPositions()
        {
            double[] defaults = new double[] { 0.08, 0.92, 0.20 };
            int index = 0;
            foreach (string id in settings.ActiveCharacterIds.ToList())
            {
                double value = defaults[Math.Min(index, defaults.Length - 1)];
                settings.CharacterNormalizedX[id] = value;
                PetWindow window;
                if (windows.TryGetValue(id, out window))
                {
                    window.SetNormalizedX(value);
                }
                index++;
            }
            SaveSettings();
        }

        private void SaveSettings()
        {
            settingsStore.Save(settings);
        }

        private void Exit()
        {
            SaveSettings();
            Dispose();
            application.Shutdown();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            fullscreenTimer.Stop();
            if (hotkeyWindow != null)
            {
                hotkeyWindow.Dispose();
                hotkeyWindow = null;
            }
            foreach (PetWindow window in windows.Values.ToList())
            {
                window.ClosePermanently();
            }
            windows.Clear();
            if (chatWindow != null)
            {
                chatWindow.ClosePermanently();
                chatWindow = null;
            }
            if (quickChatWindow != null)
            {
                quickChatWindow.ClosePermanently();
                quickChatWindow = null;
            }
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
        }
    }
}
