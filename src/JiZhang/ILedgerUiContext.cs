using JiZhang.Core;
namespace JiZhang;
internal interface ILedgerUiContext
{
    Configuration Config { get; }
    LedgerEntry[] Entries { get; }
    BalanceSample? Current { get; }
    string Status { get; }
    string DataDirectory { get; }
    IDisposable PushFont(float tier);
    void SaveConfig();
    Task<string> ExportAsync();
    Task<string> BackupAsync();
    void Restore(string path);
}
