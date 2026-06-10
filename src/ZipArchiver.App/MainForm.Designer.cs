namespace ZipArchiver.App;

partial class MainForm
{
    /// <summary>Обязательная переменная конструктора форм.</summary>
    private System.ComponentModel.IContainer components = null!;

    /// <summary>Освобождает все используемые ресурсы.</summary>
    /// <param name="disposing">true, если управляемые ресурсы должны быть удалены; иначе false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && components is not null)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Код, созданный конструктором форм Windows

    /// <summary>
    /// Требуемый метод для поддержки конструктора — не изменяйте
    /// содержимое этого метода с помощью редактора кода.
    /// </summary>
    private void InitializeComponent()
    {
        lvItems = new ListView();
        colName = new ColumnHeader();
        colType = new ColumnHeader();
        colSize = new ColumnHeader();
        colPath = new ColumnHeader();
        btnAddFiles = new Button();
        btnAddFolder = new Button();
        btnRemove = new Button();
        btnClear = new Button();
        lblCompression = new Label();
        cbCompression = new ComboBox();
        btnCreate = new Button();
        btnExtract = new Button();
        btnCancel = new Button();
        lblTotals = new Label();
        lblStatus = new Label();
        progressBar = new ProgressBar();
        SuspendLayout();
        //
        // lvItems
        //
        lvItems.AllowDrop = true;
        lvItems.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lvItems.Columns.AddRange(new ColumnHeader[] { colName, colType, colSize, colPath });
        lvItems.FullRowSelect = true;
        lvItems.Location = new Point(12, 12);
        lvItems.Name = "lvItems";
        lvItems.Size = new Size(650, 460);
        lvItems.TabIndex = 0;
        lvItems.UseCompatibleStateImageBehavior = false;
        lvItems.View = View.Details;
        lvItems.SelectedIndexChanged += LvItems_SelectedIndexChanged;
        lvItems.DragDrop += LvItems_DragDrop;
        lvItems.DragEnter += LvItems_DragEnter;
        lvItems.KeyDown += LvItems_KeyDown;
        //
        // colName
        //
        colName.Text = "Имя";
        colName.Width = 240;
        //
        // colType
        //
        colType.Text = "Тип";
        colType.Width = 70;
        //
        // colSize
        //
        colSize.Text = "Размер";
        colSize.TextAlign = HorizontalAlignment.Right;
        colSize.Width = 90;
        //
        // colPath
        //
        colPath.Text = "Расположение";
        colPath.Width = 230;
        //
        // btnAddFiles
        //
        btnAddFiles.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnAddFiles.Location = new Point(676, 12);
        btnAddFiles.Name = "btnAddFiles";
        btnAddFiles.Size = new Size(196, 34);
        btnAddFiles.TabIndex = 1;
        btnAddFiles.Text = "Добавить файлы…";
        btnAddFiles.UseVisualStyleBackColor = true;
        btnAddFiles.Click += BtnAddFiles_Click;
        //
        // btnAddFolder
        //
        btnAddFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnAddFolder.Location = new Point(676, 52);
        btnAddFolder.Name = "btnAddFolder";
        btnAddFolder.Size = new Size(196, 34);
        btnAddFolder.TabIndex = 2;
        btnAddFolder.Text = "Добавить папку…";
        btnAddFolder.UseVisualStyleBackColor = true;
        btnAddFolder.Click += BtnAddFolder_Click;
        //
        // btnRemove
        //
        btnRemove.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnRemove.Location = new Point(676, 92);
        btnRemove.Name = "btnRemove";
        btnRemove.Size = new Size(196, 34);
        btnRemove.TabIndex = 3;
        btnRemove.Text = "Удалить выбранное";
        btnRemove.UseVisualStyleBackColor = true;
        btnRemove.Click += BtnRemove_Click;
        //
        // btnClear
        //
        btnClear.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnClear.Location = new Point(676, 132);
        btnClear.Name = "btnClear";
        btnClear.Size = new Size(196, 34);
        btnClear.TabIndex = 4;
        btnClear.Text = "Очистить список";
        btnClear.UseVisualStyleBackColor = true;
        btnClear.Click += BtnClear_Click;
        //
        // lblCompression
        //
        lblCompression.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        lblCompression.AutoSize = true;
        lblCompression.Location = new Point(676, 182);
        lblCompression.Name = "lblCompression";
        lblCompression.Size = new Size(98, 15);
        lblCompression.TabIndex = 5;
        lblCompression.Text = "Степень сжатия:";
        //
        // cbCompression
        //
        cbCompression.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        cbCompression.DropDownStyle = ComboBoxStyle.DropDownList;
        cbCompression.Location = new Point(676, 202);
        cbCompression.Name = "cbCompression";
        cbCompression.Size = new Size(196, 23);
        cbCompression.TabIndex = 6;
        //
        // btnCreate
        //
        btnCreate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnCreate.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        btnCreate.Location = new Point(676, 248);
        btnCreate.Name = "btnCreate";
        btnCreate.Size = new Size(196, 42);
        btnCreate.TabIndex = 7;
        btnCreate.Text = "Создать архив…";
        btnCreate.UseVisualStyleBackColor = true;
        btnCreate.Click += BtnCreate_Click;
        //
        // btnExtract
        //
        btnExtract.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnExtract.Location = new Point(676, 296);
        btnExtract.Name = "btnExtract";
        btnExtract.Size = new Size(196, 42);
        btnExtract.TabIndex = 8;
        btnExtract.Text = "Распаковать архив…";
        btnExtract.UseVisualStyleBackColor = true;
        btnExtract.Click += BtnExtract_Click;
        //
        // btnCancel
        //
        btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnCancel.Enabled = false;
        btnCancel.Location = new Point(676, 344);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(196, 34);
        btnCancel.TabIndex = 9;
        btnCancel.Text = "Отменить операцию";
        btnCancel.UseVisualStyleBackColor = true;
        btnCancel.Click += BtnCancel_Click;
        //
        // lblTotals
        //
        lblTotals.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lblTotals.AutoEllipsis = true;
        lblTotals.Location = new Point(12, 481);
        lblTotals.Name = "lblTotals";
        lblTotals.Size = new Size(650, 18);
        lblTotals.TabIndex = 10;
        lblTotals.Text = "В списке: файлов — 0, папок — 0";
        //
        // lblStatus
        //
        lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lblStatus.AutoEllipsis = true;
        lblStatus.Location = new Point(12, 505);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(860, 18);
        lblStatus.TabIndex = 11;
        lblStatus.Text = "Готово к работе.";
        //
        // progressBar
        //
        progressBar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        progressBar.Location = new Point(12, 526);
        progressBar.Name = "progressBar";
        progressBar.Size = new Size(860, 23);
        progressBar.TabIndex = 12;
        //
        // MainForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(884, 561);
        Controls.Add(lvItems);
        Controls.Add(btnAddFiles);
        Controls.Add(btnAddFolder);
        Controls.Add(btnRemove);
        Controls.Add(btnClear);
        Controls.Add(lblCompression);
        Controls.Add(cbCompression);
        Controls.Add(btnCreate);
        Controls.Add(btnExtract);
        Controls.Add(btnCancel);
        Controls.Add(lblTotals);
        Controls.Add(lblStatus);
        Controls.Add(progressBar);
        MinimumSize = new Size(840, 560);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Архиватор ZIP";
        FormClosing += MainForm_FormClosing;
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private ListView lvItems = null!;
    private ColumnHeader colName = null!;
    private ColumnHeader colType = null!;
    private ColumnHeader colSize = null!;
    private ColumnHeader colPath = null!;
    private Button btnAddFiles = null!;
    private Button btnAddFolder = null!;
    private Button btnRemove = null!;
    private Button btnClear = null!;
    private Label lblCompression = null!;
    private ComboBox cbCompression = null!;
    private Button btnCreate = null!;
    private Button btnExtract = null!;
    private Button btnCancel = null!;
    private Label lblTotals = null!;
    private Label lblStatus = null!;
    private ProgressBar progressBar = null!;
}
