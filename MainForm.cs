using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;

namespace S7Emulator
{
    public class MainForm : Form
    {
        private readonly DbManager _dbManager = new DbManager();

        private ListBox _dbListBox = null!;
        private Button _btnAddDb = null!;
        private Button _btnRemoveDb = null!;
        private Button _btnStartStop = null!;
        private Label _statusLabel = null!;

        private DataGridView _fieldsGrid = null!;
        private Button _btnAddField = null!;
        private Button _btnRemoveField = null!;
        private Button _btnApplyFields = null!;

        private DataGridView _liveGrid = null!;
        private System.Windows.Forms.Timer _refreshTimer = null!;

        private Button _btnNewProject = null!;
        private Button _btnOpenProject = null!;
        private Button _btnSaveProject = null!;
        private Button _btnSaveProjectAs = null!;
        private string? _currentFilePath;

        private DbDefinition? SelectedDb =>
            _dbListBox.SelectedItem is DbListItem item ? _dbManager.Dbs.GetValueOrDefault(item.Number) : null;

        public MainForm()
        {
            Text = "S7 PLC Emulator (Snap7 S7Server)";
            Width = 1100;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;

            BuildLayout();
            UpdateTitle();
            _refreshTimer = new System.Windows.Forms.Timer { Interval = 200 };
            _refreshTimer.Tick += (s, e) => RefreshLiveGrid();
            _refreshTimer.Start();
        }

        private void BuildLayout()
        {
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 40 };

            _btnNewProject = new Button { Text = "New Project", Left = 10, Top = 8, Width = 90 };
            _btnOpenProject = new Button { Text = "Open...", Left = 105, Top = 8, Width = 70 };
            _btnSaveProject = new Button { Text = "Save", Left = 180, Top = 8, Width = 80 };
            _btnSaveProjectAs = new Button { Text = "Save As...", Left = 265, Top = 8, Width = 120 };
            _btnNewProject.Click += BtnNewProject_Click;
            _btnOpenProject.Click += BtnOpenProject_Click;
            _btnSaveProject.Click += BtnSaveProject_Click;
            _btnSaveProjectAs.Click += BtnSaveProjectAs_Click;

            _btnStartStop = new Button { Text = "Start Server", Left = 400, Top = 8, Width = 140 };
            _btnStartStop.Click += BtnStartStop_Click;
            _statusLabel = new Label { Text = "Status: Stopped", Left = 550, Top = 12, Width = 300, ForeColor = Color.DarkRed };

            topPanel.Controls.Add(_btnNewProject);
            topPanel.Controls.Add(_btnOpenProject);
            topPanel.Controls.Add(_btnSaveProject);
            topPanel.Controls.Add(_btnSaveProjectAs);
            topPanel.Controls.Add(_btnStartStop);
            topPanel.Controls.Add(_statusLabel);

            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 220 };

            // --- Left panel: DB list ---
            var leftPanel = new Panel { Dock = DockStyle.Fill };
            _dbListBox = new ListBox { Dock = DockStyle.Fill };
            _dbListBox.SelectedIndexChanged += (s, e) => LoadSelectedDbIntoGrids();

            var dbButtonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36 };
            _btnAddDb = new Button { Text = "Add DB", Width = 100 };
            _btnRemoveDb = new Button { Text = "Remove DB", Width = 100 };
            _btnAddDb.Click += BtnAddDb_Click;
            _btnRemoveDb.Click += BtnRemoveDb_Click;
            dbButtonPanel.Controls.Add(_btnAddDb);
            dbButtonPanel.Controls.Add(_btnRemoveDb);

            leftPanel.Controls.Add(_dbListBox);
            leftPanel.Controls.Add(dbButtonPanel);
            split.Panel1.Controls.Add(leftPanel);

            // --- Right panel: Tabs (Design / Live Values) ---
            var tabs = new TabControl { Dock = DockStyle.Fill };

            var designTab = new TabPage("Field Designer");
            _fieldsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false
            };
            _fieldsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", Name = "Name", Width = 150 });
            var typeCol = new DataGridViewComboBoxColumn { HeaderText = "Type", Name = "Type", Width = 100 };
            typeCol.Items.AddRange(Enum.GetNames(typeof(S7DataType)));
            _fieldsGrid.Columns.Add(typeCol);
            _fieldsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "String Length", Name = "StringLength", Width = 110 });
            _fieldsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Address (auto)", Name = "Address", Width = 130, ReadOnly = true });

            var designButtonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 36 };
            _btnAddField = new Button { Text = "Add Field", Width = 100 };
            _btnRemoveField = new Button { Text = "Remove Field", Width = 110 };
            _btnApplyFields = new Button { Text = "Apply (Rebuild)", Width = 150 };
            _btnAddField.Click += (s, e) => _fieldsGrid.Rows.Add("NewField", "Bool", "20", "");
            _btnRemoveField.Click += (s, e) =>
            {
                if (_fieldsGrid.CurrentRow != null && !_fieldsGrid.CurrentRow.IsNewRow)
                    _fieldsGrid.Rows.Remove(_fieldsGrid.CurrentRow);
            };
            _btnApplyFields.Click += BtnApplyFields_Click;
            designButtonPanel.Controls.Add(_btnAddField);
            designButtonPanel.Controls.Add(_btnRemoveField);
            designButtonPanel.Controls.Add(_btnApplyFields);

            designTab.Controls.Add(_fieldsGrid);
            designTab.Controls.Add(designButtonPanel);

            var liveTab = new TabPage("Live Values");
            _liveGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false
            };
            _liveGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", Name = "Name", Width = 150, ReadOnly = true });
            _liveGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Address", Name = "Address", Width = 100, ReadOnly = true });
            _liveGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", Name = "Type", Width = 80, ReadOnly = true });
            _liveGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Value", Name = "Value", Width = 150 });
            _liveGrid.CellEndEdit += LiveGrid_CellEndEdit;
            liveTab.Controls.Add(_liveGrid);

            tabs.TabPages.Add(designTab);
            tabs.TabPages.Add(liveTab);
            split.Panel2.Controls.Add(tabs);

            Controls.Add(split);
            Controls.Add(topPanel);
        }

        private void BtnNewProject_Click(object? sender, EventArgs e)
        {
            if (MessageBox.Show("All current DB definitions will be deleted. Continue?",
                    "New Project", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _dbManager.ClearAll();
            RefreshDbList();
            _fieldsGrid.Rows.Clear();
            _liveGrid.Rows.Clear();
            _currentFilePath = null;
            UpdateTitle();
        }

        private void BtnOpenProject_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog { Filter = "S7 Emulator Project (*.json)|*.json|All Files (*.*)|*.*" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                ProjectSerializer.Load(dlg.FileName, _dbManager);
                _currentFilePath = dlg.FileName;
                UpdateTitle();

                RefreshDbList();
                if (_dbListBox.Items.Count > 0)
                    _dbListBox.SelectedIndex = 0;
                else
                {
                    _fieldsGrid.Rows.Clear();
                    _liveGrid.Rows.Clear();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load the file:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSaveProject_Click(object? sender, EventArgs e)
        {
            if (_currentFilePath == null)
            {
                BtnSaveProjectAs_Click(sender, e);
                return;
            }
            SaveToPath(_currentFilePath);
        }

        private void BtnSaveProjectAs_Click(object? sender, EventArgs e)
        {
            using var dlg = new SaveFileDialog { Filter = "S7 Emulator Project (*.json)|*.json|All Files (*.*)|*.*" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            _currentFilePath = dlg.FileName;
            SaveToPath(dlg.FileName);
        }

        private void SaveToPath(string path)
        {
            try
            {
                // Only the last "Applied" DB definitions are saved; pending edits in the
                // Field Designer grid that haven't been applied yet are not included.
                ProjectSerializer.Save(path, _dbManager, includeValues: true);
                UpdateTitle();
                MessageBox.Show("Project saved.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateTitle()
        {
            string fileName = _currentFilePath == null ? "Unsaved Project" : Path.GetFileName(_currentFilePath);
            Text = $"S7 PLC Emulator (Snap7 S7Server) - {fileName}";
        }

        private void BtnAddDb_Click(object? sender, EventArgs e)
        {
            using var dlg = new AddDbDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                _dbManager.AddDb(dlg.DbNumber, dlg.DbName);
                RefreshDbList();
                SelectDbInList(dlg.DbNumber);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRemoveDb_Click(object? sender, EventArgs e)
        {
            var db = SelectedDb;
            if (db == null) return;
            _dbManager.RemoveDb(db.Number);
            RefreshDbList();
            _fieldsGrid.Rows.Clear();
            _liveGrid.Rows.Clear();
        }

        private void BtnApplyFields_Click(object? sender, EventArgs e)
        {
            var db = SelectedDb;
            if (db == null) return;

            db.Fields.Clear();
            foreach (DataGridViewRow row in _fieldsGrid.Rows)
            {
                if (row.IsNewRow) continue;
                string name = row.Cells["Name"].Value?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;

                var typeStr = row.Cells["Type"].Value?.ToString() ?? "Bool";
                var type = Enum.Parse<S7DataType>(typeStr);
                int strLen = 20;
                int.TryParse(row.Cells["StringLength"].Value?.ToString(), out strLen);
                if (strLen <= 0) strLen = 20;

                db.Fields.Add(new DbFieldDefinition
                {
                    Name = name,
                    DataType = type,
                    StringLength = strLen
                });
            }

            _dbManager.RebuildDb(db.Number);
            LoadSelectedDbIntoGrids();
            RefreshLiveGrid();
        }

        private void LiveGrid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            var db = SelectedDb;
            if (db == null) return;
            if (_liveGrid.Columns[e.ColumnIndex].Name != "Value") return;

            var row = _liveGrid.Rows[e.RowIndex];
            string fieldName = row.Cells["Name"].Value?.ToString() ?? "";
            var field = db.FindField(fieldName);
            if (field == null) return;

            string newValue = row.Cells["Value"].Value?.ToString() ?? "";
            if (!_dbManager.TryWriteField(db.Number, field, newValue))
            {
                MessageBox.Show($"Invalid value: '{newValue}' ({field.DataType})", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnStartStop_Click(object? sender, EventArgs e)
        {
            if (!_dbManager.IsRunning)
            {
                int result = _dbManager.Start();
                if (result == 0)
                {
                    _statusLabel.Text = "Status: Running (port 102)";
                    _statusLabel.ForeColor = Color.DarkGreen;
                    _btnStartStop.Text = "Stop Server";
                }
                else
                {
                    MessageBox.Show(_dbManager.ErrorText(result), "Start Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                _dbManager.Stop();
                _statusLabel.Text = "Status: Stopped";
                _statusLabel.ForeColor = Color.DarkRed;
                _btnStartStop.Text = "Start Server";
            }
        }

        private void RefreshDbList()
        {
            _dbListBox.Items.Clear();
            foreach (var db in _dbManager.Dbs.Values.OrderBy(d => d.Number))
                _dbListBox.Items.Add(new DbListItem(db.Number, db.Name));
        }

        private void SelectDbInList(int number)
        {
            for (int i = 0; i < _dbListBox.Items.Count; i++)
            {
                if (_dbListBox.Items[i] is DbListItem item && item.Number == number)
                {
                    _dbListBox.SelectedIndex = i;
                    break;
                }
            }
        }

        private void LoadSelectedDbIntoGrids()
        {
            _fieldsGrid.Rows.Clear();
            var db = SelectedDb;
            if (db == null) return;

            foreach (var field in db.Fields)
            {
                _fieldsGrid.Rows.Add(
                    field.Name, field.DataType.ToString(), field.StringLength.ToString(), field.Address);
            }

            RefreshLiveGrid();
        }

        private void RefreshLiveGrid()
        {
            var db = SelectedDb;
            if (db == null) return;

            // Rebuild the grid rows if the row count doesn't match the field count
            if (_liveGrid.Rows.Count != db.Fields.Count)
            {
                _liveGrid.Rows.Clear();
                foreach (var field in db.Fields)
                    _liveGrid.Rows.Add(field.Name, field.Address, field.DataType.ToString(), "");
            }

            for (int i = 0; i < db.Fields.Count; i++)
            {
                var field = db.Fields[i];
                string value = _dbManager.ReadFieldAsString(db.Number, field);

                // Don't overwrite the cell the user is currently editing
                if (_liveGrid.CurrentCell?.RowIndex == i && _liveGrid.CurrentCell.ColumnIndex == 3 && _liveGrid.IsCurrentCellInEditMode)
                    continue;

                _liveGrid.Rows[i].Cells["Value"].Value = value;
            }
        }

        private class DbListItem
        {
            public int Number { get; }
            public string Name { get; }
            public DbListItem(int number, string name) { Number = number; Name = name; }
            public override string ToString() => $"DB{Number} - {Name}";
        }
    }

    /// <summary>Simple dialog for entering the number/name when adding a new DB.</summary>
    public class AddDbDialog : Form
    {
        private TextBox _numberBox = null!;
        private TextBox _nameBox = null!;

        public int DbNumber { get; private set; }
        public string DbName { get; private set; } = "";

        public AddDbDialog()
        {
            Text = "Add New DB";
            Width = 300;
            Height = 160;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;

            var lbl1 = new Label { Text = "DB Number:", Left = 10, Top = 15, Width = 100 };
            _numberBox = new TextBox { Left = 120, Top = 12, Width = 140, Text = "1" };

            var lbl2 = new Label { Text = "DB Name:", Left = 10, Top = 50, Width = 100 };
            _nameBox = new TextBox { Left = 120, Top = 47, Width = 140, Text = "DB1" };

            var okBtn = new Button { Text = "Add", Left = 100, Top = 85, Width = 80, DialogResult = DialogResult.OK };
            var cancelBtn = new Button { Text = "Cancel", Left = 180, Top = 85, Width = 80, DialogResult = DialogResult.Cancel };

            okBtn.Click += (s, e) =>
            {
                if (!int.TryParse(_numberBox.Text, out int num) || num < 1)
                {
                    MessageBox.Show("Please enter a valid DB number.");
                    DialogResult = DialogResult.None;
                    return;
                }
                DbNumber = num;
                DbName = string.IsNullOrWhiteSpace(_nameBox.Text) ? $"DB{num}" : _nameBox.Text;
            };

            Controls.Add(lbl1);
            Controls.Add(_numberBox);
            Controls.Add(lbl2);
            Controls.Add(_nameBox);
            Controls.Add(okBtn);
            Controls.Add(cancelBtn);
            AcceptButton = okBtn;
            CancelButton = cancelBtn;
        }
    }
}
