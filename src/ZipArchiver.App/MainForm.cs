using System.Diagnostics;
using System.IO.Compression;
using ZipArchiver.Core;

namespace ZipArchiver.App;

/// <summary>
/// Главное окно архиватора: список выбранных файлов и папок,
/// создание ZIP-архива и распаковка существующих архивов с индикатором прогресса.
/// </summary>
public partial class MainForm : Form
{
    /// <summary>Элемент списка: путь, признак папки и размер файла (для итоговой статистики).</summary>
    private sealed record ListEntry(string Path, bool IsFolder, long FileSize);

    private readonly ArchiveService _archiveService = new();
    private readonly Progress<ArchiveProgress> _progressReporter;
    private CancellationTokenSource? _operationCts;
    private bool _isBusy;

    public MainForm()
    {
        InitializeComponent();

        // Progress<T> запоминает контекст синхронизации потока интерфейса,
        // поэтому уведомления из фонового потока попадают в форму без ручного Invoke.
        _progressReporter = new Progress<ArchiveProgress>(OnProgressChanged);

        cbCompression.Items.AddRange(new object[]
        {
            "Быстрое (слабое сжатие)",
            "Оптимальное",
            "Максимальное (медленнее)",
            "Без сжатия",
        });
        cbCompression.SelectedIndex = 1;

        UpdateTotals();
        UpdateInterfaceState();
    }

    private CompressionLevel SelectedCompressionLevel => cbCompression.SelectedIndex switch
    {
        0 => CompressionLevel.Fastest,
        2 => CompressionLevel.SmallestSize,
        3 => CompressionLevel.NoCompression,
        _ => CompressionLevel.Optimal,
    };

    // ---------- Наполнение списка ----------

    private void BtnAddFiles_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Выберите файлы для архивации",
            Filter = "Все файлы (*.*)|*.*",
            Multiselect = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            AddPaths(dialog.FileNames);
    }

    private void BtnAddFolder_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Выберите папку для архивации",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            AddPaths([dialog.SelectedPath]);
    }

    private void LvItems_DragEnter(object? sender, DragEventArgs e)
    {
        if (!_isBusy && e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private void LvItems_DragDrop(object? sender, DragEventArgs e)
    {
        if (!_isBusy && e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
            AddPaths(paths);
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        lvItems.BeginUpdate();
        try
        {
            foreach (string path in paths)
            {
                if (ContainsPath(path))
                    continue;

                if (File.Exists(path))
                {
                    var info = new FileInfo(path);
                    AddItem(new ListEntry(info.FullName, IsFolder: false, info.Length),
                        info.Name, "Файл", ByteSize.Format(info.Length), info.DirectoryName ?? "");
                }
                else if (Directory.Exists(path))
                {
                    var info = new DirectoryInfo(path);
                    AddItem(new ListEntry(info.FullName, IsFolder: true, 0),
                        info.Name, "Папка", "—", info.Parent?.FullName ?? "");
                }
            }
        }
        finally
        {
            lvItems.EndUpdate();
        }

        UpdateTotals();
        UpdateInterfaceState();
    }

    private void AddItem(ListEntry entry, string name, string type, string size, string location)
    {
        lvItems.Items.Add(new ListViewItem(new[] { name, type, size, location }) { Tag = entry });
    }

    private bool ContainsPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        return Entries().Any(e => string.Equals(e.Path, fullPath, StringComparison.OrdinalIgnoreCase));
    }

    private IEnumerable<ListEntry> Entries() =>
        lvItems.Items.Cast<ListViewItem>().Select(i => (ListEntry)i.Tag!);

    private void BtnRemove_Click(object? sender, EventArgs e) => RemoveSelectedItems();

    private void BtnClear_Click(object? sender, EventArgs e)
    {
        lvItems.Items.Clear();
        UpdateTotals();
        UpdateInterfaceState();
    }

    private void LvItems_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete && !_isBusy)
            RemoveSelectedItems();
    }

    private void LvItems_SelectedIndexChanged(object? sender, EventArgs e) => UpdateInterfaceState();

    private void RemoveSelectedItems()
    {
        foreach (ListViewItem item in lvItems.SelectedItems.Cast<ListViewItem>().ToList())
            item.Remove();

        UpdateTotals();
        UpdateInterfaceState();
    }

    // ---------- Операции с архивом ----------

    private async void BtnCreate_Click(object? sender, EventArgs e)
    {
        string[] sourcePaths = Entries().Select(entry => entry.Path).ToArray();
        if (sourcePaths.Length == 0)
        {
            MessageBox.Show(this, "Сначала добавьте в список файлы или папки.", "Список пуст",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Сохранение архива",
            Filter = "ZIP-архив (*.zip)|*.zip",
            FileName = $"Архив {DateTime.Now:yyyy-MM-dd}.zip",
            AddExtension = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        string archivePath = dialog.FileName;
        CompressionLevel level = SelectedCompressionLevel;

        bool success = await RunOperationAsync(
            ct => _archiveService.CreateArchiveAsync(sourcePaths, archivePath, level, _progressReporter, ct),
            "Идёт упаковка…");

        if (success)
        {
            lblStatus.Text = $"Архив создан: {archivePath}";
            MessageBox.Show(this,
                $"Архив успешно создан.\n\nФайл: {archivePath}\nРазмер: {ByteSize.Format(new FileInfo(archivePath).Length)}",
                "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async void BtnExtract_Click(object? sender, EventArgs e)
    {
        using var openDialog = new OpenFileDialog
        {
            Title = "Выберите ZIP-архив",
            Filter = "ZIP-архивы (*.zip)|*.zip|Все файлы (*.*)|*.*",
        };

        if (openDialog.ShowDialog(this) != DialogResult.OK)
            return;

        using var folderDialog = new FolderBrowserDialog
        {
            Description = "Выберите папку, в которую распаковать архив",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
        };

        if (folderDialog.ShowDialog(this) != DialogResult.OK)
            return;

        string archivePath = openDialog.FileName;
        string destination = folderDialog.SelectedPath;

        bool success = await RunOperationAsync(
            ct => _archiveService.ExtractArchiveAsync(archivePath, destination, _progressReporter, ct),
            "Идёт распаковка…");

        if (success)
        {
            lblStatus.Text = $"Архив распакован в: {destination}";
            DialogResult answer = MessageBox.Show(this,
                $"Архив успешно распакован.\n\nПапка: {destination}\n\nОткрыть её в проводнике?",
                "Готово", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (answer == DialogResult.Yes)
                Process.Start(new ProcessStartInfo { FileName = destination, UseShellExecute = true });
        }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        _operationCts?.Cancel();
        btnCancel.Enabled = false;
        lblStatus.Text = "Отмена операции…";
    }

    /// <summary>
    /// Общая обвязка длительной операции: блокирует элементы управления,
    /// обрабатывает отмену и ошибки, по завершении возвращает форму в исходное состояние.
    /// </summary>
    private async Task<bool> RunOperationAsync(Func<CancellationToken, Task> operation, string statusText)
    {
        _operationCts = new CancellationTokenSource();
        _isBusy = true;
        progressBar.Value = 0;
        lblStatus.Text = statusText;
        UpdateInterfaceState();

        try
        {
            await operation(_operationCts.Token);
            if (!IsDisposed)
                progressBar.Value = 100;
            return true;
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed)
                lblStatus.Text = "Операция отменена.";
            return false;
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                lblStatus.Text = "Произошла ошибка.";
                MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }
        finally
        {
            _operationCts.Dispose();
            _operationCts = null;
            _isBusy = false;

            if (!IsDisposed)
            {
                if (progressBar.Value != 100)
                    progressBar.Value = 0;
                UpdateInterfaceState();
            }
        }
    }

    private void OnProgressChanged(ArchiveProgress value)
    {
        // Форма могла закрыться, пока фоновая операция доделывала работу.
        if (IsDisposed || !_isBusy)
            return;

        progressBar.Value = Math.Clamp(value.Percent, 0, 100);
        string item = string.IsNullOrEmpty(value.CurrentItem) ? "" : $" — {value.CurrentItem}";
        lblStatus.Text =
            $"{value.Percent}% ({ByteSize.Format(value.BytesProcessed)} из {ByteSize.Format(value.TotalBytes)}){item}";
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isBusy)
            return;

        DialogResult answer = MessageBox.Show(this,
            "Выполняется операция с архивом. Прервать её и выйти?",
            "Архиватор ZIP", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (answer == DialogResult.Yes)
            _operationCts?.Cancel();
        else
            e.Cancel = true;
    }

    // ---------- Состояние интерфейса ----------

    private void UpdateInterfaceState()
    {
        bool hasItems = lvItems.Items.Count > 0;

        btnAddFiles.Enabled = !_isBusy;
        btnAddFolder.Enabled = !_isBusy;
        btnRemove.Enabled = !_isBusy && lvItems.SelectedItems.Count > 0;
        btnClear.Enabled = !_isBusy && hasItems;
        btnCreate.Enabled = !_isBusy && hasItems;
        btnExtract.Enabled = !_isBusy;
        cbCompression.Enabled = !_isBusy;
        lvItems.Enabled = !_isBusy;
        btnCancel.Enabled = _isBusy;
    }

    private void UpdateTotals()
    {
        int files = 0;
        int folders = 0;
        long totalSize = 0;

        foreach (ListEntry entry in Entries())
        {
            if (entry.IsFolder)
                folders++;
            else
            {
                files++;
                totalSize += entry.FileSize;
            }
        }

        lblTotals.Text = $"В списке: файлов — {files}, папок — {folders}, объём файлов — {ByteSize.Format(totalSize)}";
    }
}
