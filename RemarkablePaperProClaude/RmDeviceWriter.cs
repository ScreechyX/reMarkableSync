using System;
using System.Globalization;
using System.IO;
using System.Text;
using Renci.SshNet;

namespace RemarkablePaperProClaude
{
    /// <summary>
    /// Writes a PDF document into the reMarkable's document store over SSH/SFTP and
    /// restarts the UI process so the new document appears.
    ///
    /// NOTE: the on-device "xochitl" document format evolves between firmware
    /// releases. The metadata/content shape below follows the long-standing
    /// community convention and is intentionally minimal (xochitl derives page
    /// geometry from the PDF itself). If a future Paper Pro firmware rejects it,
    /// the generated PDF is also always saved locally so you can copy it across
    /// by USB instead — see the README.
    /// </summary>
    public class RmDeviceWriter : IDisposable
    {
        private const string XochitlPath = "/home/root/.local/share/remarkable/xochitl";

        private readonly SftpClient _sftp;
        private readonly SshClient _ssh;

        public RmDeviceWriter(string host, string password)
        {
            _sftp = new SftpClient(host, 22, "root", password);
            _ssh = new SshClient(host, 22, "root", password);
            _sftp.Connect();
            _ssh.Connect();
        }

        /// <summary>
        /// Uploads a PDF as a new document under <paramref name="parentId"/>
        /// (empty string = top level) and returns the new document id.
        /// </summary>
        public string UploadPdfDocument(byte[] pdf, string visibleName, string parentId)
        {
            string id = Guid.NewGuid().ToString();
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            using (var pdfStream = new MemoryStream(pdf))
                _sftp.UploadFile(pdfStream, $"{XochitlPath}/{id}.pdf");

            UploadText(BuildMetadata(visibleName, parentId ?? string.Empty, nowMs),
                $"{XochitlPath}/{id}.metadata");
            UploadText(BuildContent(), $"{XochitlPath}/{id}.content");
            // Older firmware expects a (possibly empty) pagedata file; harmless on newer.
            UploadText(string.Empty, $"{XochitlPath}/{id}.pagedata");

            return id;
        }

        /// <summary>
        /// Restarts the reMarkable UI so it re-scans the document store.
        /// </summary>
        public void RestartUi()
        {
            using (var cmd = _ssh.RunCommand("systemctl restart xochitl"))
            {
                if (cmd.ExitStatus != 0 && !string.IsNullOrWhiteSpace(cmd.Error))
                    throw new Exception($"Failed to restart xochitl: {cmd.Error.Trim()}");
            }
        }

        private void UploadText(string text, string remotePath)
        {
            using (var s = new MemoryStream(Encoding.UTF8.GetBytes(text)))
                _sftp.UploadFile(s, remotePath);
        }

        private static string BuildMetadata(string visibleName, string parentId, long nowMs)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("    \"deleted\": false,\n");
            sb.Append($"    \"lastModified\": \"{nowMs.ToString(CultureInfo.InvariantCulture)}\",\n");
            sb.Append("    \"lastOpened\": \"\",\n");
            sb.Append("    \"lastOpenedPage\": 0,\n");
            sb.Append("    \"metadatamodified\": true,\n");
            sb.Append("    \"modified\": true,\n");
            sb.Append($"    \"parent\": \"{JsonEscape(parentId)}\",\n");
            sb.Append("    \"pinned\": false,\n");
            sb.Append("    \"synced\": false,\n");
            sb.Append("    \"type\": \"DocumentType\",\n");
            sb.Append("    \"version\": 0,\n");
            sb.Append($"    \"visibleName\": \"{JsonEscape(visibleName)}\"\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        private static string BuildContent()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("    \"coverPageNumber\": 0,\n");
            sb.Append("    \"documentMetadata\": {},\n");
            sb.Append("    \"dummyDocument\": false,\n");
            sb.Append("    \"extraMetadata\": {},\n");
            sb.Append("    \"fileType\": \"pdf\",\n");
            sb.Append("    \"fontName\": \"\",\n");
            sb.Append("    \"formatVersion\": 1,\n");
            sb.Append("    \"lineHeight\": -1,\n");
            sb.Append("    \"margins\": 125,\n");
            sb.Append("    \"orientation\": \"portrait\",\n");
            sb.Append("    \"pageCount\": 0,\n");
            sb.Append("    \"sizeInBytes\": \"0\",\n");
            sb.Append("    \"textAlignment\": \"left\",\n");
            sb.Append("    \"textScale\": 1\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        private static string JsonEscape(string s)
        {
            if (s == null) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public void Dispose()
        {
            try { _sftp?.Disconnect(); } catch { }
            try { _ssh?.Disconnect(); } catch { }
            _sftp?.Dispose();
            _ssh?.Dispose();
        }
    }
}
