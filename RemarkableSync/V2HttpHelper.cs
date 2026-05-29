using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace RemarkableSync
{
    class V2HttpHelper
    {
        private static string SyncHost = "https://internal.cloud.remarkable.com";
        private static string RootUrl = SyncHost + "/sync/v4/root";
        private static string BlobUrl = SyncHost + "/sync/v3/files/";
        private const string RmFileNameHeader = "rm-filename";

        private HttpClient _client;

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public V2HttpHelper(HttpClient client)
        {
            _client = client;
        }

        public async Task<BlobStream> GetBlobStreamFromHashAsync(string hash, string rmFilename)
        {
            Logger.Debug($"Entering: hash={hash} rmFilename={rmFilename}");
            try
            {
                if (hash == "root")
                {
                    // v4 root returns JSON {"hash":"...","generation":...}
                    HttpResponseMessage rootResponse = await _client.GetAsync(RootUrl);
                    if (!rootResponse.IsSuccessStatusCode)
                    {
                        throw new Exception($"Root request failed with status code {rootResponse.StatusCode}");
                    }
                    BlobRootResponse rootJson = await HttpContentJsonExtensions.ReadFromJsonAsync<BlobRootResponse>(rootResponse.Content);
                    return new BlobStream
                    {
                        Blob = rootJson.hash,
                        Generation = rootJson.generation
                    };
                }

                var request = new HttpRequestMessage(HttpMethod.Get, BlobUrl + hash);
                request.Headers.Add(RmFileNameHeader, rmFilename);
                HttpResponseMessage response = await _client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Blob request failed with status code {response.StatusCode}");
                }

                return new BlobStream
                {
                    Blob = await response.Content.ReadAsStringAsync(),
                    Generation = 0
                };
            }
            catch (Exception err)
            {
                Logger.Error($"Failed to GET hash: {hash}. err: {err.ToString()} ");
                throw;
            }
        }

        public async Task<Stream> GetStreamFromHashAsync(string hash, string rmFilename)
        {
            Logger.Debug($"Entering: hash={hash} rmFilename={rmFilename}");
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, BlobUrl + hash);
                request.Headers.Add(RmFileNameHeader, rmFilename);
                HttpResponseMessage response = await _client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Blob stream request failed with status code {response.StatusCode}");
                }
                return await response.Content.ReadAsStreamAsync();
            }
            catch (Exception err)
            {
                Logger.Error($"Failed to GET stream for hash: {hash}. err: {err.ToString()} ");
                return null;
            }
        }
    }

    class BlobStream
    {
        public string Blob { get; set; }
        public long Generation { get; set; }
    }

    class BlobRootResponse
    {
        public string hash { get; set; }
        public long generation { get; set; }
        public long schemaVersion { get; set; }
    }
}
