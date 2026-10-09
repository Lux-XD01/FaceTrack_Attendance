// DISEÑO AUTOMÁTICO DE LA INTERFAZ
// Esta parte de Form1 (partial class) acomoda por código TODOS los controles que ya existen en
// Form1.Designer.cs dentro de paneles con título y de una estructura que se adapta sola al tamaño de la
// ventana (TableLayoutPanel). Además crea el TabControl y los paneles nuevos.
//
// Se ejecuta UNA vez, en el constructor de Form1, justo después de InitializeComponent() y ANTES de
// Estetica.AplicarEstilo(this). No hay que tocar Form1.Designer.cs.
//
// Estructura resultante:
//   raiz (2 columnas: 42 % | 58 %)
//   ├─ izquierda (2 filas)
//   │    ├─ panelVideo ................ pictureBox1
//   │    └─ panelConexion ............. lblIndicadorConexion | lblEstadoConexion | lblIP
//   └─ derecha (2 filas: 42 % | 58 %)
//        ├─ panelGrilla .............. txtBuscar + dgvAlumnos
//        └─ inferior (2 columnas)
//             ├─ panelLogs ........... TabControl: textBox2 | textBox3 | textBox1 (métricas)
//             └─ derechaInferior (2 filas)
//                  ├─ panelAcciones .. 5 botones
//                  └─ panelTemp ...... panelTemperatura

using System;
using System.Drawing;
using System.Windows.Forms;

namespace Proyecto_ReconocimientoFacial_0._1
{
    public partial class Form1
    {
        private void ConstruirInterfaz()
        {
            SuspendLayout();

            Text = "FaceTrack Attendance";
            MinimumSize = new Size(1280, 720);
            WindowState = FormWindowState.Maximized; // se adapta a cualquier pantalla; borra esta línea si no lo quieres

            // ------------------------------------------------------------
            // Contenedor raíz: columna izquierda (video) y derecha (todo lo demás)
            // ------------------------------------------------------------
            TableLayoutPanel raiz = NuevaTabla("layoutRaiz", 2, 1);
            raiz.Padding = new Padding(10);
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // ------------------------------------------------------------
            // IZQUIERDA: video arriba, estado de conexión abajo
            // ------------------------------------------------------------
            TableLayoutPanel izquierda = NuevaTabla("layoutIzquierda", 1, 2);
            izquierda.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            izquierda.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            izquierda.RowStyles.Add(new RowStyle(SizeType.Absolute, 105f)); // alto fijo del panel de conexión

            PanelRedondeado panelVideo = new PanelRedondeado();
            panelVideo.Name = "panelVideo";
            pictureBox1.Dock = DockStyle.Fill;
            pictureBox1.Margin = Padding.Empty;
            pictureBox1.BackColor = Color.Black;
            panelVideo.Controls.Add(pictureBox1);
            izquierda.Controls.Add(panelVideo, 0, 0);

            PanelRedondeado panelConexion = new PanelRedondeado();
            panelConexion.Name = "panelConexion";
            panelConexion.Titulo = "ESTADO DE CONEXIÓN";

            TableLayoutPanel conexion = NuevaTabla("layoutConexion", 3, 1);
            conexion.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            conexion.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            conexion.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            conexion.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            foreach (Label etiqueta in new[] { lblIndicadorConexion, lblEstadoConexion, lblIP })
            {
                etiqueta.Anchor = AnchorStyles.Left;      // solo "Left": así se centra verticalmente en la celda
                etiqueta.Margin = new Padding(8, 0, 8, 0);
            }
            lblEstadoConexion.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblIP.Font = new Font("Segoe UI", 11f);

            conexion.Controls.Add(lblIndicadorConexion, 0, 0);
            conexion.Controls.Add(lblEstadoConexion, 1, 0);
            conexion.Controls.Add(lblIP, 2, 0);
            panelConexion.Controls.Add(conexion);
            izquierda.Controls.Add(panelConexion, 0, 1);

            // ------------------------------------------------------------
            // DERECHA ARRIBA: búsqueda + grilla
            // ------------------------------------------------------------
            PanelRedondeado panelGrilla = new PanelRedondeado();
            panelGrilla.Name = "panelGrilla";

            TableLayoutPanel grilla = NuevaTabla("layoutGrilla", 1, 2);
            grilla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grilla.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            grilla.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // En el diseñador txtBuscar era Multiline (55 px de alto). Una caja de búsqueda debe ser de una línea.
            txtBuscar.Multiline = false;
            txtBuscar.ScrollBars = ScrollBars.None;
            txtBuscar.Anchor = AnchorStyles.Left | AnchorStyles.Right; // sin Top/Bottom: se centra vertical
            txtBuscar.Margin = new Padding(0, 0, 0, 2);

            dgvAlumnos.Dock = DockStyle.Fill;
            dgvAlumnos.Margin = Padding.Empty;
            dgvAlumnos.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            grilla.Controls.Add(txtBuscar, 0, 0);
            grilla.Controls.Add(dgvAlumnos, 0, 1);
            panelGrilla.Controls.Add(grilla);

            // ------------------------------------------------------------
            // DERECHA ABAJO: pestañas (izquierda) | acciones + temperatura (derecha)
            // ------------------------------------------------------------
            TableLayoutPanel inferior = NuevaTabla("layoutInferior", 2, 1);
            inferior.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54f));
            inferior.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));
            inferior.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // --- TabControl con las tres cajas de texto que ya existían ---
            PanelRedondeado panelLogs = new PanelRedondeado();
            panelLogs.Name = "panelLogs";
            panelLogs.Titulo = "SISTEMA DE LOGS Y BIOMETRÍA";

            TabControlOscuro tabs = new TabControlOscuro();
            tabs.Name = "tabPrincipal";
            tabs.TabPages.Add(CrearPagina("LOGS BIOMÉTRICOS (SFace)", textBox2, true));
            tabs.TabPages.Add(CrearPagina("LOGS DE CONEXIÓN", textBox3, true));
            tabs.TabPages.Add(CrearPagina("MÉTRICAS EN VIVO", textBox1, false));
            tabs.SelectedIndex = 2; // arranca mostrando las métricas
            panelLogs.Controls.Add(tabs);
            inferior.Controls.Add(panelLogs, 0, 0);

            // --- Columna derecha inferior: acciones arriba, temperatura abajo ---
            TableLayoutPanel derechaInferior = NuevaTabla("layoutDerechaInferior", 1, 2);
            derechaInferior.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            derechaInferior.RowStyles.Add(new RowStyle(SizeType.Percent, 46f));
            derechaInferior.RowStyles.Add(new RowStyle(SizeType.Percent, 54f));

            PanelRedondeado panelAcciones = new PanelRedondeado();
            panelAcciones.Name = "panelAcciones";
            panelAcciones.Titulo = "ACCIONES DE ADMINISTRACIÓN";

            TableLayoutPanel botones = NuevaTabla("layoutBotones", 3, 2);
            for (int c = 0; c < 3; c++) botones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
            for (int f = 0; f < 2; f++) botones.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            // El orden de esta lista decide la posición (3 por fila). Cámbialo si prefieres otra disposición.
            Button[] ordenBotones =
            {
                btnRegistrar, btnGuardarEdiciones, btnActualizarGrilla,
                btnEliminarSeleccionado, btnBorrarTodo
            };
            for (int i = 0; i < ordenBotones.Length; i++)
            {
                ordenBotones[i].Dock = DockStyle.Fill;
                ordenBotones[i].Margin = new Padding(4);
                botones.Controls.Add(ordenBotones[i], i % 3, i / 3);
            }
            panelAcciones.Controls.Add(botones);
            derechaInferior.Controls.Add(panelAcciones, 0, 0);

            PanelRedondeado panelTemp = new PanelRedondeado();
            panelTemp.Name = "panelTemp";
            panelTemp.Titulo = $"MONITOREO DE TEMPERATURA XIAO (INTERVALO: {intervaloTemp.TotalSeconds:F0}s)";
            panelTemperatura.Dock = DockStyle.Fill;
            panelTemperatura.Margin = Padding.Empty;
            panelTemperatura.Resize += (s, e) => panelTemperatura.Invalidate(); // el gráfico se redibuja al cambiar de tamaño
            panelTemp.Controls.Add(panelTemperatura);
            derechaInferior.Controls.Add(panelTemp, 0, 1);

            inferior.Controls.Add(derechaInferior, 1, 0);

            // ------------------------------------------------------------
            // Ensamblado final
            // ------------------------------------------------------------
            TableLayoutPanel derecha = NuevaTabla("layoutDerecha", 1, 2);
            derecha.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            derecha.RowStyles.Add(new RowStyle(SizeType.Percent, 42f));
            derecha.RowStyles.Add(new RowStyle(SizeType.Percent, 58f));
            derecha.Controls.Add(panelGrilla, 0, 0);
            derecha.Controls.Add(inferior, 0, 1);

            raiz.Controls.Add(izquierda, 0, 0);
            raiz.Controls.Add(derecha, 1, 0);

            Controls.Add(raiz);
            ResumeLayout(true);
        }

        // TableLayoutPanel sin márgenes ni relleno (el espaciado lo ponen los paneles redondeados)
        private static TableLayoutPanel NuevaTabla(string nombre, int columnas, int filas)
        {
            TableLayoutPanel tabla = new TableLayoutPanel();
            tabla.Name = nombre;
            tabla.Dock = DockStyle.Fill;
            tabla.ColumnCount = columnas;
            tabla.RowCount = filas;
            tabla.Margin = Padding.Empty;
            tabla.Padding = Padding.Empty;
            tabla.BackColor = Estetica.ColorFondo;
            return tabla;
        }

        // Crea una pestaña y le mete adentro una de las cajas de texto que ya existían
        private static TabPage CrearPagina(string titulo, TextBox caja, bool conBarraDeScroll)
        {
            TabPage pagina = new TabPage(titulo);
            pagina.Padding = new Padding(10);
            pagina.UseVisualStyleBackColor = false;

            caja.Multiline = true;
            caja.ReadOnly = true;
            caja.WordWrap = true;
            caja.ScrollBars = conBarraDeScroll ? ScrollBars.Vertical : ScrollBars.None;
            caja.Dock = DockStyle.Fill;

            pagina.Controls.Add(caja);
            return pagina;
        }
    }
}