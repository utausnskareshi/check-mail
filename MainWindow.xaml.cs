// ============================================================================
// MainWindow.xaml.cs
// メインウィンドウのコードビハインド
// ----------------------------------------------------------------------------
// アプリケーションのメインウィンドウ。以下の役割を担う:
//   - システムトレイへの常駐(起動時はウィンドウを非表示にしてトレイのみ表示)
//   - 定期的なメールチェックタイマーの管理
//   - 各アカウントの未読メール数の一覧表示
//   - 設定画面・トレイアイコンとの連携
//
// ウィンドウの動作:
//   - 起動時: 非表示(トレイ常駐)で即座にメールチェックを実行
//   - 閉じるボタン(×): ウィンドウを非表示にする(トレイに常駐し続ける)
//   - 最小化: ウィンドウを非表示にする
//   - 終了: トレイメニューの「終了」からのみ完全終了
// ============================================================================

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WpfColor = System.Windows.Media.Color;
using CheckMail.Models;
using CheckMail.Services;
using CheckMail.Views;

namespace CheckMail;

/// <summary>
/// アプリケーションのメインウィンドウ。
/// システムトレイ常駐、メールチェックタイマー、未読一覧表示を管理する。
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>設定の読み書きを行うサービス</summary>
    private readonly SettingsService _settingsService;

    /// <summary>メールサーバーへの接続と未読チェックを行うサービス</summary>
    private readonly MailChecker _mailChecker;

    /// <summary>システムトレイアイコンを管理するサービス</summary>
    private readonly TrayIconService _trayIcon;

    /// <summary>現在のアプリケーション設定</summary>
    private AppSettings _settings;

    /// <summary>定期メールチェック用のタイマー</summary>
    private System.Windows.Threading.DispatcherTimer? _timer;

    /// <summary>メールチェック処理のキャンセル用トークンソース</summary>
    private CancellationTokenSource? _checkCts;

    /// <summary>
    /// 強制終了フラグ。
    /// true の場合はウィンドウの Close イベントで実際に閉じる(通常はトレイに隠すだけ)。
    /// </summary>
    private bool _forceClose;

    /// <summary>
    /// コンストラクタ。各サービスの初期化、トレイアイコンの設定、
    /// タイマーの開始、初回メールチェックを行う。
    /// </summary>
    public MainWindow()
    {
        // 各サービスを初期化する
        _settingsService = new SettingsService();
        _mailChecker = new MailChecker();
        _settings = _settingsService.Load();    // 保存済み設定を読み込む
        _trayIcon = new TrayIconService();

        InitializeComponent();

        // トレイアイコンのイベントハンドラを登録する
        _trayIcon.ShowRequested += (_, _) => ShowWindow();                        // 「表示」メニュー
        _trayIcon.CheckNowRequested += async (_, _) => await CheckMailAsync();    // 「今すぐチェック」メニュー
        _trayIcon.SettingsRequested += (_, _) => Dispatcher.Invoke(OpenSettings); // 「設定」メニュー
        _trayIcon.ExitRequested += (_, _) => Dispatcher.Invoke(ForceClose);       // 「終了」メニュー

        // 定期チェックタイマーを開始する
        StartTimer();

        // アカウント一覧を画面に表示する
        RefreshAccountList();

        // 起動時はウィンドウを非表示にしてトレイのみ表示する
        Hide();

        // 初回のメールチェックを即座に実行する(非同期、結果を待たない)
        _ = CheckMailAsync();
    }

    /// <summary>
    /// 定期メールチェック用のタイマーを開始(または再開)する。
    /// 設定画面で間隔が変更された場合にも呼び出される。
    /// </summary>
    private void StartTimer()
    {
        // 既存のタイマーがあれば停止する
        _timer?.Stop();

        // 新しいタイマーを作成して開始する(最小間隔は1分)
        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(Math.Max(1, _settings.CheckIntervalMinutes))
        };
        _timer.Tick += async (_, _) => await CheckMailAsync();
        _timer.Start();
    }

    /// <summary>
    /// 全ての有効なメールアカウントの未読メール数をチェックする。
    /// チェック結果に応じてトレイアイコンのバッジ更新、画面更新、通知表示を行う。
    /// </summary>
    private async Task CheckMailAsync()
    {
        // 前回のチェックが実行中であればキャンセルする
        _checkCts?.Cancel();
        _checkCts = new CancellationTokenSource();

        // ステータスバーに「チェック中」と表示する
        Dispatcher.Invoke(() => StatusText.Text = "チェック中...");

        try
        {
            // 全アカウントを並列でチェックする
            var results = await _mailChecker.CheckAllAsync(_settings.Accounts, _checkCts.Token);

            // 通知判定用: チェック前の未読合計数を保持する
            int previousTotal = _settings.Accounts.Where(a => a.IsEnabled).Sum(a => a.UnreadCount);

            // チェック結果を各アカウントモデルに反映する
            foreach (var result in results)
            {
                var account = _settings.Accounts.FirstOrDefault(a => a.Id == result.AccountId);
                if (account == null) continue;
                account.UnreadCount = result.UnreadCount;
                account.LastError = result.Error;
                account.LastChecked = DateTime.Now;
            }

            // 全アカウントの未読合計数を計算する
            int totalUnread = _settings.Accounts.Where(a => a.IsEnabled).Sum(a => a.UnreadCount);

            // UIスレッドで画面とトレイアイコンを更新する
            Dispatcher.Invoke(() =>
            {
                // トレイアイコンのバッジ(未読数)を更新する
                _trayIcon.UpdateBadge(totalUnread);

                // アカウント一覧を再描画する
                RefreshAccountList();

                // ステータスバーに最終チェック時刻と未読合計を表示する
                StatusText.Text = $"最終チェック: {DateTime.Now:HH:mm:ss} | 未読合計: {totalUnread}通";

                // 新着メールが増えた場合にバルーン通知を表示する
                if (_settings.ShowNotification && totalUnread > previousTotal)
                {
                    // 未読のあるアカウント名と件数を一覧化する
                    var newAccounts = _settings.Accounts
                        .Where(a => a.IsEnabled && a.UnreadCount > 0 && a.LastError == null)
                        .Select(a => $"{a.DisplayName}: {a.UnreadCount}通");
                    _trayIcon.ShowBalloon("新着メール", string.Join("\n", newAccounts));
                }
            });
        }
        catch (OperationCanceledException)
        {
            // キャンセルされた場合は何もしない(次のチェックに委ねる)
        }
        catch (Exception ex)
        {
            // 予期しないエラーが発生した場合はステータスバーに表示する
            Dispatcher.Invoke(() => StatusText.Text = $"エラー: {ex.Message}");
        }
    }

    /// <summary>
    /// アカウント一覧の表示を最新状態に更新する。
    /// 有効なアカウントのみを表示対象とする。
    /// </summary>
    private void RefreshAccountList()
    {
        AccountListView.ItemsSource = null;
        AccountListView.ItemsSource = _settings.Accounts.Where(a => a.IsEnabled).ToList();
    }

    /// <summary>
    /// メインウィンドウを表示してフォアグラウンドに移動する。
    /// トレイアイコンのダブルクリックや「表示」メニューから呼び出される。
    /// </summary>
    private void ShowWindow()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();  // ウィンドウをフォアグラウンドに移動する
        });
    }

    /// <summary>
    /// 設定画面をモーダルダイアログとして表示する。
    /// 設定が保存された場合は、設定を再読み込みしてタイマーとチェックを再開する。
    /// </summary>
    private void OpenSettings()
    {
        var window = new SettingsWindow(_settingsService, _settings);
        window.ShowDialog();  // モーダル表示(設定画面が閉じるまで待機)

        if (window.Saved)
        {
            // 設定が変更された場合: 設定を再読み込みし、タイマーとチェックを再開する
            _settings = _settingsService.Load();
            StartTimer();
            RefreshAccountList();
            _ = CheckMailAsync();  // 設定変更後すぐにチェックを実行する
        }
    }

    /// <summary>
    /// アプリケーションを完全に終了する。
    /// トレイアイコンの「終了」メニューから呼び出される。
    /// リソースの解放、タイマー停止、チェックのキャンセルを行う。
    /// </summary>
    private void ForceClose()
    {
        _forceClose = true;       // 強制終了フラグを設定
        _trayIcon.Dispose();      // トレイアイコンを解放する
        _timer?.Stop();           // タイマーを停止する
        _checkCts?.Cancel();      // 実行中のチェックをキャンセルする
        Application.Current.Shutdown();  // アプリケーションを終了する
    }

    /// <summary>「今すぐチェック」ボタンのクリックイベント</summary>
    private void CheckNow_Click(object sender, RoutedEventArgs e) => _ = CheckMailAsync();

    /// <summary>「設定」ボタンのクリックイベント</summary>
    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    /// <summary>
    /// ウィンドウの閉じるボタン(×)が押されたときのイベントハンドラ。
    /// 通常はウィンドウを非表示にしてトレイに隠す(アプリは終了しない)。
    /// ForceClose() で _forceClose=true の場合のみ実際に閉じる。
    /// </summary>
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_forceClose)
        {
            e.Cancel = true;  // ウィンドウのクローズをキャンセルする
            Hide();           // ウィンドウを非表示にする(トレイに常駐)
        }
    }

    /// <summary>
    /// ウィンドウの状態が変化したときのイベントハンドラ。
    /// 最小化されたときにウィンドウを非表示にしてトレイに隠す。
    /// </summary>
    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            Hide();
    }
}

// ============================================================================
// 以下は XAML のデータバインディングで使用する値コンバーター
// ============================================================================

/// <summary>
/// null値をVisibility.Collapsedに変換するコンバーター。
/// エラーメッセージの表示/非表示制御に使用する。
/// null → Collapsed(非表示), 非null → Visible(表示)
/// </summary>
public class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value == null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 未読数を背景色に変換するコンバーター。
/// 未読バッジの色制御に使用する。
/// 1以上 → 赤(#EA4335)、0 → グレー(#969696)
/// </summary>
public class CountToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value as int? ?? 0;
        return count > 0
            ? new SolidColorBrush(WpfColor.FromRgb(234, 67, 53))    // 赤色(未読あり)
            : new SolidColorBrush(WpfColor.FromRgb(150, 150, 150));  // グレー(未読なし)
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
