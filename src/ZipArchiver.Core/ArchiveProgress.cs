namespace ZipArchiver.Core;

/// <summary>
/// Снимок состояния длительной операции с архивом (упаковки или распаковки).
/// Передаётся в интерфейс через <see cref="IProgress{T}"/>.
/// </summary>
/// <param name="Percent">Готовность операции в процентах (0–100).</param>
/// <param name="CurrentItem">Имя обрабатываемого файла (относительный путь внутри архива).</param>
/// <param name="BytesProcessed">Сколько байт уже обработано.</param>
/// <param name="TotalBytes">Общий объём данных операции в байтах.</param>
public readonly record struct ArchiveProgress(
    int Percent,
    string CurrentItem,
    long BytesProcessed,
    long TotalBytes);
