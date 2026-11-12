using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace Webpoint
{
    public class Program : Application
    {
        private const string MutexName = @"Local\Webpoint_SingleInstance_Mutex";
        private const string PipeName = @"Webpoint_IPC_Pipe";

        private static Mutex appMutex;
        private static Thread pipeServerThread;
        private static volatile bool isRunning = true;

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool fUnknown);

        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Webpoint", "debug.log");
                    File.AppendAllText(logPath, DateTime.Now.ToString("o") + " [AppDomain Exception]: " + e.ExceptionObject + Environment.NewLine);
                }
                catch { }
            };

            string arg = (args != null && args.Length > 0) ? args[0].Trim('"', ' ') : "";

            if (arg.Equals("--render-preview", StringComparison.OrdinalIgnoreCase))
            {
                string target = (args.Length > 1) ? args[1] : @"C:\Users\Nihal\.gemini\antigravity\brain\58ffe092-a0fb-4110-82ac-94e8a964f4c2\picker_preview.png";
                PickerWindow.RenderToFile(target);
                return;
            }

            // Check if already running via Mutex
            bool createdNew;
            appMutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                // Send command / URL to the existing background instance
                try
                {
                    using (NamedPipeClientStream client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                    {
                        client.Connect(800);
                        using (StreamWriter writer = new StreamWriter(client, Encoding.UTF8))
                        {
                            writer.WriteLine(string.IsNullOrEmpty(arg) ? "--settings" : arg);
                            writer.Flush();
                        }
                        try { client.WaitForPipeDrain(); } catch { }
                    }
                    return; // Ultra-fast exit (< 10ms!)
                }
                catch
                {
                    // Fall back to standalone if pipe connection fails
                }
            }

            Program app = new Program();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.DispatcherUnhandledException += (s, e) =>
            {
                try
                {
                    string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Webpoint", "debug.log");
                    File.AppendAllText(logPath, DateTime.Now.ToString("o") + " [Dispatcher Exception]: " + e.Exception + Environment.NewLine);
                }
                catch { }
                e.Handled = true;
            };

            // Start IPC background pipe listener
            StartPipeServer(app);

            // Pre-cache icons into RAM
            IconCache.Preload();

            // Handle initial argument
            if (arg.Equals("--standby", StringComparison.OrdinalIgnoreCase))
            {
                // Started in background (for instant response)
            }
            else if (arg.Equals("--settings", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(arg))
            {
                SettingsWindow win = new SettingsWindow();
                win.Show();
            }
            else
            {
                PickerWindow win = new PickerWindow(arg);
                win.Show();
            }

            app.Run();
        }

        private static void StartPipeServer(Program app)
        {
            pipeServerThread = new Thread(() =>
            {
                while (isRunning)
                {
                    try
                    {
                        using (NamedPipeServerStream server = new NamedPipeServerStream(
                            PipeName,
                            PipeDirection.In,
                            NamedPipeServerStream.MaxAllowedServerInstances,
                            PipeTransmissionMode.Byte,
                            PipeOptions.None))
                        {
                            server.WaitForConnection();

                            using (StreamReader reader = new StreamReader(server, Encoding.UTF8))
                            {
                                string msg = reader.ReadLine();
                                if (!string.IsNullOrWhiteSpace(msg))
                                {
                                    app.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        try
                                        {
                                            HandleIncomingMessage(msg.Trim());
                                        }
                                        catch (Exception ex)
                                        {
                                            try
                                            {
                                                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Webpoint", "debug.log");
                                                File.AppendAllText(logPath, DateTime.Now.ToString("o") + " [HandleIncoming Exception]: " + ex + Environment.NewLine);
                                            }
                                            catch { }
                                        }
                                    }));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Webpoint", "debug.log");
                            File.AppendAllText(logPath, DateTime.Now.ToString("o") + " [Pipe Exception]: " + ex + Environment.NewLine);
                        }
                        catch { }
                        Thread.Sleep(50);
                    }
                }
            })
            {
                IsBackground = true
            };
            pipeServerThread.Start();
        }

        private static void HandleIncomingMessage(string msg)
        {
            if (msg.Equals("--quit", StringComparison.OrdinalIgnoreCase))
            {
                isRunning = false;
                Application.Current.Shutdown();
            }
            else if (msg.Equals("--settings", StringComparison.OrdinalIgnoreCase))
            {
                SettingsWindow win = new SettingsWindow();
                win.Show();
                win.Activate();
            }
            else
            {
                PickerWindow win = new PickerWindow(msg);
                win.Show();
                win.Activate();
            }
        }
    }

    public static class IconCache
    {
        private static Dictionary<string, ImageSource> cache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        public static void Preload()
        {
            GetIcon(@"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe");
            GetIcon(@"C:\Program Files\Google\Chrome\Application\chrome.exe");
        }

        public static ImageSource GetIcon(string exePath)
        {
            if (cache.ContainsKey(exePath))
            {
                return cache[exePath];
            }

            try
            {
                if (File.Exists(exePath))
                {
                    using (System.Drawing.Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath))
                    {
                        if (icon != null)
                        {
                            ImageSource src = Imaging.CreateBitmapSourceFromHIcon(
                                icon.Handle,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            src.Freeze();
                            cache[exePath] = src;
                            return src;
                        }
                    }
                }
            }
            catch { }

            return null;
        }
    }

    public class AppSettings
    {
        public string ViewMode { get; set; } // "grid" or "list"
        public bool ShowProfileIcons { get; set; }
        public bool ShowEdgeInPrivate { get; set; }
        public bool ShowChromeIncognito { get; set; }
        public bool InstantLaunch { get; set; }

        public static string SettingsPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json"); }
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings
            {
                ViewMode = "grid",
                ShowProfileIcons = true, // Keep browser profile icons visible
                ShowEdgeInPrivate = true,
                ShowChromeIncognito = true,
                InstantLaunch = true
            };

            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    Match m = Regex.Match(json, "\"view_mode\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        settings.ViewMode = m.Groups[1].Value.Trim().ToLowerInvariant();
                    }

                    if (json.IndexOf("\"show_profile_icons\"\\s*:\\s*true", StringComparison.OrdinalIgnoreCase) >= 0)
                        settings.ShowProfileIcons = true;

                    if (json.IndexOf("\"show_edge_inprivate\"\\s*:\\s*false", StringComparison.OrdinalIgnoreCase) >= 0)
                        settings.ShowEdgeInPrivate = false;

                    if (json.IndexOf("\"show_chrome_incognito\"\\s*:\\s*false", StringComparison.OrdinalIgnoreCase) >= 0)
                        settings.ShowChromeIncognito = false;

                    if (json.IndexOf("\"instant_launch\"\\s*:\\s*false", StringComparison.OrdinalIgnoreCase) >= 0)
                        settings.InstantLaunch = false;
                }
            }
            catch { }

            return settings;
        }

        public void Save()
        {
            try
            {
                string json = string.Format(
                    "{{\r\n  \"view_mode\": \"{0}\",\r\n  \"show_profile_icons\": {1},\r\n  \"show_edge_inprivate\": {2},\r\n  \"show_chrome_incognito\": {3},\r\n  \"instant_launch\": {4}\r\n}}",
                    ViewMode,
                    ShowProfileIcons ? "true" : "false",
                    ShowEdgeInPrivate ? "true" : "false",
                    ShowChromeIncognito ? "true" : "false",
                    InstantLaunch ? "true" : "false"
                );
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }

    public class SettingsWindow : Window
    {
        private AppSettings settings;
        private RadioButton rbGrid;
        private RadioButton rbList;
        private CheckBox cbIcons;
        private CheckBox cbEdge;
        private CheckBox cbChrome;
        private CheckBox cbInstant;

        public SettingsWindow()
        {
            settings = AppSettings.Load();

            Title = "Webpoint Settings";
            Width = 470;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x20, 0x20, 0x20));
            Foreground = System.Windows.Media.Brushes.White;
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI");
            ResizeMode = ResizeMode.NoResize;

            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(icoPath))
                {
                    Icon = BitmapFrame.Create(new Uri(icoPath));
                }
            }
            catch { }

            BuildUI();
        }

        private void BuildUI()
        {
            StackPanel root = new StackPanel { Margin = new Thickness(24) };

            TextBlock title = new TextBlock
            {
                Text = "Webpoint Settings",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            root.Children.Add(title);

            TextBlock desc = new TextBlock
            {
                Text = "Configure layout, window icons, and instant startup speed.",
                FontSize = 13,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9E, 0x9E, 0x9E)),
                Margin = new Thickness(0, 0, 0, 18)
            };
            root.Children.Add(desc);

            // Section: Layout
            TextBlock section1 = new TextBlock
            {
                Text = "Layout Style",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            root.Children.Add(section1);

            StackPanel layoutGroup = new StackPanel { Margin = new Thickness(6, 0, 0, 14) };
            rbGrid = new RadioButton
            {
                Content = " \u229E  Grid View (Side-by-side tiles)",
                FontSize = 13.5,
                Foreground = System.Windows.Media.Brushes.White,
                IsChecked = settings.ViewMode == "grid",
                Margin = new Thickness(0, 0, 0, 6)
            };
            rbList = new RadioButton
            {
                Content = " \u2630  List View (Vertical lines)",
                FontSize = 13.5,
                Foreground = System.Windows.Media.Brushes.White,
                IsChecked = settings.ViewMode != "grid",
                Margin = new Thickness(0, 0, 0, 6)
            };
            layoutGroup.Children.Add(rbGrid);
            layoutGroup.Children.Add(rbList);
            root.Children.Add(layoutGroup);

            // Section: Appearance
            TextBlock sectionAppr = new TextBlock
            {
                Text = "Appearance",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 8)
            };
            root.Children.Add(sectionAppr);

            StackPanel apprGroup = new StackPanel { Margin = new Thickness(6, 0, 0, 14) };
            cbIcons = new CheckBox
            {
                Content = " Show browser icons next to profiles",
                FontSize = 13,
                Foreground = System.Windows.Media.Brushes.White,
                IsChecked = settings.ShowProfileIcons,
                Margin = new Thickness(0, 0, 0, 4)
            };
            apprGroup.Children.Add(cbIcons);
            root.Children.Add(apprGroup);

            // Section: Performance
            TextBlock sectionPerf = new TextBlock
            {
                Text = "Performance & Speed",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 8)
            };
            root.Children.Add(sectionPerf);

            StackPanel perfGroup = new StackPanel { Margin = new Thickness(6, 0, 0, 14) };
            cbInstant = new CheckBox
            {
                Content = " Instant Launch (Background Standby Worker for 0-lag startup)",
                FontSize = 13,
                Foreground = System.Windows.Media.Brushes.White,
                IsChecked = settings.InstantLaunch,
                Margin = new Thickness(0, 0, 0, 4)
            };
            perfGroup.Children.Add(cbInstant);
            root.Children.Add(perfGroup);

            // Section: Privacy Options
            TextBlock section2 = new TextBlock
            {
                Text = "Private Browsing Links",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 8)
            };
            root.Children.Add(section2);

            StackPanel privacyGroup = new StackPanel { Margin = new Thickness(6, 0, 0, 18) };
            cbEdge = new CheckBox
            {
                Content = " Show 'Open in Edge InPrivate'",
                FontSize = 13.5,
                Foreground = System.Windows.Media.Brushes.White,
                IsChecked = settings.ShowEdgeInPrivate,
                Margin = new Thickness(0, 0, 0, 6)
            };
            cbChrome = new CheckBox
            {
                Content = " Show 'Open in Chrome Incognito'",
                FontSize = 13.5,
                Foreground = System.Windows.Media.Brushes.White,
                IsChecked = settings.ShowChromeIncognito,
                Margin = new Thickness(0, 0, 0, 6)
            };
            privacyGroup.Children.Add(cbEdge);
            privacyGroup.Children.Add(cbChrome);
            root.Children.Add(privacyGroup);

            // Buttons row
            StackPanel btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 6, 0, 0)
            };

            Button btnDefaultApps = new Button
            {
                Content = "Set as Default App",
                Padding = new Thickness(14, 8, 14, 8),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x38, 0x38, 0x38)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0)
            };
            btnDefaultApps.Click += (s, e) =>
            {
                try { Process.Start("ms-settings:defaultapps"); } catch { }
            };
            btnRow.Children.Add(btnDefaultApps);

            Button btnSave = new Button
            {
                Content = "Save & Close",
                Padding = new Thickness(18, 8, 18, 8),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            btnSave.Click += (s, e) =>
            {
                settings.ViewMode = (rbGrid.IsChecked == true) ? "grid" : "list";
                settings.ShowProfileIcons = cbIcons.IsChecked == true;
                settings.InstantLaunch = cbInstant.IsChecked == true;
                settings.ShowEdgeInPrivate = cbEdge.IsChecked == true;
                settings.ShowChromeIncognito = cbChrome.IsChecked == true;
                settings.Save();
                Close();
            };
            btnRow.Children.Add(btnSave);

            root.Children.Add(btnRow);
            Content = root;
        }
    }

    public class ProfileOption
    {
        public ImageSource Icon { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string ExePath { get; set; }
        public string Arguments { get; set; }
    }

    public class PickerWindow : Window
    {
        private string targetUrl;
        private AppSettings settings;
        private List<Border> itemContainers = new List<Border>();
        private List<ProfileOption> options = new List<ProfileOption>();
        private int selectedIndex = 0;

        private static readonly SolidColorBrush CardBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2B, 0x2B, 0x2B));
        private static readonly SolidColorBrush CardBorder = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3E, 0x3E, 0x3E));
        private static readonly SolidColorBrush HoverBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x38, 0x38, 0x38));
        private static readonly SolidColorBrush NormalBg = System.Windows.Media.Brushes.Transparent;
        private static readonly SolidColorBrush TileBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x32, 0x32, 0x32));
        private static readonly SolidColorBrush TileBorder = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x40, 0x40, 0x40));
        private static readonly SolidColorBrush TextPrimary = System.Windows.Media.Brushes.White;
        private static readonly SolidColorBrush TextSecondary = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9E, 0x9E, 0x9E));
        private static readonly SolidColorBrush LinkColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x70, 0xC4, 0xDF));

        public PickerWindow(string url, bool isPreview = false)
        {
            targetUrl = url;
            settings = AppSettings.Load();

            Title = "Webpoint";
            Width = settings.ViewMode == "grid" ? 480 : 430;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;

            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(icoPath))
                {
                    Icon = BitmapFrame.Create(new Uri(icoPath));
                }
            }
            catch { }

            if (!isPreview)
            {
                Deactivated += (s, e) => Close();
            }
            KeyDown += PickerWindow_KeyDown;

            LoadProfiles();
            BuildUI();

            Loaded += (s, e) =>
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                Program.SetForegroundWindow(hwnd);
                Program.SwitchToThisWindow(hwnd, true);

                Activate();
                Focus();
                UpdateSelection();
            };
        }

        public static void RenderToFile(string outPng)
        {
            try
            {
                Program app = new Program();
                PickerWindow win = new PickerWindow("https://techno.academy", true);
                win.Loaded += (s, e) =>
                {
                    try
                    {
                        Grid root = (Grid)win.Content;
                        Border card = (Border)root.Children[0];

                        int w = (int)Math.Max(card.ActualWidth, 420);
                        int h = (int)Math.Max(card.ActualHeight, 280);

                        RenderTargetBitmap rtb = new RenderTargetBitmap(w + 32, h + 32, 96, 96, PixelFormats.Pbgra32);
                        DrawingVisual dv = new DrawingVisual();
                        using (DrawingContext dc = dv.RenderOpen())
                        {
                            dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x18, 0x18, 0x18)), null, new Rect(0, 0, w + 32, h + 32));
                            VisualBrush vb = new VisualBrush(card);
                            dc.DrawRectangle(vb, null, new Rect(16, 16, w, h));
                        }
                        rtb.Render(dv);

                        PngBitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(rtb));
                        using (FileStream fs = new FileStream(outPng, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            encoder.Save(fs);
                        }
                    }
                    finally
                    {
                        app.Shutdown();
                    }
                };
                app.Run(win);
            }
            catch (Exception ex)
            {
                try
                {
                    string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Webpoint", "debug.log");
                    File.AppendAllText(logPath, DateTime.Now.ToString("o") + " [RenderToFile Exception]: " + ex + Environment.NewLine);
                }
                catch { }
            }
        }

        private void LoadProfiles()
        {
            string edgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
            string chromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";

            ImageSource edgeIcon = IconCache.GetIcon(edgePath);
            ImageSource chromeIcon = IconCache.GetIcon(chromePath);

            // 1. Edge - Nihal Work
            options.Add(new ProfileOption
            {
                Icon = edgeIcon,
                Title = "Microsoft Edge",
                Subtitle = "Nihal Work \u2022 nihal@techno.academy",
                ExePath = edgePath,
                Arguments = "\"" + targetUrl + "\" \"--profile-directory=Default\" --no-default-browser-check"
            });

            // 2. Edge - Nihal Local
            options.Add(new ProfileOption
            {
                Icon = edgeIcon,
                Title = "Microsoft Edge",
                Subtitle = "Nihal Local (Personal)",
                ExePath = edgePath,
                Arguments = "\"" + targetUrl + "\" \"--profile-directory=Profile 1\" --no-default-browser-check"
            });

            // 3. Chrome - Mohamed Nihal
            options.Add(new ProfileOption
            {
                Icon = chromeIcon,
                Title = "Google Chrome",
                Subtitle = "Mohamed Nihal \u2022 Personal",
                ExePath = chromePath,
                Arguments = "\"" + targetUrl + "\" \"--profile-directory=Default\" --no-default-browser-check"
            });
        }

        private void BuildUI()
        {
            Grid root = new Grid { Margin = new Thickness(16) };

            Border card = new Border
            {
                Background = CardBg,
                BorderBrush = CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(24, 20, 24, 18),
                Effect = new DropShadowEffect
                {
                    Color = System.Windows.Media.Colors.Black,
                    BlurRadius = 24,
                    ShadowDepth = 4,
                    Opacity = 0.60
                }
            };

            // Red Close Button [X] to cancel / close window
            Border closeBtn = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(4),
                Background = System.Windows.Media.Brushes.Transparent,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8, -4, -6, 0),
                ToolTip = "Close / Cancel"
            };

            // ENABLE WINDOW MOVING/DRAGGING ANYWHERE IN WINDOWS
            card.MouseLeftButtonDown += (s, e) =>
            {
                // Drag move if clicking card background, header, or non-interactive space
                DependencyObject d = e.OriginalSource as DependencyObject;
                bool isInteractive = false;
                while (d != null && d != card)
                {
                    if (d == closeBtn)
                    {
                        isInteractive = true;
                        break;
                    }

                    FrameworkElement fe = d as FrameworkElement;
                    if (fe != null)
                    {
                        if (fe.Cursor == Cursors.Hand || fe is Button || itemContainers.Contains(fe as Border))
                        {
                            isInteractive = true;
                            break;
                        }
                    }
                    d = VisualTreeHelper.GetParent(d);
                }

                if (!isInteractive && e.LeftButton == MouseButtonState.Pressed)
                {
                    try { DragMove(); } catch { }
                }
            };

            StackPanel panel = new StackPanel();

            string protocol = "'link'";
            try
            {
                if (targetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) protocol = "'https' link";
                else if (targetUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) protocol = "'http' link";
            }
            catch { }

            // Header Grid with Title on Left, Close Button [X] on Right
            Grid headerGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 14)
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Title & Subtitle Stack (Draggable)
            StackPanel titleStack = new StackPanel { Cursor = Cursors.SizeAll };

            TextBlock title = new TextBlock
            {
                Text = "Select a profile to open this " + protocol,
                FontSize = 18.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextPrimary,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Display, Segoe UI, sans-serif"),
                Margin = new Thickness(0, 0, 0, 4)
            };
            titleStack.Children.Add(title);

            TextBlock subtitle = new TextBlock
            {
                Text = "Suggested profiles",
                FontSize = 13,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCF, 0xCF, 0xCF)),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI, sans-serif")
            };
            titleStack.Children.Add(subtitle);

            titleStack.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    try { DragMove(); } catch { }
                }
            };

            Grid.SetColumn(titleStack, 0);
            headerGrid.Children.Add(titleStack);

            // Configure Close Button [X] glyph and events
            TextBlock closeGlyph = new TextBlock
            {
                Text = "\u2715",
                FontSize = 12,
                FontWeight = FontWeights.Normal,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xBB, 0xBB, 0xBB)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI, sans-serif"),
                IsHitTestVisible = false
            };
            closeBtn.Child = closeGlyph;

            closeBtn.MouseEnter += (s, e) =>
            {
                closeBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC4, 0x2B, 0x1C)); // Windows 11 red
                closeGlyph.Foreground = System.Windows.Media.Brushes.White;
            };
            closeBtn.MouseLeave += (s, e) =>
            {
                closeBtn.Background = System.Windows.Media.Brushes.Transparent;
                closeGlyph.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xBB, 0xBB, 0xBB));
            };
            closeBtn.PreviewMouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                Close();
            };
            closeBtn.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                Close();
            };
            closeBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                Close();
            };

            Grid.SetColumn(closeBtn, 1);
            headerGrid.Children.Add(closeBtn);

            panel.Children.Add(headerGrid);

            if (settings.ViewMode == "grid")
            {
                panel.Children.Add(BuildGridView());
            }
            else
            {
                panel.Children.Add(BuildListView());
            }

            UIElement privateFooter = BuildPrivateFooter();
            if (privateFooter != null)
            {
                panel.Children.Add(privateFooter);
            }

            card.Child = panel;
            root.Children.Add(card);
            Content = root;
        }

        private UIElement BuildListView()
        {
            StackPanel listPanel = new StackPanel();

            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                ProfileOption opt = options[i];

                Border item = new Border
                {
                    Background = NormalBg,
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 2, 0, 2),
                    Cursor = Cursors.Hand
                };

                Grid grid = new Grid();

                if (settings.ShowProfileIcons && opt.Icon != null)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    System.Windows.Controls.Image img = new System.Windows.Controls.Image
                    {
                        Source = opt.Icon,
                        Width = 30,
                        Height = 30,
                        Margin = new Thickness(0, 0, 14, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(img, 0);
                    grid.Children.Add(img);
                }
                else
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }

                StackPanel textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                TextBlock mainText = new TextBlock
                {
                    Text = opt.Title,
                    FontSize = 14.5,
                    FontWeight = FontWeights.Normal,
                    Foreground = TextPrimary,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI")
                };
                textPanel.Children.Add(mainText);

                TextBlock subText = new TextBlock
                {
                    Text = opt.Subtitle,
                    FontSize = 12,
                    Foreground = TextSecondary,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI"),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                textPanel.Children.Add(subText);

                Grid.SetColumn(textPanel, (settings.ShowProfileIcons && opt.Icon != null) ? 1 : 0);
                grid.Children.Add(textPanel);

                item.Child = grid;

                item.MouseEnter += (s, e) =>
                {
                    selectedIndex = index;
                    UpdateSelection();
                };

                item.MouseLeftButtonDown += (s, e) => e.Handled = true;
                item.MouseLeftButtonUp += (s, e) => ExecuteOption(opt);

                itemContainers.Add(item);
                listPanel.Children.Add(item);
            }

            return listPanel;
        }

        private UIElement BuildGridView()
        {
            UniformGrid grid = new UniformGrid
            {
                Columns = options.Count,
                Margin = new Thickness(0, 0, 0, 4)
            };

            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                ProfileOption opt = options[i];

                Border tile = new Border
                {
                    Background = TileBg,
                    BorderBrush = TileBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, settings.ShowProfileIcons ? 16 : 18, 10, settings.ShowProfileIcons ? 16 : 18),
                    Margin = new Thickness(4),
                    Cursor = Cursors.Hand
                };

                StackPanel stack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                if (settings.ShowProfileIcons && opt.Icon != null)
                {
                    System.Windows.Controls.Image img = new System.Windows.Controls.Image
                    {
                        Source = opt.Icon,
                        Width = 38,
                        Height = 38,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    stack.Children.Add(img);
                }

                string shortTitle = opt.Title;
                string profileLabel = opt.Subtitle;
                if (profileLabel.Contains("•"))
                {
                    profileLabel = profileLabel.Substring(0, profileLabel.IndexOf("•")).Trim();
                }
                if (profileLabel.Contains("("))
                {
                    profileLabel = profileLabel.Substring(0, profileLabel.IndexOf("(")).Trim();
                }

                TextBlock nameText = new TextBlock
                {
                    Text = profileLabel,
                    FontSize = 13.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextPrimary,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI"),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                stack.Children.Add(nameText);

                TextBlock browserText = new TextBlock
                {
                    Text = shortTitle,
                    FontSize = 11.5,
                    Foreground = TextSecondary,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                stack.Children.Add(browserText);

                tile.Child = stack;

                tile.MouseEnter += (s, e) =>
                {
                    selectedIndex = index;
                    UpdateSelection();
                };

                tile.MouseLeftButtonDown += (s, e) => e.Handled = true;
                tile.MouseLeftButtonUp += (s, e) => ExecuteOption(opt);

                itemContainers.Add(tile);
                grid.Children.Add(tile);
            }

            return grid;
        }

        private UIElement BuildPrivateFooter()
        {
            if (!settings.ShowEdgeInPrivate && !settings.ShowChromeIncognito)
                return null;

            WrapPanel footer = new WrapPanel
            {
                Margin = new Thickness(0, 12, 0, 0)
            };

            string edgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
            string chromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";

            if (settings.ShowEdgeInPrivate)
            {
                Border edgeBtn = CreateLinkButton("Open in Edge InPrivate", () =>
                {
                    ExecuteOption(new ProfileOption
                    {
                        ExePath = edgePath,
                        Arguments = "\"" + targetUrl + "\" -inprivate"
                    });
                });
                edgeBtn.Margin = new Thickness(0, 0, 16, 4);
                footer.Children.Add(edgeBtn);
            }

            if (settings.ShowChromeIncognito)
            {
                Border chromeBtn = CreateLinkButton("Open in Chrome Incognito", () =>
                {
                    ExecuteOption(new ProfileOption
                    {
                        ExePath = chromePath,
                        Arguments = "\"" + targetUrl + "\" --incognito"
                    });
                });
                chromeBtn.Margin = new Thickness(0, 0, 0, 4);
                footer.Children.Add(chromeBtn);
            }

            return footer;
        }

        private Border CreateLinkButton(string text, Action onClick)
        {
            Border btn = new Border
            {
                Padding = new Thickness(4, 6, 4, 4),
                Background = NormalBg,
                Cursor = Cursors.Hand
            };

            TextBlock tb = new TextBlock
            {
                Text = text,
                FontSize = 12.5,
                Foreground = LinkColor,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text, Segoe UI"),
                IsHitTestVisible = false
            };

            btn.Child = tb;
            btn.MouseEnter += (s, e) => tb.TextDecorations = TextDecorations.Underline;
            btn.MouseLeave += (s, e) => tb.TextDecorations = null;
            btn.PreviewMouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };
            btn.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };
            btn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };

            return btn;
        }

        private void PickerWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.Down || e.Key == Key.Right)
            {
                if (selectedIndex < options.Count - 1)
                {
                    selectedIndex++;
                    UpdateSelection();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up || e.Key == Key.Left)
            {
                if (selectedIndex > 0)
                {
                    selectedIndex--;
                    UpdateSelection();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (selectedIndex >= 0 && selectedIndex < options.Count)
                {
                    ExecuteOption(options[selectedIndex]);
                }
                e.Handled = true;
            }
        }

        private void UpdateSelection()
        {
            for (int i = 0; i < itemContainers.Count; i++)
            {
                if (settings.ViewMode == "grid")
                {
                    itemContainers[i].Background = (i == selectedIndex)
                        ? HoverBg
                        : TileBg;
                    itemContainers[i].BorderBrush = (i == selectedIndex)
                        ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x60, 0xCD, 0xFF))
                        : TileBorder;
                }
                else
                {
                    itemContainers[i].Background = (i == selectedIndex) ? HoverBg : NormalBg;
                }
            }
        }

        private void ExecuteOption(ProfileOption opt)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = opt.ExePath,
                    Arguments = opt.Arguments,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not launch browser: " + ex.Message);
            }
            finally
            {
                Close();
            }
        }
    }
}
