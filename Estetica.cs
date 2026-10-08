// ESTÉTICA - Todo lo puramente visual: paleta de colores, estilo de controles y dibujo de gráficos.
//
// Esta clase NO sabe nada de cámara, SQL, hilos ni reconocimiento facial. Solo recibe controles
// o datos ya calculados y los pinta. Form1 sigue siendo dueño de toda la lógica de conexión IP,
// transmisión del stream y los datos (incluido el historial de temperatura) — acá solo se dibuja.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Proyecto_ReconocimientoFacial_0._1
{
    public static class Estetica
    {
        // ---- Paleta ----
        public static readonly Color ColorFondo = Color.FromArgb(244, 246, 248);
        public static readonly Color ColorPrimario = Color.FromArgb(44, 62, 80);      // azul slate oscuro
        public static readonly Color ColorAcento = Color.FromArgb(41, 128, 185);      // azul medio
        public static readonly Color ColorPeligro = Color.FromArgb(192, 57, 43);      // rojo apagado
        public static readonly Color ColorExito = Color.FromArgb(39, 174, 96);        // verde
        public static readonly Color ColorConsolaFondo = Color.FromArgb(30, 30, 30);
        public static readonly Color ColorConsolaTexto = Color.FromArgb(212, 212, 212);

        // ------------------------------------------------------------------
        // ESTILO DE CONTROLES
        // ------------------------------------------------------------------

        // Aplica la paleta a todo el formulario, recorriendo los controles que ya están puestos
        // en el Designer (no hace falta tocarlos ahí a mano).
        public static void AplicarEstilo(Form formulario)
        {
            formulario.BackColor = ColorFondo;
            formulario.Font = new Font("Segoe UI", 9f);
            AplicarEstiloRecursivo(formulario.Controls);
        }

        private static void AplicarEstiloRecursivo(Control.ControlCollection controles)
        {
            foreach (Control control in controles)
            {
                switch (control)
                {
                    case Button boton:
                        bool esPeligroso = boton.Name == "btnBorrarTodo" || boton.Name == "btnEliminarSeleccionado";
                        boton.FlatStyle = FlatStyle.Flat;
                        boton.FlatAppearance.BorderSize = 0;
                        boton.BackColor = esPeligroso ? ColorPeligro : ColorAcento;
                        boton.ForeColor = Color.White;
                        boton.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
                        boton.Cursor = Cursors.Hand;
                        boton.Height = Math.Max(boton.Height, 32);
                        break;

                    case TextBox caja:
                        // Las cajas de texto grandes (estado, datos biométricos, log) se estilizan como
                        // "consola"; las de una sola línea (ej. txtBuscar) se dejan con look normal.
                        if (caja.Multiline)
                        {
                            caja.BackColor = ColorConsolaFondo;
                            caja.ForeColor = ColorConsolaTexto;
                            caja.Font = new Font("Consolas", 9f);
                            caja.BorderStyle = BorderStyle.FixedSingle;
                        }
                        break;

                    case DataGridView grilla:
                        grilla.BackgroundColor = Color.White;
                        grilla.BorderStyle = BorderStyle.None;
                        grilla.GridColor = Color.FromArgb(224, 224, 224);
                        grilla.EnableHeadersVisualStyles = false;
                        grilla.ColumnHeadersDefaultCellStyle.BackColor = ColorPrimario;
                        grilla.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                        grilla.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                        grilla.DefaultCellStyle.SelectionBackColor = ColorAcento;
                        grilla.DefaultCellStyle.SelectionForeColor = Color.White;
                        grilla.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
                        grilla.RowHeadersVisible = false;
                        break;

                    case Label etiqueta:
                        etiqueta.ForeColor = ColorPrimario;
                        break;

                    case Panel panelHijo:
                        AplicarEstiloRecursivo(panelHijo.Controls);
                        break;
                }
            }
        }

        // Dale formato de "puntito" grande al label que usás como indicador de conexión.
        // El color (verde/rojo) lo decide Form1 según si hay stream activo o no.
        public static void EstilizarIndicadorConexion(Label indicador)
        {
            indicador.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            indicador.Text = "●";
        }

        // ------------------------------------------------------------------
        // GRÁFICO DE TEMPERATURA
        // ------------------------------------------------------------------
        // Solo dibuja: recibe los datos ya armados (Form1 es quien los mide, guarda y limita el
        // historial). No hace red, no guarda estado propio.
        public static void DibujarGraficoTemperatura(Graphics g, Rectangle area,
                                                      IReadOnlyList<(DateTime Hora, double Temp)> datos)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            if (datos.Count < 2)
            {
                using (Font f = new Font("Segoe UI", 9f))
                using (Brush b = new SolidBrush(Color.Gray))
                    g.DrawString("Esperando lecturas de temperatura...", f, b, 10, 10);
                return;
            }

            const int margenIzq = 34, margenInf = 20, margenSup = 12, margenDer = 14;
            Rectangle grafico = new Rectangle(area.Left + margenIzq, area.Top + margenSup,
                                              area.Width - margenIzq - margenDer, area.Height - margenSup - margenInf);
            if (grafico.Width <= 0 || grafico.Height <= 0) return;

            // Rango del eje Y en números ENTEROS (redondeado hacia afuera para no cortar el trazo)
            int tempMin = (int)Math.Floor(datos.Min(d => d.Temp)) - 1;
            int tempMax = (int)Math.Ceiling(datos.Max(d => d.Temp)) + 1;
            if (tempMax - tempMin < 4) tempMax = tempMin + 4;

            // Paso entre líneas horizontales: siempre un entero, apuntando a ~5-6 líneas en total
            int pasoY = Math.Max(1, (int)Math.Ceiling((tempMax - tempMin) / 5.0));

            using (Font fEjes = new Font("Segoe UI", 7.5f))
            using (Brush bEjes = new SolidBrush(Color.DimGray))
            using (Pen gridPen = new Pen(Color.FromArgb(235, 235, 235)))
            using (Pen ejePen = new Pen(Color.Gainsboro))
            {
                // ---- Cuadrícula y etiquetas del eje Y (°C, enteros) ----
                for (int t = tempMin; t <= tempMax; t += pasoY)
                {
                    float y = grafico.Bottom - (float)(t - tempMin) / (tempMax - tempMin) * grafico.Height;
                    g.DrawLine(gridPen, grafico.Left, y, grafico.Right, y);

                    string etiqueta = t.ToString();
                    SizeF tamEtiqueta = g.MeasureString(etiqueta, fEjes);
                    g.DrawString(etiqueta, fEjes, bEjes, grafico.Left - tamEtiqueta.Width - 4, y - tamEtiqueta.Height / 2);
                }

                // ---- Cuadrícula y etiquetas del eje X (hora, en 5 marcas parejas) ----
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

                // ---- Ejes principales, más marcados que la cuadrícula ----
                g.DrawLine(ejePen, grafico.Left, grafico.Top, grafico.Left, grafico.Bottom);
                g.DrawLine(ejePen, grafico.Left, grafico.Bottom, grafico.Right, grafico.Bottom);
            }

            // ---- Línea de temperatura ----
            PointF[] puntos = new PointF[datos.Count];
            for (int i = 0; i < datos.Count; i++)
            {
                float x = grafico.Left + (float)i / (datos.Count - 1) * grafico.Width;
                float y = grafico.Bottom - (float)(datos[i].Temp - tempMin) / (tempMax - tempMin) * grafico.Height;
                puntos[i] = new PointF(x, y);
            }

            using (Pen lineaPen = new Pen(ColorAcento, 2f))
                g.DrawLines(lineaPen, puntos);

            PointF ultimoPunto = puntos[puntos.Length - 1];
            using (Brush puntoBrush = new SolidBrush(ColorPeligro))
                g.FillEllipse(puntoBrush, ultimoPunto.X - 3, ultimoPunto.Y - 3, 6, 6);

            using (Font fActual = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (Brush bActual = new SolidBrush(ColorPrimario))
            {
                string texto = $"{datos[datos.Count - 1].Temp:F1} °C";
                SizeF tam = g.MeasureString(texto, fActual);
                g.DrawString(texto, fActual, bActual, grafico.Right - tam.Width, area.Top - 2);
            }
        }
    }
}