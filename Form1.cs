// RECONOCIMIENTO FACIAL - VERSIÓN CORREGIDA (v4)
// Emgu.CV 4.8.0.5324 | YuNet (detección) + SFace (reconocimiento) | XIAO ESP32S3 (stream MJPEG)
// SQL Server para persistencia de asistencia y plantillas biométricas.
//
// CONTROLES REQUERIDOS EN EL DISEÑADOR (agregarlos a mano, ver instrucciones del chat):
//   DataGridView dgvAlumnos | TextBox txtBuscar | Button btnRegistrar
//   Button btnBorrarTodo | Button btnGuardarEdiciones | Button btnEliminarSeleccionado
//   Button btnActualizarGrilla
//
// Cambios v4:
//   - Texto sobre el video dibujado con GDI+ (Unicode): ya no salen "?" en tildes, "ñ", "¿", "¡".
//   - Sobre el recuadro solo va "Desconocido" / "[Nombre] (score)". Las indicaciones van al pie, con color.
//   - Textos de indicación en forma de "usted", según el Word de objetivos.
//   - Anti-parpadeo: histéresis del score de detección (entra a 0.72, sale a 0.68) + filtro temporal
//     de mensajes (FiltroMensajeGuia).
//   - txtBuscar: Enter busca y vacía la caja; si no existe, aviso en la caja; clic en la caja restaura la grilla.
//     Se elimina btnBuscar.
//   - Al renombrar en la grilla se vacían los votos del reconocimiento (evita un desfase de ~0.5 s).
//
// Cambios respecto a la versión anterior (v3):
//   - Se reemplazaron las teclas ESPACIO/SUPR por botones (btnRegistrar / btnBorrarTodo).
//   - btnBorrarTodo pide confirmación DOS veces antes de borrar.
//   - Persistencia en SQL Server: al registrar, la plantilla se guarda en la base; al reconocer,
//     se marca "Presente" con fecha/hora; al iniciar el programa, se recargan todos los registros.
//   - Panel de administración: grilla editable (Nombre/Apellido/Legajo), búsqueda y borrado individual.
//   - Si SQL Server no está disponible, el programa sigue funcionando solo en memoria (no bloquea
//     las pruebas de reconocimiento) y lo avisa en el log.

using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Face;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Proyecto_ReconocimientoFacial_0._1
{
    public partial class Form1 : Form
    {
        // ------------------------------------------------------------------
        // PARÁMETROS AJUSTABLES
        // ------------------------------------------------------------------
        private const float UMBRAL_SFACE = 0.363f;        // Similitud coseno mínima (valor recomendado por OpenCV)
        private const float MARGEN_AMBIGUO = 0.08f;       // Diferencia mínima entre 1er y 2do candidato para confiar
        // Histéresis del score de YuNet: para EMPEZAR a seguir un rostro hace falta score >= ENTRADA; una vez
        // seguido, se lo conserva mientras el score sea >= SALIDA. Con un único umbral (antes 0.7) un score que
        // oscila entre 0.68 y 0.72 hacía aparecer/desaparecer el rostro y sus mensajes a cada fotograma.
        private const float SCORE_DETECCION_ENTRADA = 0.72f;
        private const float SCORE_DETECCION_SALIDA = 0.68f;
        private const int MS_MANTENER_SEGUIMIENTO = 800;  // tiempo sin ver rostro antes de exigir de nuevo el score de ENTRADA
        private const float SCORE_REGISTRO = 0.9f;        // Confianza exigida al REGISTRAR
        private const int MIN_LADO_RECONOCER = 50;        // Lado mínimo (px, resolución original) para reconocer
        private const int MIN_LADO_REGISTRO = 70;         // Lado mínimo (px, resolución original) para registrar
        private const int MARGEN_BORDE = 4;               // Margen (px) al borde: rostro cortado = alineación mala
        private const double UMBRAL_NITIDEZ = 40.0;           // Varianza del Laplaciano al REGISTRAR (ajustá con los valores del log)
        private const double UMBRAL_NITIDEZ_RECONOCER = 40.0; // Idem, pero al RECONOCER en vivo. Separado por si querés
                                                              // relajarlo un poco en video (es más exigente estar siempre
                                                              // igual de nítido en movimiento que en una foto de registro quieta).
        private const double UMBRAL_YAW_MIN = 0.6;            // Relación ojo-nariz mínima: por debajo, está de perfil
        private const double UMBRAL_YAW_MAX = 1.67;           // Relación ojo-nariz máxima: por encima, está de perfil (al otro lado)
        private const double BRILLO_MIN = 50.0;           // Media de gris (0-255) mínima: por debajo, a contraluz
        private const double BRILLO_MAX = 210.0;          // Media de gris (0-255) máxima: por encima, luz directa
        private const double ASIMETRIA_MAX = 45.0;        // Diferencia de brillo izq/der máxima: por encima, contraluz lateral
        private const float UMBRAL_CONSISTENCIA = 0.40f;  // Las muestras del registro deben parecerse a la primera
        private const int MUESTRAS_REGISTRO = 5;          // Muestras por persona
        private const int MS_ENTRE_MUESTRAS = 300;        // Separación entre muestras
        private const int MS_TIMEOUT_REGISTRO = 10000;    // Tiempo máximo para completar un registro
        private const int VENTANA_VOTOS = 7;              // Fotogramas usados para confirmar identidad
        private const int VOTOS_MINIMOS = 4;              // Coincidencias necesarias dentro de la ventana
        private const int MAX_PERSONAS = 4;
        private const int ESCALA_MAX_DETECCION = 640;     // Lado máximo (px) para correr YuNet; frames mayores se reducen
        private const string ESTADO_ANALIZANDO = "Analizando...";
        private const string ESTADO_AMBIGUO = "Verificando...";

        // Si un rostro toca el borde y ocupa más que esta fracción del fotograma, se considera "demasiado cerca"
        // (mensaje "Aléjese"); si es más chico, simplemente está corrido ("Póngase en el centro").
        private const float FRACCION_ROSTRO_DEMASIADO_CERCA = 0.55f;

        // Filtro anti-parpadeo de las indicaciones al pie (ver FiltroMensajeGuia)
        private const int RETARDO_APARICION_GUIA_MS = 400;     // el problema debe persistir este tiempo para mostrarse
        private const int RETARDO_DESAPARICION_GUIA_MS = 700;  // el rostro debe estar bien este tiempo para ocultarla
        private const int MIN_VISIBLE_GUIA_MS = 1200;          // una vez mostrada, se queda al menos esto
        private const int MS_PIE_REGISTRADO = 4000;            // duración de "Usted ha sido registrado en el sistema"

        // Textos de indicación para el usuario (forma "usted", como en el Word de objetivos)
        private const string MSG_ACERCARSE = "Acérquese a la cámara";
        private const string MSG_ALEJARSE = "Aléjese de la cámara";
        private const string MSG_CENTRO = "Póngase en el centro";
        private const string MSG_QUIETO = "Manténgase quieto";
        private const string MSG_DE_FRENTE = "Mire de frente a la cámara";
        private const string MSG_NIVELAR = "Mantenga la cabeza derecha";
        private const string MSG_POCA_LUZ = "Falta luz: ilumine su rostro de frente";
        private const string MSG_MUCHA_LUZ = "Demasiada luz directa sobre su rostro";
        private const string MSG_LUZ_LATERAL = "Luz de costado: póngase de frente a la luz";

        // Cámara ESP32 - debe coincidir EXACTO con la IP fija que pusiste en el .ino (local_IP).
        // Si volvés a cambiar la IP fija en el firmware, actualizala también acá.
        private readonly string ipCamara = "192.168.1.112";
        private readonly string streamUrl;
        private readonly string tempUrl;
        private readonly TimeSpan intervaloTemp = TimeSpan.FromSeconds(20);
        private const double TEMP_ADVERTENCIA_C = 70.0;

        // SQL Server - cambiar "servidorSql" por el nombre que ves al conectar en SSMS
        private readonly string servidorSql = "LUX\\SQLEXPRESS"; // ej: "DESKTOP-ABC123\\SQLEXPRESS" o "localhost"
        private readonly string baseDeDatosSql = "FaceTrackAttendance";
        private AdministradorSQL administradorSQL;
        private volatile bool sqlDisponible = false;

        // ------------------------------------------------------------------
        // ESTADO
        // ------------------------------------------------------------------
        private FaceDetectorYN detectorNet;
        private FaceRecognizerSF recognizerNet;

        // Protege redes, listaPersonas y el estado del registro (solo los toca el hilo de procesamiento y el cierre)
        private readonly object lockObjetosNativos = new object();
        private volatile bool isDisposing = false;

        private readonly List<PersonaRegistrada> listaPersonas = new List<PersonaRegistrada>();

        // Registro multi-muestra
        private volatile bool solicitudRegistro = false;
        private volatile bool solicitudBorrado = false;
        private volatile bool registroActivo = false;
        private readonly List<Mat> muestrasRegistro = new List<Mat>();
        private DateTime inicioRegistro = DateTime.MinValue;
        private DateTime ultimaMuestra = DateTime.MinValue;

        // Confirmación temporal de identidad
        private readonly Queue<KeyValuePair<string, double>> ultimosVotos = new Queue<KeyValuePair<string, double>>();
        private DateTime ultimoRostroVisto = DateTime.MinValue;

        // Notificaciones con cooldown
        private string ultimoSujetoNotificado = "";
        private DateTime ultimaNotificacion = DateTime.MinValue;
        private readonly TimeSpan cooldownNotificacion = TimeSpan.FromSeconds(5);

        // Último estado mostrado (evita inundar la interfaz)
        private string ultimoEstado = "";
        private DateTime ultimoEstadoTs = DateTime.MinValue;

        // Buffer del último fotograma (red -> procesamiento)
        private readonly object lockFrame = new object();
        private byte[] ultimoJpeg;
        private readonly AutoResetEvent nuevoFrame = new AutoResetEvent(false);
        // Visualización independiente del procesamiento
        private byte[] ultimoJpegVisor;
        private readonly AutoResetEvent nuevoFrameVisor = new AutoResetEvent(false);
        private volatile SuperposicionFrame superposicionPublicada;
        private const int MS_VIGENCIA_SUPERPOSICION = 700; // si el último resultado es más viejo, no se dibuja

        private static readonly byte[] SOI = { 0xFF, 0xD8 };
        private static readonly byte[] EOI = { 0xFF, 0xD9 };

        private CancellationTokenSource cancellationTokenSource;
        private Size ultimoTamanoDeteccion = Size.Empty;
        private int frameUIPendiente = 0; // 1 = la interfaz aún no terminó de pintar el fotograma anterior
        private bool formCargado = false;

        // Contador para nombrar "Sujeto N" SOLO cuando SQL no está disponible. A propósito NUNCA se basa en
        // listaPersonas.Count: si se basara en el conteo actual, borrar a alguien y registrar a otra persona
        // podía reciclar un nombre ya usado (el bug de "dos Sujeto 2" que viste). Este contador solo sube.
        private int proximoIdLocalFallback = 0;

        // Mismos tonos que antes (antes eran MCvScalar en orden BGR; ahora son Color de GDI+, en RGB).
        private static readonly Color COLOR_VERDE = Color.FromArgb(0, 255, 0);
        private static readonly Color COLOR_ROJO = Color.FromArgb(255, 0, 0);
        private static readonly Color COLOR_AMARILLO = Color.FromArgb(255, 255, 0);
        private static readonly Color COLOR_AZUL = Color.FromArgb(0, 128, 255);

        // ------------------------------------------------------------------
        // SUPERPOSICIÓN SOBRE EL VIDEO (datos; el dibujo está en DibujarSuperposicion)
        // ------------------------------------------------------------------
        private sealed class EtiquetaRostro
        {
            public Rectangle Rect;
            public string Texto;   // null = solo recuadro, sin texto
            public Color Color;
        }

        private sealed class SuperposicionFrame
        {
            public EtiquetaRostro Rostro;
            public string Pie;
            public Color ColorPie;
            public DateTime Creada; // momento en que ProcessFrame terminó de armarla
        }

        // Todos estos campos los toca únicamente el hilo de procesamiento (dentro de ProcessFrame).
        private SuperposicionFrame superposicion = new SuperposicionFrame();
        private DateTime ultimaActualizacionMetricas = DateTime.MinValue;
        private const int MS_ENTRE_METRICAS = 200; // máx. ~5 actualizaciones por segundo
        private string guiaCruda;      // indicación que pide el fotograma ACTUAL (null = todo bien); aún sin filtrar
        private string pieEstadoTexto; // estado normal del fotograma actual (presente, no registrado, registrando...)
        private Color pieEstadoColor;
        private string pieTemporalTexto;
        private Color pieTemporalColor;
        private DateTime pieTemporalHasta = DateTime.MinValue;
        private readonly FiltroMensajeGuia filtroGuia =
            new FiltroMensajeGuia(RETARDO_APARICION_GUIA_MS, RETARDO_DESAPARICION_GUIA_MS, MIN_VISIBLE_GUIA_MS);

        // Historial de temperatura para el gráfico (reemplaza el log de texto repetitivo)
        private readonly object lockHistorialTemp = new object();
        private readonly List<(DateTime Hora, double Temp)> historialTemperatura = new List<(DateTime, double)>();
        private const int MAX_PUNTOS_TEMP = 30; // a 20s cada uno, ~10 minutos de historial visible

        // Panel de estado de conexión (IP fija + indicador verde/rojo). Los colores viven en Estetica.
        private DateTime ultimoFrameRecibido = DateTime.MinValue;
        private System.Windows.Forms.Timer timerEstadoConexion;

        public Form1()
        {
            InitializeComponent();

            ConstruirInterfaz(); // se encarga de enganchar eventos y aplicar estilo a los controles

            streamUrl = $"http://{ipCamara}:81/stream";
            tempUrl = $"http://{ipCamara}/temp";

            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;

            Estetica.AplicarEstilo(this);

            // txtBuscar: se guarda el color de texto ya estilizado para poder restaurarlo tras mostrar el aviso en rojo
            colorTextoBuscarOriginal = txtBuscar.ForeColor;
            txtBuscar.KeyDown += txtBuscar_KeyDown;
            txtBuscar.KeyPress += txtBuscar_KeyPress;
            txtBuscar.Click += (s, e) => RestaurarBusqueda();  // clic con el mouse
            txtBuscar.Enter += (s, e) => RestaurarBusqueda();  // entrar con Tab

            // El PictureBox se ajusta al tamaño que le diste en el diseñador, sin importar la
            // resolución real de la cámara (esto es lo que resuelve el desborde con FRAMESIZE_HVGA).
            // Esto queda en Form1 porque es parte de cómo se muestra el stream, no de la paleta visual.
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.BorderStyle = BorderStyle.FixedSingle;

            // Enganchá estos métodos a los eventos Click de los botones desde el Designer
            // (o descomentá estas líneas si preferís engancharlos acá):
            // btnRegistrar.Click += btnRegistrar_Click;
            // btnBorrarTodo.Click += btnBorrarTodo_Click;
            // btnGuardarEdiciones.Click += btnGuardarEdiciones_Click;
            // btnEliminarSeleccionado.Click += btnEliminarSeleccionado_Click;
            // btnActualizarGrilla.Click += btnActualizarGrilla_Click;
            // panelTemperatura.Paint += panelTemperatura_Paint;
        }

        // ------------------------------------------------------------------
        // CARGA
        // ------------------------------------------------------------------
        private void Form1_Load(object sender, EventArgs e)
        {
            if (formCargado) return;
            formCargado = true;

            ActualizarMetricas(null, null, null); // deja textBox1 con el panel de métricas en "--"
            textBox2.Text = "=== DATOS BIOMÉTRICOS ===" + Environment.NewLine;
            LogMensaje("=== LOGS DE CONEXIÓN ESP32 ===");
            LogMensaje("Usá el botón 'Registrar' para capturar un rostro y 'Borrar todo' para reiniciar la lista.");

            try
            {
                string pathDetector = BuscarModelo("face_detection_yunet_2023mar.onnx");
                string pathRecognizer = BuscarModelo("face_recognition_sface_2021dec.onnx");

                if (pathDetector == null || pathRecognizer == null)
                {
                    LogMensaje("[ERROR ONNX] Archivos de modelo .onnx no encontrados.");
                    MessageBox.Show("No se encontraron los archivos de modelos ONNX.\n" +
                                    "Cópialos junto al .exe o en la carpeta 'models' " +
                                    "(Propiedades > Copiar en el directorio de salida > Copiar si es posterior).",
                                    "Error de Archivo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                detectorNet = new FaceDetectorYN(
                    model: pathDetector,
                    config: "",
                    inputSize: new Size(320, 320),
                    scoreThreshold: SCORE_DETECCION_SALIDA,
                    nmsThreshold: 0.3f,
                    topK: 5000,
                    backendId: Emgu.CV.Dnn.Backend.OpenCV,
                    targetId: Emgu.CV.Dnn.Target.Cpu
                );

                recognizerNet = new FaceRecognizerSF(
                    model: pathRecognizer,
                    config: "",
                    backendId: Emgu.CV.Dnn.Backend.OpenCV,
                    targetId: Emgu.CV.Dnn.Target.Cpu
                );

                LogMensaje("[OK] Modelos ONNX cargados correctamente.");
            }
            catch (Exception ex)
            {
                LogMensaje($"[ERROR ONNX] Fallo al cargar modelos: {ex.Message}");
                return;
            }

            InicializarSql();

            // El ping es solo informativo y no bloquea la interfaz ni impide conectar
            Task.Run(() =>
            {
                try
                {
                    using (Ping ping = new Ping())
                    {
                        PingReply reply = ping.Send(ipCamara, 2000);
                        if (reply != null && reply.Status == IPStatus.Success)
                            LogMensaje($"[OK] Ping exitoso a {ipCamara} ({reply.RoundtripTime} ms).");
                        else
                            LogMensaje($"[AVISO] Sin respuesta al ping de {ipCamara} (se intentará conectar igual).");
                    }
                }
                catch (Exception ex)
                {
                    LogMensaje($"[AVISO] Ping falló: {ex.Message}");
                }
            });

            LogMensaje($"Conectando a {streamUrl} ...");
            IniciarStreamCamara();
            IniciarSondeoTemperatura();
            IniciarMonitorConexion();
        }

        private static string BuscarModelo(string archivo)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidatos =
            {
                Path.Combine(baseDir, archivo),
                Path.Combine(baseDir, "models", archivo),
                archivo,
                Path.Combine("models", archivo)
            };
            return candidatos.FirstOrDefault(File.Exists);
        }



        // ------------------------------------------------------------------
        // SQL SERVER
        // ------------------------------------------------------------------
        private void InicializarSql()
        {
            try
            {
                administradorSQL = new AdministradorSQL(servidorSql, baseDeDatosSql);
                administradorSQL.AsegurarBaseDeDatos();
                administradorSQL.AsegurarEsquema();
                sqlDisponible = true;
                LogMensaje("[OK] Conectado a SQL Server.");

                CargarPersonasDesdeSql();
                ActualizarGrilla();
            }
            catch (Exception ex)
            {
                sqlDisponible = false;
                LogMensaje($"[AVISO] SQL Server no disponible: {ex.Message}");
                LogMensaje("[AVISO] Se sigue trabajando solo en memoria (sin persistencia).");
            }
        }

        // Recupera las plantillas guardadas en SQL para no perder los registros al reiniciar el programa
        private void CargarPersonasDesdeSql()
        {
            if (!sqlDisponible) return;

            foreach (var item in administradorSQL.ObtenerPlantillas())
            {
                float[] valores = BytesAVector(item.Plantilla);
                Mat centroide = new Mat(1, valores.Length, DepthType.Cv32F, 1);
                centroide.SetTo(valores);

                listaPersonas.Add(new PersonaRegistrada
                {
                    Id = item.Id,
                    IdBd = item.Id,
                    Nombre = item.Nombre,
                    Plantilla = centroide
                });
            }

            if (listaPersonas.Count > 0)
                LogMensaje($"[OK] {listaPersonas.Count} persona(s) cargada(s) desde SQL Server.");
        }

        // Refresca la grilla del panel de administración (siempre desde el hilo de UI)

        private void ActualizarGrilla()
        {
            if (!sqlDisponible) return;

            string filtro = filtroBusquedaActivo; // se captura acá: la consulta corre en otro hilo

            Task.Run(() =>
            {
                DataTable tabla;
                try
                {
                    tabla = (filtro == null) ? administradorSQL.ObtenerTodos() : administradorSQL.Buscar(filtro);
                }
                catch (Exception ex)
                {
                    LogMensaje($"[AVISO] No se pudo actualizar la grilla: {ex.Message}");
                    return;
                }

                EjecutarEnUI(() =>
                {
                    if (filtro != filtroBusquedaActivo) return; // la búsqueda cambió mientras se consultaba
                    try { MostrarTablaEnGrilla(tabla); }
                    catch (Exception ex) { LogMensaje($"[AVISO] No se pudo mostrar la grilla: {ex.Message}"); }
                });
            });
        }

        // Asigna la tabla a la grilla y reaplica sus reglas. Antes la búsqueda asignaba el DataSource sin
        // reaplicar los ReadOnly, por lo que tras buscar las columnas Id/Estado/FechaHora podían quedar editables.
        private void MostrarTablaEnGrilla(DataTable tabla)
        {
            dgvAlumnos.DataSource = tabla;
            dgvAlumnos.AllowUserToAddRows = false; // sin esto, la fila en blanco del final intenta crear un alumno sin Plantilla

            if (dgvAlumnos.Columns["Id"] != null) dgvAlumnos.Columns["Id"].ReadOnly = true;
            if (dgvAlumnos.Columns["Estado"] != null) dgvAlumnos.Columns["Estado"].ReadOnly = true;
            if (dgvAlumnos.Columns["FechaHora"] != null) dgvAlumnos.Columns["FechaHora"].ReadOnly = true;
        }

        private static byte[] VectorABytes(float[] v)
        {
            byte[] b = new byte[v.Length * sizeof(float)];
            Buffer.BlockCopy(v, 0, b, 0, b.Length);
            return b;
        }

        private static float[] BytesAVector(byte[] b)
        {
            float[] v = new float[b.Length / sizeof(float)];
            Buffer.BlockCopy(b, 0, v, 0, b.Length);
            return v;
        }

        private static float[] ValoresDeMat(Mat m)
        {
            float[] v = new float[m.Cols];
            m.CopyTo(v);
            return v;
        }

        // ---- Botones del panel de administración ----

        private void btnRegistrar_Click(object sender, EventArgs e)
        {
            if (!registroActivo) solicitudRegistro = true;
        }

        private void btnBorrarTodo_Click(object sender, EventArgs e)
        {
            DialogResult r1 = MessageBox.Show(
                "Esto borrará TODOS los rostros registrados, tanto en memoria como en SQL Server.\n¿Deseas continuar?",
                "Confirmar borrado", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r1 != DialogResult.Yes) return;

            DialogResult r2 = MessageBox.Show(
                "Esta acción NO se puede deshacer.\n¿Confirmás que querés borrar todos los registros ahora?",
                "Última confirmación", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
            if (r2 != DialogResult.Yes) return;

            solicitudBorrado = true;
        }

        private void btnActualizarGrilla_Click(object sender, EventArgs e)
        {
            RestaurarBusqueda(); // "Actualizar" siempre vuelve a mostrar a todos
            ActualizarGrilla();
        }

        // ---- Búsqueda (txtBuscar) ----
        // Flujo: Enter -> se busca y la caja se vacía.
        //   - Si hay resultados: la grilla queda filtrada y la caja vacía.
        //   - Si NO hay: la grilla queda vacía y la caja muestra el aviso en rojo.
        // Al hacer clic en la caja (o al empezar a escribir sobre el aviso) se borra el aviso y la grilla
        // vuelve a mostrar a todos.
        private string filtroBusquedaActivo = null;   // null = la grilla muestra a todos
        private bool mensajeBusquedaVisible = false;  // true = txtBuscar contiene el aviso de "no está en la base"
        private Color colorTextoBuscarOriginal;

        private void txtBuscar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;

            e.Handled = true;
            e.SuppressKeyPress = true; // evita el "ding" de Windows al presionar Enter en un TextBox de una línea
            EjecutarBusqueda();
        }

        // KeyPress solo se dispara con teclas que producen un carácter (no con Shift, flechas, etc.).
        // Si el aviso está en pantalla, se borra ANTES de que entre la letra nueva.
        private void txtBuscar_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (mensajeBusquedaVisible) RestaurarBusqueda();
        }

        private void EjecutarBusqueda()
        {
            if (!sqlDisponible) { MostrarEstado("SQL Server no está disponible."); return; }

            string texto = txtBuscar.Text.Trim();

            // Enter con la caja vacía = volver a ver a todos
            if (texto.Length == 0)
            {
                RestaurarBusqueda();
                return;
            }

            try
            {
                DataTable tabla = administradorSQL.Buscar(texto);

                filtroBusquedaActivo = texto; // así los refrescos automáticos (reconocimiento) no pisan el resultado
                MostrarTablaEnGrilla(tabla);

                if (tabla.Rows.Count == 0)
                {
                    mensajeBusquedaVisible = true;
                    txtBuscar.ForeColor = Estetica.ColorPeligro;
                    txtBuscar.Text = $"El usuario que está buscando {texto} no está en la base de datos.";
                    txtBuscar.SelectionStart = 0;   // que se vea el comienzo del aviso
                    txtBuscar.SelectionLength = 0;
                }
                else
                {
                    txtBuscar.Clear();
                }
            }
            catch (Exception ex)
            {
                LogMensaje($"[AVISO] Error en la búsqueda: {ex.Message}");
            }
        }

        // Deja todo como al inicio: sin filtro, sin aviso, color original. Se puede llamar las veces que sea.
        private void RestaurarBusqueda()
        {
            if (!mensajeBusquedaVisible && filtroBusquedaActivo == null) return;

            bool habiaMensaje = mensajeBusquedaVisible;

            mensajeBusquedaVisible = false;
            filtroBusquedaActivo = null;
            txtBuscar.ForeColor = colorTextoBuscarOriginal;

            // Solo se borra el texto si era el AVISO. Si el usuario ya escribió algo propio y hace clic
            // para mover el cursor, no se le debe borrar lo que tipeó.
            if (habiaMensaje) txtBuscar.Clear();

            ActualizarGrilla();
        }

        private void btnGuardarEdiciones_Click(object sender, EventArgs e)
        {
            if (!sqlDisponible) { MostrarEstado("SQL Server no está disponible."); return; }

            try
            {
                DataTable tabla = dgvAlumnos.DataSource as DataTable;
                if (tabla == null) return;

                // BUG CORREGIDO: antes se podían guardar dos alumnos con el mismo Nombre+Apellido
                // (ej. dos "Lucía") sin ningún aviso, lo cual hacía imposible saber a cuál se estaba
                // reconociendo en el video. Se valida ANTES de guardar, no después.
                string duplicado = BuscarNombreDuplicado(tabla);
                if (duplicado != null)
                {
                    MessageBox.Show($"Hay más de un alumno con el nombre \"{duplicado}\" (mismo Nombre y Apellido).\n" +
                                    "Corregilo antes de guardar para no confundir a quién reconoce la cámara.",
                                    "Nombre duplicado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                administradorSQL.GuardarEdiciones(tabla);

                // BUG CORREGIDO: renombrar en la grilla NO actualizaba el nombre que usa el reconocimiento
                // en vivo (listaPersonas), porque son dos listas separadas. El video seguía mostrando el
                // nombre viejo hasta reiniciar el programa. Ahora se sincronizan en el mismo momento.
                SincronizarNombresEnMemoria(tabla);

                MostrarEstado("Cambios guardados en SQL Server.");
                ActualizarGrilla();
            }
            catch (Exception ex)
            {
                LogMensaje($"[ERROR] No se pudieron guardar los cambios: {ex.Message}");
            }
        }

        // Busca si hay dos o más filas con el mismo Nombre+Apellido (comparación sin mayúsculas/espacios).
        // Devuelve el nombre en conflicto, o null si no hay duplicados.
        private string BuscarNombreDuplicado(DataTable tabla)
        {
            var grupos = tabla.AsEnumerable()
                .Select(f => new
                {
                    Clave = ((f["Nombre"]?.ToString() ?? "") + "|" + (f["Apellido"]?.ToString() ?? "")).Trim().ToLowerInvariant(),
                    Nombre = f["Nombre"]?.ToString() ?? ""
                })
                .Where(x => x.Clave.Replace("|", "").Length > 0) // ignora filas sin nombre
                .GroupBy(x => x.Clave);

            var conflicto = grupos.FirstOrDefault(g => g.Count() > 1);
            return conflicto?.First().Nombre;
        }

        // Actualiza el Nombre en memoria de cada PersonaRegistrada activa, para que el video y los logs
        // reflejen al instante lo que acabás de guardar en la grilla (sin esperar a reiniciar el programa).
        private void SincronizarNombresEnMemoria(DataTable tabla)
        {
            lock (lockObjetosNativos)
            {
                bool huboCambio = false;

                foreach (DataRow fila in tabla.Rows)
                {
                    int id = Convert.ToInt32(fila["Id"]);
                    PersonaRegistrada persona = listaPersonas.FirstOrDefault(p => p.IdBd == id);
                    if (persona == null) continue;

                    string nuevoNombre = fila["Nombre"]?.ToString() ?? persona.Nombre;
                    if (nuevoNombre != persona.Nombre)
                    {
                        persona.Nombre = nuevoNombre;
                        huboCambio = true;
                    }
                }

                // Los votos de reconocimiento (ultimosVotos) guardan el nombre VIEJO como texto. Si no se
                // vacían, durante ~7 fotogramas la identidad confirmada sería un nombre que ya no existe,
                // y no se encontraría a la persona para marcarla presente. Se vacían dentro del mismo lock
                // que usa el hilo de procesamiento, así que no hay carrera.
                if (huboCambio)
                {
                    ultimosVotos.Clear();
                    ultimoSujetoNotificado = "";
                }
            }
        }

        private void btnEliminarSeleccionado_Click(object sender, EventArgs e)
        {
            if (!sqlDisponible) { MostrarEstado("SQL Server no está disponible."); return; }
            if (dgvAlumnos.CurrentRow == null) return;

            int id = Convert.ToInt32(dgvAlumnos.CurrentRow.Cells["Id"].Value);
            DialogResult r = MessageBox.Show($"¿Eliminar el registro Id {id}?", "Confirmar eliminación",
                                             MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            try
            {
                administradorSQL.EliminarSujeto(id);

                lock (lockObjetosNativos)
                {
                    PersonaRegistrada p = listaPersonas.FirstOrDefault(x => x.IdBd == id);
                    if (p != null)
                    {
                        listaPersonas.Remove(p);
                        p.Dispose();
                    }
                }

                ActualizarGrilla();
            }
            catch (Exception ex)
            {
                LogMensaje($"[ERROR] No se pudo eliminar: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // UTILIDADES DE INTERFAZ
        // ------------------------------------------------------------------
        // ------------------------------------------------------------------
        // PANEL DE ESTADO DE CONEXIÓN
        // ------------------------------------------------------------------
        // Requiere en el Designer: Label "lblIP", Label "lblIndicadorConexion" (el puntito de color),
        // Label "lblEstadoConexion" (el texto "Conectado"/"Desconectado").
        //
        // No se basa solo en si hubo una excepción de red: si el stream se cuelga sin tirar error
        // todavía, esto lo detecta igual porque mide cuánto hace que NO llega un fotograma nuevo.
        private void IniciarMonitorConexion()
        {
            lblIP.Text = $"IP: {ipCamara}";
            Estetica.EstilizarIndicadorConexion(lblIndicadorConexion);
            lblIndicadorConexion.ForeColor = Estetica.ColorPeligro;
            lblEstadoConexion.Text = "Desconectado";

            timerEstadoConexion = new System.Windows.Forms.Timer { Interval = 2000 };
            timerEstadoConexion.Tick += (s, e) =>
            {
                bool conectado = ultimoFrameRecibido != DateTime.MinValue &&
                                 (DateTime.Now - ultimoFrameRecibido) < TimeSpan.FromSeconds(5);

                lblIndicadorConexion.ForeColor = conectado ? Estetica.ColorExito : Estetica.ColorPeligro;
                lblEstadoConexion.Text = conectado ? "Conectado" : "Desconectado";
            };
            timerEstadoConexion.Start();
        }

        // ------------------------------------------------------------------
        // GRÁFICO DE TEMPERATURA
        // ------------------------------------------------------------------
        // Requiere en el Designer: un Panel llamado "panelTemperatura" con su evento Paint
        // enganchado a panelTemperatura_Paint (doble clic en el Designer sobre el evento Paint,
        // o descomentar la línea en el constructor). Form1 solo junta los datos (el historial);
        // quién y cómo se dibuja el gráfico vive en Estetica.
        private void panelTemperatura_Paint(object sender, PaintEventArgs e)
        {
            List<(DateTime Hora, double Temp)> datos;
            lock (lockHistorialTemp) datos = new List<(DateTime, double)>(historialTemperatura);

            Estetica.DibujarGraficoTemperatura(e.Graphics, panelTemperatura.ClientRectangle, datos);
        }

        private void EjecutarEnUI(Action accion)
        {
            if (isDisposing || IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(accion); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private void LogMensaje(string mensaje)
        {
            if (isDisposing || IsDisposed) return;

            if (InvokeRequired)
            {
                EjecutarEnUI(() => LogMensaje(mensaje));
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            textBox3.AppendText($"[{timestamp}] {mensaje}{Environment.NewLine}");
        }

        // Estado de registro/lectura en textBox1 (con antirrebote para no saturar la UI)
        private void MostrarEstado(string mensaje)
        {
            DateTime ahora = DateTime.Now;
            if (mensaje == ultimoEstado && (ahora - ultimoEstadoTs).TotalMilliseconds < 1000) return;

            ultimoEstado = mensaje;
            ultimoEstadoTs = ahora;

        // LogMensaje(mensaje); // LogMensaje ya agrega la hora
        }

        // ------------------------------------------------------------------
        // TEMPERATURA DEL XIAO (sondeo periódico, no por fotograma)
        // ------------------------------------------------------------------
        private void IniciarSondeoTemperatura()
        {
            CancellationToken token = cancellationTokenSource.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && !isDisposing)
                {
                    try { await Task.Delay(intervaloTemp, token); }
                    catch (TaskCanceledException) { break; }

                    await ConsultarTemperaturaEsp32();
                }
            }, token);
        }

        private async Task ConsultarTemperaturaEsp32()
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(tempUrl);
                request.Timeout = 3000;
                request.Method = "GET";

                using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string texto = (await reader.ReadToEndAsync()).Trim();

                    if (double.TryParse(texto, System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out double temp))
                    {
                        // Se reemplaza el log de texto por fotograma (molesto al ir mirando el historial)
                        // por el gráfico: cada lectura se guarda con su hora y se pide un repintado.
                        lock (lockHistorialTemp)
                        {
                            historialTemperatura.Add((DateTime.Now, temp));
                            while (historialTemperatura.Count > MAX_PUNTOS_TEMP) historialTemperatura.RemoveAt(0);
                        }
                        EjecutarEnUI(() => panelTemperatura.Invalidate());

                        if (temp >= TEMP_ADVERTENCIA_C)
                            LogMensaje($"[AVISO] Temperatura del XIAO alta ({temp:F1} °C). Verifica ventilación.");
                    }
                    else
                    {
                        LogMensaje($"[TEMP XIAO] Respuesta no numérica: '{texto}'");
                    }
                }
            }
            catch (Exception)
            {
                // El endpoint /temp puede no existir todavía en el firmware: no se satura el log con esto.
                System.Diagnostics.Debug.WriteLine("[TEMP XIAO] Endpoint no disponible o sin respuesta.");
            }
        }

        // ------------------------------------------------------------------
        // RED: LECTURA DEL STREAM MJPEG
        // ------------------------------------------------------------------
        private void IniciarStreamCamara()
        {
            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = cancellationTokenSource.Token;

            Task.Factory.StartNew(() => CaptureMjpegStream(streamUrl, token), token,
                                  TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Task.Factory.StartNew(() => BucleProcesamiento(token), token,
                                  TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Task.Factory.StartNew(() => BucleVisualizacion(token), token,
                      TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void CaptureMjpegStream(string url, CancellationToken token)
        {
            byte[] buffer = new byte[1024 * 512];

            while (!token.IsCancellationRequested && !isDisposing)
            {
                HttpWebRequest request = null;
                HttpWebResponse response = null;
                CancellationTokenRegistration registro = default(CancellationTokenRegistration);

                try
                {
                    request = (HttpWebRequest)WebRequest.Create(url);
                    request.Timeout = 5000;
                    request.ReadWriteTimeout = 8000;   // si la ESP32 se cuelga, se reconecta en vez de bloquearse para siempre
                    request.KeepAlive = false;
                    request.AllowReadStreamBuffering = false;

                    HttpWebRequest reqLocal = request;
                    registro = token.Register(() => { try { reqLocal.Abort(); } catch { } }); // desbloquea Read() al cerrar

                    response = (HttpWebResponse)request.GetResponse();

                    using (Stream stream = response.GetResponseStream())
                    {
                        if (stream == null) throw new IOException("Respuesta sin flujo de datos.");

                        LogMensaje("[ÉXITO] Transmisión activa.");
                        int readOffset = 0;

                        while (!token.IsCancellationRequested && !isDisposing)
                        {
                            int bytesRead = stream.Read(buffer, readOffset, buffer.Length - readOffset);
                            if (bytesRead <= 0) break;

                            readOffset += bytesRead;
                            readOffset = ExtraerFrames(buffer, readOffset);
                        }
                    }

                    token.WaitHandle.WaitOne(500); // el servidor cerró la conexión: pequeña pausa antes de reconectar
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested && !isDisposing)
                    {
                        LogMensaje($"[STREAM] {ex.Message}. Reintentando...");
                        token.WaitHandle.WaitOne(1000);
                    }
                }
                finally
                {
                    registro.Dispose();
                    response?.Close();
                    request?.Abort();
                }
            }
        }

        // Extrae todos los JPEG completos del buffer, publica el último y devuelve los bytes que quedan pendientes.
        private int ExtraerFrames(byte[] buffer, int readOffset)
        {
            while (true)
            {
                int start = FindBytes(buffer, 0, readOffset, SOI);
                if (start < 0)
                {
                    // Sin inicio de JPEG: descartar todo salvo el último byte (podría ser el 0xFF de un marcador partido)
                    if (readOffset > 1) { buffer[0] = buffer[readOffset - 1]; readOffset = 1; }
                    break;
                }

                // El fin se busca DESPUÉS del inicio (antes podía tomar un 0xFFD9 de un fotograma anterior y atascarse)
                int end = FindBytes(buffer, start + 2, readOffset, EOI);
                if (end < 0)
                {
                    // Fotograma incompleto: descartar lo anterior al inicio y esperar más datos
                    if (start > 0)
                    {
                        Buffer.BlockCopy(buffer, start, buffer, 0, readOffset - start);
                        readOffset -= start;
                    }
                    break;
                }

                int length = (end + 2) - start;
                byte[] jpeg = new byte[length];
                Buffer.BlockCopy(buffer, start, jpeg, 0, length);
                PublicarFrame(jpeg);

                int remaining = readOffset - (end + 2);
                if (remaining > 0) Buffer.BlockCopy(buffer, end + 2, buffer, 0, remaining);
                readOffset = remaining;
                if (remaining == 0) break;
            }

            if (readOffset >= buffer.Length - 4096) readOffset = 0; // seguridad ante datos corruptos
            return readOffset;
        }

        private static int FindBytes(byte[] source, int from, int length, byte[] pattern)
        {
            for (int i = from; i <= length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }

        private void PublicarFrame(byte[] jpeg)
        {
            lock (lockFrame) { ultimoJpeg = jpeg; ultimoJpegVisor = jpeg; } // sobrescribe: siempre el MÁS RECIENTE
            ultimoFrameRecibido = DateTime.Now;
            nuevoFrame.Set();
            nuevoFrameVisor.Set();
        }

        // ------------------------------------------------------------------
        // PROCESAMIENTO (hilo independiente de la red)
        // ------------------------------------------------------------------
        private void BucleProcesamiento(CancellationToken token)
        {
            while (!token.IsCancellationRequested && !isDisposing)
            {
                if (!nuevoFrame.WaitOne(500)) continue;

                byte[] jpeg;
                lock (lockFrame) { jpeg = ultimoJpeg; ultimoJpeg = null; }
                if (jpeg == null) continue;

                try
                {
                    using (Mat frame = new Mat())
                    {
                        CvInvoke.Imdecode(jpeg, ImreadModes.Color, frame);
                        if (!frame.IsEmpty) ProcessFrame(frame);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PROCESO] {ex.Message}");
                }
            }
        }

        // Hilo de visualización: muestra cada fotograma apenas llega, SIN esperar al reconocimiento.
        private void BucleVisualizacion(CancellationToken token)
        {
            while (!token.IsCancellationRequested && !isDisposing)
            {
                if (!nuevoFrameVisor.WaitOne(500)) continue;

                byte[] jpeg;
                lock (lockFrame) { jpeg = ultimoJpegVisor; ultimoJpegVisor = null; }
                if (jpeg == null) continue;

                try
                {
                    using (Mat frame = new Mat())
                    {
                        CvInvoke.Imdecode(jpeg, ImreadModes.Color, frame);
                        if (frame.IsEmpty) continue;

                        SuperposicionFrame sp = superposicionPublicada;
                        if (sp != null && (DateTime.Now - sp.Creada).TotalMilliseconds > MS_VIGENCIA_SUPERPOSICION)
                            sp = null; // procesamiento demorado: mejor video limpio que un recuadro desfasado

                        MostrarFrame(frame, sp);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[VISOR] {ex.Message}");
                }
            }
        }

        private void ProcessFrame(Mat frame)
        {
            if (isDisposing || IsDisposed) return;

            // Cada fotograma arranca con la superposición vacía (recuadro + etiqueta del rostro y mensaje
            // al pie). Se llena durante el procesamiento y recién se dibuja en MostrarFrame, con GDI+.
            superposicion = new SuperposicionFrame();
            guiaCruda = null;
            pieEstadoTexto = null;

            try
            {
                lock (lockObjetosNativos)
                {
                    if (detectorNet == null || recognizerNet == null || isDisposing) return;

                    GestionarSolicitudes();

                    float escala;
                    using (Mat faces = DetectarRostros(frame, out escala))
                    {
                        float[] data;
                        int idx = SeleccionarRostroPrincipal(faces, out data);

                        if (idx >= 0 && escala != 1f)
                        {
                            // Los resultados vienen en coordenadas del frame reducido: se reescalan
                            // al tamaño original antes de usarlos (recorte y dibujo siguen en alta resolución).
                            int o = idx * faces.Cols;
                            for (int k = 0; k < 14; k++) data[o + k] /= escala;
                        }

                        if (idx < 0)
                        {
                            // Durante un registro, que no haya rostro también se le indica al usuario en pantalla
                            if (registroActivo) guiaCruda = MSG_CENTRO;
                            ResetearEstadoSiNoHayRostro();
                            ActualizarMetricas(null, null, null);
                        }
                        else
                        {
                            ProcesarRostro(frame, data, faces.Cols, idx);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error procesando fotograma: {ex.Message}");
            }

            ComponerPie();
            // Se publica el resultado terminado; el hilo de visualización lo dibuja sobre el video en vivo.
            superposicion.Creada = DateTime.Now;
            superposicionPublicada = superposicion;
        }

        // Corre YuNet sobre una copia reducida si el frame es grande; el frame original no se toca.
        private Mat DetectarRostros(Mat frame, out float escala)
        {
            escala = 1f;
            int ladoMax = Math.Max(frame.Width, frame.Height);
            Mat entrada = frame;
            bool copiaCreada = false;

            if (ladoMax > ESCALA_MAX_DETECCION)
            {
                escala = (float)ESCALA_MAX_DETECCION / ladoMax;
                entrada = new Mat();
                CvInvoke.Resize(frame, entrada, Size.Empty, escala, escala, Inter.Linear);
                copiaCreada = true;
            }

            if (ultimoTamanoDeteccion != entrada.Size)
            {
                ultimoTamanoDeteccion = entrada.Size;
                detectorNet.InputSize = entrada.Size;
            }

            Mat faces = new Mat();
            detectorNet.Detect(entrada, faces);

            if (copiaCreada) entrada.Dispose();
            return faces;
        }

        // Elige el rostro MÁS GRANDE (el más cercano a la cámara) entre los que superan el score mínimo
        private int SeleccionarRostroPrincipal(Mat faces, out float[] data)
        {
            data = null;
            if (faces.IsEmpty || faces.Rows == 0) return -1;

            data = new float[faces.Rows * faces.Cols];
            faces.CopyTo(data);

            int mejor = -1;
            float mayorArea = 0f;

            // Histéresis: si ya se venía siguiendo un rostro hace poco, alcanza con SALIDA; si no, se exige ENTRADA.
            bool seguimientoActivo = (DateTime.Now - ultimoRostroVisto).TotalMilliseconds < MS_MANTENER_SEGUIMIENTO;
            float scoreMinimo = seguimientoActivo ? SCORE_DETECCION_SALIDA : SCORE_DETECCION_ENTRADA;

            for (int i = 0; i < faces.Rows; i++)
            {
                int o = i * faces.Cols;
                if (data[o + 14] < scoreMinimo) continue;

                float area = data[o + 2] * data[o + 3];
                if (area > mayorArea) { mayorArea = area; mejor = i; }
            }
            return mejor;
        }

        private void ProcesarRostro(Mat frame, float[] data, int cols, int i)
        {
            int o = i * cols;
            ultimoRostroVisto = DateTime.Now;

            float w = data[o + 2];
            float h = data[o + 3];

            Rectangle rect = new Rectangle((int)data[o], (int)data[o + 1], (int)w, (int)h);
            rect = Rectangle.Intersect(rect, new Rectangle(0, 0, frame.Width, frame.Height));
            if (rect.Width < 20 || rect.Height < 20) return;

            // Rostro demasiado pequeño para reconocer con fiabilidad. La indicación va al pie de la imagen
            // (no sobre el rostro) y pasa por el filtro anti-parpadeo.
            if (!registroActivo && (w < MIN_LADO_RECONOCER || h < MIN_LADO_RECONOCER))
            {
                MarcarRostro(rect, null, COLOR_AMARILLO);
                guiaCruda = MSG_ACERCARSE;
                ActualizarMetricas(null, data[o + 14], null);   // <- NUEVA
                return;
            }

            // Fila de detección en coordenadas ya corregidas (data puede venir reescalado desde ProcessFrame)
            float[] filaValores = new float[cols];
            Array.Copy(data, o, filaValores, 0, cols);

            using (Mat filaRostro = new Mat(1, cols, DepthType.Cv32F, 1))
            using (Mat faceAligned = new Mat())
            using (Mat feature = new Mat())
            {
                filaRostro.SetTo(filaValores);
                recognizerNet.AlignCrop(frame, filaRostro, faceAligned);
                recognizerNet.Feature(faceAligned, feature);

                if (feature.IsEmpty) return;

                // ---------- MODO REGISTRO ----------
                if (registroActivo)
                {
                    AvanzarRegistro(frame.Size, data, o, faceAligned, feature);
                    if (registroActivo)
                    {
                        // Sobre el rostro solo va el recuadro; el progreso se muestra al pie.
                        MarcarRostro(rect, null, COLOR_AZUL);
                        ActualizarMetricas(null, data[o + 14], null);
                        pieEstadoTexto = $"Registrando... {muestrasRegistro.Count}/{MUESTRAS_REGISTRO}. {MSG_QUIETO}";
                        pieEstadoColor = COLOR_AZUL;
                    }
                    return;
                }

                // ---------- MODO RECONOCIMIENTO ----------
                // Misma validación que al registrar (pose de frente, nitidez, luz), pero con los
                // umbrales más permisivos de "paraRegistro: false". Si la cara está de perfil, lejos,
                // borrosa o mal iluminada, se descarta ACÁ, antes de intentar comparar contra nadie.
                string motivoRechazo;
                double nitidezReconocimiento;
                if (!EsRostroValido(frame.Size, data, o, faceAligned, paraRegistro: false, out motivoRechazo, out nitidezReconocimiento))
                {
                    MarcarRostro(rect, null, COLOR_AMARILLO);
                    guiaCruda = motivoRechazo; // el filtro decide si ya es momento de mostrarla
                    ActualizarMetricas(nitidezReconocimiento, data[o + 14], null);
                    return; // no se arriesga una identificación con un rostro que no cumple el estándar mínimo
                }

                double mejorSim = 0.0, segundaSim = 0.0;
                string mejorNombre = null;

                foreach (PersonaRegistrada persona in listaPersonas)
                {
                    double sim = recognizerNet.Match(persona.Plantilla, feature, FaceRecognizerSF.DisType.Cosine);
                    if (sim > mejorSim)
                    {
                        segundaSim = mejorSim;
                        mejorSim = sim;
                        mejorNombre = persona.Nombre;
                    }
                    else if (sim > segundaSim)
                    {
                        segundaSim = sim;
                    }
                }

                bool margenSuficiente = (mejorSim - segundaSim) >= MARGEN_AMBIGUO;
                string candidato;

                if (mejorNombre != null && mejorSim >= UMBRAL_SFACE && margenSuficiente)
                    candidato = mejorNombre;
                else if (mejorNombre != null && mejorSim >= UMBRAL_SFACE && !margenSuficiente)
                    candidato = ESTADO_AMBIGUO; // dos personas registradas dan una similitud muy parecida
                else
                    candidato = "Desconocido";

                double simPromedio;
                string identidad = ConfirmarIdentidad(candidato, mejorSim, out simPromedio);

                bool analizando = (identidad == ESTADO_ANALIZANDO || identidad == ESTADO_AMBIGUO);
                bool conocido = !analizando && identidad != "Desconocido";
                Color color = analizando ? COLOR_AMARILLO : (conocido ? COLOR_VERDE : COLOR_ROJO);

                // Solo el recuadro de color; el nombre y el estado van únicamente al pie.
                MarcarRostro(rect, null, color);
                ActualizarMetricas(nitidezReconocimiento, data[o + 14], simPromedio);


                // Mensaje al pie de la imagen
                if (analizando)
                {
                    pieEstadoTexto = MSG_QUIETO;
                    pieEstadoColor = COLOR_AMARILLO;
                }
                else if (conocido)
                {
                    pieEstadoTexto = $"{identidad} está presente";
                    pieEstadoColor = COLOR_VERDE;
                }
                else
                {
                    pieEstadoTexto = "Desconocido: no está registrado en el sistema"; 
                    pieEstadoColor = COLOR_ROJO;
                }

                if (!analizando)
                {
                    bool esDiferenteSujeto = (identidad != ultimoSujetoNotificado);
                    bool cooldownExpirado = (DateTime.Now - ultimaNotificacion) > cooldownNotificacion;

                    if (esDiferenteSujeto || cooldownExpirado)
                    {
                        ultimoSujetoNotificado = identidad;
                        ultimaNotificacion = DateTime.Now;

                        string sujetoCopy = identidad;
                        double simCopy = simPromedio;
                        EjecutarEnUI(() => ActualizarInterfazLectura(sujetoCopy, simCopy));

                        // Marca presente en SQL fuera del lock: la latencia de red no debe frenar el video.
                        if (sqlDisponible && conocido)
                        {
                            PersonaRegistrada persona = listaPersonas.FirstOrDefault(p => p.Nombre == identidad);
                            if (persona?.IdBd != null)
                            {
                                int idParaSql = persona.IdBd.Value;
                                Task.Run(() =>
                                {
                                    try
                                    {
                                        administradorSQL.MarcarPresente(idParaSql);
                                        ActualizarGrilla();
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[SQL] {ex.Message}");
                                    }
                                });
                            }
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // SUPERPOSICIÓN: recuadro, etiqueta del rostro y mensaje al pie
        // ------------------------------------------------------------------
        // Se arma durante el procesamiento (solo guarda datos) y se dibuja en MostrarFrame con GDI+.
        // CvInvoke.PutText usa fuentes Hershey, que SOLO tienen ASCII: cualquier tilde, "ñ", "¿" o "¡"
        // sale como "?". GDI+ dibuja texto Unicode normal (Segoe UI), así que se ven bien.
        private void MarcarRostro(Rectangle rect, string texto, Color color)
        {
            superposicion.Rostro = new EtiquetaRostro { Rect = rect, Texto = texto, Color = color };
        }

        // ------------------------------------------------------------------
        // MÉTRICAS EN VIVO
        // ------------------------------------------------------------------
        // Se llama desde el hilo de procesamiento. Un valor null significa "no disponible" y se muestra "--".
        private void ActualizarMetricas(double? nitidez, double? confianzaYuNet, double? similitud)
        {
            DateTime ahora = DateTime.Now;
            if ((ahora - ultimaActualizacionMetricas).TotalMilliseconds < MS_ENTRE_METRICAS) return;
            ultimaActualizacionMetricas = ahora;

            string sNitidez = (nitidez.HasValue && nitidez.Value > 0) ? nitidez.Value.ToString("F0") : "--";
            string sConfianza = confianzaYuNet.HasValue ? (confianzaYuNet.Value * 100.0).ToString("F0") + "%" : "--";
            string sSimilitud = similitud.HasValue ? similitud.Value.ToString("F2") : "--";

            string texto =
                "MÉTRICAS EN VIVO:" + Environment.NewLine +
                "--------------" + Environment.NewLine +
                $"NITIDEZ (Fija: >{UMBRAL_NITIDEZ_RECONOCER:F0}):  {sNitidez}" + Environment.NewLine +
                $"CONFIDENCIA (YuNet):  {sConfianza}" + Environment.NewLine +
                $"SIMILITUD (Fija: >{UMBRAL_SFACE:F2}):  {sSimilitud}";

            EjecutarEnUI(() => textBox1.Text = texto);
        }
        // Elige qué mensaje va al pie. Prioridad: 1) mensaje temporal (ej. "Usted ha sido registrado"),
        // 2) indicación de posicionamiento ya filtrada (sin parpadeo), 3) estado normal (presente, etc.).
        private void ComponerPie()
        {
            DateTime ahora = DateTime.Now;

            // Se actualiza SIEMPRE (aunque haya un temporal) para que los tiempos del filtro sigan corriendo.
            string guia = filtroGuia.Actualizar(guiaCruda, ahora);

            if (pieTemporalTexto != null && ahora < pieTemporalHasta)
            {
                superposicion.Pie = pieTemporalTexto;
                superposicion.ColorPie = pieTemporalColor;
            }
            else if (guia != null)
            {
                superposicion.Pie = guia;
                superposicion.ColorPie = COLOR_AMARILLO;
            }
            else if (pieEstadoTexto != null)
            {
                superposicion.Pie = pieEstadoTexto;
                superposicion.ColorPie = pieEstadoColor;
            }
        }

        private void EstablecerPieTemporal(string texto, Color color, int milisegundos)
        {
            pieTemporalTexto = texto;
            pieTemporalColor = color;
            pieTemporalHasta = DateTime.Now.AddMilliseconds(milisegundos);
        }

        private static void DibujarSuperposicion(Bitmap bmp, SuperposicionFrame sp)
        {
            if (sp == null || (sp.Rostro == null && string.IsNullOrEmpty(sp.Pie))) return;

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                if (sp.Rostro != null) DibujarEtiquetaRostro(g, bmp.Size, sp.Rostro);
                if (!string.IsNullOrEmpty(sp.Pie)) DibujarPie(g, bmp.Size, sp.Pie, sp.ColorPie);
            }
        }

        // Recuadro + texto ARRIBA del rostro (si no cabe arriba, se pone dentro del recuadro).
        private static void DibujarEtiquetaRostro(Graphics g, Size tamano, EtiquetaRostro et)
        {
            using (Pen lapiz = new Pen(et.Color, 2f))
                g.DrawRectangle(lapiz, et.Rect);

            if (string.IsNullOrEmpty(et.Texto)) return;

            float px = Math.Max(13f, tamano.Height * 0.045f); // el tamaño escala con la resolución del fotograma
            using (Font fuente = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush pincelTexto = new SolidBrush(et.Color))
            using (Brush fondo = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
            {
                SizeF medida = g.MeasureString(et.Texto, fuente);
                float x = Math.Min(Math.Max(et.Rect.X, 0), Math.Max(0f, tamano.Width - medida.Width));
                float y = et.Rect.Y - medida.Height - 2;
                if (y < 0) y = et.Rect.Y + 2;

                g.FillRectangle(fondo, x, y, medida.Width, medida.Height);
                g.DrawString(et.Texto, fuente, pincelTexto, x, y);
            }
        }

        // Banda oscura semitransparente en la parte inferior, con el texto centrado y del color indicado.
        private static void DibujarPie(Graphics g, Size tamano, string texto, Color color)
        {
            float px = Math.Max(14f, tamano.Height * 0.055f);
            float margen = tamano.Width * 0.03f;
            float anchoTexto = tamano.Width - 2f * margen;

            using (Font fuente = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Brush pincelTexto = new SolidBrush(color))
            using (Brush fondo = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
            using (StringFormat formato = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                SizeF medida = g.MeasureString(texto, fuente, (int)anchoTexto); // si es largo, se parte en líneas
                float alto = medida.Height + px * 0.6f;
                float yBanda = tamano.Height - alto;

                g.FillRectangle(fondo, 0f, yBanda, tamano.Width, alto);
                g.DrawString(texto, fuente, pincelTexto, new RectangleF(margen, yBanda, anchoTexto, alto), formato);
            }
        }

        // Votación sobre los últimos fotogramas: elimina el "parpadeo" de identidades por ruido del stream
        private string ConfirmarIdentidad(string candidato, double similitud, out double simPromedio)
        {
            ultimosVotos.Enqueue(new KeyValuePair<string, double>(candidato, similitud));
            while (ultimosVotos.Count > VENTANA_VOTOS) ultimosVotos.Dequeue();

            var ganador = ultimosVotos.GroupBy(v => v.Key).OrderByDescending(g => g.Count()).First();
            simPromedio = ganador.Average(v => v.Value);

            if (ultimosVotos.Count < VOTOS_MINIMOS || ganador.Count() < VOTOS_MINIMOS)
                return candidato == ESTADO_AMBIGUO ? ESTADO_AMBIGUO : ESTADO_ANALIZANDO;

            return ganador.Key;
        }

        private void ResetearEstadoSiNoHayRostro()
        {
            if ((DateTime.Now - ultimoRostroVisto).TotalSeconds > 1.0)
                ultimosVotos.Clear();

            if ((DateTime.Now - ultimaNotificacion).TotalSeconds > 2)
                ultimoSujetoNotificado = "";
        }

        // ------------------------------------------------------------------
        // REGISTRO (varias muestras validadas -> una plantilla promedio)
        // ------------------------------------------------------------------
        private void GestionarSolicitudes()
        {
            if (solicitudBorrado)
            {
                solicitudBorrado = false;
                CancelarRegistro(null);

                if (sqlDisponible)
                {
                    try { administradorSQL.EliminarTodos(); }
                    catch (Exception ex) { LogMensaje($"[ERROR] No se pudo borrar en SQL Server: {ex.Message}"); }
                }

                foreach (PersonaRegistrada p in listaPersonas) p.Dispose();
                listaPersonas.Clear();
                ultimosVotos.Clear();

                EjecutarEnUI(() => textBox2.AppendText("[Registros borrados]" + Environment.NewLine));
                MostrarEstado("Todos los registros fueron borrados.");
                ActualizarGrilla();
            }

            if (solicitudRegistro)
            {
                solicitudRegistro = false;

                if (listaPersonas.Count >= MAX_PERSONAS)
                {
                    MostrarEstado($"Límite alcanzado ({MAX_PERSONAS} personas). Usa 'Borrar todo' para liberar espacio.");
                }
                else if (!registroActivo)
                {
                    LiberarMuestrasRegistro();
                    registroActivo = true;
                    inicioRegistro = DateTime.Now;
                    ultimaMuestra = DateTime.MinValue;
                    MostrarEstado("Registro iniciado: mire de frente a la cámara y manténgase quieto.");
                }
            }

            if (registroActivo && (DateTime.Now - inicioRegistro).TotalMilliseconds > MS_TIMEOUT_REGISTRO)
            {
                CancelarRegistro("Registro cancelado por tiempo. Presiona 'Registrar' para intentarlo de nuevo.");
            }
        }

        private void AvanzarRegistro(Size frameSize, float[] data, int o, Mat faceAligned, Mat feature)
        {
            string motivo;
            double nitidez;

            if (!EsRostroValido(frameSize, data, o, faceAligned, paraRegistro: true, out motivo, out nitidez))
            {
                guiaCruda = motivo; // va al pie de la imagen, ya filtrada contra parpadeos
                return;
            }

            if ((DateTime.Now - ultimaMuestra).TotalMilliseconds < MS_ENTRE_MUESTRAS) return;

            // Todas las muestras deben ser de la misma persona
            if (muestrasRegistro.Count > 0)
            {
                double consistencia = recognizerNet.Match(muestrasRegistro[0], feature, FaceRecognizerSF.DisType.Cosine);
                if (consistencia < UMBRAL_CONSISTENCIA)
                {
                    guiaCruda = "Debe haber una sola persona frente a la cámara";
                    return;
                }
            }

            Mat copia = new Mat();
            feature.CopyTo(copia);
            muestrasRegistro.Add(copia);
            ultimaMuestra = DateTime.Now;

            LogMensaje($"[REGISTRO] Muestra {muestrasRegistro.Count}/{MUESTRAS_REGISTRO} (nitidez {nitidez:F0}, score {data[o + 14]:F2})");
            MostrarEstado($"Registrando... {muestrasRegistro.Count}/{MUESTRAS_REGISTRO}");

            if (muestrasRegistro.Count >= MUESTRAS_REGISTRO) FinalizarRegistro();
        }

        // BUG CORREGIDO: esta validación (pose de frente, nitidez, luz) antes SOLO se llamaba al
        // registrar (AvanzarRegistro). El reconocimiento en vivo nunca la usaba — por eso una cara
        // de perfil, lejos o borrosa igual se intentaba reconocer, usando solo tamaño/brillo/asimetría.
        // Ahora la misma función sirve para los dos modos: "paraRegistro" decide qué tan exigentes son
        // el score mínimo, el tamaño mínimo y la nitidez mínima (el registro siempre es más estricto
        // que el reconocimiento, porque de ahí sale la plantilla que se usa para comparar todo después).
        private bool EsRostroValido(Size frameSize, float[] data, int o, Mat faceAligned, bool paraRegistro,
                                    out string motivo, out double nitidez)
        {
            motivo = "";
            nitidez = 0;

            float x = data[o];
            float y = data[o + 1];
            float w = data[o + 2];
            float h = data[o + 3];
            float score = data[o + 14];

            // En reconocimiento se usa el umbral de SALIDA: el de ENTRADA ya se exigió en SeleccionarRostroPrincipal.
            float scoreMinimo = paraRegistro ? SCORE_REGISTRO : SCORE_DETECCION_SALIDA;
            int ladoMinimo = paraRegistro ? MIN_LADO_REGISTRO : MIN_LADO_RECONOCER;

            if (score < scoreMinimo)
            {
                motivo = MSG_DE_FRENTE;
                return false;
            }

            if (w < ladoMinimo || h < ladoMinimo)
            {
                motivo = MSG_ACERCARSE;
                return false;
            }

            if (x < MARGEN_BORDE || y < MARGEN_BORDE ||
                x + w > frameSize.Width - MARGEN_BORDE || y + h > frameSize.Height - MARGEN_BORDE)
            {
                // Rostro cortado por el borde: si es grande, está demasiado cerca; si es chico, está corrido.
                bool demasiadoCerca = Math.Max(w / frameSize.Width, h / frameSize.Height) > FRACCION_ROSTRO_DEMASIADO_CERCA;
                motivo = demasiadoCerca ? MSG_ALEJARSE : MSG_CENTRO;
                return false;
            }

            PointF ojoDerecho = new PointF(data[o + 4], data[o + 5]);
            PointF ojoIzquierdo = new PointF(data[o + 6], data[o + 7]);
            PointF nariz = new PointF(data[o + 8], data[o + 9]);

            double deltaY = ojoIzquierdo.Y - ojoDerecho.Y;
            double deltaX = ojoIzquierdo.X - ojoDerecho.X;
            double anguloRoll = Math.Abs(Math.Atan2(deltaY, deltaX) * (180.0 / Math.PI));

            if (anguloRoll > 12.0)
            {
                motivo = MSG_NIVELAR;
                return false;
            }

            // Esta es la relación que detecta PERFIL: con la cara de frente, la nariz queda a mitad
            // de camino entre los dos ojos (relación cercana a 1.0). Girando la cabeza hacia un
            // costado, la nariz se acerca a uno de los ojos y se aleja del otro, y la relación se
            // dispara para arriba o para abajo. Fuera de [0.6, 1.67] ya se considera perfil, no frente.
            double distOjoDerNariz = Math.Abs(nariz.X - ojoDerecho.X);
            double distOjoIzqNariz = Math.Abs(ojoIzquierdo.X - nariz.X);
            double relacionYaw = distOjoDerNariz / (distOjoIzqNariz + 1e-5);

            if (relacionYaw < UMBRAL_YAW_MIN || relacionYaw > UMBRAL_YAW_MAX)
            {
                motivo = MSG_DE_FRENTE;
                return false;
            }

            double brillo = CalcularBrillo(faceAligned);
            if (brillo < BRILLO_MIN || brillo > BRILLO_MAX)
            {
                motivo = brillo < BRILLO_MIN ? MSG_POCA_LUZ : MSG_MUCHA_LUZ;
                return false;
            }

            double asimetria = CalcularAsimetriaIluminacion(faceAligned);
            if (asimetria > ASIMETRIA_MAX)
            {
                motivo = MSG_LUZ_LATERAL;
                return false;
            }

            nitidez = CalcularNitidez(faceAligned);
            double umbralNitidezAplicado = paraRegistro ? UMBRAL_NITIDEZ : UMBRAL_NITIDEZ_RECONOCER;
            if (nitidez < umbralNitidezAplicado)
            {
                motivo = MSG_QUIETO; // el valor de nitidez sigue disponible en el parámetro de salida "nitidez" para depurar
                return false;
            }

            return true;
        }

        // Varianza del Laplaciano sobre el rostro alineado (112x112): mide el enfoque/movimiento
        private double CalcularNitidez(Mat faceAligned)
        {
            using (Mat gris = new Mat())
            using (Mat laplaciano = new Mat())
            {
                CvInvoke.CvtColor(faceAligned, gris, ColorConversion.Bgr2Gray);
                CvInvoke.Laplacian(gris, laplaciano, DepthType.Cv64F);

                MCvScalar media = new MCvScalar();
                MCvScalar desviacion = new MCvScalar();
                CvInvoke.MeanStdDev(laplaciano, ref media, ref desviacion);
                return desviacion.V0 * desviacion.V0;
            }
        }

        // Media de gris del rostro alineado: valores bajos = a contraluz/oscuro, altos = luz directa
        private double CalcularBrillo(Mat faceAligned)
        {
            using (Mat gris = new Mat())
            {
                CvInvoke.CvtColor(faceAligned, gris, ColorConversion.Bgr2Gray);
                return CvInvoke.Mean(gris).V0;
            }
        }

        // Diferencia de brillo entre la mitad izquierda y derecha del rostro. CalcularBrillo() solo
        // detecta cuando TODA la cara está muy oscura o muy clara en promedio; no detecta el caso típico
        // de contraluz lateral (una ventana de un lado) donde el promedio general puede parecer normal
        // pero un lado de la cara está quemado de luz y el otro en sombra, degradando igual el vector.
        private double CalcularAsimetriaIluminacion(Mat faceAligned)
        {
            using (Mat gris = new Mat())
            {
                CvInvoke.CvtColor(faceAligned, gris, ColorConversion.Bgr2Gray);
                int mitad = gris.Cols / 2;

                using (Mat izquierda = new Mat(gris, new Rectangle(0, 0, mitad, gris.Rows)))
                using (Mat derecha = new Mat(gris, new Rectangle(mitad, 0, gris.Cols - mitad, gris.Rows)))
                {
                    double brilloIzq = CvInvoke.Mean(izquierda).V0;
                    double brilloDer = CvInvoke.Mean(derecha).V0;
                    return Math.Abs(brilloIzq - brilloDer);
                }
            }
        }

        // Promedio normalizado de varias muestras: una sola plantilla por persona en vez de 5 vectores sueltos
        private Mat CalcularCentroide(List<Mat> muestras)
        {
            int dim = muestras[0].Cols;
            float[] suma = new float[dim];

            foreach (Mat m in muestras)
            {
                float[] v = ValoresDeMat(m);
                for (int k = 0; k < dim; k++) suma[k] += v[k];
            }

            float norma = 0f;
            for (int k = 0; k < dim; k++)
            {
                suma[k] /= muestras.Count;
                norma += suma[k] * suma[k];
            }
            norma = (float)Math.Sqrt(norma);
            if (norma > 1e-6f)
                for (int k = 0; k < dim; k++) suma[k] /= norma;

            Mat centroide = new Mat(1, dim, DepthType.Cv32F, 1);
            centroide.SetTo(suma);
            return centroide;
        }

        private void FinalizarRegistro()
        {
            // Evitar duplicados: si el rostro ya coincide con alguien registrado, no se crea otra persona
            PersonaRegistrada duplicada = null;
            foreach (PersonaRegistrada p in listaPersonas)
            {
                foreach (Mat muestra in muestrasRegistro)
                {
                    double sim = recognizerNet.Match(p.Plantilla, muestra, FaceRecognizerSF.DisType.Cosine);
                    if (sim >= UMBRAL_SFACE) { duplicada = p; break; }
                }
                if (duplicada != null) break;
            }

            if (duplicada != null)
            {
                CancelarRegistro($"Este rostro ya está registrado como {duplicada.Nombre}. No se creó un duplicado.");
                return;
            }

            Mat centroide = CalcularCentroide(muestrasRegistro);
            PersonaRegistrada nueva = new PersonaRegistrada { Plantilla = centroide };

            // BUG CORREGIDO: antes el nombre salía de "listaPersonas.Count + 1". Ese número depende de
            // cuánta gente hay CARGADA EN MEMORIA en este momento, no de cuánta gente se registró alguna
            // vez. Si borrabas a alguien (el conteo bajaba) y registrabas a otra persona, el nombre se
            // repetía (por eso viste dos "Sujeto 2"). Ahora el nombre sale del Id real de SQL Server
            // (IDENTITY), que NUNCA se repite ni se reutiliza aunque borres filas — cada persona nueva
            // recibe un Id más alto que cualquiera que haya existido antes, para siempre.
            if (sqlDisponible)
            {
                try
                {
                    byte[] plantillaBytes = VectorABytes(ValoresDeMat(centroide));
                    // Inserta con un nombre provisorio y en la misma operación lo renombra a "Sujeto {Id real}"
                    nueva.IdBd = administradorSQL.InsertarSujetoAutomatico(plantillaBytes);
                    nueva.Id = nueva.IdBd.Value;
                    nueva.Nombre = $"Sujeto {nueva.IdBd.Value}";
                }
                catch (Exception ex)
                {
                    LogMensaje($"[AVISO] No se pudo guardar en SQL Server: {ex.Message}");
                }
            }

            if (nueva.Nombre == null)
            {
                // Sin SQL: contador local que solo avanza (ver comentario en su declaración)
                proximoIdLocalFallback++;
                nueva.Id = proximoIdLocalFallback;
                nueva.Nombre = $"Sujeto {proximoIdLocalFallback}";
            }

            listaPersonas.Add(nueva);
            LiberarMuestrasRegistro(); // las muestras crudas ya no hacen falta: quedó la plantilla
            registroActivo = false;
            ultimosVotos.Clear();
            ActualizarGrilla();

            // Confirmación para el usuario, al pie de la imagen (texto del Word de objetivos)
            filtroGuia.Reiniciar();
            EstablecerPieTemporal("Usted ha sido registrado en el sistema", COLOR_VERDE, MS_PIE_REGISTRADO);

            string nombre = nueva.Nombre;
            int total = listaPersonas.Count;
            string horaLocal = DateTime.Now.ToString("HH:mm:ss");

            EjecutarEnUI(() =>
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"[{horaLocal}] ¡CAPTURA EXITOSA!");
                sb.AppendLine($"  • Registrado como: {nombre}");
                sb.AppendLine($"  • Muestras usadas para la plantilla: {MUESTRAS_REGISTRO}");
                sb.AppendLine($"  • Estado: Guardado en memoria ({total}/{MAX_PERSONAS})");
                sb.AppendLine("--------------------------------------------------");
                textBox2.AppendText(sb.ToString());
                
            });
        }

        private void CancelarRegistro(string motivo)
        {
            LiberarMuestrasRegistro();
            registroActivo = false;
            if (motivo != null) MostrarEstado(motivo);
        }

        private void LiberarMuestrasRegistro()
        {
            foreach (Mat m in muestrasRegistro) m.Dispose();
            muestrasRegistro.Clear();
        }

        // ------------------------------------------------------------------
        // INTERFAZ
        // ------------------------------------------------------------------
        private void ActualizarInterfazLectura(string sujeto, double similitud)
        {
            if (isDisposing || IsDisposed) return;

            string horaLocal = DateTime.Now.ToString("HH:mm:ss");

            if (sujeto != "Desconocido")
                textBox2.AppendText($"[{horaLocal}] ACCESO CONCEDIDO: {sujeto} (Similitud: {similitud:F2}){Environment.NewLine}");
            else
                textBox2.AppendText($"[{horaLocal}] ACCESO DENEGADO: DESCONOCIDO (Similitud: {similitud:F2}){Environment.NewLine}");
        
        }

        // Muestra el fotograma sin acumular bitmaps si la interfaz va más lenta que el procesamiento
        private void MostrarFrame(Mat frame, SuperposicionFrame sp)
        {
            if (pictureBox1 == null || IsDisposed || isDisposing) return;
            if (Interlocked.Exchange(ref frameUIPendiente, 1) == 1) return; // la UI sigue ocupada: descartar este

            Bitmap frameBitmap = null;
            try
            {
                frameBitmap = frame.ToBitmap();
                DibujarSuperposicion(frameBitmap, sp); // texto y recuadro con GDI+ (Unicode)
                Bitmap bmp = frameBitmap;

                BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (isDisposing || IsDisposed) { bmp.Dispose(); return; }

                        Image anterior = pictureBox1.Image;
                        pictureBox1.Image = bmp;
                        anterior?.Dispose();
                    }
                    finally
                    {
                        Interlocked.Exchange(ref frameUIPendiente, 0);
                    }
                }));
            }
            catch (Exception)
            {
                frameBitmap?.Dispose();
                Interlocked.Exchange(ref frameUIPendiente, 0);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            isDisposing = true;
            cancellationTokenSource?.Cancel();
            timerEstadoConexion?.Stop();

            // Si el hilo de procesamiento está a mitad de un fotograma, este lock espera a que termine
            lock (lockObjetosNativos)
            {
                detectorNet?.Dispose();
                detectorNet = null;

                recognizerNet?.Dispose();
                recognizerNet = null;

                LiberarMuestrasRegistro();
                foreach (PersonaRegistrada p in listaPersonas) p.Dispose();
                listaPersonas.Clear();
            }
        }
    }

    // Filtro anti-parpadeo para las indicaciones al usuario.
    //   - APARECER: el problema debe verse ininterrumpidamente durante "retardoAparicion".
    //   - PERMANECER: una vez visible, no se oculta ni cambia antes de "minVisible".
    //   - DESAPARECER: el rostro debe estar bien ininterrumpidamente durante "retardoDesaparicion".
    // Si el mensaje del fotograma oscila (mal, bien, mal, bien...), nunca se cumple una espera completa
    // y lo mostrado no cambia: no hay parpadeo.
    public class FiltroMensajeGuia
    {
        private readonly int retardoAparicionMs;
        private readonly int retardoDesaparicionMs;
        private readonly int minVisibleMs;

        private string crudo;                       // lo que dice el fotograma actual (null = todo bien)
        private DateTime crudoDesde = DateTime.MinValue;   // desde cuándo dice lo mismo
        private string mostrado;                    // lo que se muestra ahora
        private DateTime mostradoDesde = DateTime.MinValue;

        public FiltroMensajeGuia(int retardoAparicionMs, int retardoDesaparicionMs, int minVisibleMs)
        {
            this.retardoAparicionMs = retardoAparicionMs;
            this.retardoDesaparicionMs = retardoDesaparicionMs;
            this.minVisibleMs = minVisibleMs;
        }

        public string Actualizar(string mensajeCrudo, DateTime ahora)
        {
            if (mensajeCrudo != crudo)
            {
                crudo = mensajeCrudo;
                crudoDesde = ahora;
            }

            if (crudo == mostrado) return mostrado;

            double estable = (ahora - crudoDesde).TotalMilliseconds;
            double visible = (ahora - mostradoDesde).TotalMilliseconds;

            if (mostrado == null)
            {
                // Nada visible: solo aparece si el problema persiste
                if (estable >= retardoAparicionMs)
                {
                    mostrado = crudo;
                    mostradoDesde = ahora;
                }
            }
            else
            {
                // Hay algo visible: ocultarlo (crudo == null) o cambiarlo por otro exige más estabilidad y tiempo mínimo
                int espera = (crudo == null) ? retardoDesaparicionMs : retardoAparicionMs;
                if (estable >= espera && visible >= minVisibleMs)
                {
                    mostrado = crudo;
                    mostradoDesde = ahora;
                }
            }

            return mostrado;
        }

        public void Reiniciar()
        {
            crudo = null;
            crudoDesde = DateTime.MinValue;
            mostrado = null;
            mostradoDesde = DateTime.MinValue;
        }
    }

    public class PersonaRegistrada : IDisposable
    {
        public int Id { get; set; }
        public int? IdBd { get; set; }       // Id real en SQL Server (null si todavía no se guardó)
        public string Nombre { get; set; }

        // Vector 128D promedio y normalizado de las muestras validadas al registrar
        public Mat Plantilla { get; set; }

        public void Dispose()
        {
            Plantilla?.Dispose();
            Plantilla = null;
        }
    }
}