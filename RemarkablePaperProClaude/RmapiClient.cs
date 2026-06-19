using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace RemarkablePaperProClaude
{
    /// <summary>
    /// Thin wrapper around the external <c>rmapi</c> binary
    /// (https://github.com/ddvk/rmapi), which talks to the reMarkable cloud and —
    /// unlike the read-only RemarkableSync cloud client — can also upload.
    ///
    /// Authentication is owned by rmapi itself (run <c>rmapi</c> once to link your
    /// account with a one-time connect code); this wrapper just invokes get/put.
    /// </summary>
    public class RmapiClient
    {
        private readonly string _exe;

        public RmapiClient(string rmapiPath)
        {
            _exe = string.IsNullOrWhiteSpace(rmapiPath) ? "rmapi" : rmapiPath;
        }

        /// <summary>
        /// Verifies rmapi is runnable and authenticated. Throws with guidance if not.
        /// </summary>
        public void EnsureReady()
        {
            ProcResult r = Run(new[] { "ls" }, null);
            if (r.ExitCode != 0)
            {
                throw new Exception(
                    "rmapi is not ready. Install it from https://github.com/ddvk/rmapi and run it once to link " +
                    "your reMarkable account with a one-time code from " +
                    "https://my.remarkable.com/device/desktop/connect.\nrmapi said: " + Trunc(r.Error + r.Output));
            }
        }

        /// <summary>
        /// Downloads the notebook at the given rmapi path into <paramref name="workDir"/>,
        /// unpacks it, and returns the folder that contains its {uuid}.content file
        /// (ready for LocalFolderDataSource / RmDocument).
        /// </summary>
        public string DownloadToFolder(string notebookPath, string workDir)
        {
            Directory.CreateDirectory(workDir);

            ProcResult r = Run(new[] { "get", notebookPath }, workDir);
            if (r.ExitCode != 0)
            {
                throw new Exception(
                    $"rmapi could not download \"{notebookPath}\". Check the name/path (use a full path like " +
                    $"\"/Folder/Claude\" for nested notebooks).\nrmapi said: " + Trunc(r.Error + r.Output));
            }

            // rmapi writes a .rmdoc/.zip archive into the working directory.
            string archive = Directory.GetFiles(workDir)
                .Where(IsArchive)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            string searchRoot;
            if (archive != null)
            {
                searchRoot = Path.Combine(workDir, "extracted");
                Directory.CreateDirectory(searchRoot);
                ZipFile.ExtractToDirectory(archive, searchRoot);
            }
            else
            {
                // Some rmapi versions extract in place rather than leaving an archive.
                searchRoot = workDir;
            }

            string contentFile = Directory
                .GetFiles(searchRoot, "*.content", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (contentFile == null)
            {
                throw new Exception(
                    $"The rmapi download for \"{notebookPath}\" did not contain a reMarkable document " +
                    "(no .content file was found). It may be a folder rather than a notebook.");
            }

            return Path.GetDirectoryName(contentFile);
        }

        /// <summary>
        /// Uploads a file (e.g. the answer PDF) into the given cloud folder
        /// ("/" or empty = top level). It will sync to the device.
        /// </summary>
        public void Upload(string filePath, string destDir)
        {
            string[] args = string.IsNullOrWhiteSpace(destDir) || destDir == "/"
                ? new[] { "put", filePath }
                : new[] { "put", filePath, destDir };

            ProcResult r = Run(args, null);
            if (r.ExitCode != 0)
            {
                throw new Exception(
                    $"rmapi could not upload \"{filePath}\" to \"{destDir}\". The destination folder must already " +
                    "exist in your reMarkable cloud.\nrmapi said: " + Trunc(r.Error + r.Output));
            }
        }

        private static bool IsArchive(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".zip" || ext == ".rmdoc" || ext == ".rmn";
        }

        private ProcResult Run(string[] args, string workingDir)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _exe,
                Arguments = BuildArguments(args),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };
            if (!string.IsNullOrEmpty(workingDir))
                psi.WorkingDirectory = workingDir;

            Process proc;
            try
            {
                proc = Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                throw new Exception(
                    $"Could not run rmapi (\"{_exe}\"). Install it from https://github.com/ddvk/rmapi and put it on " +
                    "your PATH, or pass --rmapi <path-to-rmapi>.");
            }

            // Close stdin so any interactive auth prompt gets EOF and fails fast
            // instead of hanging this process forever.
            try { proc.StandardInput.Close(); } catch { /* ignore */ }

            var outTask = proc.StandardOutput.ReadToEndAsync();
            var errTask = proc.StandardError.ReadToEndAsync();
            proc.WaitForExit();

            return new ProcResult
            {
                ExitCode = proc.ExitCode,
                Output = outTask.Result ?? string.Empty,
                Error = errTask.Result ?? string.Empty,
            };
        }

        private static string BuildArguments(string[] args)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(QuoteArg(args[i]));
            }
            return sb.ToString();
        }

        // Standard Windows command-line argument quoting.
        private static string QuoteArg(string arg)
        {
            if (arg.Length > 0 && arg.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
                return arg;

            var sb = new StringBuilder();
            sb.Append('"');
            for (int i = 0; i < arg.Length; i++)
            {
                int backslashes = 0;
                while (i < arg.Length && arg[i] == '\\') { backslashes++; i++; }

                if (i == arg.Length)
                {
                    sb.Append('\\', backslashes * 2);
                    break;
                }
                if (arg[i] == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                }
                else
                {
                    sb.Append('\\', backslashes);
                    sb.Append(arg[i]);
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static string Trunc(string s)
        {
            s = (s ?? string.Empty).Trim();
            return s.Length > 800 ? s.Substring(0, 800) + " ..." : s;
        }

        private class ProcResult
        {
            public int ExitCode;
            public string Output;
            public string Error;
        }
    }
}
