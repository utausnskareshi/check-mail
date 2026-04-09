// ============================================================================
// AppSettings.cs
// アプリケーション全体の設定モデル
// ----------------------------------------------------------------------------
// メールアカウント一覧、チェック間隔、自動起動設定など、
// アプリケーション全体の設定を保持する。
// JSONファイルとしてシリアライズ/デシリアライズされる。
// ============================================================================

namespace CheckMail.Models;

/// <summary>
/// アプリケーション全体の設定を保持するモデルクラス。
/// SettingsService によりJSONファイルに永続化される。
/// </summary>
public class AppSettings
{
    /// <summary>登録されたメールアカウントの一覧</summary>
    public List<MailAccount> Accounts { get; set; } = new();

    /// <summary>メールを自動チェックする間隔(分単位、最小1分)</summary>
    public int CheckIntervalMinutes { get; set; } = 5;

    /// <summary>Windows起動時にアプリを自動起動するかどうか</summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>新着メール検出時にWindowsの通知(バルーン)を表示するかどうか</summary>
    public bool ShowNotification { get; set; } = true;
}
