using System.IO.Compression;

namespace ZipArchiver.Core;

/// <summary>
/// Сервис упаковки файлов и папок в ZIP-архив и распаковки архивов.
/// Операции выполняются в фоновом потоке, поддерживают отмену через
/// <see cref="CancellationToken"/> и сообщают о ходе работы через <see cref="IProgress{T}"/>.
/// </summary>
public sealed class ArchiveService
{
    private const int CopyBufferSize = 81920;

    /// <summary>
    /// Упаковывает указанные файлы и папки в ZIP-архив.
    /// Файлы попадают в корень архива, папки — целиком, с сохранением структуры подпапок.
    /// </summary>
    /// <param name="sourcePaths">Пути к файлам и папкам, которые нужно упаковать.</param>
    /// <param name="archivePath">Путь к создаваемому ZIP-файлу.</param>
    /// <param name="compressionLevel">Степень сжатия.</param>
    /// <param name="progress">Получатель уведомлений о ходе операции (необязательно).</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    public Task CreateArchiveAsync(
        IReadOnlyCollection<string> sourcePaths,
        string archivePath,
        CompressionLevel compressionLevel = CompressionLevel.Optimal,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        if (sourcePaths.Count == 0)
            throw new ArgumentException("Не выбрано ни одного файла или папки.", nameof(sourcePaths));

        return Task.Run(
            () => CreateArchive(sourcePaths, archivePath, compressionLevel, progress, cancellationToken),
            cancellationToken);
    }

    /// <summary>Распаковывает ZIP-архив в указанную папку с сохранением структуры.</summary>
    /// <param name="archivePath">Путь к существующему ZIP-файлу.</param>
    /// <param name="destinationDirectory">Папка, в которую распаковывается содержимое.</param>
    /// <param name="progress">Получатель уведомлений о ходе операции (необязательно).</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    public Task ExtractArchiveAsync(
        string archivePath,
        string destinationDirectory,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Архив не найден.", archivePath);

        return Task.Run(
            () => ExtractArchive(archivePath, destinationDirectory, progress, cancellationToken),
            cancellationToken);
    }

    // ---------- Упаковка ----------

    private static void CreateArchive(
        IReadOnlyCollection<string> sourcePaths,
        string archivePath,
        CompressionLevel compressionLevel,
        IProgress<ArchiveProgress>? progress,
        CancellationToken cancellationToken)
    {
        // Сначала собираем полный план записей: так заранее известен общий объём данных
        // (для расчёта процентов), а создаваемый архив гарантированно не попадёт сам в себя.
        List<ArchiveEntryPlan> entries = CollectEntries(sourcePaths);
        long totalBytes = entries.Where(e => !e.IsDirectory).Sum(e => new FileInfo(e.SourcePath!).Length);

        try
        {
            using var zipStream = new FileStream(archivePath, FileMode.Create, FileAccess.Write);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

            long processedBytes = 0;
            int lastPercent = -1;

            foreach (ArchiveEntryPlan plan in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (plan.IsDirectory)
                {
                    // Пустая папка хранится в ZIP как запись с косой чертой на конце.
                    archive.CreateEntry(plan.EntryName);
                    continue;
                }

                ReportItemStart(progress, plan.EntryName, processedBytes, totalBytes);

                ZipArchiveEntry entry = archive.CreateEntry(plan.EntryName, compressionLevel);
                SetEntryTimestamp(entry, plan.SourcePath!);

                using Stream entryStream = entry.Open();
                using var sourceStream = new FileStream(plan.SourcePath!, FileMode.Open, FileAccess.Read, FileShare.Read);
                CopyWithProgress(sourceStream, entryStream, plan.EntryName, totalBytes,
                    ref processedBytes, ref lastPercent, progress, cancellationToken);
            }

            progress?.Report(new ArchiveProgress(100, "", totalBytes, totalBytes));
        }
        catch
        {
            // При ошибке или отмене не оставляем на диске недописанный архив.
            TryDeleteFile(archivePath);
            throw;
        }
    }

    /// <summary>План одной записи архива: исходный файл и имя внутри архива (null — пустая папка).</summary>
    private sealed record ArchiveEntryPlan(string? SourcePath, string EntryName)
    {
        public bool IsDirectory => SourcePath is null;
    }

    private static List<ArchiveEntryPlan> CollectEntries(IReadOnlyCollection<string> sourcePaths)
    {
        var entries = new List<ArchiveEntryPlan>();
        // Имена в корне архива не должны повторяться, иначе распаковка перезапишет файлы.
        var usedTopLevelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string rawPath in sourcePaths)
        {
            string path = Path.GetFullPath(rawPath);

            if (File.Exists(path))
            {
                string entryName = MakeUniqueName(usedTopLevelNames, Path.GetFileName(path));
                entries.Add(new ArchiveEntryPlan(path, entryName));
            }
            else if (Directory.Exists(path))
            {
                string rootEntryName = MakeUniqueName(usedTopLevelNames, new DirectoryInfo(path).Name);
                AddDirectory(entries, path, rootEntryName);
            }
            else
            {
                throw new FileNotFoundException($"Файл или папка не найдены: {path}", path);
            }
        }

        return entries;
    }

    private static void AddDirectory(List<ArchiveEntryPlan> entries, string directory, string rootEntryName)
    {
        bool isEmpty = true;

        foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            isEmpty = false;
            string relative = Path.GetRelativePath(directory, file).Replace(Path.DirectorySeparatorChar, '/');
            entries.Add(new ArchiveEntryPlan(file, rootEntryName + "/" + relative));
        }

        // Пустые подпапки тоже сохраняем — записями вида «папка/».
        foreach (string subDirectory in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories))
        {
            if (Directory.EnumerateFileSystemEntries(subDirectory).Any())
                continue;

            isEmpty = false;
            string relative = Path.GetRelativePath(directory, subDirectory).Replace(Path.DirectorySeparatorChar, '/');
            entries.Add(new ArchiveEntryPlan(null, rootEntryName + "/" + relative + "/"));
        }

        if (isEmpty)
            entries.Add(new ArchiveEntryPlan(null, rootEntryName + "/"));
    }

    private static string MakeUniqueName(HashSet<string> usedNames, string name)
    {
        if (usedNames.Add(name))
            return name;

        string stem = Path.GetFileNameWithoutExtension(name);
        string extension = Path.GetExtension(name);
        for (int i = 2; ; i++)
        {
            string candidate = $"{stem} ({i}){extension}";
            if (usedNames.Add(candidate))
                return candidate;
        }
    }

    private static void SetEntryTimestamp(ZipArchiveEntry entry, string sourceFile)
    {
        // Формат ZIP хранит даты только в диапазоне 1980–2107 годов.
        DateTime lastWrite = File.GetLastWriteTime(sourceFile);
        if (lastWrite.Year is >= 1980 and <= 2107)
            entry.LastWriteTime = lastWrite;
    }

    // ---------- Распаковка ----------

    private static void ExtractArchive(
        string archivePath,
        string destinationDirectory,
        IProgress<ArchiveProgress>? progress,
        CancellationToken cancellationToken)
    {
        string destinationRoot = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(destinationRoot);

        using ZipArchive archive = OpenArchive(archivePath);

        long totalBytes = archive.Entries.Sum(e => e.Length);
        long processedBytes = 0;
        int lastPercent = -1;

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string targetPath = GetSafeTargetPath(destinationRoot, entry.FullName);

            if (IsDirectoryEntry(entry))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            ReportItemStart(progress, entry.FullName, processedBytes, totalBytes);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            using (Stream entryStream = entry.Open())
            using (var targetStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
            {
                CopyWithProgress(entryStream, targetStream, entry.FullName, totalBytes,
                    ref processedBytes, ref lastPercent, progress, cancellationToken);
            }

            File.SetLastWriteTime(targetPath, entry.LastWriteTime.DateTime);
        }

        progress?.Report(new ArchiveProgress(100, "", totalBytes, totalBytes));
    }

    private static ZipArchive OpenArchive(string archivePath)
    {
        try
        {
            return ZipFile.OpenRead(archivePath);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException(
                $"Файл «{Path.GetFileName(archivePath)}» не является корректным ZIP-архивом.", ex);
        }
    }

    /// <summary>
    /// Преобразует имя записи архива в абсолютный путь внутри папки назначения.
    /// Защищает от атаки Zip Slip: запись вида «../../file» не сможет выйти за её пределы.
    /// </summary>
    private static string GetSafeTargetPath(string destinationRoot, string entryName)
    {
        string relativePath = entryName
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(destinationRoot, relativePath));

        string rootPrefix = destinationRoot.EndsWith(Path.DirectorySeparatorChar)
            ? destinationRoot
            : destinationRoot + Path.DirectorySeparatorChar;
        // На Windows пути сравниваются без учёта регистра, на других системах — с учётом.
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!fullPath.StartsWith(rootPrefix, comparison)
            && !string.Equals(fullPath, Path.TrimEndingDirectorySeparator(destinationRoot), comparison))
        {
            throw new InvalidDataException($"Архив содержит запись с недопустимым путём: «{entryName}».");
        }

        return fullPath;
    }

    private static bool IsDirectoryEntry(ZipArchiveEntry entry) =>
        entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');

    // ---------- Общее ----------

    private static void CopyWithProgress(
        Stream source,
        Stream destination,
        string currentItem,
        long totalBytes,
        ref long processedBytes,
        ref int lastPercent,
        IProgress<ArchiveProgress>? progress,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[CopyBufferSize];
        int bytesRead;
        while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            destination.Write(buffer, 0, bytesRead);
            processedBytes += bytesRead;

            // Сообщаем о прогрессе только при изменении процента,
            // чтобы не заваливать поток интерфейса лишними обновлениями.
            int percent = CalculatePercent(processedBytes, totalBytes);
            if (percent != lastPercent)
            {
                lastPercent = percent;
                progress?.Report(new ArchiveProgress(percent, currentItem, processedBytes, totalBytes));
            }
        }
    }

    private static void ReportItemStart(
        IProgress<ArchiveProgress>? progress, string item, long processedBytes, long totalBytes)
    {
        progress?.Report(new ArchiveProgress(CalculatePercent(processedBytes, totalBytes), item, processedBytes, totalBytes));
    }

    private static int CalculatePercent(long processedBytes, long totalBytes) =>
        totalBytes == 0 ? 100 : (int)(processedBytes * 100 / totalBytes);

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Файл мог не успеть создаться или быть занят — это не мешает сообщить об исходной ошибке.
        }
    }
}
