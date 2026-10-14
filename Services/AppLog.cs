using System.Security.Cryptography;
using System.Text;
using BetterCM.Config;
namespace BetterCM.Services;
public static class AppLog
{
    internal const long MaximumBytes = 2 * 1024 * 1024;
    public static void Write(string scope, Exception exception) => WriteAt(ConfigStore.DataDirectory, scope, exception);
    internal static void WriteAt(string dataDirectory, string scope, Exception exception)
    {
        bool acquired = false;
        try
        {
            string directory = Path.GetFullPath(dataDirectory);
            string identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(directory.ToUpperInvariant())))[..16];
            using var gate = new Mutex(false, @"Local\BetterCM.Log." + identity);
            try { acquired = gate.WaitOne(1000); } catch (AbandonedMutexException) { acquired = true; }
            try
            {
                if (!acquired) return;
                Directory.CreateDirectory(directory);
                string line = $"[{DateTime.Now:O}] pid={Environment.ProcessId} {scope}: {exception}\n";
                if (line.Length > 16384) line = line[..16000] + "\n[日志条目过长，已截断]\n";
                string current = Path.Combine(directory, "BetterCM.log");
                if (File.Exists(current) && new FileInfo(current).Length + Encoding.UTF8.GetByteCount(line) > MaximumBytes)
                    File.Move(current, Path.Combine(directory, "BetterCM.prev.log"), true);
                File.AppendAllText(current, line, new UTF8Encoding(false));
            }
            finally { if (acquired) gate.ReleaseMutex(); }
        }
        catch { /* Logging must not disrupt input or native actions. */ }
    }
}
