// EXPORTACIÓN DE LA GRILLA (Guardar como...)
using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Globalization;

namespace Proyecto_ReconocimientoFacial_0._1
{
    public partial class Form1
    {
        private Button btnGuardarComo;

        // Se llama desde ConstruirInterfaz() (Form1_Diseno.cs)
        private void ConfigurarExportacion()
        {
            btnGuardarComo = new Button();
            btnGuardarComo.Name = "btnGuardarComo";
            btnGuardarComo.Text = "Guardar como...";
            btnGuardarComo.Click += (s, e) => ExportarGrilla(); // abre directo el cuadro de Windows
        }

        // ---------------------------------------------------------------------------
        // EXPORTACIÓN GENÉRICA (la usan la grilla de asistencia y el panel de horarios)
        // ---------------------------------------------------------------------------
        // Mismo orden que las entradas de FILTRO_EXPORTACION (FilterIndex empieza en 1)
        private static readonly string[] FORMATOS_EXPORTACION = { "pdf", "csv", "json", "xml", "html", "txt" };

        private const string FILTRO_EXPORTACION =
            "PDF (*.pdf)|*.pdf" +
            "|CSV - separado por comas (*.csv)|*.csv" +
            "|JSON (*.json)|*.json" +
            "|XML (*.xml)|*.xml" +
            "|Página web (*.html)|*.html" +
            "|Texto tabulado (*.txt)|*.txt";

        private const char SEPARADOR_CSV = ';';

        // true: la primera línea del CSV es "sep=," para que Excel en español (que espera ';') lo separe en columnas.
        // false: CSV puro, sin esa línea (para programas que no la entienden, por ejemplo pandas).
        private static readonly bool CSV_FORZAR_EXCEL = true;

        private void ExportarGrilla()
        {
            DataTable tabla = dgvAlumnos.DataSource as DataTable;
            if (tabla == null || tabla.Rows.Count == 0)
            {
                MessageBox.Show("No hay datos en la grilla para exportar.", "Guardar como...",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ExportarTabla(tabla, "Asistencia", "FaceTrack Attendance - Registro de asistencia", "Alumnos");
        }

        // Decide el formato: 1º la extensión REAL del nombre; 2º, si no tiene (o no es soportada), el "Tipo:" elegido.
        private static string ResolverFormatoExportacion(string rutaElegida, int indiceFiltro, out string rutaFinal)
        {
            string formato = null;
            switch (Path.GetExtension(rutaElegida).ToLowerInvariant())
            {
                case ".pdf": formato = "pdf"; break;
                case ".csv": formato = "csv"; break;
                case ".json": formato = "json"; break;
                case ".xml": formato = "xml"; break;
                case ".html":
                case ".htm": formato = "html"; break;
                case ".txt": formato = "txt"; break;
            }

            if (formato != null)
            {
                rutaFinal = rutaElegida; // la extensión escrita queda tal cual y el contenido coincide con ella
                return formato;
            }

            formato = FORMATOS_EXPORTACION[Math.Max(0, indiceFiltro - 1)];
            rutaFinal = rutaElegida + "." + formato; // sin extensión válida: se agrega la del tipo elegido
            return formato;
        }

        private void ExportarTabla(DataTable tabla, string prefijoArchivo, string tituloDocumento, string nombreXml)
        {
            using (SaveFileDialog dialogo = new SaveFileDialog())
            {
                dialogo.Title = "Guardar como...";
                dialogo.Filter = FILTRO_EXPORTACION;
                dialogo.FilterIndex = 1;       // PDF seleccionado por defecto
                dialogo.DefaultExt = "pdf";
                dialogo.AddExtension = true;
                dialogo.FileName = $"{prefijoArchivo}_{DateTime.Now:yyyyMMdd_HHmm}";
                dialogo.OverwritePrompt = true;
                dialogo.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (dialogo.ShowDialog(this) != DialogResult.OK) return;

                string ruta;
                string formato = ResolverFormatoExportacion(dialogo.FileName, dialogo.FilterIndex, out ruta);

                // Si agregamos la extensión nosotros, Windows no pudo avisar de una posible sobrescritura
                if (ruta != dialogo.FileName && File.Exists(ruta) &&
                    MessageBox.Show(this, $"El archivo ya existe:\n{ruta}\n\n¿Querés reemplazarlo?", "Guardar como...",
                                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                try
                {
                    switch (formato)
                    {
                        case "csv":
                            string cabecera = CSV_FORZAR_EXCEL ? "sep=" + SEPARADOR_CSV + Environment.NewLine : "";
                            File.WriteAllText(ruta, cabecera + ATexto(tabla, SEPARADOR_CSV, true), new UTF8Encoding(true));
                            break;
                        case "txt": File.WriteAllText(ruta, ATexto(tabla, '\t', false), new UTF8Encoding(true)); break;
                        case "json": File.WriteAllText(ruta, AJson(tabla), new UTF8Encoding(false)); break;
                        case "html": File.WriteAllText(ruta, AHtml(tabla, tituloDocumento), new UTF8Encoding(false)); break;
                        case "pdf": GuardarPdf(tabla, ruta, tituloDocumento); break;
                        case "xml":
                            DataTable copia = tabla.Copy();
                            copia.TableName = nombreXml;
                            copia.WriteXml(ruta, XmlWriteMode.IgnoreSchema);
                            break;
                    }

                    LogMensaje($"[OK] Exportado ({formato.ToUpper()}) a: {ruta}");
                    MessageBox.Show($"Archivo guardado correctamente en:\n{ruta}", "Guardar como...",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    LogMensaje($"[ERROR] No se pudo exportar: {ex.Message}");
                    MessageBox.Show($"No se pudo guardar el archivo:\n{ex.Message}", "Guardar como...",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string Celda(object valor)
        {
            if (valor == null || valor == DBNull.Value) return "";
            if (valor is DateTime fecha) return fecha.ToString("yyyy-MM-dd HH:mm:ss");
            return valor.ToString();
        }

        // CSV (separador ';' porque Excel en español lo espera así) o TXT (separador tab)
        private static string ATexto(DataTable tabla, char separador, bool entrecomillar)
        {
            StringBuilder sb = new StringBuilder();

            for (int c = 0; c < tabla.Columns.Count; c++)
            {
                if (c > 0) sb.Append(separador);
                sb.Append(entrecomillar ? Comillas(tabla.Columns[c].ColumnName) : tabla.Columns[c].ColumnName);
            }
            sb.AppendLine();

            foreach (DataRow fila in tabla.Rows)
            {
                for (int c = 0; c < tabla.Columns.Count; c++)
                {
                    if (c > 0) sb.Append(separador);
                    string texto = Celda(fila[c]);
                    sb.Append(entrecomillar ? Comillas(texto) : texto.Replace('\t', ' '));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string Comillas(string texto)
        {
            return "\"" + texto.Replace("\"", "\"\"") + "\"";
        }

        private static string AJson(DataTable tabla)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[");
            for (int f = 0; f < tabla.Rows.Count; f++)
            {
                DataRow fila = tabla.Rows[f];
                sb.Append("  {");
                for (int c = 0; c < tabla.Columns.Count; c++)
                {
                    if (c > 0) sb.Append(", ");
                    sb.Append('"').Append(EscaparJson(tabla.Columns[c].ColumnName)).Append("\": ");

                    object v = fila[c];
                    if (v == null || v == DBNull.Value) sb.Append("null");
                    else if (v is int || v is long) sb.Append(v);
                    else sb.Append('"').Append(EscaparJson(Celda(v))).Append('"');
                }
                sb.Append("}").AppendLine(f < tabla.Rows.Count - 1 ? "," : "");
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static string EscaparJson(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < ' ') sb.Append("\\u" + ((int)ch).ToString("x4"));
                        else sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }

        // Escribe un PDF a mano (sin librerías): A4 horizontal, fuente Helvetica, tabla con paginado.
        private static void GuardarPdf(DataTable tabla, string ruta, string titulo)
        {
            CultureInfo inv = CultureInfo.InvariantCulture; // los decimales del PDF deben usar punto, no coma
            const float ancho = 842f, alto = 595f, margen = 36f;
            const float tamFuente = 9f, altoFila = 18f, anchoCaracter = 4.95f;

            // Ancho de cada columna proporcional al largo de su contenido
            int nCols = tabla.Columns.Count;
            int[] pesos = new int[nCols];
            for (int c = 0; c < nCols; c++)
            {
                int max = tabla.Columns[c].ColumnName.Length;
                foreach (DataRow fila in tabla.Rows) max = Math.Max(max, Celda(fila[c]).Length);
                pesos[c] = Math.Max(6, Math.Min(max, 40)) + 2;
            }
            float usable = ancho - 2 * margen;
            float sumaPesos = 0; foreach (int p in pesos) sumaPesos += p;
            float[] anchos = new float[nCols];
            int[] maxChars = new int[nCols];
            for (int c = 0; c < nCols; c++)
            {
                anchos[c] = usable * pesos[c] / sumaPesos;
                maxChars[c] = Math.Max(3, (int)((anchos[c] - 8f) / anchoCaracter));
            }

            int filasPorPagina = (int)((alto - 2 * margen - 45f - altoFila) / altoFila);
            int totalPaginas = Math.Max(1, (int)Math.Ceiling(tabla.Rows.Count / (double)filasPorPagina));

            // Contenido (dibujo) de cada página
            List<string> contenidos = new List<string>();
            for (int pag = 0; pag < totalPaginas; pag++)
            {
                StringBuilder s = new StringBuilder();
                s.Append($"0 g BT /F2 14 Tf {margen.ToString(inv)} {(alto - margen - 14).ToString(inv)} Td ({PdfTexto(titulo, 90)}) Tj ET\n");
                s.Append($"0.4 g BT /F1 8 Tf {margen.ToString(inv)} {(alto - margen - 28).ToString(inv)} Td ({PdfTexto($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm}   |   Página {pag + 1} de {totalPaginas}   |   Total: {tabla.Rows.Count}", 120)}) Tj ET\n");

                float yTop = alto - margen - 45f;

                // Encabezado
                float x = margen;
                for (int c = 0; c < nCols; c++)
                {
                    float yFila = yTop - altoFila;
                    s.Append($"0.13 g {x.ToString(inv)} {yFila.ToString(inv)} {anchos[c].ToString(inv)} {altoFila.ToString(inv)} re f\n");
                    s.Append($"1 g BT /F2 {tamFuente.ToString(inv)} Tf {(x + 4).ToString(inv)} {(yFila + 5.5f).ToString(inv)} Td ({PdfTexto(tabla.Columns[c].ColumnName, maxChars[c])}) Tj ET\n");
                    x += anchos[c];
                }

                // Filas de esta página
                int desde = pag * filasPorPagina;
                int hasta = Math.Min(tabla.Rows.Count, desde + filasPorPagina);
                for (int r = desde; r < hasta; r++)
                {
                    float yFila = yTop - altoFila * (r - desde + 2);
                    x = margen;
                    for (int c = 0; c < nCols; c++)
                    {
                        string fondo = (r % 2 == 0) ? "0.96 g" : "1 g";
                        s.Append($"{fondo} {x.ToString(inv)} {yFila.ToString(inv)} {anchos[c].ToString(inv)} {altoFila.ToString(inv)} re f\n");
                        s.Append($"0.7 G 0.5 w {x.ToString(inv)} {yFila.ToString(inv)} {anchos[c].ToString(inv)} {altoFila.ToString(inv)} re S\n");
                        s.Append($"0 g BT /F1 {tamFuente.ToString(inv)} Tf {(x + 4).ToString(inv)} {(yFila + 5.5f).ToString(inv)} Td ({PdfTexto(Celda(tabla.Rows[r][c]), maxChars[c])}) Tj ET\n");
                        x += anchos[c];
                    }
                }
                contenidos.Add(s.ToString());
            }

            // Armado del archivo: 1 catálogo, 2 páginas, 3 y 4 fuentes, luego (página, contenido) por cada hoja
            StringBuilder pdf = new StringBuilder();
            List<int> offsets = new List<int>();
            pdf.Append("%PDF-1.4\n");

            Action<string> objeto = cuerpo =>
            {
                offsets.Add(pdf.Length);
                pdf.Append($"{offsets.Count} 0 obj\n{cuerpo}\nendobj\n");
            };

            StringBuilder kids = new StringBuilder();
            for (int i = 0; i < totalPaginas; i++) kids.Append($"{5 + 2 * i} 0 R ");

            objeto("<< /Type /Catalog /Pages 2 0 R >>");
            objeto($"<< /Type /Pages /Kids [{kids}] /Count {totalPaginas} >>");
            objeto("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
            objeto("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

            for (int i = 0; i < totalPaginas; i++)
            {
                objeto($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {ancho.ToString(inv)} {alto.ToString(inv)}] " +
                       $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {6 + 2 * i} 0 R >>");
                objeto($"<< /Length {contenidos[i].Length} >>\nstream\n{contenidos[i]}\nendstream");
            }

            int posXref = pdf.Length;
            pdf.Append($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
            foreach (int off in offsets) pdf.Append(off.ToString("D10") + " 00000 n \n");
            pdf.Append($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{posXref}\n%%EOF");

            // Todos los caracteres son <= 255, así que 1 char = 1 byte en Latin-1 y los offsets coinciden
            File.WriteAllBytes(ruta, Encoding.GetEncoding("iso-8859-1").GetBytes(pdf.ToString()));
        }

        // Prepara un texto para ir dentro de un string PDF: recorta, escapa \ ( ) y reemplaza lo que no entra en Latin-1
        private static string PdfTexto(string texto, int maxCaracteres)
        {
            if (texto == null) texto = "";
            texto = texto.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            if (texto.Length > maxCaracteres)
                texto = texto.Substring(0, Math.Max(1, maxCaracteres - 3)) + "...";

            StringBuilder sb = new StringBuilder();
            foreach (char ch in texto)
            {
                if (ch == '\\' || ch == '(' || ch == ')') sb.Append('\\').Append(ch);
                else if (ch < 32 || ch > 255 || (ch >= 127 && ch < 160)) sb.Append('?');
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        private static string AHtml(DataTable tabla, string titulo)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>" + WebUtility.HtmlEncode(titulo) + "</title>");
            sb.AppendLine("<style>body{font-family:Segoe UI,Arial;margin:24px}table{border-collapse:collapse}" +
                          "th,td{border:1px solid #999;padding:6px 12px;text-align:left}th{background:#222;color:#fff}</style>");
            sb.AppendLine("</head><body><h2>" + WebUtility.HtmlEncode(titulo) + "</h2><table><tr>");
            foreach (DataColumn col in tabla.Columns)
                sb.Append("<th>").Append(WebUtility.HtmlEncode(col.ColumnName)).Append("</th>");
            sb.AppendLine("</tr>");
            foreach (DataRow fila in tabla.Rows)
            {
                sb.Append("<tr>");
                for (int c = 0; c < tabla.Columns.Count; c++)
                    sb.Append("<td>").Append(WebUtility.HtmlEncode(Celda(fila[c]))).Append("</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table></body></html>");
            return sb.ToString();
        }
    }


}