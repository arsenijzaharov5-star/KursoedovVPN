using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using ServiceLib.Handler;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using v2rayN.Manager;

namespace v2rayN.Views;

public partial class MainWindow
{
    private sealed class UiProfileOption
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private static Config _config;
    private CheckUpdateView? _checkUpdateView;
    private BackupAndRestoreView? _backupAndRestoreView;
    private readonly DispatcherTimer _connTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _moveDebounceTimer = new() { Interval = TimeSpan.FromMilliseconds(220) };
    private DateTime? _connectedAt;
    private bool _isConnectedUi;
    private bool _videoPausedForMove;

    public MainWindow()
    {
        InitializeComponent();

        _connTimer.Tick += (_, _) =>
        {
            if (_connectedAt == null)
            {
                txtConnTimer.Text = "00:00:00";
                txtConnSpeed.Text = "Скорость (↓/↑): 0 B/s / 0 B/s";
                return;
            }
            var span = DateTime.Now - _connectedAt.Value;
            txtConnTimer.Text = span.ToString(@"hh\:mm\:ss");
            var sp = StatusBarViewModel.Instance.SpeedProxyDisplay;
            txtConnSpeed.Text = $"Скорость (↓/↑): {(sp.IsNullOrEmpty() ? "0 B/s / 0 B/s" : sp)}";
        };

        _config = AppManager.Instance.Config;
        ThreadPool.RegisterWaitForSingleObject(App.ProgramStarted, OnProgramStarted, null, -1, false);

        App.Current.SessionEnding += Current_SessionEnding;
        Closing += MainWindow_Closing;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        menuSettingsSetUWP.Click += MenuSettingsSetUWP_Click;
        menuPromotion.Click += MenuPromotion_Click;
        menuClose.Click += MenuClose_Click;
        menuCheckUpdate.Click += MenuCheckUpdate_Click;
        menuBackupAndRestore.Click += MenuBackupAndRestore_Click;
        Loaded += MainWindow_Loaded;
        LocationChanged += MainWindow_LocationOrSizeChanged;
        SizeChanged += MainWindow_LocationOrSizeChanged;
        StateChanged += MainWindow_StateChanged;
        _moveDebounceTimer.Tick += MoveDebounceTimer_Tick;

        ViewModel = new MainWindowViewModel(UpdateViewHandler);

        switch (_config.UiItem.MainGirdOrientation)
        {
            case EGirdOrientation.Horizontal:
                tabProfiles.Content ??= new ProfilesView();
                tabMsgView.Content ??= new MsgView();
                tabClashProxies.Content ??= new ClashProxiesView();
                tabClashConnections.Content ??= new ClashConnectionsView();
                gridMain.Visibility = Visibility.Visible;
                break;

            case EGirdOrientation.Vertical:
                tabProfiles1.Content ??= new ProfilesView();
                tabMsgView1.Content ??= new MsgView();
                tabClashProxies1.Content ??= new ClashProxiesView();
                tabClashConnections1.Content ??= new ClashConnectionsView();
                gridMain1.Visibility = Visibility.Visible;
                break;

            case EGirdOrientation.Tab:
            default:
                tabProfiles2.Content ??= new ProfilesView();
                tabMsgView2.Content ??= new MsgView();
                tabClashProxies2.Content ??= new ClashProxiesView();
                tabClashConnections2.Content ??= new ClashConnectionsView();
                gridMain2.Visibility = Visibility.Visible;
                break;
        }
        pbTheme.Content ??= new ThemeSettingView();

        this.WhenActivated(disposables =>
        {
            //servers
            this.BindCommand(ViewModel, vm => vm.AddVmessServerCmd, v => v.menuAddVmessServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddVlessServerCmd, v => v.menuAddVlessServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddShadowsocksServerCmd, v => v.menuAddShadowsocksServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddSocksServerCmd, v => v.menuAddSocksServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddHttpServerCmd, v => v.menuAddHttpServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddTrojanServerCmd, v => v.menuAddTrojanServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddHysteria2ServerCmd, v => v.menuAddHysteria2Server).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddTuicServerCmd, v => v.menuAddTuicServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddWireguardServerCmd, v => v.menuAddWireguardServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddAnytlsServerCmd, v => v.menuAddAnytlsServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddCustomServerCmd, v => v.menuAddCustomServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddPolicyGroupServerCmd, v => v.menuAddPolicyGroupServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddProxyChainServerCmd, v => v.menuAddProxyChainServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddServerViaClipboardCmd, v => v.menuAddServerViaClipboard).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddServerViaScanCmd, v => v.menuAddServerViaScan).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddServerViaImageCmd, v => v.menuAddServerViaImage).DisposeWith(disposables);

            //sub
            this.BindCommand(ViewModel, vm => vm.SubSettingCmd, v => v.menuSubSetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubUpdateCmd, v => v.menuSubUpdate).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubUpdateViaProxyCmd, v => v.menuSubUpdateViaProxy).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubGroupUpdateCmd, v => v.menuSubGroupUpdate).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubGroupUpdateViaProxyCmd, v => v.menuSubGroupUpdateViaProxy).DisposeWith(disposables);

            //setting
            this.BindCommand(ViewModel, vm => vm.OptionSettingCmd, v => v.menuOptionSetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingSettingCmd, v => v.menuRoutingSetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.DNSSettingCmd, v => v.menuDNSSetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.FullConfigTemplateCmd, v => v.menuFullConfigTemplate).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.GlobalHotkeySettingCmd, v => v.menuGlobalHotkeySetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RebootAsAdminCmd, v => v.menuRebootAsAdmin).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ClearServerStatisticsCmd, v => v.menuClearServerStatistics).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenTheFileLocationCmd, v => v.menuOpenTheFileLocation).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RegionalPresetDefaultCmd, v => v.menuRegionalPresetsDefault).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RegionalPresetRussiaCmd, v => v.menuRegionalPresetsRussia).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RegionalPresetIranCmd, v => v.menuRegionalPresetsIran).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.ReloadCmd, v => v.menuReload).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.BlReloadEnabled, v => v.menuReload.IsEnabled).DisposeWith(disposables);

            switch (_config.UiItem.MainGirdOrientation)
            {
                case EGirdOrientation.Horizontal:
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabMsgView.Visibility).DisposeWith(disposables);
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies.Visibility).DisposeWith(disposables);
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections.Visibility).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain.SelectedIndex).DisposeWith(disposables);
                    break;

                case EGirdOrientation.Vertical:
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabMsgView1.Visibility).DisposeWith(disposables);
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies1.Visibility).DisposeWith(disposables);
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections1.Visibility).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain1.SelectedIndex).DisposeWith(disposables);
                    break;

                case EGirdOrientation.Tab:
                default:
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies2.Visibility).DisposeWith(disposables);
                    this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections2.Visibility).DisposeWith(disposables);
                    this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain2.SelectedIndex).DisposeWith(disposables);
                    break;
            }

            AppEvents.SendSnackMsgRequested
              .AsObservable()
              .ObserveOn(RxSchedulers.MainThreadScheduler)
              .Subscribe(async content => await DelegateSnackMsg(content))
              .DisposeWith(disposables);

            AppEvents.AppExitRequested
              .AsObservable()
              .ObserveOn(RxSchedulers.MainThreadScheduler)
              .Subscribe(_ => StorageUI())
              .DisposeWith(disposables);

            AppEvents.ShutdownRequested
             .AsObservable()
             .ObserveOn(RxSchedulers.MainThreadScheduler)
             .Subscribe(content => Shutdown(content))
             .DisposeWith(disposables);

            AppEvents.ShowHideWindowRequested
             .AsObservable()
             .ObserveOn(RxSchedulers.MainThreadScheduler)
             .Subscribe(blShow => ShowHideWindow(blShow))
             .DisposeWith(disposables);
        });

        Title = $"{Utils.GetVersion()} - {(Utils.IsAdministrator() ? ResUI.RunAsAdmin : ResUI.NotRunAsAdmin)}";
        if (_config.UiItem.AutoHideStartup)
        {
            WindowState = WindowState.Minimized;
        }

        if (!_config.GuiItem.EnableHWA)
        {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        }

        AddHelpMenuItem();
        WindowsManager.Instance.RegisterGlobalHotkey(_config, OnHotkeyHandler, null);

        _ = LoadProfilesToUiAsync();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var videoPath = EnsureBackgroundVideo();
            if (!videoPath.IsNullOrEmpty())
            {
                bgVideo.Source = new Uri(videoPath!, UriKind.Absolute);
                bgVideo.Position = TimeSpan.Zero;
                bgVideo.Play();
            }
        }
        catch
        {
            // ignore background video errors
        }
    }

    private string? EnsureBackgroundVideo()
    {
        var baseDir = AppContext.BaseDirectory;
        var backgroundsDir = Path.Combine(baseDir, "Resources", "Backgrounds");
        var outputVideo = Path.Combine(backgroundsDir, "WALLPAPERS.mp4");

        if (File.Exists(outputVideo))
        {
            return outputVideo;
        }

        var part00 = Path.Combine(backgroundsDir, "WALLPAPERS.mp4.part-00");
        var part01 = Path.Combine(backgroundsDir, "WALLPAPERS.mp4.part-01");
        if (!File.Exists(part00) || !File.Exists(part01))
        {
            return null;
        }

        using var output = File.Create(outputVideo);
        using (var input0 = File.OpenRead(part00))
        {
            input0.CopyTo(output);
        }
        using (var input1 = File.OpenRead(part01))
        {
            input1.CopyTo(output);
        }

        return outputVideo;
    }

    private void MainWindow_LocationOrSizeChanged(object? sender, EventArgs e)
    {
        try
        {
            if (bgVideo.Source is null)
            {
                return;
            }

            if (!_videoPausedForMove)
            {
                _videoPausedForMove = true;
                bgVideo.Pause();
                bgVideo.Opacity = 0.10;
                bgPoster.Opacity = 0.22;
            }

            _moveDebounceTimer.Stop();
            _moveDebounceTimer.Start();
        }
        catch
        {
            // ignore background video errors
        }
    }

    private void MoveDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _moveDebounceTimer.Stop();

        try
        {
            if (!_videoPausedForMove)
            {
                return;
            }

            _videoPausedForMove = false;
            bgVideo.Opacity = 0.24;
            bgPoster.Opacity = 0.14;
            bgVideo.Play();
        }
        catch
        {
            // ignore background video errors
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        try
        {
            if (WindowState == WindowState.Minimized)
            {
                bgVideo.Pause();
                return;
            }

            if (!_videoPausedForMove && bgVideo.Source is not null)
            {
                bgVideo.Play();
            }
        }
        catch
        {
            // ignore background video errors
        }
    }

    private void BgVideo_MediaOpened(object sender, RoutedEventArgs e)
    {
        try
        {
            bgVideo.Position = TimeSpan.Zero;
            bgVideo.Play();
        }
        catch
        {
            // ignore background video errors
        }
    }

    private void BgVideo_MediaEnded(object sender, RoutedEventArgs e)
    {
        try
        {
            bgVideo.Position = TimeSpan.Zero;
            bgVideo.Play();
        }
        catch
        {
            // ignore background video errors
        }
    }

    #region Event

    private void OnProgramStarted(object state, bool timeout)
    {
        Application.Current?.Dispatcher.Invoke((Action)(() =>
        {
            ShowHideWindow(true);
        }));
    }

    private async Task DelegateSnackMsg(string content)
    {
        MainSnackbar.MessageQueue?.Enqueue(content);
        await Task.CompletedTask;
    }

    private async Task<bool> UpdateViewHandler(EViewAction action, object? obj)
    {
        switch (action)
        {
            case EViewAction.AddServerWindow:
                if (obj is null)
                {
                    return false;
                }

                return new AddServerWindow((ProfileItem)obj).ShowDialog() ?? false;

            case EViewAction.AddServer2Window:
                if (obj is null)
                {
                    return false;
                }

                return new AddServer2Window((ProfileItem)obj).ShowDialog() ?? false;

            case EViewAction.AddGroupServerWindow:
                if (obj is null)
                {
                    return false;
                }

                return new AddGroupServerWindow((ProfileItem)obj).ShowDialog() ?? false;

            case EViewAction.DNSSettingWindow:
                return new DNSSettingWindow().ShowDialog() ?? false;

            case EViewAction.RoutingSettingWindow:
                return new RoutingSettingWindow().ShowDialog() ?? false;

            case EViewAction.OptionSettingWindow:
                return new OptionSettingWindow().ShowDialog() ?? false;

            case EViewAction.FullConfigTemplateWindow:
                return new FullConfigTemplateWindow().ShowDialog() ?? false;

            case EViewAction.GlobalHotkeySettingWindow:
                return new GlobalHotkeySettingWindow().ShowDialog() ?? false;

            case EViewAction.SubSettingWindow:
                return new SubSettingWindow().ShowDialog() ?? false;

            case EViewAction.ScanScreenTask:
                await ScanScreenTaskAsync();
                break;

            case EViewAction.ScanImageTask:
                await ScanImageTaskAsync();
                break;

            case EViewAction.AddServerViaClipboard:
                await AddServerViaClipboardAsync();
                break;
        }

        return await Task.FromResult(true);
    }

    private void OnHotkeyHandler(EGlobalHotkey e)
    {
        switch (e)
        {
            case EGlobalHotkey.ShowForm:
                ShowHideWindow(null);
                break;

            case EGlobalHotkey.SystemProxyClear:
            case EGlobalHotkey.SystemProxySet:
            case EGlobalHotkey.SystemProxyUnchanged:
            case EGlobalHotkey.SystemProxyPac:
                AppEvents.SysProxyChangeRequested.Publish((ESysProxyType)((int)e - 1));
                break;
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        ShowHideWindow(false);
    }

    private async void Current_SessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        Logging.SaveLog("Current_SessionEnding");
        StorageUI();
        await AppManager.Instance.AppExitAsync(false);
    }

    private void Shutdown(bool obj)
    {
        Application.Current.Shutdown();
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            switch (e.Key)
            {
                case Key.V:
                    if (Keyboard.FocusedElement is TextBox)
                    {
                        return;
                    }
                    AddServerViaClipboardAsync().ContinueWith(_ => { });

                    break;

                case Key.S:
                    ScanScreenTaskAsync().ContinueWith(_ => { });
                    break;
            }
        }
        else
        {
            if (e.Key == Key.F5)
            {
                ViewModel?.Reload();
            }
        }
    }

    private void MenuClose_Click(object sender, RoutedEventArgs e)
    {
        StorageUI();
        ShowHideWindow(false);
    }

    private void MenuPromotion_Click(object sender, RoutedEventArgs e)
    {
        ProcUtils.ProcessStart($"{Utils.Base64Decode(Global.PromotionUrl)}?t={DateTime.Now.Ticks}");
    }

    private void MenuSettingsSetUWP_Click(object sender, RoutedEventArgs e)
    {
        ProcUtils.ProcessStart(Utils.GetBinPath("EnableLoopback.exe"));
    }

    public async Task AddServerViaClipboardAsync()
    {
        var clipboardData = WindowsUtils.GetClipboardData();
        if (clipboardData.IsNotEmpty() && ViewModel != null)
        {
            await ViewModel.AddServerViaClipboardAsync(clipboardData);
        }
    }

    private async Task ScanScreenTaskAsync()
    {
        ShowHideWindow(false);

        if (Application.Current?.MainWindow is Window window)
        {
            var bytes = QRCodeWindowsUtils.CaptureScreen(window);
            await ViewModel?.ScanScreenResult(bytes);
        }

        ShowHideWindow(true);
    }

    private async Task ScanImageTaskAsync()
    {
        if (UI.OpenFileDialog(out var fileName, "PNG|*.png|All|*.*") != true)
        {
            return;
        }
        if (fileName.IsNullOrEmpty())
        {
            return;
        }
        await ViewModel?.ScanImageResult(fileName);
    }

    private void MenuCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        _checkUpdateView ??= new CheckUpdateView();
        DialogHost.Show(_checkUpdateView, "RootDialog");
    }

    private void MenuBackupAndRestore_Click(object sender, RoutedEventArgs e)
    {
        _backupAndRestoreView ??= new BackupAndRestoreView();
        DialogHost.Show(_backupAndRestoreView, "RootDialog");
    }


    private static void TriggerMenuClick(MenuItem menu)
    {
        menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }

    private void BtnNavHome_Click(object sender, RoutedEventArgs e)
    {
        // Keep user on simplified home screen
    }

    private void BtnNavProfiles_Click(object sender, RoutedEventArgs e)
    {
        TriggerMenuClick(menuSubSetting);
    }

    private async void BtnNavSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (uiProfileCombo.SelectedValue is not string id || id.IsNullOrEmpty())
            {
                MessageBox.Show("Сначала добавь и выбери профиль.", "kursoedovVPN", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var profiles = await AppManager.Instance.ProfileItems(_config.SubIndexId) ?? [];
            var profile = profiles.FirstOrDefault(p => p.IndexId == id);
            if (profile == null)
            {
                MessageBox.Show("Профиль не найден.", "kursoedovVPN", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _ = new AddServerWindow(profile).ShowDialog();
            await LoadProfilesToUiAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка открытия параметров: {ex.Message}", "kursoedovVPN", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnNavLogs_Click(object sender, RoutedEventArgs e)
    {
        TriggerMenuClick(menuOpenTheFileLocation);
    }

    private void BtnNavAbout_Click(object sender, RoutedEventArgs e)
    {
    }

    private void BtnViewLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logDir = Utils.GetLogPath();
            if (!Directory.Exists(logDir))
            {
                MessageBox.Show("Папка логов пока не создана.", "kursoedovVPN", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var files = Directory.GetFiles(logDir, "*.txt")
                .OrderByDescending(File.GetLastWriteTime)
                .Take(3)
                .ToList();

            if (files.Count == 0)
            {
                MessageBox.Show("Логи пока пустые.", "kursoedovVPN", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            static IEnumerable<string> ReadLastLinesSafe(string path, int maxLines)
            {
                try
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var sr = new StreamReader(fs);
                    var all = new List<string>();
                    while (!sr.EndOfStream)
                    {
                        all.Add(sr.ReadLine() ?? string.Empty);
                    }
                    return all.TakeLast(maxLines);
                }
                catch (Exception ex)
                {
                    return [$"[Не удалось прочитать лог: {ex.Message}]"];
                }
            }

            var parts = new List<string>();
            foreach (var f in files)
            {
                var lines = ReadLastLinesSafe(f, 120);
                parts.Add($"===== {Path.GetFileName(f)} =====\n" + string.Join("\n", lines));
            }

            var dialog = new Window
            {
                Title = "Логи подключения",
                Width = 980,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var root = new DockPanel { Margin = new Thickness(10) };

            var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var btnOpenFolder = new Button { Content = "Открыть папку логов", Width = 180, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
            btnOpenFolder.Click += (_, _) => ProcUtils.ProcessStart(logDir);
            var btnCopy = new Button { Content = "Скопировать", Width = 120, Height = 30 };

            var tb = new TextBox
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                IsReadOnly = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                AcceptsReturn = true,
                AcceptsTab = true,
                Text = string.Join("\n\n", parts)
            };

            btnCopy.Click += (_, _) =>
            {
                Clipboard.SetText(tb.Text ?? string.Empty);
                MessageBox.Show("Логи скопированы в буфер.", "kursoedovVPN", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            top.Children.Add(btnOpenFolder);
            top.Children.Add(btnCopy);
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);
            root.Children.Add(tb);

            dialog.Content = root;
            _ = dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка чтения логов: {ex.Message}", "kursoedovVPN", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (uiProfileCombo.SelectedValue is not string id || id.IsNullOrEmpty())
            {
                MessageBox.Show("Выбери профиль для удаления.", "kursoedovVPN", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var item = await AppManager.Instance.GetProfileItem(id);
            if (item == null)
            {
                MessageBox.Show("Профиль не найден.", "kursoedovVPN", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Удалить профиль '{(item.Remarks.IsNullOrEmpty() ? item.GetSummary() : item.Remarks)}'?",
                "kursoedovVPN",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            await ConfigHandler.RemoveServers(_config, new List<ProfileItem> { item });
            await LoadProfilesToUiAsync();

            var profiles = await AppManager.Instance.ProfileItems(_config.SubIndexId) ?? [];
            if (profiles.Count == 0)
            {
                _config.IndexId = string.Empty;
                await ConfigHandler.SaveConfig(_config);
                SetConnectVisual(false);
            }

            MessageBox.Show("Профиль удалён.", "kursoedovVPN", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка удаления профиля: {ex.Message}", "kursoedovVPN", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnAddKey_Click(object sender, RoutedEventArgs e)
    {
        var cm = new ContextMenu();

        var fromClipboard = new MenuItem() { Header = "Вставить ссылку из буфера" };
        fromClipboard.Click += async (_, _) =>
        {
            try
            {
                var text = WindowsUtils.GetClipboardData();
                if (text.IsNullOrEmpty())
                {
                    MessageBox.Show("Буфер обмена пуст. Скопируй ключ и попробуй снова.", "kursoedovVPN", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (ViewModel != null)
                {
                    var ok = await ImportLinkAndRefreshAsync(text);
                    MessageBox.Show(ok
                            ? "Профиль добавлен. Выбери его в списке профилей."
                            : "Ключ не импортирован. Проверь формат ссылки (trojan://...)",
                        "kursoedovVPN", MessageBoxButton.OK,
                        ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        var manualTrojan = new MenuItem() { Header = "Ввести ключ вручную" };
        manualTrojan.Click += async (_, _) =>
        {
            try
            {
                var input = PromptForKey();
                if (input.IsNullOrEmpty())
                {
                    return;
                }

                if (ViewModel != null)
                {
                    var ok = await ImportLinkAndRefreshAsync(input);
                    MessageBox.Show(ok
                            ? "Профиль добавлен. Выбери его в списке профилей."
                            : "Ключ не импортирован. Проверь формат ссылки (trojan://...)",
                        "kursoedovVPN", MessageBoxButton.OK,
                        ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        cm.Items.Add(fromClipboard);
        cm.Items.Add(manualTrojan);
        cm.PlacementTarget = btnAddKey;
        cm.IsOpen = true;
    }

    private string PromptForKey()
    {
        var dialog = new Window
        {
            Title = "Ввести ключ",
            Width = 560,
            Height = 190,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F6F6F6"))
        };

        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = "Вставь trojan:// или другую ссылку:", Margin = new Thickness(0, 0, 0, 8) });

        var tb = new TextBox { Height = 30 };
        panel.Children.Add(tb);

        var btn = new Button
        {
            Content = "Добавить",
            Margin = new Thickness(0, 12, 0, 0),
            Width = 120,
            Height = 34,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        btn.Click += (_, _) => dialog.DialogResult = true;
        panel.Children.Add(btn);

        dialog.Content = panel;
        var ok = dialog.ShowDialog() == true;
        return ok ? tb.Text.Trim() : string.Empty;
    }

    private async Task LoadProfilesToUiAsync()
    {
        var profiles = await AppManager.Instance.ProfileItems(_config.SubIndexId) ?? [];
        var options = profiles
            .Where(p => !p.IndexId.IsNullOrEmpty())
            .Select(p => new UiProfileOption
            {
                Id = p.IndexId,
                Name = p.Remarks.IsNullOrEmpty() ? p.GetSummary() : p.Remarks
            })
            .ToList();

        uiProfileCombo.ItemsSource = options;
        if (_config.IndexId.IsNotEmpty())
        {
            uiProfileCombo.SelectedValue = _config.IndexId;
        }
        else if (options.Count > 0)
        {
            uiProfileCombo.SelectedIndex = 0;
            _config.IndexId = options[0].Id;
            await ConfigHandler.SaveConfig(_config);
        }
    }

    private async void UiProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (uiProfileCombo.SelectedValue is not string id || id.IsNullOrEmpty())
        {
            return;
        }

        _config.IndexId = id;
        await ConfigHandler.SaveConfig(_config);
        AppEvents.SetDefaultServerRequested.Publish(id);
    }

    private static void ApplyStrictTrojanTemplate(ProfileItem item, string link)
    {
        if (item.ConfigType != EConfigType.Trojan)
        {
            return;
        }

        var url = Utils.TryUri(link);
        var query = url != null ? Utils.ParseQueryString(url.Query) : null;

        // 1) Всегда держим транспорт строго как в рабочем v2box профиле.
        item.CoreType = ECoreType.sing_box;
        item.Network = nameof(ETransport.tcp);
        item.HeaderType = Global.None;
        item.StreamSecurity = Global.StreamSecurity; // tls
        item.AllowInsecure = "false";
        item.Alpn = "h2";
        item.Fingerprint = "chrome";
        item.RequestHost = string.Empty;
        item.Path = string.Empty;

        // 2) Поля из ссылки (источник истины).
        if (url != null)
        {
            item.Address = url.IdnHost;
            item.Port = url.Port;
            item.Password = Utils.UrlDecode(url.UserInfo);

            var remarks = url.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
            if (remarks.IsNotEmpty())
            {
                item.Remarks = remarks;
            }

            var sni = Utils.UrlDecode(query?["sni"] ?? string.Empty);
            item.Sni = sni.IsNotEmpty() ? sni : "vpn.kursoedov.xyz";
        }

        if (item.Sni.IsNullOrEmpty())
        {
            item.Sni = "vpn.kursoedov.xyz";
        }
    }

    private async Task<bool> ImportLinkAndRefreshAsync(string link)
    {
        var beforeProfiles = await AppManager.Instance.ProfileItems(_config.SubIndexId) ?? [];
        var beforeIds = beforeProfiles.Select(x => x.IndexId).Where(x => x.IsNotEmpty()).ToHashSet();

        await ViewModel.AddServerViaClipboardAsync(link.Trim());
        await Task.Delay(300);

        var afterProfiles = await AppManager.Instance.ProfileItems(_config.SubIndexId) ?? [];
        var added = afterProfiles.FirstOrDefault(x => x.IndexId.IsNotEmpty() && !beforeIds.Contains(x.IndexId));

        if (added?.IndexId is { } newId && newId.IsNotEmpty())
        {
            var saved = await AppManager.Instance.GetProfileItem(newId);
            if (saved != null)
            {
                ApplyStrictTrojanTemplate(saved, link.Trim());
                await ConfigHandler.AddServer(_config, saved);
            }
        }

        await LoadProfilesToUiAsync();

        if (added?.IndexId is { } id && id.IsNotEmpty())
        {
            uiProfileCombo.SelectedValue = id;
            _config.IndexId = id;
            await ConfigHandler.SaveConfig(_config);
            AppEvents.SetDefaultServerRequested.Publish(id);
        }

        return added != null;
    }

    private void SetConnectVisual(bool connected)
    {
        _isConnectedUi = connected;
        if (connected)
        {
            btnConnectMain.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B3B3B"));
            btnConnectMain.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B3B3B"));
            txtConnStatus.Text = "Подключено";
            txtConnStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#222222"));
            if (_connectedAt == null)
            {
                _connectedAt = DateTime.Now;
                _connTimer.Start();
            }
        }
        else
        {
            btnConnectMain.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2F2F2F"));
            btnConnectMain.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2F2F2F"));
            txtConnStatus.Text = "Не подключено";
            txtConnStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B6B6B"));
            _connectedAt = null;
            _connTimer.Stop();
            txtConnTimer.Text = "00:00:00";
            txtConnSpeed.Text = "Скорость (↓/↑): 0 B/s / 0 B/s";
        }
    }

    private static bool IsAnyCoreRunning()
    {
        return
            AppManager.Instance.IsRunningCore(ECoreType.Xray)
            || AppManager.Instance.IsRunningCore(ECoreType.sing_box)
            || AppManager.Instance.IsRunningCore(ECoreType.mihomo)
            || AppManager.Instance.IsRunningCore(ECoreType.v2fly)
            || AppManager.Instance.IsRunningCore(ECoreType.v2fly_v5);
    }

    private static async Task<bool> IsPortOpenAsync(int port, int timeoutMs = 1200)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(Global.Loopback, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(timeoutMs));
            return completed == connectTask && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryStartXrayFallback()
    {
        try
        {
            var xrayExe = Utils.GetBinPath(Utils.GetExeName(ECoreType.Xray.ToString()), ECoreType.Xray.ToString());
            var configPath = Utils.GetBinConfigPath(Global.CoreConfigFileName);
            if (!File.Exists(xrayExe) || !File.Exists(configPath))
            {
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = xrayExe,
                Arguments = $"run -c \"{configPath}\"",
                WorkingDirectory = Path.GetDirectoryName(xrayExe) ?? Utils.GetBinPath("", ECoreType.Xray.ToString()),
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            _ = Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsTunnelHealthyAsync()
    {
        try
        {
            var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
            if (port <= 0)
            {
                return false;
            }

            var candidates = new List<string>();
            var cfgUrl = AppManager.Instance.Config.SpeedTestItem.SpeedPingTestUrl;
            if (cfgUrl.IsNotEmpty())
            {
                candidates.Add(cfgUrl);
            }

            candidates.AddRange(
            [
                "https://cp.cloudflare.com/generate_204",
                "https://www.cloudflare.com/cdn-cgi/trace",
                "https://www.gstatic.com/generate_204",
                "https://www.msftconnecttest.com/connecttest.txt"
            ]);

            var proxy = new WebProxy($"socks5://{Global.Loopback}:{port}");
            foreach (var url in candidates.Distinct())
            {
                var ping = await ConnectionHandler.GetRealPingTime(url, proxy, 8);
                if (ping > 0)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private async void BtnConnectMain_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isConnectedUi)
            {
                await CoreManager.Instance.CoreStop();
                AppEvents.SysProxyChangeRequested.Publish(ESysProxyType.ForcedClear);
                SetConnectVisual(false);
                return;
            }

            if (uiProfileCombo.SelectedValue is not string id || id.IsNullOrEmpty())
            {
                MessageBox.Show("Сначала добавь и выбери профиль (trojan://...)", "kursoedovVPN", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AppEvents.SetDefaultServerRequested.Publish(id);

            // Force sing-box for trojan profiles (single-core strategy).
            var selected = await AppManager.Instance.GetProfileItem(id);
            if (selected != null && selected.ConfigType == EConfigType.Trojan && selected.CoreType != ECoreType.sing_box)
            {
                selected.CoreType = ECoreType.sing_box;
                await ConfigHandler.AddServer(_config, selected);
            }

            // TUN-first behavior: make desktop traffic go through VPN without manual proxy setup.
            if (!_config.TunModeItem.EnableTun)
            {
                _config.TunModeItem.EnableTun = true;
                await ConfigHandler.SaveConfig(_config);
            }

            if (ViewModel == null)
            {
                MessageBox.Show("Внутренняя ошибка: ViewModel не инициализирован", "kursoedovVPN", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await ViewModel.Reload();

            var tunnelHealthy = false;
            var coreRunningSeen = false;
            for (var i = 0; i < 5; i++)
            {
                await Task.Delay(700);
                var coreRunning = IsAnyCoreRunning();
                coreRunningSeen = coreRunningSeen || coreRunning;
                if (!coreRunning)
                {
                    continue;
                }

                tunnelHealthy = await IsTunnelHealthyAsync();
                if (tunnelHealthy)
                {
                    break;
                }
            }

            if (!tunnelHealthy)
            {
                var socksPort = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
                if (socksPort > 0 && await IsPortOpenAsync(socksPort))
                {
                    tunnelHealthy = true;
                    coreRunningSeen = true;
                }
                else if (TryStartXrayFallback())
                {
                    await Task.Delay(900);
                    if (socksPort > 0 && await IsPortOpenAsync(socksPort))
                    {
                        tunnelHealthy = true;
                        coreRunningSeen = true;
                    }
                }
            }

            if (tunnelHealthy || coreRunningSeen)
            {
                AppEvents.SysProxyChangeRequested.Publish(ESysProxyType.ForcedChange);
                SetConnectVisual(true);

                if (!tunnelHealthy)
                {
                    MessageBox.Show(
                        "Core запущен, но авто-проверка туннеля не подтвердила доступ к тестовым URL. Проверь IP вручную через 2ip.ru.",
                        "kursoedovVPN",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            else
            {
                await CoreManager.Instance.CoreStop();
                AppEvents.SysProxyChangeRequested.Publish(ESysProxyType.ForcedClear);
                SetConnectVisual(false);
                MessageBox.Show(
                    "Туннель не поднялся: core не запустился. Проверь ключ trojan://, SNI/порт и доступность сервера.",
                    "kursoedovVPN",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            SetConnectVisual(false);
            MessageBox.Show($"Ошибка подключения: {ex.Message}", "kursoedovVPN", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    #endregion Event

    #region UI

    public void ShowHideWindow(bool? blShow)
    {
        var bl = blShow ?? !AppManager.Instance.ShowInTaskbar;
        if (bl)
        {
            this?.Show();
            if (this?.WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            this?.Activate();
            this?.Focus();
        }
        else
        {
            this?.Hide();
        }
        AppManager.Instance.ShowInTaskbar = bl;
    }

    protected override void OnLoaded(object? sender, RoutedEventArgs e)
    {
        base.OnLoaded(sender, e);
        if (_config.UiItem.AutoHideStartup)
        {
            ShowHideWindow(false);
        }
        RestoreUI();
    }

    private void RestoreUI()
    {
        if (_config.UiItem.MainGirdHeight1 > 0 && _config.UiItem.MainGirdHeight2 > 0)
        {
            if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Horizontal)
            {
                gridMain.ColumnDefinitions[0].Width = new GridLength(_config.UiItem.MainGirdHeight1, GridUnitType.Star);
                gridMain.ColumnDefinitions[2].Width = new GridLength(_config.UiItem.MainGirdHeight2, GridUnitType.Star);
            }
            else if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Vertical)
            {
                gridMain1.RowDefinitions[0].Height = new GridLength(_config.UiItem.MainGirdHeight1, GridUnitType.Star);
                gridMain1.RowDefinitions[2].Height = new GridLength(_config.UiItem.MainGirdHeight2, GridUnitType.Star);
            }
        }
    }

    private void StorageUI()
    {
        ConfigHandler.SaveWindowSizeItem(_config, GetType().Name, Width, Height);

        if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Horizontal)
        {
            ConfigHandler.SaveMainGirdHeight(_config, gridMain.ColumnDefinitions[0].ActualWidth, gridMain.ColumnDefinitions[2].ActualWidth);
        }
        else if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Vertical)
        {
            ConfigHandler.SaveMainGirdHeight(_config, gridMain1.RowDefinitions[0].ActualHeight, gridMain1.RowDefinitions[2].ActualHeight);
        }
    }

    private void AddHelpMenuItem()
    {
        var coreInfo = CoreInfoManager.Instance.GetCoreInfo();
        foreach (var it in coreInfo
            .Where(t => t.CoreType is not ECoreType.v2fly
                        and not ECoreType.hysteria))
        {
            var item = new MenuItem()
            {
                Tag = it.Url.Replace(@"/releases", ""),
                Header = string.Format(ResUI.menuWebsiteItem, it.CoreType.ToString().Replace("_", " ")).UpperFirstChar()
            };
            item.Click += MenuItem_Click;
            menuHelp.Items.Add(item);
        }
    }

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item)
        {
            ProcUtils.ProcessStart(item.Tag.ToString());
        }
    }

    #endregion UI
}
