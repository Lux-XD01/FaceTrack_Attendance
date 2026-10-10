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
    public partial class Form1 : IMessageFilter
    {
        // ---- Panel de horarios ----
        private readonly GestorHorarios gestorHorarios = new GestorHorarios();
        private PanelRedondeado panelHorarios;
        private TextBox txtHoraIni, txtMinIni, txtHoraFin, txtMinFin;
        private ComboBox cboDia;
        private Button btnHorarioGuardar, btnHorarioLimpiar, btnHorarioVaciar, btnHorarioDescargar;
        private Label lblHorarioIndicador, lblHorarioEstado;
        private TextBox textBoxHorario;
        private System.Windows.Forms.Timer timerHorario;

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
            TableLayoutPanel izquierda = NuevaTabla("layoutIzquierda", 1, 3);
            izquierda.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            izquierda.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));   // video
            izquierda.RowStyles.Add(new RowStyle(SizeType.Absolute, 190f));  // horarios
            izquierda.RowStyles.Add(new RowStyle(SizeType.Absolute, 105f));  // conexión

            PanelRedondeado panelVideo = new PanelRedondeado();
            panelVideo.Name = "panelVideo";
            pictureBox1.Dock = DockStyle.Fill;
            pictureBox1.Margin = Padding.Empty;
            pictureBox1.BackColor = Color.Black;
            panelVideo.Controls.Add(pictureBox1);
            izquierda.Controls.Add(panelVideo, 0, 0);
            panelHorarios = ConstruirPanelHorarios();
            izquierda.Controls.Add(panelHorarios, 0, 1);

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
            izquierda.Controls.Add(panelConexion, 0, 2);

            // ------------------------------------------------------------
            // DERECHA ARRIBA: búsqueda + grilla
            // ------------------------------------------------------------
            PanelRedondeado panelGrilla = new PanelRedondeado();
            panelGrilla.Name = "panelGrilla";

            TableLayoutPanel grilla = NuevaTabla("layoutGrilla", 1, 2);
            grilla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            grilla.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            grilla.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // Configuración del TextBox interno
            txtBuscar.Multiline = false;
            txtBuscar.ScrollBars = ScrollBars.None;

            // Envolvemos el txtBuscar dentro del nuevo PanelBusquedaRedondeado
            PanelBusquedaRedondeado buscadorRedondeado = new PanelBusquedaRedondeado(txtBuscar);

            dgvAlumnos.Dock = DockStyle.Fill;
            dgvAlumnos.Margin = Padding.Empty;
            dgvAlumnos.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            grilla.Controls.Add(buscadorRedondeado, 0, 0);
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
            tabs.TabPages.Add(CrearPagina("LOGS (SFace)", textBox2, true));
            tabs.TabPages.Add(CrearPagina("LOGS CONEXIÓN", textBox3, true));
            tabs.TabPages.Add(CrearPagina("MÉTRICAS VIVO", textBox1, false));
            textBoxHorario = new TextBox();
            textBoxHorario.Name = "textBoxHorario";
            tabs.TabPages.Add(CrearPagina("HORARIO", textBoxHorario, true));
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
            ConfigurarExportacion(); // crea btnGuardarComo y su menú (Form1.Exportar.cs)

            // El orden de esta lista decide la posición (3 por fila). Cámbialo si prefieres otra disposición.
            Button[] ordenBotones =
            {
                btnRegistrar, btnGuardarEdiciones, btnActualizarGrilla,
                btnEliminarSeleccionado, btnBorrarTodo, btnGuardarComo
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

        // Panel HORARIOS: entre el video y el estado de conexión.
        // Usa UNA sola tabla (no anidar tablas: Estetica les pondría el color de fondo equivocado).
        private PanelRedondeado ConstruirPanelHorarios()
        {
            PanelRedondeado panel = new PanelRedondeado();
            panel.Name = "panelHorarios";
            panel.Titulo = "HORARIOS";

            txtHoraIni = NuevaCajaHora("txtHoraIni", "HH");
            txtMinIni = NuevaCajaHora("txtMinIni", "MM");
            txtHoraFin = NuevaCajaHora("txtHoraFin", "HH");
            txtMinFin = NuevaCajaHora("txtMinFin", "MM");

            cboDia = new ComboBox();
            cboDia.Name = "cboDia";
            cboDia.DropDownStyle = ComboBoxStyle.DropDownList;
            cboDia.FlatStyle = FlatStyle.Flat;
            cboDia.Width = 105;
            cboDia.Anchor = AnchorStyles.Left;
            cboDia.Margin = new Padding(8, 0, 0, 0);
            cboDia.Items.AddRange(DIAS_NOMBRES);
            cboDia.SelectedIndexChanged += (s, e) => CargarCamposHorarioDelDia();

            FlowLayoutPanel fila = new FlowLayoutPanel();
            fila.Name = "flowHorario";
            fila.Dock = DockStyle.Fill;
            fila.WrapContents = false;
            fila.Margin = Padding.Empty;
            fila.Controls.Add(NuevaEtiquetaHorario("Fijar Asistencia De"));
            fila.Controls.Add(txtHoraIni);
            fila.Controls.Add(NuevaEtiquetaHorario(":"));
            fila.Controls.Add(txtMinIni);
            fila.Controls.Add(NuevaEtiquetaHorario("hasta"));
            fila.Controls.Add(txtHoraFin);
            fila.Controls.Add(NuevaEtiquetaHorario(":"));
            fila.Controls.Add(txtMinFin);
            fila.Controls.Add(cboDia);

            btnHorarioGuardar = NuevoBotonHorario("btnHorarioGuardar", "💾", "Guardar");
            btnHorarioLimpiar = NuevoBotonHorario("btnHorarioLimpiar", "🔄", "Limpiar");
            btnHorarioVaciar = NuevoBotonHorario("btnHorarioVaciar", "🗑", "Vaciar Día");
            btnHorarioDescargar = NuevoBotonHorario("btnHorarioDescargar", "📥", "Descargar");
            btnHorarioGuardar.Click += BtnHorarioGuardar_Click;
            btnHorarioLimpiar.Click += BtnHorarioLimpiar_Click;
            btnHorarioVaciar.Click += BtnHorarioVaciar_Click;
            btnHorarioDescargar.Click += BtnHorarioDescargar_Click;

            lblHorarioIndicador = new Label();
            lblHorarioIndicador.Name = "lblHorarioIndicador";
            lblHorarioIndicador.AutoSize = true;
            lblHorarioIndicador.Text = "●";
            lblHorarioIndicador.Anchor = AnchorStyles.Left;
            lblHorarioIndicador.Margin = new Padding(2, 0, 4, 0);

            lblHorarioEstado = new Label();
            lblHorarioEstado.Name = "lblHorarioEstado";
            lblHorarioEstado.AutoSize = true;
            lblHorarioEstado.Anchor = AnchorStyles.Left;
            lblHorarioEstado.Margin = Padding.Empty;

            FlowLayoutPanel filaEstado = new FlowLayoutPanel();
            filaEstado.Name = "flowHorarioEstado";
            filaEstado.Dock = DockStyle.Fill;
            filaEstado.WrapContents = false;
            filaEstado.Margin = Padding.Empty;
            filaEstado.Controls.Add(lblHorarioIndicador);
            filaEstado.Controls.Add(lblHorarioEstado);

            TableLayoutPanel tabla = new TableLayoutPanel();
            tabla.Name = "layoutHorarios";
            tabla.Dock = DockStyle.Fill;
            tabla.Margin = Padding.Empty;
            tabla.Padding = Padding.Empty;
            tabla.ColumnCount = 4;
            tabla.RowCount = 3;
            for (int c = 0; c < 4; c++) tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            tabla.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            tabla.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));

            tabla.Controls.Add(fila, 0, 0);
            tabla.SetColumnSpan(fila, 4);
            tabla.Controls.Add(btnHorarioGuardar, 0, 1);
            tabla.Controls.Add(btnHorarioLimpiar, 1, 1);
            tabla.Controls.Add(btnHorarioVaciar, 2, 1);
            tabla.Controls.Add(btnHorarioDescargar, 3, 1);
            tabla.Controls.Add(filaEstado, 0, 2);
            tabla.SetColumnSpan(filaEstado, 4);
            panel.Controls.Add(tabla);

            return panel;
        }

        // REQUISITO 1: caja HH/MM solo numérica, máximo 2 caracteres (los eventos están en Form1.cs)
        private TextBox NuevaCajaHora(string nombre, string cue)
        {
            TextBox caja = new TextBox();
            caja.Name = nombre;
            caja.MaxLength = 2;
            caja.Width = 40;
            caja.TextAlign = HorizontalAlignment.Center;
            caja.Anchor = AnchorStyles.Left;
            caja.Margin = new Padding(2, 0, 2, 0);
            caja.KeyPress += HoraKeyPress;
            caja.TextChanged += HoraTextChanged;
            caja.Leave += HoraLeave;
            // Texto gris "HH"/"MM" con la caja vacía (PlaceholderText no existe en .NET Framework)
            caja.HandleCreated += (s, e) =>
                HorSendMessage(((TextBox)s).Handle, HOR_EM_SETCUEBANNER, IntPtr.Zero, cue);
            return caja;
        }

        private static Label NuevaEtiquetaHorario(string texto)
        {
            Label l = new Label();
            l.AutoSize = true;
            l.Text = texto;
            l.Font = new Font("Segoe UI", 10.5f);
            l.Anchor = AnchorStyles.Left;
            l.Margin = new Padding(4, 0, 4, 0);
            return l;
        }

        private static Button NuevoBotonHorario(string nombre, string icono, string texto)
        {
            Button b = new Button();
            b.Name = nombre;
            b.Text = icono + Environment.NewLine + texto;
            b.Dock = DockStyle.Fill;
            b.Margin = new Padding(4);
            b.TextAlign = ContentAlignment.MiddleCenter;
            return b;
        }

        // Estetica.AplicarEstilo(this) pisa fuentes y colores; esto deja la apariencia final del panel.
        // Se llama desde el constructor de Form1 DESPUÉS de AplicarEstilo.
        private void AplicarEstiloHorarios()
        {
            foreach (TextBox caja in new[] { txtHoraIni, txtMinIni, txtHoraFin, txtMinFin })
            {
                caja.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
                caja.TextAlign = HorizontalAlignment.Center;
                caja.BackColor = Estetica.ColorConsolaFondo;
                caja.ForeColor = Estetica.ColorTextoClaro;
                caja.Width = 40;
            }

            cboDia.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            cboDia.BackColor = Estetica.ColorBotonSecundario;
            cboDia.ForeColor = Estetica.ColorTextoClaro;

            foreach (Button b in new[] { btnHorarioGuardar, btnHorarioLimpiar, btnHorarioVaciar, btnHorarioDescargar })
                b.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

            lblHorarioIndicador.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            lblHorarioEstado.Font = new Font("Segoe UI", 11f);

            ActualizarIndicadorHorario();
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