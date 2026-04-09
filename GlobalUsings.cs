// ============================================================================
// GlobalUsings.cs
// グローバルusing定義ファイル
// ----------------------------------------------------------------------------
// WPF (System.Windows) と WinForms (System.Windows.Forms) の両方を使用する
// プロジェクトでは、Application や MessageBox などのクラス名が衝突するため、
// グローバルusingエイリアスで WPF 側を優先的に使用するよう明示的に指定する。
// ============================================================================

global using Application = System.Windows.Application;
global using MessageBox = System.Windows.MessageBox;
global using MessageBoxButton = System.Windows.MessageBoxButton;
global using MessageBoxImage = System.Windows.MessageBoxImage;
global using MessageBoxResult = System.Windows.MessageBoxResult;
