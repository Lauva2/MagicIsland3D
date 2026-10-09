using System;
using System.IO;
using System.Text;

namespace MagicIsland.Saving
{
    // Atomic replacement keeps the last committed file intact if a new write fails.
    public sealed class SaveFileStore
    {
        public string FilePath { get; }
        public string BackupPath => FilePath + ".bak";
        public SaveFileStore(string directory) { FilePath = Path.Combine(directory, "save.json"); }
        public void Write(string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string temporary = FilePath + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, BackupPath);
                else File.Move(temporary, FilePath);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        public bool TryRead<T>(Func<string, T> parseAndValidate, out T data, out string error, out bool recovered)
        {
            data = default;
            error = null;
            recovered = false;
            foreach (string path in new[] { FilePath, BackupPath })
            {
                if (!File.Exists(path)) continue;
                try
                {
                    data = parseAndValidate(File.ReadAllText(path, Encoding.UTF8));
                    recovered = path == BackupPath;
                    return true;
                }
                catch (Exception exception) { error = exception.Message; }
            }
            return false;
        }
    }
}
