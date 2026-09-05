using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace GenshinDesktopPet
{
    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                CharacterCatalog catalog = CharacterCatalog.Load(Path.Combine(baseDirectory, "characters.json"));
                if (args.Any(a => string.Equals(a, "--self-test", StringComparison.OrdinalIgnoreCase)))
                {
                    return SelfTest.Run(baseDirectory, catalog);
                }
                Application application = new Application();
                application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                AppController controller = new AppController(application, baseDirectory, catalog);
                controller.Start();
                bool inputSmokeTest = args.Any(a => string.Equals(a, "--input-smoke-test", StringComparison.OrdinalIgnoreCase));
                if (inputSmokeTest || args.Any(a => string.Equals(a, "--smoke-test", StringComparison.OrdinalIgnoreCase)))
                {
                    System.Windows.Threading.DispatcherTimer smokeTimer = new System.Windows.Threading.DispatcherTimer();
                    smokeTimer.Interval = TimeSpan.FromSeconds(inputSmokeTest ? 1 : 2);
                    smokeTimer.Tick += delegate
                    {
                        smokeTimer.Stop();
                        string inputDetails = "not-requested";
                        bool inputOk = !inputSmokeTest || controller.RunInputPipelineSelfTest(out inputDetails);
                        if (inputSmokeTest)
                        {
                            string chatDetails;
                            bool chatOk = controller.RunChatUiSelfTest(out chatDetails);
                            inputOk = inputOk && chatOk;
                            File.WriteAllText(Path.Combine(baseDirectory, "input-smoke-result.txt"), inputDetails + ";" + chatDetails);
                        }
                        controller.Dispose();
                        application.Shutdown(inputOk ? 0 : 3);
                    };
                    smokeTimer.Start();
                }
                return application.Run();
            }
            catch (Exception exception)
            {
                if (args.Any(a => a.IndexOf("smoke-test", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke-error.txt"), exception.ToString()); }
                    catch { }
                    return 1;
                }
                System.Windows.MessageBox.Show(exception.ToString(), "桌宠启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
        }
    }

    internal static class SelfTest
    {
        public static int Run(string baseDirectory, CharacterCatalog catalog)
        {
            int failures = 0;
            Point dragTarget = PetWindow.CalculateDragPosition(new Point(100, 80), new Point(25, 40), new Point(145, 110));
            if (Math.Abs(dragTarget.X - 70) > 0.001 || Math.Abs(dragTarget.Y - 70) > 0.001)
            {
                Console.Error.WriteLine("拖动坐标不是 1:1：" + dragTarget);
                failures++;
            }
            foreach (CharacterDefinition character in catalog.characters)
            {
                string folder = Path.Combine(baseDirectory, "assets", "characters", character.folder);
                if (!Directory.Exists(folder))
                {
                    Console.Error.WriteLine("缺少角色目录：" + folder);
                    failures++;
                    continue;
                }
                string[] requiredPrefixes = new string[] { "idle_", "move_", "bounce_", "wave_", "climb_", "climb_down_", "fall_", "cry_", "happy_", "sleep_", "curious_", "snack_", "spin_", "special_" };
                foreach (string prefix in requiredPrefixes)
                {
                    if (Directory.GetFiles(folder, prefix + "*.png").Length == 0)
                    {
                        Console.Error.WriteLine(character.displayName + " 缺少动作：" + prefix);
                        failures++;
                    }
                }
                foreach (string imagePath in Directory.GetFiles(folder, "*.png"))
                {
                    try
                    {
                        using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(imagePath))
                        {
                            if (bitmap.Width != 384 || bitmap.Height != 384)
                            {
                                Console.Error.WriteLine(character.displayName + " 尺寸异常：" + Path.GetFileName(imagePath));
                                failures++;
                                continue;
                            }
                            bool nonEmpty = false;
                            for (int y = 0; y < bitmap.Height && !nonEmpty; y += 4)
                            {
                                for (int x = 0; x < bitmap.Width; x += 4)
                                {
                                    if (bitmap.GetPixel(x, y).A > 0)
                                    {
                                        nonEmpty = true;
                                        break;
                                    }
                                }
                            }
                            if (!nonEmpty)
                            {
                                Console.Error.WriteLine(character.displayName + " 空白帧：" + Path.GetFileName(imagePath));
                                failures++;
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        Console.Error.WriteLine(character.displayName + " 无法读取：" + Path.GetFileName(imagePath) + " " + exception.Message);
                        failures++;
                    }
                }
                PetWindow testWindow = null;
                try
                {
                    testWindow = new PetWindow(character, folder, 100, 0.08);
                    string[] requiredMenuHeaders = new string[] { "派蒙聊天", "打开完整聊天窗口", "显示/隐藏快捷输入行（Ctrl+L）", "打个招呼", "跳一下", "好奇地看看", "装可怜", "投喂小零食", "转圈逗她", "安慰一下", "去爬墙", "角色专属动作", "点击互动说明", "Ctrl+滚轮：翻看历史会话", "缩放", "回到任务栏", "隐藏这个角色", "退出桌宠" };
                    foreach (string header in requiredMenuHeaders)
                    {
                        if (!MenuContainsHeader(testWindow.ContextMenu, header))
                        {
                            Console.Error.WriteLine(character.displayName + " 右键菜单缺少：" + header);
                            failures++;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(character.displayName + " 右键菜单初始化失败：" + exception.Message);
                    failures++;
                }
                finally
                {
                    if (testWindow != null)
                    {
                        testWindow.ClosePermanently();
                    }
                }
            }
            try
            {
                ChatProviderClient client = new ChatProviderClient();
                string deepSeek = ChatProviderClient.NormalizeEndpoint("https://api.deepseek.com");
                string openAi = ChatProviderClient.NormalizeEndpoint("https://api.openai.com/v1/");
                string parsed = client.ParseAssistantContent("{\"choices\":[{\"message\":{\"content\":\"你好\"}}]}");
                string streamed = client.ParseStreamDelta("{\"choices\":[{\"delta\":{\"content\":\"派蒙\"}}]}");
                string localReply;
                bool localMatched = PaimonQuickReplyRules.TryReply("派蒙，你好！", out localReply);
                string originReply;
                bool originMatched = PaimonQuickReplyRules.TryReply("你来自哪？", out originReply);
                string unknownReply;
                bool unknownMatched = PaimonQuickReplyRules.TryReply("请计算一颗陌生行星的轨道参数", out unknownReply);
                string[] normalizedExamples = new string[]
                {
                    "派蒙，请问你叫什么呀？",
                    "那个……你在干啥呢？",
                    "派蒙，你从哪儿来的呀？",
                    "　ＨＥＬＬＯ！　",
                    "我想问一下，你会飞吗？"
                };
                bool normalizationMatched = true;
                foreach (string example in normalizedExamples)
                {
                    string exampleReply;
                    if (!PaimonQuickReplyRules.TryReply(example, out exampleReply) || string.IsNullOrWhiteSpace(exampleReply))
                    {
                        Console.Error.WriteLine("归一化样例未命中：" + example);
                        normalizationMatched = false;
                    }
                }
                string negationReply;
                bool negationWasMisclassified = PaimonQuickReplyRules.TryReply("派蒙，你不喜欢我吗？", out negationReply);
                ChatMemoryDocument memory = ChatMemoryDocument.CreateEmpty();
                for (int index = 0; index < 45; index++)
                {
                    memory.RecentMessages.Add(new ChatMessageRecord { Role = index % 2 == 0 ? "user" : "assistant", Content = "消息" + index, TimestampUtc = DateTime.UtcNow.AddMinutes(index).ToString("o") });
                }
                System.Collections.Generic.List<ChatRequestMessage> context = PaimonChatEngine.BuildConversation(memory);
                bool originIsSafe = originMatched && (originReply.IndexOf("来历", StringComparison.Ordinal) >= 0 || originReply.IndexOf("故乡", StringComparison.Ordinal) >= 0);
                bool coreOk = deepSeek == "https://api.deepseek.com/chat/completions" && openAi == "https://api.openai.com/v1/chat/completions" && parsed == "你好" && streamed == "派蒙" && localMatched && !string.IsNullOrWhiteSpace(localReply) && originIsSafe && !unknownMatched && normalizationMatched && !negationWasMisclassified && PaimonQuickReplyRules.RuleCount == 500 && context.Count == 17 && context[0].content.IndexOf("不可变更", StringComparison.Ordinal) >= 0 && context[0].content.IndexOf("不主动追问", StringComparison.Ordinal) >= 0;
                if (!coreOk)
                {
                    Console.Error.WriteLine("聊天核心自检失败。normalization=" + normalizationMatched + ", negationSafe=" + !negationWasMisclassified + ", rules=" + PaimonQuickReplyRules.RuleCount);
                    failures++;
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("聊天核心自检异常：" + exception.Message);
                failures++;
            }
            Console.WriteLine(failures == 0 ? "SELF_TEST_OK" : "SELF_TEST_FAILED=" + failures);
            return failures == 0 ? 0 : 2;
        }

        private static bool MenuContainsHeader(System.Windows.Controls.ItemsControl root, string expected)
        {
            foreach (object raw in root.Items)
            {
                System.Windows.Controls.MenuItem item = raw as System.Windows.Controls.MenuItem;
                if (item == null)
                {
                    continue;
                }
                if (string.Equals(Convert.ToString(item.Header), expected, StringComparison.Ordinal))
                {
                    return true;
                }
                if (MenuContainsHeader(item, expected))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
