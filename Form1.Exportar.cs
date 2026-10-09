// EXPORTACIÓN DE LA GRILLA (Guardar como...)
using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace Proyecto_ReconocimientoFacial_0._1
{
    public partial class Form1
    {
        private Button btnGuardarComo;
        private ContextMenuStrip menuExportar;

        // Se llama desde ConstruirInterfaz() (Form1.Diseno.cs)
        private void ConfigurarExportacion()
        {
            btnGuardarComo = new Button();
            btnGuardarComo.Name = "btnGuardarComo";
            btnGuardarComo.Text = "Guardar como...  ▾";

            menuExportar = new ContextMenuStrip();
            menuExportar.Items.Add("CSV (.csv) - Excel / hojas de cálculo", null, (s, e) => ExportarGrilla("csv"));
            menuExportar.Items.Add("JSON (.json)", null, (s, e) => ExportarGrilla("json"));
            menuExportar.Items.Add("XML (.xml)", null, (s, e) => ExportarGrilla("xml"));
            menuExportar.Items.Add("Página web (.html)", null, (s, e) => ExportarGrilla("html"));
            menuExportar.Items.Add("Texto tabulado (.txt)", null, (s, e) => ExportarGrilla("txt"));

            // El menú aparece justo debajo del botón
            btnGuardarComo.Click += (s, e) => menuExportar.Show(btnGuardarComo, new Point(0, btnGuardarComo.Height));
        }

        private void ExportarGrilla(string formato)
        {
            DataTable tabla = dgvAlumnos.DataSource as DataTable;
            if (tabla == null || tabla.Rows.Count == 0)
            {
                MessageBox.Show("No hay datos en la grilla para exportar.", "Guardar como...",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string filtro;
            switch (formato)
            {
                case "csv": filtro = "CSV (*.csv)|*.csv"; break;
                case "json": filtro = "JSON (*.json)|*.json"; break;
                case "xml": filtro = "XML (*.xml)|*.xml"; break;
                case "html": filtro = "Página web (*.html)|*.html"; break;
                default: filtro = "Texto (*.txt)|*.txt"; break;
            }

            using (SaveFileDialog dialogo = new SaveFileDialog())
            {
                dialogo.Title = "Guardar grilla como...";
                dialogo.Filter = filtro;
                dialogo.FileName = $"Asistencia_{DateTime.Now:yyyyMMdd_HHmm}";
                dialogo.OverwritePrompt = true;
                dialogo.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (dialogo.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    string ruta = dialogo.FileName;
                    switch (formato)
                    {
                        case "csv": File.WriteAllText(ruta, ATexto(tabla, ';', true), new UTF8Encoding(true)); break;
                        case "txt": File.WriteAllText(ruta, ATexto(tabla, '\t', false), new UTF8Encoding(true)); break;
                        case "json": File.WriteAllText(ruta, AJson(tabla), new UTF8Encoding(false)); break;
                        case "html": File.WriteAllText(ruta, AHtml(tabla), new UTF8Encoding(false)); break;
                        case "xml":
                            DataTable copia = tabla.Copy();
                            copia.TableName = "Alumnos";
                            copia.WriteXml(ruta, XmlWriteMode.IgnoreSchema);
                            break;
                    }

                    LogMensaje($"[OK] Grilla exportada a: {ruta}");
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

        private static string AHtml(DataTable tabla)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Asistencia</title>");
            sb.AppendLine("<style>body{font-family:Segoe UI,Arial;margin:24px}table{border-collapse:collapse}" +
                          "th,td{border:1px solid #999;padding:6px 12px;text-align:left}th{background:#222;color:#fff}</style>");
            sb.AppendLine("</head><body><h2>FaceTrack Attendance</h2><table><tr>");
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