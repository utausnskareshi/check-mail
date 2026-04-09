// ============================================================================
// ViewModelBase.cs
// MVVM パターンの基盤クラス
// ----------------------------------------------------------------------------
// WPF の MVVM (Model-View-ViewModel) パターンで使用する
// ViewModelの基底クラスとコマンドクラスを提供する。
//
// ViewModelBase:
//   INotifyPropertyChanged を実装し、プロパティ変更時に
//   UI への自動通知を行う仕組みを提供する。
//
// RelayCommand:
//   ICommand を実装し、ViewModel から View のボタンクリック等の
//   アクションをバインディング経由で処理できるようにする。
// ============================================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CheckMail.ViewModels;

/// <summary>
/// ViewModelの基底クラス。INotifyPropertyChanged を実装し、
/// プロパティ変更時にUIへ自動通知する機能を提供する。
/// </summary>
public class ViewModelBase : INotifyPropertyChanged
{
    /// <summary>プロパティ値が変更されたときに発火するイベント</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// プロパティ変更通知を発火する。
    /// [CallerMemberName] により、呼び出し元のプロパティ名が自動的に設定される。
    /// </summary>
    /// <param name="name">変更されたプロパティ名(自動設定)</param>
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// プロパティ値を更新し、値が変化した場合のみ変更通知を発火する。
    /// 値が同じ場合は何もせず false を返す。
    /// </summary>
    /// <typeparam name="T">プロパティの型</typeparam>
    /// <param name="field">バッキングフィールドへの参照</param>
    /// <param name="value">新しい値</param>
    /// <param name="name">プロパティ名(自動設定)</param>
    /// <returns>値が変更された場合は true、変更なしの場合は false</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

/// <summary>
/// WPF の ICommand インターフェースを実装する汎用コマンドクラス。
/// デリゲートを受け取り、ボタンクリック等のアクションを ViewModel で処理できるようにする。
/// </summary>
public class RelayCommand : ICommand
{
    /// <summary>コマンド実行時に呼び出されるデリゲート</summary>
    private readonly Action<object?> _execute;

    /// <summary>コマンドが実行可能かどうかを判定するデリゲート(nullの場合は常に実行可能)</summary>
    private readonly Func<object?, bool>? _canExecute;

    /// <summary>
    /// パラメータ付きのコンストラクタ。
    /// </summary>
    /// <param name="execute">コマンド実行時の処理</param>
    /// <param name="canExecute">実行可能判定(省略可能)</param>
    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>
    /// パラメータなしのコンストラクタ。
    /// パラメータなしの Action/Func を受け取り、内部でラップする。
    /// </summary>
    /// <param name="execute">コマンド実行時の処理</param>
    /// <param name="canExecute">実行可能判定(省略可能)</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
        : this(_ => execute(), canExecute != null ? _ => canExecute() : null) { }

    /// <summary>
    /// コマンドの実行可能状態が変化したときに発火するイベント。
    /// WPF の CommandManager.RequerySuggested に委譲することで、
    /// UIの状態が自動的に更新される。
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>コマンドが現在実行可能かどうかを返す</summary>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <summary>コマンドを実行する</summary>
    public void Execute(object? parameter) => _execute(parameter);
}
