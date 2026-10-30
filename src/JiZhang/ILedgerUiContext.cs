using JiZhang.Core;
namespace JiZhang;
internal interface ILedgerUiContext
{
    Configuration Config { get; }
    LedgerEntry[] Entries { get; }
    BalanceSample? Current { get; }
    string Status { get; }
    string DataDirectory { get; }
    void SaveConfig();
    Task<string> ExportAsync();
    Task<string> BackupAsync();
    void Restore(string path);
}
