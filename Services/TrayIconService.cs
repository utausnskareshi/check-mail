// ============================================================================
// TrayIconService.cs
// システムトレイアイコン管理サービス
// ----------------------------------------------------------------------------
// Windows のシステムトレイ(通知領域)にアイコンを表示し、
// 未読メール数に応じたバッジ(数字入り赤丸)をオーバーレイ表示する。
//
// 主な機能:
//   - 封筒デザインのベースアイコンをプログラムで動的に描画
//   - 未読メール数に応じたバッジ付きアイコンを動的に生成
//   - 右クリックコンテキストメニュー(表示/チェック/設定/終了)
//   - ダブルクリックでメインウィンドウ表示
//   - バルーン通知(Windowsトースト通知)の表示
// ============================================================================

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace CheckMail.Services;

/// <summary>
/// システムトレイ(通知領域)のアイコン管理を行うサービス。
/// IDisposableを実装し、アプリケーション終了時にリソースを解放する。
/// </summary>
public class TrayIconService : IDisposable
{
    /// <summary>WinForms NotifyIcon (システムトレイアイコンの実体)</summary>
    private readonly WinForms.NotifyIcon _notifyIcon;

    /// <summary>未読なし時に表示する基本アイコン(封筒デザイン)</summary>
    private readonly Icon _baseIcon;

    /// <summary>リソースが既に解放済みかどうかのフラグ</summary>
    private bool _disposed;

    // --- イベント: メインウィンドウ側でハンドラを登録して処理を委譲する ---

    /// <summary>メインウィンドウの表示が要求されたときに発火するイベント</summary>
    public event EventHandler? ShowRequested;

    /// <summary>即時メールチェックが要求されたときに発火するイベント</summary>
    public event EventHandler? CheckNowRequested;

    /// <summary>設定画面の表示が要求されたときに発火するイベント</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>アプリケーションの終了が要求されたときに発火するイベント</summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// コンストラクタ。システムトレイにアイコンとコンテキストメニューを設定する。
    /// </summary>
    public TrayIconService()
    {
        // 封筒デザインのベースアイコンを動的に作成する
        _baseIcon = CreateBaseIcon();

        // システムトレイアイコンを作成して表示する
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = _baseIcon,
            Text = "CheckMail - メール受信チェッカー",
            Visible = true
        };

        // 右クリック時に表示するコンテキストメニューを構築する
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("表示", null, (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("今すぐチェック", null, (_, _) => CheckNowRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("設定", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new WinForms.ToolStripSeparator());  // 区切り線
        menu.Items.Add("終了", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon.ContextMenuStrip = menu;

        // ダブルクリックでメインウィンドウを表示する
        _notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 未読メール数に応じてトレイアイコンを更新する。
    /// 未読が0の場合はベースアイコン(封筒のみ)、
    /// 1以上の場合はバッジ(赤丸+数字)付きアイコンを表示する。
    /// </summary>
    /// <param name="unreadCount">未読メールの合計数</param>
    public void UpdateBadge(int unreadCount)
    {
        if (_disposed) return;

        // 現在のアイコンを解放してから新しいアイコンを設定する
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Icon = unreadCount > 0
            ? CreateBadgeIcon(unreadCount)  // バッジ付きアイコンを生成
            : _baseIcon;                    // ベースアイコン(バッジなし)

        // ツールチップテキストも更新する(マウスホバー時に表示)
        _notifyIcon.Text = unreadCount > 0
            ? $"CheckMail - 未読: {unreadCount}通"
            : "CheckMail - 新着なし";
    }

    /// <summary>
    /// Windowsのバルーン通知(トースト通知)を表示する。
    /// 新着メール検出時に使用される。
    /// </summary>
    /// <param name="title">通知のタイトル</param>
    /// <param name="message">通知の本文</param>
    public void ShowBalloon(string title, string message)
    {
        if (_disposed) return;
        // 5000ms(5秒間)バルーン通知を表示する
        _notifyIcon.ShowBalloonTip(5000, title, message, WinForms.ToolTipIcon.Info);
    }

    /// <summary>
    /// 未読なし時に表示するベースアイコン(青い封筒デザイン)を動的に描画する。
    /// 32x32ピクセルのビットマップに封筒の本体とフラップ(蓋)を描画する。
    /// </summary>
    /// <returns>封筒デザインのアイコン</returns>
    private static Icon CreateBaseIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;  // アンチエイリアスを有効化
        g.Clear(Color.Transparent);                   // 背景を透明に設定

        // 封筒の本体部分(青い長方形)を描画
        using var bodyBrush = new SolidBrush(Color.FromArgb(66, 133, 244));
        g.FillRectangle(bodyBrush, 2, 8, 28, 18);

        // 封筒のフラップ(蓋)部分を白い線でV字型に描画
        using var flapPen = new Pen(Color.White, 2f);
        g.DrawLine(flapPen, 2, 8, 16, 20);    // 左上から中央下へ
        g.DrawLine(flapPen, 30, 8, 16, 20);   // 右上から中央下へ

        // 封筒の外枠を描画
        using var borderPen = new Pen(Color.FromArgb(50, 100, 200), 1f);
        g.DrawRectangle(borderPen, 2, 8, 28, 18);

        // ビットマップからアイコンハンドルを生成して返す
        return Icon.FromHandle(bmp.GetHicon());
    }

    /// <summary>
    /// 未読数バッジ付きアイコンを動的に描画する。
    /// 封筒アイコンの右上に赤い丸と白い数字でバッジを表示する。
    /// 100以上の場合は "99+" と表示する。
    /// </summary>
    /// <param name="count">表示する未読メール数</param>
    /// <returns>バッジ付きの封筒アイコン</returns>
    private static Icon CreateBadgeIcon(int count)
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;  // 文字描画品質を向上
        g.Clear(Color.Transparent);

        // 封筒の本体(バッジ用にやや小さくして右上にスペースを確保)
        using var bodyBrush = new SolidBrush(Color.FromArgb(66, 133, 244));
        g.FillRectangle(bodyBrush, 1, 10, 24, 16);

        // 封筒のフラップ(蓋)
        using var flapPen = new Pen(Color.White, 1.5f);
        g.DrawLine(flapPen, 1, 10, 13, 20);
        g.DrawLine(flapPen, 25, 10, 13, 20);

        // バッジの赤い丸(右上に配置)
        using var badgeBrush = new SolidBrush(Color.FromArgb(234, 67, 53));
        g.FillEllipse(badgeBrush, 16, 0, 16, 16);

        // バッジ内の未読数テキスト(100以上は"99+"と表示)
        var text = count > 99 ? "99+" : count.ToString();
        using var font = new Font("Segoe UI", count > 99 ? 5f : 7f, System.Drawing.FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,      // 水平中央揃え
            LineAlignment = StringAlignment.Center    // 垂直中央揃え
        };
        // バッジ円の中央にテキストを描画する
        g.DrawString(text, font, textBrush, new RectangleF(16, 0, 16, 16), sf);

        return Icon.FromHandle(bmp.GetHicon());
    }

    /// <summary>
    /// リソースを解放する。トレイアイコンを非表示にし、関連リソースを破棄する。
    /// アプリケーション終了時に呼び出される。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // トレイアイコンを非表示にしてからリソースを解放する
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _baseIcon.Dispose();
    }
}
