using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace Proyecto_ReconocimientoFacial_0._1
{
    public static class Estetica
    {
        // ---- Paleta de colores (Dark Synthwave / Cyberpunk Soft) ----
        public static readonly Color ColorFondo = Color.FromArgb(32, 18, 48);          // Púrpura oscuro profundo
        public static readonly Color ColorPanel = Color.FromArgb(52, 28, 72);          // Púrpura medio para contenedores
        public static readonly Color ColorPrimario = Color.FromArgb(88, 42, 114);      // Púrpura vivo
        public static readonly Color ColorAcento = Color.FromArgb(240, 140, 110);      // Coral / Salmón
        public static readonly Color ColorBotonSecundario = Color.FromArgb(120, 80, 150); // Violeta suave
        public static readonly Color ColorTextoClaro = Color.FromArgb(250, 235, 220);   // Crema claro
        public static readonly Color ColorPeligro = Color.FromArgb(210, 70, 70);       // Rojo coral
        public static readonly Color ColorExito = Color.FromArgb(60, 190, 110);        // Verde brillante
        public static readonly Color ColorConsolaFondo = Color.FromArgb(22, 12, 34);   // Púrpura casi negro
        public static readonly Color ColorConsolaTexto = Color.FromArgb(255, 180, 140); // Crema/Naranja consola
        public static readonly Color ColorBorde = Color.FromArgb(120, 70, 150);        // Borde de paneles y pestañas

        // ------------------------------------------------------------------
        // ESTILO GENERAL (se aplica recorriendo TODOS los controles, también los anidados)
        // ------------------------------------------------------------------
        public static void AplicarEstilo(Form formulario)
        {
            formulario.BackColor = ColorFondo;
            formulario.Font = new Font("Segoe UI", 9f);
            AplicarEstiloRecursivo(formulario.Controls);
        }

        // IMPORTANTE: en este switch los tipos más específicos van ANTES que los generales
        // (PanelRedondeado y TableLayoutPanel antes que Panel), si no el compilador da error CS8120.
        private static void AplicarEstiloRecursivo(Control.ControlCollection controles)
        {
            foreach (Control control in controles)
            {
                switch (control)
                {
                    case Button boton:
                        EstilizarBoton(boton);
                        break;

                    case TextBox caja:
                        EstilizarCaja(caja);
                        break;

                    case DataGridView grilla:
                        EstilizarGrilla(grilla);
                        break;

                    case Label etiqueta:
                        etiqueta.ForeColor = ColorTextoClaro;
                        break;

                    case TabControl tab:
                        tab.BackColor = ColorPanel;
                        foreach (TabPage page in tab.TabPages)
                        {
                            page.BackColor = ColorConsolaFondo;
                            page.ForeColor = ColorTextoClaro;
                            AplicarEstiloRecursivo(page.Controls);
                        }
                        break;

                    case PanelRedondeado redondeado:
                        redondeado.BackColor = ColorPanel;
                        AplicarEstiloRecursivo(redondeado.Controls);
                        break;

                    case TableLayoutPanel tabla:
                        // Dentro de un panel redondeado hereda su color; los de más afuera usan el fondo general
                        tabla.BackColor = (tabla.Parent is PanelRedondeado) ? ColorPanel : ColorFondo;
                        AplicarEstiloRecursivo(tabla.Controls);
                        break;

                    case Panel panelHijo:
                        panelHijo.BackColor = (panelHijo.Name == "panelTemperatura") ? ColorConsolaFondo : ColorPanel;
                        AplicarEstiloRecursivo(panelHijo.Controls);
                        break;
                }
            }
        }

        private static void EstilizarBoton(Button boton)
        {
            bool esPeligroso = boton.Name == "btnBorrarTodo" || boton.Name == "btnEliminarSeleccionado";
            bool esPrincipal = boton.Name == "btnRegistrar";

            boton.FlatStyle = FlatStyle.Flat;
            boton.FlatAppearance.BorderSize = 0;
            boton.UseVisualStyleBackColor = false;

            if (esPeligroso)
            {
                boton.BackColor = ColorPeligro;
                boton.ForeColor = ColorTextoClaro;
            }
            else if (esPrincipal)
            {
                boton.BackColor = ColorAcento;
                boton.ForeColor = ColorFondo; // texto oscuro sobre botón claro
            }
            else
            {
                boton.BackColor = ColorBotonSecundario;
                boton.ForeColor = ColorTextoClaro;
            }

            boton.FlatAppearance.MouseOverBackColor = ControlPaint.Light(boton.BackColor, 0.2f);
            boton.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(boton.BackColor, 0.1f);
            boton.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            boton.Cursor = Cursors.Hand;
        }

        private static void EstilizarCaja(TextBox caja)
        {
            caja.BackColor = ColorConsolaFondo;

            if (caja.Multiline)
            {
                // Cajas de logs / métricas: sin borde (el borde lo pone la pestaña o el panel)
                caja.BorderStyle = BorderStyle.None;
                caja.ForeColor = ColorConsolaTexto;
                caja.Font = new Font("Consolas", 9.5f);
            }
            else
            {
                // Caja de búsqueda
                caja.BorderStyle = BorderStyle.FixedSingle;
                caja.ForeColor = ColorTextoClaro;
                caja.Font = new Font("Segoe UI", 10f);
            }

            // textBox1 muestra las MÉTRICAS EN VIVO: texto grande y con el color de acento
            if (caja.Name == "textBox1")
            {
                caja.ForeColor = ColorAcento;
                caja.Font = new Font("Consolas", 12f, FontStyle.Bold);
            }
        }

        private static void EstilizarGrilla(DataGridView grilla)
        {
            grilla.BackgroundColor = ColorConsolaFondo;
            grilla.BorderStyle = BorderStyle.None;
            grilla.GridColor = ColorPanel;
            grilla.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grilla.EnableHeadersVisualStyles = false;
            grilla.ColumnHeadersDefaultCellStyle.BackColor = ColorPrimario;
            grilla.ColumnHeadersDefaultCellStyle.ForeColor = ColorTextoClaro;
            grilla.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            grilla.DefaultCellStyle.BackColor = ColorConsolaFondo;
            grilla.DefaultCellStyle.ForeColor = ColorTextoClaro;
            grilla.DefaultCellStyle.SelectionBackColor = ColorAcento;
            grilla.DefaultCellStyle.SelectionForeColor = ColorFondo;
            grilla.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(38, 22, 56);
            grilla.RowHeadersVisible = false;
        }

        public static void EstilizarIndicadorConexion(Label indicador)
        {
            indicador.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            indicador.Text = "●";
        }

        // ------------------------------------------------------------------
        // GRÁFICO DE TEMPERATURA (sin cambios respecto a tu versión)
        // ------------------------------------------------------------------
        public static void DibujarGraficoTemperatura(Graphics g, Rectangle area,
                                                      IReadOnlyList<(DateTime Hora, double Temp)> datos)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(ColorConsolaFondo);

            if (datos.Count < 2)
            {
                using (Font f = new Font("Segoe UI", 9f))
                using (Brush b = new SolidBrush(ColorTextoClaro))
                    g.DrawString("Esperando lecturas de temperatura...", f, b, 10, 10);
                return;
            }

            const int margenIzq = 34, margenInf = 20, margenSup = 12, margenDer = 14;
            Rectangle grafico = new Rectangle(area.Left + margenIzq, area.Top + margenSup,
                                              area.Width - margenIzq - margenDer, area.Height - margenSup - margenInf);
            if (grafico.Width <= 0 || grafico.Height <= 0) return;

            int tempMin = (int)Math.Floor(datos.Min(d => d.Temp)) - 1;
            int tempMax = (int)Math.Ceiling(datos.Max(d => d.Temp)) + 1;
            if (tempMax - tempMin < 4) tempMax = tempMin + 4;

            int pasoY = Math.Max(1, (int)Math.Ceiling((tempMax - tempMin) / 5.0));

            using (Font fEjes = new Font("Segoe UI", 7.5f))
            using (Brush bEjes = new SolidBrush(ColorTextoClaro))
            using (Pen gridPen = new Pen(Color.FromArgb(50, 30, 70)))
            using (Pen ejePen = new Pen(ColorPrimario))
            {
                for (int t = tempMin; t <= tempMax; t += pasoY)
                {
                    float y = grafico.Bottom - (float)(t - tempMin) / (tempMax - tempMin) * grafico.Height;
                    g.DrawLine(gridPen, grafico.Left, y, grafico.Right, y);

                    string etiqueta = t.ToString();
                    SizeF tamEtiqueta = g.MeasureString(etiqueta, fEjes);
                    g.DrawString(etiqueta, fEjes, bEjes, grafico.Left - tamEtiqueta.Width - 4, y - tamEtiqueta.Height / 2);
                }

                const int divisionesX = 4;
                for (int i = 0; i <= divisionesX; i++)
                {
                    int indiceDato = (int)Math.Round(i * (datos.Count - 1) / (double)divisionesX);
                    float x = grafico.Left + (float)i / divisionesX * grafico.Width;
                    g.DrawLine(gridPen, x, grafico.Top, x, grafico.Bottom);

                    string etiquetaHora = datos[indiceDato].Hora.ToString("HH:mm:ss");
                    SizeF tamHora = g.MeasureString(etiquetaHora, fEjes);
                    float xTexto = Math.Min(Math.Max(x - tamHora.Width / 2, grafico.Left), grafico.Right - tamHora.Width);
                    g.DrawString(etiquetaHora, fEjes, bEjes, xTexto, grafico.Bottom + 3);
                }

                g.DrawLine(ejePen, grafico.Left, grafico.Top, grafico.Left, grafico.Bottom);
                g.DrawLine(ejePen, grafico.Left, grafico.Bottom, grafico.Right, grafico.Bottom);
            }

            PointF[] puntos = new PointF[datos.Count];
            for (int i = 0; i < datos.Count; i++)
            {
                float x = grafico.Left + (float)i / (datos.Count - 1) * grafico.Width;
                float y = grafico.Bottom - (float)(datos[i].Temp - tempMin) / (tempMax - tempMin) * grafico.Height;
                puntos[i] = new PointF(x, y);
            }

            using (Pen lineaPen = new Pen(ColorAcento, 2.5f))
                g.DrawLines(lineaPen, puntos);

            PointF ultimoPunto = puntos[puntos.Length - 1];
            using (Brush puntoBrush = new SolidBrush(ColorPeligro))
                g.FillEllipse(puntoBrush, ultimoPunto.X - 4, ultimoPunto.Y - 4, 8, 8);

            using (Font fActual = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (Brush bActual = new SolidBrush(ColorAcento))
            {
                string texto = $"{datos[datos.Count - 1].Temp:F1} °C";
                SizeF tam = g.MeasureString(texto, fActual);
                g.DrawString(texto, fActual, bActual, grafico.Right - tam.Width, area.Top - 2);
            }
        }
    }

    // ----------------------------------------------------------------------
    // PANEL REDONDEADO CON TÍTULO
    // ----------------------------------------------------------------------
    // Se usa desde Form1.Diseno.cs. Si se le asigna Titulo, reserva espacio arriba y lo dibuja centrado.
    // Los controles que se agreguen adentro con Dock = Fill respetan ese espacio (Padding).
    public class PanelRedondeado : Panel
    {
        private string titulo = "";

        public int Radio { get; set; } = 14;

        public string Titulo
        {
            get { return titulo; }
            set
            {
                titulo = value ?? "";
                Padding = titulo.Length > 0 ? new Padding(12, 38, 12, 12) : new Padding(12);
                Invalidate();
            }
        }

        public PanelRedondeado()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            Dock = DockStyle.Fill;
            Margin = new Padding(6);
            Padding = new Padding(12);
            BackColor = Estetica.ColorPanel; // los controles hijos (labels) heredan este color
        }

        // Todo se pinta en OnPaint (así las esquinas redondeadas muestran el color del contenedor)
        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Estetica.ColorFondo);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Rectangle r = new Rectangle(1, 1, Width - 3, Height - 3);
            if (r.Width <= 0 || r.Height <= 0) return;

            using (GraphicsPath ruta = RutaRedondeada(r, Radio))
            using (Brush fondo = new SolidBrush(Estetica.ColorPanel))
            using (Pen borde = new Pen(Estetica.ColorBorde, 1.5f))
            {
                g.FillPath(fondo, ruta);
                g.DrawPath(borde, ruta);
            }

            if (titulo.Length > 0)
            {
                using (Font fuente = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (Brush pincel = new SolidBrush(Estetica.ColorTextoClaro))
                using (StringFormat formato = new StringFormat())
                {
                    formato.Alignment = StringAlignment.Center;
                    formato.LineAlignment = StringAlignment.Center;
                    formato.Trimming = StringTrimming.EllipsisCharacter;
                    formato.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(titulo, fuente, pincel, new RectangleF(12, 6, Math.Max(10, Width - 24), 28), formato);
                }
            }
        }

        private static GraphicsPath RutaRedondeada(Rectangle r, int radio)
        {
            int d = Math.Max(2, radio * 2);
            GraphicsPath ruta = new GraphicsPath();
            ruta.AddArc(r.X, r.Y, d, d, 180, 90);
            ruta.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            ruta.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            ruta.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            ruta.CloseFigure();
            return ruta;
        }
    }

    // ----------------------------------------------------------------------
    // TABCONTROL OSCURO
    // ----------------------------------------------------------------------
    // El TabControl normal de Windows no se puede pintar de oscuro (queda una franja blanca).
    // Este dibuja él mismo la barra de pestañas y el borde, con la paleta de Estetica.
    public class TabControlOscuro : TabControl
    {
        public TabControlOscuro()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            Dock = DockStyle.Fill;
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold); // se usa también para medir las pestañas
            Padding = new Point(10, 5);
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Estetica.ColorPanel);

            // Borde alrededor del área de contenido de las páginas
            Rectangle pagina = DisplayRectangle;
            pagina.Inflate(2, 2);
            using (Pen borde = new Pen(Estetica.ColorBorde))
                g.DrawRectangle(borde, pagina);

            using (StringFormat formato = new StringFormat())
            {
                formato.Alignment = StringAlignment.Center;
                formato.LineAlignment = StringAlignment.Center;
                formato.FormatFlags = StringFormatFlags.NoWrap;

                for (int i = 0; i < TabCount; i++)
                {
                    Rectangle r = GetTabRect(i);
                    bool seleccionada = (i == SelectedIndex);

                    using (Brush fondo = new SolidBrush(seleccionada ? Estetica.ColorConsolaFondo : Estetica.ColorPrimario))
                        g.FillRectangle(fondo, r);

                    if (seleccionada)
                    {
                        using (Pen acento = new Pen(Estetica.ColorAcento, 2f))
                            g.DrawLine(acento, r.Left, r.Top + 1, r.Right, r.Top + 1);
                    }

                    using (Brush texto = new SolidBrush(seleccionada ? Estetica.ColorAcento : Estetica.ColorTextoClaro))
                        g.DrawString(TabPages[i].Text, Font, texto, r, formato);
                }
            }
        }
    }
}