using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RemarkablePaperProClaude
{
    /// <summary>
    /// A small JSON-file <see cref="RemarkableSync.IConfigStore"/> for the console
    /// tool. It persists the reMarkable cloud device/user tokens between runs so the
    /// one-time connect code is only needed once. Values are encrypted at rest with
    /// DPAPI (current user), mirroring the registry store the OneNote add-in uses.
    /// </summary>
    public class FileConfigStore : RemarkableSync.IConfigStore
    {
        private readonly string _path;
        private readonly Dictionary<string, string> _data; // key -> base64(DPAPI(value))

        public FileConfigStore(string path)
        {
            _path = path;
            _data = Load(path);
        }

        public string GetConfig(string key)
        {
            if (!_data.TryGetValue(key, out string stored) || string.IsNullOrEmpty(stored))
                return null;

            try
            {
                byte[] plain = ProtectedData.Unprotect(
                    Convert.FromBase64String(stored), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                // Corrupt or written by a different user — treat as missing.
                return null;
            }
        }

        public bool SetConfigs(Dictionary<string, string> configs)
        {
            try
            {
                foreach (KeyValuePair<string, string> kv in configs)
                {
                    byte[] encrypted = ProtectedData.Protect(
                        Encoding.UTF8.GetBytes(kv.Value ?? string.Empty), null, DataProtectionScope.CurrentUser);
                    _data[kv.Key] = Convert.ToBase64String(encrypted);
                }
                Save();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Dictionary<string, string> Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
                    if (loaded != null)
                        return loaded;
                }
            }
            catch
            {
                // Missing or corrupt config — start fresh.
            }
            return new Dictionary<string, string>();
        }

        private void Save()
        {
            string dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(_path, JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
        }

        public void Dispose()
        {
        }
    }
}
