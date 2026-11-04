using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LaboratorioPestanas.Core;
using LaboratorioPestanas.Experiments;

namespace LaboratorioPestanas.UI
{
    /// <summary>
    /// Ventana principal organizada en pestañas: Datos · Búsqueda individual · Experimento · Gráfica · Marco teórico.
    /// La interfaz solo orquesta; toda la lógica vive en las capas Core y Experiments.
    /// </summary>
    public sealed class MainForm : Form
    {
        private static readonly string[] TechniqueOrder = { ExperimentRunner.Linear, ExperimentRunner.Binary, ExperimentRunner.Hash };
        private const int PreviewLimit = 1000;

        // Barra superior
        private readonly TextBox _txtSeed = new TextBox { Text = "20261103", Width = 160 };

        // Pestaña Datos
        private readonly NumericUpDown _nudSize = new NumericUpDown();
        private readonly Button _btnGenerate = new Button { Text = "Generar conjunto", AutoSize = true };
        private readonly ListBox _lstOriginal = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        private readonly ListBox _lstSorted = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        private readonly Label _lblInfo = new Label { AutoSize = true, Text = "Sin datos. Genere un conjunto." };

        // Pestaña Búsqueda individual
        private readonly TextBox _txtKey = new TextBox { Width = 160 };
        private readonly Button _btnSearch = new Button { Text = "Buscar con las 3 técnicas", AutoSize = true };
        private readonly DataGridView _gridSingle = CreateGrid();
        private readonly Label _lblSingle = new Label { AutoSize = true };

        // Pestaña Experimento
        private readonly CheckedListBox _chkSizes = new CheckedListBox { CheckOnClick = true, Width = 160, Height = 110 };
        private readonly NumericUpDown _nudQueries = new NumericUpDown();
        private readonly Button _btnRun = new Button { Text = "Ejecutar experimento", AutoSize = true };
        private readonly Button _btnCancel = new Button { Text = "Cancelar", AutoSize = true, Enabled = false };
        private readonly Button _btnExport = new Button { Text = "Exportar CSV…", AutoSize = true, Enabled = false };
        private readonly ProgressBar _progress = new ProgressBar { Style = ProgressBarStyle.Marquee, Width = 160, Visible = false };
        private readonly DataGridView _gridExp = CreateGrid();

        // Pestaña Gráfica
        private readonly ComboBox _cmbMetric = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        private readonly ComboBox _cmbCase = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        private readonly BarChartPanel _chart = new BarChartPanel { Dock = DockStyle.Fill };

        private readonly ToolStripStatusLabel _status = new ToolStripStatusLabel("Listo.");

        private DataSet _current;
        private List<ExperimentRow> _lastRows = new List<ExperimentRow>();
        private CancellationTokenSource _cts;

        public MainForm()
        {
            Text = "Programa 1 · Laboratorio de búsquedas (pestañas · encadenamiento separado)";
            MinimumSize = new Size(980, 640);
            Size = new Size(1180, 760);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildDataTab());
            tabs.TabPages.Add(BuildSingleSearchTab());
            tabs.TabPages.Add(BuildExperimentTab());
            tabs.TabPages.Add(BuildChartTab());
            tabs.TabPages.Add(BuildTheoryTab());

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 8, 8, 0) };
            top.Controls.Add(new Label { Text = "Semilla (entero ≥ 0):", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
            top.Controls.Add(_txtSeed);
            top.Controls.Add(new Label
            {
                Text = "La misma semilla reproduce exactamente los mismos datos y conteos.",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Margin = new Padding(12, 6, 0, 0)
            });

            var statusStrip = new StatusStrip();
            statusStrip.Items.Add(_status);

            Controls.Add(tabs);
            Controls.Add(top);
            Controls.Add(statusStrip);

            FormClosing += (s, e) => _cts?.Cancel();
        }

        // ───────────────────────────── Construcción de pestañas ─────────────────────────────

        private TabPage BuildDataTab()
        {
            _nudSize.Minimum = DataSet.MinSize;
            _nudSize.Maximum = DataSet.MaxSize;
            _nudSize.Value = 1000;
            _nudSize.ThousandsSeparator = true;
            _nudSize.Width = 120;
            _btnGenerate.Click += OnGenerateClick;

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
            bar.Controls.Add(new Label { Text = "Tamaño N:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
            bar.Controls.Add(_nudSize);
            bar.Controls.Add(_btnGenerate);
            bar.Controls.Add(_lblInfo);
            _lblInfo.Margin = new Padding(16, 6, 0, 0);

            var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            split.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            split.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            split.Controls.Add(new Label { Text = $"Orden original (primeros {PreviewLimit:N0}) — búsqueda secuencial", AutoSize = true }, 0, 0);
            split.Controls.Add(new Label { Text = $"Orden ascendente (primeros {PreviewLimit:N0}) — búsqueda binaria", AutoSize = true }, 1, 0);
            split.Controls.Add(_lstOriginal, 0, 1);
            split.Controls.Add(_lstSorted, 1, 1);

            var page = new TabPage("1 · Datos") { Padding = new Padding(6) };
            page.Controls.Add(split);
            page.Controls.Add(bar);
            return page;
        }

        private TabPage BuildSingleSearchTab()
        {
            _btnSearch.Click += OnSearchClick;
            _txtKey.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; OnSearchClick(s, EventArgs.Empty); }
            };

            _gridSingle.Columns.Add("tec", "Técnica");
            _gridSingle.Columns.Add("enc", "¿Encontrado?");
            _gridSingle.Columns.Add("pos", "Posición / índice");
            _gridSingle.Columns.Add("cmp", "Comparaciones");
            _gridSingle.Columns.Add("ite", "Iteraciones");

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
            bar.Controls.Add(new Label { Text = "Clave entera a buscar:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
            bar.Controls.Add(_txtKey);
            bar.Controls.Add(_btnSearch);

            _lblSingle.Dock = DockStyle.Bottom;
            _lblSingle.Padding = new Padding(6);
            _lblSingle.Text = "Posición: índice en el arreglo original (secuencial y hash) o en el arreglo ordenado (binaria).";

            var page = new TabPage("2 · Búsqueda individual") { Padding = new Padding(6) };
            page.Controls.Add(_gridSingle);
            page.Controls.Add(_lblSingle);
            page.Controls.Add(bar);
            return page;
        }

        private TabPage BuildExperimentTab()
        {
            foreach (int n in new[] { 1_000, 10_000, 100_000, 500_000, 1_000_000 })
                _chkSizes.Items.Add(n.ToString("N0", CultureInfo.CurrentCulture), n <= 100_000);

            _nudQueries.Minimum = 1;
            _nudQueries.Maximum = 100_000;
            _nudQueries.Value = 1000;
            _nudQueries.ThousandsSeparator = true;
            _nudQueries.Width = 100;

            _btnRun.Click += OnRunClick;
            _btnCancel.Click += (s, e) => _cts?.Cancel();
            _btnExport.Click += OnExportClick;

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                Width = 200,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(6)
            };
            panel.Controls.Add(new Label { Text = "Tamaños N a evaluar:", AutoSize = true });
            panel.Controls.Add(_chkSizes);
            panel.Controls.Add(new Label { Text = "Consultas por caso\n(existentes y no existentes):", AutoSize = true, Margin = new Padding(3, 10, 3, 3) });
            panel.Controls.Add(_nudQueries);
            panel.Controls.Add(_btnRun);
            panel.Controls.Add(_btnCancel);
            panel.Controls.Add(_progress);
            panel.Controls.Add(_btnExport);

            string[] headers = { "N", "Técnica", "Caso", "Comp. prom.", "Comp. mín.", "Comp. máx.", "Iter. prom.", "Iter. máx.", "Comp. teórica", "µs/búsqueda", "Notas" };
            foreach (string h in headers) _gridExp.Columns.Add(h, h);

            var page = new TabPage("3 · Experimento") { Padding = new Padding(6) };
            page.Controls.Add(_gridExp);
            page.Controls.Add(panel);
            return page;
        }

        private TabPage BuildChartTab()
        {
            _cmbMetric.Items.AddRange(new object[] { "Comparaciones promedio", "Iteraciones promedio", "Comparaciones máximas (peor caso observado)" });
            _cmbMetric.SelectedIndex = 0;
            _cmbCase.Items.AddRange(new object[] { ExperimentRunner.Hit, ExperimentRunner.Miss });
            _cmbCase.SelectedIndex = 0;
            _cmbMetric.SelectedIndexChanged += (s, e) => RefreshChart();
            _cmbCase.SelectedIndexChanged += (s, e) => RefreshChart();

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(6) };
            bar.Controls.Add(new Label { Text = "Métrica:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
            bar.Controls.Add(_cmbMetric);
            bar.Controls.Add(new Label { Text = "Caso:", AutoSize = true, Margin = new Padding(16, 6, 6, 0) });
            bar.Controls.Add(_cmbCase);

            var page = new TabPage("4 · Gráfica") { Padding = new Padding(6) };
            page.Controls.Add(_chart);
            page.Controls.Add(bar);
            return page;
        }

        private static TabPage BuildTheoryTab()
        {
            var box = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Consolas", 10f),
                Text = TheoryText.Content
            };
            var page = new TabPage("5 · Marco teórico") { Padding = new Padding(6) };
            page.Controls.Add(box);
            return page;
        }

        private static DataGridView CreateGrid()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                BackgroundColor = Color.White
            };
        }

        // ───────────────────────────── Manejadores ─────────────────────────────

        private void OnGenerateClick(object sender, EventArgs e)
        {
            try
            {
                ulong seed = ReadSeed();
                int n = (int)_nudSize.Value;
                DataSet.ValidateSize(n);

                Cursor = Cursors.WaitCursor;
                _current = DataSet.Generate(n, new SplitMix64(unchecked(seed + (ulong)n)));

                FillPreview(_lstOriginal, _current.Original);
                FillPreview(_lstSorted, _current.Sorted);
                ChainedHashTable t = _current.Table;
                _lblInfo.Text = string.Format(CultureInfo.CurrentCulture,
                    "N = {0:N0} · rango [0, {1:N0}) · tabla: m = {2:N0}, α = {3:F3}, cubetas usadas = {4:N0}, cadena máx. = {5}",
                    n, _current.UpperBound, t.Capacity, t.LoadFactor, t.UsedBuckets(), t.LongestChain());
                _status.Text = "Conjunto generado correctamente.";
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is InvalidOperationException || ex is OverflowException)
            {
                ShowWarning(ex.Message);
            }
            catch (OutOfMemoryException)
            {
                ShowWarning("Memoria insuficiente para ese tamaño de N.");
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void OnSearchClick(object sender, EventArgs e)
        {
            if (_current == null)
            {
                ShowWarning("Primero genere un conjunto en la pestaña «Datos».");
                return;
            }

            string text = _txtKey.Text.Trim();
            if (text.Length == 0)
            {
                ShowWarning("Escriba una clave entera.");
                _txtKey.Focus();
                return;
            }
            if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int key))
            {
                ShowWarning($"«{text}» no es un entero válido de 32 bits (rango {int.MinValue:N0} a {int.MaxValue:N0}).");
                _txtKey.SelectAll();
                _txtKey.Focus();
                return;
            }

            var results = new (string Name, SearchResult R)[]
            {
                (ExperimentRunner.Linear, SequentialSearch.Find(_current.Original, key)),
                (ExperimentRunner.Binary, BinarySearch.Find(_current.Sorted, key)),
                (ExperimentRunner.Hash, _current.Table.Find(key))
            };

            _gridSingle.Rows.Clear();
            foreach (var (name, r) in results)
                _gridSingle.Rows.Add(name, r.Found ? "Sí" : "No", r.Found ? r.Position.ToString("N0", CultureInfo.CurrentCulture) : "—",
                    r.Comparisons.ToString("N0", CultureInfo.CurrentCulture), r.Iterations.ToString("N0", CultureInfo.CurrentCulture));

            _status.Text = results[0].R.Found ? $"La clave {key} existe en el conjunto." : $"La clave {key} no existe en el conjunto.";
        }

        private async void OnRunClick(object sender, EventArgs e)
        {
            ExperimentConfig cfg;
            try
            {
                var sizes = new List<int>();
                foreach (object item in _chkSizes.CheckedItems)
                    sizes.Add(int.Parse((string)item, NumberStyles.AllowThousands, CultureInfo.CurrentCulture));
                cfg = new ExperimentConfig(sizes, (int)_nudQueries.Value, ReadSeed());
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is OverflowException)
            {
                ShowWarning(ex.Message);
                return;
            }

            SetBusy(true);
            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;
            var progress = new Progress<string>(msg => _status.Text = msg);
            try
            {
                List<ExperimentRow> rows = await Task.Run(() => ExperimentRunner.Run(cfg, progress, token), token);
                _lastRows = rows;
                FillExperimentGrid(rows);
                RefreshChart();
                _btnExport.Enabled = rows.Count > 0;
                _status.Text = $"Experimento terminado: {rows.Count} filas de resultados.";
            }
            catch (OperationCanceledException)
            {
                _status.Text = "Experimento cancelado por el usuario.";
            }
            catch (OutOfMemoryException)
            {
                ShowWarning("Memoria insuficiente. Reduzca N o el número de consultas.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "El experimento falló:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                SetBusy(false);
            }
        }

        private void OnExportClick(object sender, EventArgs e)
        {
            if (_lastRows.Count == 0)
            {
                ShowWarning("No hay resultados que exportar.");
                return;
            }
            using var dialog = new SaveFileDialog
            {
                Filter = "Valores separados por comas (*.csv)|*.csv",
                FileName = "resultados_programa1.csv"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                ExperimentRunner.ExportCsv(_lastRows, dialog.FileName);
                _status.Text = "Resultados exportados a " + dialog.FileName;
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                ShowWarning("No se pudo guardar el archivo:\n" + ex.Message);
            }
        }

        // ───────────────────────────── Auxiliares ─────────────────────────────

        private ulong ReadSeed()
        {
            string text = _txtSeed.Text.Trim();
            if (!ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed))
                throw new FormatException("La semilla debe ser un entero sin signo (0 a 18 446 744 073 709 551 615).");
            return seed;
        }

        private static void FillPreview(ListBox list, int[] values)
        {
            int count = Math.Min(PreviewLimit, values.Length);
            var items = new object[count];
            for (int i = 0; i < count; i++)
                items[i] = string.Format(CultureInfo.CurrentCulture, "[{0,6}]  {1:N0}", i, values[i]);
            list.BeginUpdate();
            list.Items.Clear();
            list.Items.AddRange(items);
            list.EndUpdate();
        }

        private void FillExperimentGrid(IEnumerable<ExperimentRow> rows)
        {
            var c = CultureInfo.CurrentCulture;
            _gridExp.Rows.Clear();
            foreach (ExperimentRow r in rows)
            {
                _gridExp.Rows.Add(r.N.ToString("N0", c), r.Technique, r.Case,
                    r.AvgComparisons.ToString("N2", c), r.MinComparisons.ToString("N0", c), r.MaxComparisons.ToString("N0", c),
                    r.AvgIterations.ToString("N2", c), r.MaxIterations.ToString("N0", c),
                    r.TheoreticalComparisons.ToString("N2", c), r.AvgMicroseconds.ToString("N3", c), r.Notes);
            }
        }

        private void RefreshChart()
        {
            if (_lastRows.Count == 0)
            {
                _chart.Clear();
                return;
            }
            string caseName = (string)_cmbCase.SelectedItem;
            int metric = _cmbMetric.SelectedIndex;
            List<int> sizes = _lastRows.Select(r => r.N).Distinct().OrderBy(n => n).ToList();

            var values = new double[TechniqueOrder.Length, sizes.Count];
            for (int s = 0; s < TechniqueOrder.Length; s++)
            {
                for (int g = 0; g < sizes.Count; g++)
                {
                    ExperimentRow row = _lastRows.FirstOrDefault(r => r.N == sizes[g] && r.Technique == TechniqueOrder[s] && r.Case == caseName);
                    if (row == null) continue;
                    values[s, g] = metric == 0 ? row.AvgComparisons : metric == 1 ? row.AvgIterations : row.MaxComparisons;
                }
            }
            string[] groups = sizes.Select(n => "N = " + n.ToString("N0", CultureInfo.CurrentCulture)).ToArray();
            _chart.SetData(groups, TechniqueOrder, values, $"{_cmbMetric.SelectedItem} · claves {caseName.ToLowerInvariant()}s", "escala logarítmica");
        }

        private void SetBusy(bool busy)
        {
            _btnRun.Enabled = !busy;
            _btnCancel.Enabled = busy;
            _btnGenerate.Enabled = !busy;
            _chkSizes.Enabled = !busy;
            _nudQueries.Enabled = !busy;
            _txtSeed.Enabled = !busy;
            _progress.Visible = busy;
            if (busy) _btnExport.Enabled = false;
            else _btnExport.Enabled = _lastRows.Count > 0;
        }

        private void ShowWarning(string message)
        {
            MessageBox.Show(this, message, "Validación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
