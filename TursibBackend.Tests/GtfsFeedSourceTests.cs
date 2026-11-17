using System.IO.Compression;
using System.Net;
using Xunit;

namespace TursibBackend.Tests;

public class GtfsFeedSourceTests : IDisposable
{
    private readonly string _work = Path.Combine(Path.GetTempPath(), $"gtfs-test-{Guid.NewGuid():N}");

    public GtfsFeedSourceTests() => Directory.CreateDirectory(_work);
    public void Dispose() { try { Directory.Delete(_work, true); } catch (IOException) { } }

    private string Feed(string name, bool complete = true, string subfolder = "")
    {
        var dir = Path.Combine(_work, name, subfolder);
        Directory.CreateDirectory(dir);
        foreach (var file in GtfsFeedSource.RequiredFiles.Where(f => complete || f != "shapes.txt"))
            File.WriteAllText(Path.Combine(dir, file), "header\nrow1\nrow2\n");
        File.WriteAllText(Path.Combine(dir, "calendar.txt"), "header\nrow\n");
        return Path.Combine(_work, name);
    }

    private string Zip(string folder)
    {
        var zip = folder + ".zip";
        ZipFile.CreateFromDirectory(folder, zip);
        return zip;
    }

    [Fact]
    public async Task AFolder_IsUsedAsIs_AndNotDeleted()
    {
        var folder = Feed("plain");
        using (var feed = await GtfsFeedSource.ResolveAsync(folder))
        {
            Assert.Equal(folder, feed.Folder);
            Assert.Empty(feed.MissingFiles());
            Assert.Equal(2, feed.RowCounts()["routes.txt"]);
            Assert.Equal(1, feed.RowCounts()["calendar.txt"]);
        }
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public async Task AZip_IsExtracted_EvenWhenTheFilesAreInASubfolder_AndCleanedUp()
    {
        var zip = Zip(Feed("nested", subfolder: "tursib.gtfs_2026"));
        string folder;
        using (var feed = await GtfsFeedSource.ResolveAsync(zip))
        {
            folder = feed.Folder;
            Assert.True(File.Exists(Path.Combine(folder, "routes.txt")));
            Assert.Empty(feed.MissingFiles());
        }
        Assert.False(Directory.Exists(folder));                     // arhiva temporară a fost ștearsă
    }

    private sealed class ZipHandler(byte[] body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? Requested { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requested = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
        }
    }

    [Fact]
    public async Task AUrl_IsDownloadedAndExtracted()
    {
        var handler = new ZipHandler(File.ReadAllBytes(Zip(Feed("remote"))));
        using var feed = await GtfsFeedSource.ResolveAsync("https://tursib.ro/trasee/gtfs", new HttpClient(handler));

        Assert.Equal("https://tursib.ro/trasee/gtfs", handler.Requested!.ToString());
        Assert.Empty(feed.MissingFiles());
    }

    [Fact]
    public async Task AFailedDownload_Throws()
    {
        var handler = new ZipHandler(Array.Empty<byte>(), HttpStatusCode.NotFound);
        await Assert.ThrowsAsync<HttpRequestException>(() => GtfsFeedSource.ResolveAsync("https://example.com/gtfs.zip", new HttpClient(handler)));
    }

    [Fact]
    public async Task MissingRequiredFiles_AreReported()
    {
        using var feed = await GtfsFeedSource.ResolveAsync(Feed("incomplete", complete: false));
        Assert.Equal(new[] { "shapes.txt" }, feed.MissingFiles());
    }

    [Fact]
    public async Task AnUnknownSource_Throws() =>
        await Assert.ThrowsAsync<FileNotFoundException>(() => GtfsFeedSource.ResolveAsync(Path.Combine(_work, "nope")));
}
