// ============================================================================
// SettingsWindow.xaml.cs
// 設定画面のコードビハインド
// ----------------------------------------------------------------------------
// メールアカウントの追加・編集・削除、接続テスト、
// チェック間隔や自動起動などの一般設定を管理する画面。
//
// 画面構成:
//   左側: アカウント一覧リスト(＋/－ボタンで追加/削除)
//   右側: 選択中アカウントの詳細設定フォーム
//   下部: 一般設定(チェック間隔、自動起動、通知)
//
// 操作フロー:
//   1. アカウントを左側リストから選択 → 右側に詳細が表示される
//   2. 各項目を編集し「保存」ボタンで一時保存
//   3. 「接続テスト」で実際にサーバーへ接続確認
//   4. 「全て保存して閉じる」で設定ファイルに書き込み
// ============================================================================

using System.Windows;
using System.Windows.Controls;
using CheckMail.Models;
using CheckMail.Services;

namespace CheckMail.Views;

/// <summary>
/// メールアカウントと一般設定を管理する設定画面。
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>設定の読み書きを行うサービス</summary>
    private readonly SettingsService _settingsService;

    /// <summary>現在編集中のアプリケーション設定</summary>
    private AppSettings _settings;

    /// <summary>現在選択中(編集中)のメールアカウント</summary>
    private MailAccount? _currentAccount;

    /// <summary>編集中のアプリケーション設定を外部から参照するプロパティ</summary>
    public AppSettings Settings => _settings;

    /// <summary>設定が保存されて閉じられたかどうか(true: 保存済み)</summary>
    public bool Saved { get; private set; }

    /// <summary>
    /// コンストラクタ。設定サービスと現在の設定を受け取り、画面を初期化する。
    /// </summary>
    /// <param name="settingsService">設定の読み書きサービス</param>
    /// <param name="settings">現在のアプリケーション設定</param>
    public SettingsWindow(SettingsService settingsService, AppSettings settings)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _settings = settings;

        // 設定値を画面に反映する
        LoadSettings();
    }

    /// <summary>
    /// 設定値を画面の各コントロールに反映する。
    /// アカウント一覧と一般設定を画面に表示する。
    /// </summary>
    private void LoadSettings()
    {
        // アカウント一覧をリストボックスに表示する
        AccountList.ItemsSource = null;
        AccountList.ItemsSource = _settings.Accounts;

        // 一般設定を各コントロールに反映する
        TxtInterval.Text = _settings.CheckIntervalMinutes.ToString();
        ChkStartWithWindows.IsChecked = _settings.StartWithWindows;
        ChkShowNotification.IsChecked = _settings.ShowNotification;

        // アカウントが存在する場合は最初のアカウントを選択する
        if (_settings.Accounts.Count > 0)
            AccountList.SelectedIndex = 0;
        else
            ClearDetailPanel();
    }

    /// <summary>
    /// 詳細パネルの全入力フィールドをクリアする。
    /// アカウントが1つもない場合に呼び出される。
    /// </summary>
    private void ClearDetailPanel()
    {
        _currentAccount = null;
        TxtDisplayName.Text = "";
        TxtEmail.Text = "";
        TxtServer.Text = "";
        TxtPort.Text = "";
        CmbProtocol.SelectedIndex = 0;   // デフォルト: IMAP
        ChkSsl.IsChecked = true;          // デフォルト: SSL有効
        TxtUserName.Text = "";
        TxtPassword.Password = "";
        ChkEnabled.IsChecked = true;      // デフォルト: 有効
    }

    /// <summary>
    /// 指定されたアカウントの設定値を詳細パネルに読み込んで表示する。
    /// パスワードはDPAPIで復号化して表示する。
    /// </summary>
    /// <param name="account">表示するメールアカウント</param>
    private void LoadAccount(MailAccount account)
    {
        _currentAccount = account;
        TxtDisplayName.Text = account.DisplayName;
        TxtEmail.Text = account.EmailAddress;
        TxtServer.Text = account.Server;
        TxtPort.Text = account.Port.ToString();
        CmbProtocol.SelectedIndex = account.Protocol == MailProtocol.IMAP ? 0 : 1;
        ChkSsl.IsChecked = account.UseSsl;
        TxtUserName.Text = account.UserName;
        TxtPassword.Password = SettingsService.DecryptPassword(account.EncryptedPassword);
        ChkEnabled.IsChecked = account.IsEnabled;
    }

    /// <summary>
    /// 詳細パネルの入力値を現在選択中のアカウントモデルに反映する。
    /// パスワードはDPAPIで暗号化してからモデルに格納する。
    /// </summary>
    private void SaveCurrentAccountToModel()
    {
        if (_currentAccount == null) return;
        _currentAccount.DisplayName = TxtDisplayName.Text;
        _currentAccount.EmailAddress = TxtEmail.Text;
        _currentAccount.Server = TxtServer.Text;

        // ポート番号の数値変換(変換失敗時はデフォルト993を使用)
        _currentAccount.Port = int.TryParse(TxtPort.Text, out var p) ? p : 993;
        _currentAccount.Protocol = CmbProtocol.SelectedIndex == 0 ? MailProtocol.IMAP : MailProtocol.POP3;
        _currentAccount.UseSsl = ChkSsl.IsChecked == true;
        _currentAccount.UserName = TxtUserName.Text;

        // パスワードをDPAPIで暗号化して保存する
        _currentAccount.EncryptedPassword = SettingsService.EncryptPassword(TxtPassword.Password);
        _currentAccount.IsEnabled = ChkEnabled.IsChecked == true;
    }

    /// <summary>
    /// アカウント一覧の選択が変更されたときのイベントハンドラ。
    /// 選択されたアカウントの設定を詳細パネルに表示する。
    /// </summary>
    private void AccountList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AccountList.SelectedItem is MailAccount account)
            LoadAccount(account);
    }

    /// <summary>
    /// プロトコル(IMAP/POP3)の選択が変更されたときのイベントハンドラ。
    /// プロトコルとSSL設定に応じて、デフォルトのポート番号を自動設定する。
    ///   IMAP + SSL → 993, IMAP → 143
    ///   POP3 + SSL → 995, POP3 → 110
    /// </summary>
    private void CmbProtocol_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TxtPort == null) return;  // 初期化中はスキップ
        bool isSsl = ChkSsl?.IsChecked == true;
        if (CmbProtocol.SelectedIndex == 0) // IMAP
            TxtPort.Text = isSsl ? "993" : "143";
        else // POP3
            TxtPort.Text = isSsl ? "995" : "110";
    }

    /// <summary>
    /// 「＋」ボタンのクリックイベントハンドラ。
    /// 新しいメールアカウントをデフォルト設定で追加し、一覧で選択状態にする。
    /// </summary>
    private void AddAccount_Click(object sender, RoutedEventArgs e)
    {
        var account = new MailAccount
        {
            DisplayName = "新規アカウント",
            Port = 993,
            Protocol = MailProtocol.IMAP,
            UseSsl = true,
            IsEnabled = true
        };
        _settings.Accounts.Add(account);

        // 一覧を更新して新しいアカウントを選択する
        AccountList.ItemsSource = null;
        AccountList.ItemsSource = _settings.Accounts;
        AccountList.SelectedItem = account;
    }

    /// <summary>
    /// 「－」ボタンのクリックイベントハンドラ。
    /// 確認ダイアログを表示してから、選択中のアカウントを削除する。
    /// </summary>
    private void RemoveAccount_Click(object sender, RoutedEventArgs e)
    {
        if (_currentAccount == null) return;

        // 削除確認ダイアログを表示する
        var result = MessageBox.Show(
            $"アカウント「{_currentAccount.DisplayName}」を削除しますか？",
            "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        // アカウントを削除して一覧を更新する
        _settings.Accounts.Remove(_currentAccount);
        AccountList.ItemsSource = null;
        AccountList.ItemsSource = _settings.Accounts;

        // 残りのアカウントがあれば最初のものを選択、なければクリアする
        if (_settings.Accounts.Count > 0)
            AccountList.SelectedIndex = 0;
        else
            ClearDetailPanel();
    }

    /// <summary>
    /// 「保存」ボタンのクリックイベントハンドラ。
    /// 現在の入力内容をアカウントモデルに一時保存する(ファイルには書き込まない)。
    /// </summary>
    private void SaveAccount_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentAccountToModel();

        // 一覧の表示名を更新するために再バインドする
        AccountList.ItemsSource = null;
        AccountList.ItemsSource = _settings.Accounts;
        AccountList.SelectedItem = _currentAccount;
        MessageBox.Show("アカウント設定を保存しました。", "CheckMail", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// 「接続テスト」ボタンのクリックイベントハンドラ。
    /// 入力されたサーバー情報で実際にメールサーバーへ接続を試行し、
    /// 成功時は未読数を表示、失敗時はエラーメッセージを表示する。
    /// </summary>
    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        // 入力チェック: サーバーとユーザー名は必須
        if (string.IsNullOrEmpty(TxtServer.Text) || string.IsNullOrEmpty(TxtUserName.Text))
        {
            MessageBox.Show("サーバーとユーザー名を入力してください。", "CheckMail", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 現在の入力内容をモデルに反映してからテストを実行する
        SaveCurrentAccountToModel();
        if (_currentAccount == null) return;

        // メールチェッカーで接続テストを実行する
        var checker = new MailChecker();
        var results = await checker.CheckAllAsync(new[] { _currentAccount });
        var result = results.FirstOrDefault();

        // テスト結果をダイアログで表示する
        if (result?.Error != null)
            MessageBox.Show($"接続エラー:\n{result.Error}", "接続テスト", MessageBoxButton.OK, MessageBoxImage.Error);
        else
            MessageBox.Show($"接続成功！ 未読メール: {result?.UnreadCount ?? 0}通", "接続テスト", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// 「全て保存して閉じる」ボタンのクリックイベントハンドラ。
    /// 全ての設定をファイルに書き込み、画面を閉じる。
    /// </summary>
    private void SaveAll_Click(object sender, RoutedEventArgs e)
    {
        // 現在編集中のアカウント設定をモデルに反映する
        SaveCurrentAccountToModel();

        // 一般設定を反映する(チェック間隔は最小1分)
        _settings.CheckIntervalMinutes = int.TryParse(TxtInterval.Text, out var interval) && interval >= 1 ? interval : 5;
        _settings.StartWithWindows = ChkStartWithWindows.IsChecked == true;
        _settings.ShowNotification = ChkShowNotification.IsChecked == true;

        // 設定をJSONファイルに保存する
        _settingsService.Save(_settings);
        Saved = true;  // 保存完了フラグを設定する
        Close();
    }

    /// <summary>
    /// 「キャンセル」ボタンのクリックイベントハンドラ。
    /// 変更を破棄して画面を閉じる。
    /// </summary>
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
