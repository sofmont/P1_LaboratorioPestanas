using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace LaboratorioPestanas.UI
{
    /// <summary>
    /// Gráfica de barras agrupadas con eje Y logarítmico, dibujada con GDI+ (sin dependencias externas).
    /// La escala logarítmica es necesaria: O(n) y O(1) difieren en varios órdenes de magnitud.
    /// </summary>
    public sealed class BarChartPanel : Panel
    {
        private static readonly Color[] Palette =
        {
            Color.FromArgb(31, 119, 180),
            Color.FromArgb(230, 120, 20),
            Color.FromArgb(44, 160, 44),
            Color.FromArgb(150, 80, 190)
        };

        private IReadOnlyList<string> _groups = Array.Empty<string>();
        private IReadOnlyList<string> _series = Array.Empty<string>();
        private double[,] _values;
        private string _title = "Gráfica comparativa";
        private string _yLabel = string.Empty;

        public BarChartPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.White;
        }

        /// <param name="values">values[serie, grupo]</param>
        public void SetData(IReadOnlyList<string> groups, IReadOnlyList<string> series, double[,] values, string title, string yLabel)
        {
            if (groups == null) throw new ArgumentNullException(nameof(groups));
            if (series == null) throw new ArgumentNullException(nameof(series));
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.GetLength(0) != series.Count || values.GetLength(1) != groups.Count)
                throw new ArgumentException("Las dimensiones de la matriz no coinciden con series × grupos.");

            _groups = groups;
            _series = series;
            _values = values;
            _title = title ?? string.Empty;
            _yLabel = yLabel ?? string.Empty;
            Invalidate();
        }

        public void Clear()
        {
            _values = null;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var titleFont = new Font(Font.FontFamily, 11f, FontStyle.Bold);
            using var smallFont = new Font(Font.FontFamily, 8f);
            using var textBrush = new SolidBrush(Color.FromArgb(40, 40, 40));
            using var gridPen = new Pen(Color.FromArgb(225, 225, 225));
            using var axisPen = new Pen(Color.FromArgb(90, 90, 90));

            g.DrawString(_title, titleFont, textBrush, 10, 8);

            var plot = new Rectangle(80, 45, Width - 80 - 25, Height - 45 - 80);
            if (plot.Width < 60 || plot.Height < 60) return;

            if (_values == null || _groups.Count == 0)
            {
                g.DrawString("Ejecute un experimento para ver la gráfica.", Font, textBrush, plot.Left, plot.Top + 20);
                return;
            }

            double maxV = 0;
            foreach (double v in _values) if (v > maxV) maxV = v;
            int topExp = Math.Max(1, (int)Math.Ceiling(Math.Log10(Math.Max(maxV, 1))));
            const int bottomExp = -1;
            float Y(double v)
            {
                double lv = Math.Log10(Math.Max(v, 0.1));
                return plot.Bottom - (float)((lv - bottomExp) / (topExp - bottomExp) * plot.Height);
            }

            // Rejilla en potencias de 10.
            for (int ex = bottomExp; ex <= topExp; ex++)
            {
                double tick = Math.Pow(10, ex);
                float y = Y(tick);
                g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                string label = tick < 1 ? tick.ToString("0.0", CultureInfo.CurrentCulture) : tick.ToString("N0", CultureInfo.CurrentCulture);
                SizeF sz = g.MeasureString(label, smallFont);
                g.DrawString(label, smallFont, textBrush, plot.Left - sz.Width - 4, y - sz.Height / 2);
            }
            g.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
            g.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);

            // Barras.
            float groupWidth = (float)plot.Width / _groups.Count;
            float barWidth = groupWidth * 0.8f / Math.Max(1, _series.Count);
            for (int gi = 0; gi < _groups.Count; gi++)
            {
                float gx = plot.Left + gi * groupWidth + groupWidth * 0.1f;
                for (int si = 0; si < _series.Count; si++)
                {
                    double v = _values[si, gi];
                    float top = Y(v);
                    var rect = new RectangleF(gx + si * barWidth + 1, top, barWidth - 2, plot.Bottom - top);
                    using (var b = new SolidBrush(Palette[si % Palette.Length]))
                        g.FillRectangle(b, rect);

                    string vText = v >= 100 ? v.ToString("N0", CultureInfo.CurrentCulture) : v.ToString("0.00", CultureInfo.CurrentCulture);
                    SizeF vs = g.MeasureString(vText, smallFont);
                    g.DrawString(vText, smallFont, textBrush, rect.Left + (rect.Width - vs.Width) / 2, top - vs.Height - 1);
                }
                string gl = _groups[gi];
                SizeF gs = g.MeasureString(gl, Font);
                g.DrawString(gl, Font, textBrush, plot.Left + gi * groupWidth + (groupWidth - gs.Width) / 2, plot.Bottom + 6);
            }

            // Leyenda.
            float lx = plot.Left;
            float ly = plot.Bottom + 34;
            for (int si = 0; si < _series.Count; si++)
            {
                using (var b = new SolidBrush(Palette[si % Palette.Length]))
                    g.FillRectangle(b, lx, ly + 2, 12, 12);
                g.DrawString(_series[si], Font, textBrush, lx + 16, ly);
                lx += 16 + g.MeasureString(_series[si], Font).Width + 24;
            }

            // Etiqueta vertical del eje Y.
            GraphicsState state = g.Save();
            g.TranslateTransform(14, plot.Top + plot.Height / 2f);
            g.RotateTransform(-90);
            SizeF ys = g.MeasureString(_yLabel, smallFont);
            g.DrawString(_yLabel, smallFont, textBrush, -ys.Width / 2, 0);
            g.Restore(state);
        }
    }
}
