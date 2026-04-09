// ============================================================================
// SettingsService.cs
// 設定の読み書きサービス
// ----------------------------------------------------------------------------
// アプリケーション設定(AppSettings)のJSON永続化と、
// パスワードのDPAPI暗号化/復号化、Windows自動起動レジストリ管理を担当する。
//
// 設定ファイルの保存先:
//   %APPDATA%\CheckMail\settings.json
//
// パスワードの暗号化:
//   Windows DPAPI (Data Protection API) を使用し、現在のWindowsユーザーの
//   資格情報でパスワードを暗号化する。暗号化されたデータはBase64文字列として
//   JSONに保存される。同じWindowsユーザーでのみ復号化可能。
// ============================================================================

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CheckMail.Models;
using Microsoft.Win32;

namespace CheckMail.Services;

/// <summary>
/// アプリケーション設定の読み書き、パスワード暗号化、自動起動管理を行うサービス。
/// </summary>
public class SettingsService
{
    /// <summary>設定ファイルの保存ディレクトリ(%APPDATA%\CheckMail)</summary>
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CheckMail");

    /// <summary>設定ファイルのフルパス</summary>
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    /// <summary>JSONシリアライズ時のオプション(整形出力を有効化)</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>Windows自動起動用レジストリキーのパス</summary>
    private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>レジストリに登録するアプリケーション名</summary>
    private const string AppName = "CheckMail";

    /// <summary>
    /// 設定ファイルからアプリケーション設定を読み込む。
    /// ファイルが存在しない場合はデフォルト設定を返す。
    /// </summary>
    /// <returns>読み込んだアプリケーション設定</returns>
    public AppSettings Load()
    {
        // 設定ファイルが存在しない場合(初回起動時など)はデフォルト設定を返す
        if (!File.Exists(SettingsPath))
            return new AppSettings();

        var json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
    }

    /// <summary>
    /// アプリケーション設定をJSONファイルに保存する。
    /// 同時にWindows自動起動レジストリも更新する。
    /// </summary>
    /// <param name="settings">保存するアプリケーション設定</param>
    public void Save(AppSettings settings)
    {
        // 保存先ディレクトリが存在しない場合は作成する
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);

        // Windows起動時の自動起動設定をレジストリに反映する
        UpdateStartupRegistry(settings.StartWithWindows);
    }

    /// <summary>
    /// パスワードをWindows DPAPIで暗号化し、Base64文字列として返す。
    /// 暗号化は現在のWindowsユーザーのスコープで行われるため、
    /// 同じユーザーアカウントでのみ復号化が可能。
    /// </summary>
    /// <param name="password">暗号化する平文パスワード</param>
    /// <returns>Base64エンコードされた暗号化パスワード</returns>
    public static string EncryptPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) return "";

        // パスワード文字列をUTF-8バイト配列に変換
        var bytes = Encoding.UTF8.GetBytes(password);

        // DPAPIで暗号化(CurrentUserスコープ: このWindowsユーザーのみ復号可能)
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);

        // 暗号化バイト配列をBase64文字列に変換して返す
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// DPAPIで暗号化されたBase64文字列を復号化し、平文パスワードを返す。
    /// 復号化に失敗した場合(別ユーザーのデータなど)は空文字列を返す。
    /// </summary>
    /// <param name="encrypted">Base64エンコードされた暗号化パスワード</param>
    /// <returns>復号化された平文パスワード</returns>
    public static string DecryptPassword(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return "";
        try
        {
            // Base64文字列を暗号化バイト配列に戻す
            var bytes = Convert.FromBase64String(encrypted);

            // DPAPIで復号化
            var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);

            // バイト配列をUTF-8文字列に変換して返す
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            // 復号化失敗時(データ破損、別ユーザーのデータなど)は空文字列を返す
            return "";
        }
    }

    /// <summary>
    /// Windowsの自動起動レジストリ(HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run)
    /// を更新する。有効にすると、Windows起動時にアプリが自動的に起動する。
    /// </summary>
    /// <param name="enable">true: 自動起動を有効化、false: 自動起動を無効化</param>
    private static void UpdateStartupRegistry(bool enable)
    {
        // レジストリキーを書き込み可能モードで開く
        using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
        if (key == null) return;

        if (enable)
        {
            // 現在の実行ファイルパスをレジストリに登録する
            var exePath = Environment.ProcessPath;
            if (exePath != null)
                key.SetValue(AppName, $"\"{exePath}\"");
        }
        else
        {
            // レジストリからエントリを削除する(存在しなくてもエラーにしない)
            key.DeleteValue(AppName, false);
        }
    }
}
