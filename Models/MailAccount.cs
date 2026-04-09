// ============================================================================
// MailAccount.cs
// メールアカウント設定モデル
// ----------------------------------------------------------------------------
// 1つのメールアカウントに関する接続情報と、実行時の状態(未読数など)を保持する。
// 接続情報はJSONファイルに永続化され、実行時の状態は [JsonIgnore] により
// シリアライズ対象外となる。
// ============================================================================

using System.Text.Json.Serialization;

namespace CheckMail.Models;

/// <summary>
/// メール受信に使用するプロトコルの種別。
/// IMAP は未読検索(UNSEEN)が可能、POP3 はメッセージ総数のみ取得可能。
/// </summary>
public enum MailProtocol
{
    IMAP,
    POP3
}

/// <summary>
/// メールアカウントの設定情報と実行時ステータスを保持するモデルクラス。
/// </summary>
public class MailAccount
{
    /// <summary>アカウントを一意に識別するID(GUID形式)</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>画面表示用のアカウント名(例: "会社メール", "個人Gmail")</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>メールアドレス(例: user@example.com)</summary>
    public string EmailAddress { get; set; } = "";

    /// <summary>メールサーバーのホスト名(例: imap.gmail.com)</summary>
    public string Server { get; set; } = "";

    /// <summary>メールサーバーのポート番号(IMAP SSL: 993, POP3 SSL: 995)</summary>
    public int Port { get; set; } = 993;

    /// <summary>使用するプロトコル(IMAP または POP3)</summary>
    public MailProtocol Protocol { get; set; } = MailProtocol.IMAP;

    /// <summary>SSL/TLS接続を使用するかどうか</summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>認証に使用するユーザー名</summary>
    public string UserName { get; set; } = "";

    /// <summary>Windows DPAPIで暗号化されたパスワード(Base64エンコード済み)</summary>
    public string EncryptedPassword { get; set; } = "";

    /// <summary>このアカウントのメールチェックを有効にするかどうか</summary>
    public bool IsEnabled { get; set; } = true;

    // --- 以下は実行時のみ使用する一時的な状態(JSONには保存しない) ---

    /// <summary>最後のチェックで検出された未読メール数</summary>
    [JsonIgnore]
    public int UnreadCount { get; set; }

    /// <summary>最後のチェックで発生したエラーメッセージ(正常時はnull)</summary>
    [JsonIgnore]
    public string? LastError { get; set; }

    /// <summary>最後にメールチェックを実行した日時</summary>
    [JsonIgnore]
    public DateTime? LastChecked { get; set; }
}
