// ============================================================================
// MailChecker.cs
// メール受信チェックサービス
// ----------------------------------------------------------------------------
// MailKitライブラリを使用して、IMAP/POP3サーバーに接続し、
// 未読メール数を取得する。複数アカウントを並列にチェックできる。
//
// IMAP の場合:
//   受信トレイ(INBOX)を読み取り専用で開き、UNSEEN(未読)メールを
//   検索して件数を取得する。サーバー側で未読管理されるため正確。
//
// POP3 の場合:
//   POP3プロトコルには未読/既読の概念がないため、
//   サーバー上のメッセージ総数を返す。
// ============================================================================

using CheckMail.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Search;

namespace CheckMail.Services;

/// <summary>
/// 1つのアカウントに対するメールチェック結果を保持するクラス。
/// </summary>
public class MailCheckResult
{
    /// <summary>チェック対象のアカウントID</summary>
    public string AccountId { get; set; } = "";

    /// <summary>検出された未読メール数(IMAP: UNSEEN数, POP3: メッセージ総数)</summary>
    public int UnreadCount { get; set; }

    /// <summary>チェック中に発生したエラーメッセージ(正常時はnull)</summary>
    public string? Error { get; set; }
}

/// <summary>
/// IMAP/POP3サーバーに接続してメールの未読数をチェックするサービス。
/// MailKitライブラリを使用し、SSL/TLS接続に対応する。
/// </summary>
public class MailChecker
{
    /// <summary>サーバー接続のタイムアウト時間(ミリ秒)</summary>
    private const int TimeoutMs = 15000;

    /// <summary>
    /// 複数のメールアカウントを並列にチェックし、各アカウントの未読数を返す。
    /// 無効化されているアカウント(IsEnabled=false)はスキップされる。
    /// </summary>
    /// <param name="accounts">チェック対象のメールアカウント一覧</param>
    /// <param name="ct">キャンセルトークン(チェック中断用)</param>
    /// <returns>各アカウントのチェック結果リスト</returns>
    public async Task<List<MailCheckResult>> CheckAllAsync(IEnumerable<MailAccount> accounts, CancellationToken ct = default)
    {
        // 有効なアカウントのみを対象に、並列でチェックを実行する
        var tasks = accounts
            .Where(a => a.IsEnabled)
            .Select(a => CheckAccountAsync(a, ct));

        // 全アカウントのチェック完了を待機する
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    /// <summary>
    /// 1つのメールアカウントをチェックする。
    /// プロトコル(IMAP/POP3)に応じて適切なチェック処理を呼び分ける。
    /// エラー発生時はMailCheckResult.Errorにメッセージを格納する。
    /// </summary>
    /// <param name="account">チェック対象のメールアカウント</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>チェック結果</returns>
    private async Task<MailCheckResult> CheckAccountAsync(MailAccount account, CancellationToken ct)
    {
        try
        {
            // DPAPIで暗号化されたパスワードを復号化する
            var password = SettingsService.DecryptPassword(account.EncryptedPassword);

            // プロトコルに応じたチェック処理を実行する
            int count = account.Protocol == MailProtocol.IMAP
                ? await CheckImapAsync(account, password, ct)
                : await CheckPop3Async(account, password, ct);

            return new MailCheckResult { AccountId = account.Id, UnreadCount = count };
        }
        catch (Exception ex)
        {
            // 接続エラー、認証エラーなどをキャッチしてエラーメッセージを返す
            return new MailCheckResult
            {
                AccountId = account.Id,
                UnreadCount = 0,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// IMAPサーバーに接続して未読メール数を取得する。
    /// 受信トレイを読み取り専用で開き、UNSEEN(未読)のメールを検索する。
    /// </summary>
    /// <param name="account">メールアカウント設定</param>
    /// <param name="password">復号化済みパスワード</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>未読メール数</returns>
    private static async Task<int> CheckImapAsync(MailAccount account, string password, CancellationToken ct)
    {
        using var client = new ImapClient();
        client.Timeout = TimeoutMs;

        // IMAPサーバーに接続する(SSL/TLS設定に従う)
        await client.ConnectAsync(account.Server, account.Port, account.UseSsl, ct);

        // ユーザー名とパスワードで認証する
        await client.AuthenticateAsync(account.UserName, password, ct);

        // 受信トレイ(INBOX)を読み取り専用モードで開く
        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

        // 未読(UNSEEN)メールを検索してUID一覧を取得し、件数を数える
        var uids = await inbox.SearchAsync(SearchQuery.NotSeen, ct);
        int count = uids.Count;

        // サーバーから切断する(QUITコマンドを送信)
        await client.DisconnectAsync(true, ct);
        return count;
    }

    /// <summary>
    /// POP3サーバーに接続してメッセージ数を取得する。
    /// POP3には未読/既読の概念がないため、サーバー上のメッセージ総数を返す。
    /// </summary>
    /// <param name="account">メールアカウント設定</param>
    /// <param name="password">復号化済みパスワード</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>サーバー上のメッセージ総数</returns>
    private static async Task<int> CheckPop3Async(MailAccount account, string password, CancellationToken ct)
    {
        using var client = new Pop3Client();
        client.Timeout = TimeoutMs;

        // POP3サーバーに接続する(SSL/TLS設定に従う)
        await client.ConnectAsync(account.Server, account.Port, account.UseSsl, ct);

        // ユーザー名とパスワードで認証する
        await client.AuthenticateAsync(account.UserName, password, ct);

        // サーバー上のメッセージ総数を取得する
        int count = client.Count;

        // サーバーから切断する
        await client.DisconnectAsync(true, ct);
        return count;
    }
}
