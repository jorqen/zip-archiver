using System.IO.Compression;
using Xunit;

namespace ZipArchiver.Core.Tests;

public sealed class ArchiveServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ziparchiver-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ArchiveService _service = new();

    public ArchiveServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Временная папка очистится системой.
        }
    }

    [Fact]
    public async Task CreateAndExtract_RoundTrip_PreservesFilesAndStructure()
    {
        byte[] binary = RandomBytes(300_000);
        string rootFile = CreateFile("отчёт.txt", "привет"u8.ToArray());
        CreateFile(Path.Combine("data", "a.txt"), "AAA"u8.ToArray());
        CreateFile(Path.Combine("data", "sub", "b.bin"), binary);
        Directory.CreateDirectory(Path.Combine(_root, "data", "empty"));

        string zipPath = Path.Combine(_root, "result.zip");
        string outDir = Path.Combine(_root, "out");

        await _service.CreateArchiveAsync([rootFile, Path.Combine(_root, "data")], zipPath);
        await _service.ExtractArchiveAsync(zipPath, outDir);

        Assert.Equal("привет"u8.ToArray(), File.ReadAllBytes(Path.Combine(outDir, "отчёт.txt")));
        Assert.Equal("AAA"u8.ToArray(), File.ReadAllBytes(Path.Combine(outDir, "data", "a.txt")));
        Assert.Equal(binary, File.ReadAllBytes(Path.Combine(outDir, "data", "sub", "b.bin")));
        Assert.True(Directory.Exists(Path.Combine(outDir, "data", "empty")), "пустая папка должна сохраниться");
    }

    [Fact]
    public async Task CreateArchive_FilesWithSameName_GetUniqueEntryNames()
    {
        string first = CreateFile(Path.Combine("d1", "a.txt"), "1"u8.ToArray());
        string second = CreateFile(Path.Combine("d2", "a.txt"), "2"u8.ToArray());
        string zipPath = Path.Combine(_root, "dup.zip");

        await _service.CreateArchiveAsync([first, second], zipPath);

        using ZipArchive zip = ZipFile.OpenRead(zipPath);
        Assert.Equal(["a.txt", "a (2).txt"], zip.Entries.Select(e => e.FullName).ToArray());
    }

    [Fact]
    public async Task CreateArchive_ReportsMonotonicProgressEndingAt100()
    {
        CreateFile(Path.Combine("data", "big.bin"), RandomBytes(1_500_000));
        string zipPath = Path.Combine(_root, "progress.zip");

        var reports = new List<ArchiveProgress>();
        await _service.CreateArchiveAsync([Path.Combine(_root, "data")], zipPath,
            progress: new SynchronousProgress<ArchiveProgress>(reports.Add));

        Assert.NotEmpty(reports);
        Assert.Equal(100, reports[^1].Percent);
        for (int i = 1; i < reports.Count; i++)
            Assert.True(reports[i].Percent >= reports[i - 1].Percent, "прогресс не должен убывать");
    }

    [Fact]
    public async Task CreateArchive_WhenCancelled_RemovesPartialArchive()
    {
        CreateFile(Path.Combine("data", "big.bin"), RandomBytes(5_000_000));
        string zipPath = Path.Combine(_root, "cancelled.zip");

        using var cts = new CancellationTokenSource();
        var cancelOnFirstReport = new SynchronousProgress<ArchiveProgress>(_ => cts.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _service.CreateArchiveAsync([Path.Combine(_root, "data")], zipPath,
                progress: cancelOnFirstReport, cancellationToken: cts.Token));

        Assert.False(File.Exists(zipPath), "недописанный архив должен удаляться");
    }

    [Fact]
    public async Task ExtractArchive_EntryEscapingDestination_IsRejected()
    {
        // Архив с записью «../evil.txt» — попытка атаки Zip Slip.
        string zipPath = Path.Combine(_root, "evil.zip");
        using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = zip.CreateEntry("../evil.txt");
            using Stream stream = entry.Open();
            stream.Write("pwned"u8);
        }

        string outDir = Path.Combine(_root, "out");

        await Assert.ThrowsAsync<InvalidDataException>(() => _service.ExtractArchiveAsync(zipPath, outDir));
        Assert.False(File.Exists(Path.Combine(_root, "evil.txt")), "файл не должен записываться вне папки назначения");
    }

    [Fact]
    public async Task ExtractArchive_CorruptedFile_ThrowsInvalidDataException()
    {
        string fakePath = Path.Combine(_root, "fake.zip");
        File.WriteAllBytes(fakePath, "это не zip-файл"u8.ToArray());

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            _service.ExtractArchiveAsync(fakePath, Path.Combine(_root, "out")));
    }

    [Fact]
    public async Task CreateArchive_MissingSource_ThrowsFileNotFound()
    {
        string zipPath = Path.Combine(_root, "missing.zip");

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _service.CreateArchiveAsync([Path.Combine(_root, "нет-такого.txt")], zipPath));

        Assert.False(File.Exists(zipPath));
    }

    // ---------- Вспомогательное ----------

    private string CreateFile(string relativePath, byte[] content)
    {
        string fullPath = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content);
        return fullPath;
    }

    private static byte[] RandomBytes(int count)
    {
        byte[] bytes = new byte[count];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }

    /// <summary>
    /// IProgress без захвата контекста синхронизации: отчёты приходят синхронно
    /// в рабочем потоке, что делает проверки в тестах детерминированными.
    /// </summary>
    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
