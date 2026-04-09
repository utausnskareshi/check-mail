// ============================================================================
// App.xaml.cs
// アプリケーションのエントリポイント
// ----------------------------------------------------------------------------
// アプリケーション全体のライフサイクル管理を行う。
//
// シングルインスタンス制御:
//   名前付きMutexを使用して、アプリケーションの二重起動を防止する。
//   既に起動中の場合はメッセージを表示して終了する。
//
// ShutdownMode:
//   App.xaml で ShutdownMode="OnExplicitShutdown" を指定しているため、
//   ウィンドウを閉じてもアプリケーションは終了しない。
//   トレイメニューの「終了」から Application.Current.Shutdown() を
//   呼び出した場合のみ終了する。
// ============================================================================

using System.Windows;

namespace CheckMail;

/// <summary>
/// アプリケーションのエントリポイントクラス。
/// 二重起動防止とアプリケーションライフサイクルを管理する。
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 二重起動防止用のMutex。
    /// アプリケーション全体で1つのインスタンスのみ起動を許可する。
    /// </summary>
    private static Mutex? _mutex;

    /// <summary>
    /// アプリケーション起動時に呼び出される。
    /// 名前付きMutexで二重起動を検出し、既に起動中の場合は終了する。
    /// </summary>
    /// <param name="e">起動イベント引数</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        // 名前付きMutexを作成し、二重起動を検出する
        // createdNew が false の場合、別のインスタンスが既にMutexを保持している
        _mutex = new Mutex(true, "CheckMail_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            // 既にアプリケーションが起動中の場合はメッセージを表示して終了する
            MessageBox.Show("CheckMailは既に起動しています。", "CheckMail", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }
}
