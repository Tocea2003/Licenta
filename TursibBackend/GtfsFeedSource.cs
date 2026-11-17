using System.IO.Compression;

namespace TursibBackend
{
    /// <summary>
    /// De unde vine un feed GTFS: un folder, o arhivă .zip sau un URL (ex. https://tursib.ro/trasee/gtfs, care
    /// redirecționează spre arhiva curentă). Arhivele se dezarhivează într-un folder temporar, șters la final.
    /// </summary>
    public sealed class GtfsFeedSource : IDisposable
    {
        public static readonly string[] RequiredFiles = { "routes.txt", "stops.txt", "trips.txt", "stop_times.txt", "shapes.txt" };
        public static readonly string[] OptionalFiles = { "calendar.txt", "calendar_dates.txt" };
        public const long MaxDownloadBytes = 200L * 1024 * 1024;

        private readonly string? _tempDir;

        /// <summary>Folderul care conține fișierele .txt ale feed-ului.</summary>
        public string Folder { get; }

        private GtfsFeedSource(string folder, string? tempDir)
        {
            Folder = folder;
            _tempDir = tempDir;
        }

        public static async Task<GtfsFeedSource> ResolveAsync(string source, HttpClient? http = null, CancellationToken ct = default)
        {
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var temp = NewTempDir();
                try
                {
                    var zip = Path.Combine(temp, "feed.zip");
                    var client = http ?? new HttpClient();
                    try
                    {
                        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
                        response.EnsureSuccessStatusCode();
                        if (response.Content.Headers.ContentLength > MaxDownloadBytes)
                            throw new InvalidDataException($"Feed-ul are peste {MaxDownloadBytes / 1024 / 1024} MB.");
                        await using var input = await response.Content.ReadAsStreamAsync(ct);
                        await using var output = File.Create(zip);
                        await CopyLimitedAsync(input, output, MaxDownloadBytes, ct);
                    }
                    finally
                    {
                        if (http == null) client.Dispose();
                    }
                    return new GtfsFeedSource(Extract(zip, Path.Combine(temp, "feed")), temp);
                }
                catch
                {
                    TryDelete(temp);
                    throw;
                }
            }

            if (File.Exists(source) && source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var temp = NewTempDir();
                try { return new GtfsFeedSource(Extract(source, Path.Combine(temp, "feed")), temp); }
                catch { TryDelete(temp); throw; }
            }

            if (Directory.Exists(source)) return new GtfsFeedSource(source, null);
            throw new FileNotFoundException($"Nu găsesc feed-ul GTFS: {source} (folder, .zip sau URL http/https).");
        }

        /// <summary>Fișierele obligatorii care lipsesc din feed.</summary>
        public IReadOnlyList<string> MissingFiles() =>
            RequiredFiles.Where(f => !File.Exists(Path.Combine(Folder, f))).ToList();

        /// <summary>Numărul de rânduri (fără antet) din fiecare fișier al feed-ului, pentru verificarea înainte de import.</summary>
        public IReadOnlyDictionary<string, int> RowCounts()
        {
            var counts = new Dictionary<string, int>();
            foreach (var file in RequiredFiles.Concat(OptionalFiles))
            {
                var path = Path.Combine(Folder, file);
                if (File.Exists(path))
                    counts[file] = Math.Max(0, File.ReadLines(path).Count(l => l.Length > 0) - 1);
            }
            return counts;
        }

        /// <summary>
        /// Dezarhivează și întoarce folderul cu fișierele feed-ului (unele arhive le au într-un subfolder).
        /// ExtractToDirectory refuză intrările care ar ieși din folder (zip slip).
        /// </summary>
        private static string Extract(string zip, string destination)
        {
            ZipFile.ExtractToDirectory(zip, destination);
            if (File.Exists(Path.Combine(destination, "routes.txt"))) return destination;
            var nested = Directory.GetFiles(destination, "routes.txt", SearchOption.AllDirectories).FirstOrDefault();
            return nested != null ? Path.GetDirectoryName(nested)! : destination;
        }

        private static async Task CopyLimitedAsync(Stream input, Stream output, long limit, CancellationToken ct)
        {
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                total += read;
                if (total > limit) throw new InvalidDataException($"Feed-ul are peste {limit / 1024 / 1024} MB.");
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }

        private static string NewTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), $"tursib-gtfs-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void TryDelete(string dir)
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        public void Dispose()
        {
            if (_tempDir != null) TryDelete(_tempDir);
        }
    }
}
