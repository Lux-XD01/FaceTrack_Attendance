// HORARIOS DE ASISTENCIA - Lógica pura (sin controles visuales)
//
// Contiene:
//   * HorarioDia ........ el horario de UN día de la semana (inicio / fin, o vacío)
//   * EstadoFichaje ..... los posibles resultados al intentar fichar
//   * ResultadoFichaje .. el resultado completo (si se permite, qué texto va a SQL, qué mensaje mostrar)
//   * GestorHorarios .... guarda los horarios en memoria y decide el estado de un fichaje
//
// IMPORTANTE: GestorHorarios.Evaluar() se llama desde el hilo de video (ProcesarRostro), no desde el
// hilo de la interfaz. Por eso el diccionario de horarios nunca se modifica "en el lugar": cada cambio
// crea una copia nueva y reemplaza la referencia (campo volatile). Así el hilo de video siempre lee
// un diccionario completo y consistente, sin necesidad de locks.

using System;
using System.Collections.Generic;

namespace Proyecto_ReconocimientoFacial_0._1
{
    public class HorarioDia
    {
        public DayOfWeek Dia { get; set; }
        public TimeSpan? Inicio { get; set; }
        public TimeSpan? Fin { get; set; }

        public bool EstaVacio
        {
            get { return !Inicio.HasValue || !Fin.HasValue; }
        }
    }

    public enum EstadoFichaje
    {
        Presente,           // dentro de los primeros 15 min  -> se guarda "Presente"
        LlegadaTarde,       // entre +15 min y (fin - 1 h)    -> se guarda "Llegada tarde"
        TodaviaNoEmpezo,    // antes de la hora de inicio     -> rechazado
        LlegadaTardia,      // desde (fin - 1 h) hasta el fin -> rechazado
        ClasesTerminadas,   // a partir de la hora de fin     -> rechazado
        SinHorario          // no hay horario cargado hoy     -> depende de BloquearSinHorario
    }

    public class ResultadoFichaje
    {
        public EstadoFichaje Estado { get; set; }
        public bool Permitido { get; set; }

        // Texto que se guarda en la columna Estado de la tabla Alumnos (solo si Permitido)
        public string TextoSql { get; set; }

        // Texto del indicador del panel de horarios ("Horario de asistencia activo", etc.)
        public string Descripcion { get; set; }

        // Mensaje para la persona frente a la cámara (pie de la imagen)
        public string MensajePantalla(string nombre)
        {
            switch (Estado)
            {
                case EstadoFichaje.Presente:
                case EstadoFichaje.SinHorario:
                    return Permitido ? nombre + " está presente" : Descripcion;
                case EstadoFichaje.LlegadaTarde:
                    return nombre + ": llegada tarde registrada";
                case EstadoFichaje.LlegadaTardia:
                    return "No se puede registrar, llegada tardía";
                case EstadoFichaje.ClasesTerminadas:
                    return "Las clases terminaron por hoy";
                case EstadoFichaje.TodaviaNoEmpezo:
                    return "Todavía no comenzó el horario de asistencia";
                default:
                    return Descripcion;
            }
        }
    }

    public class GestorHorarios
    {
        // Los márgenes son proporcionales a la duración de la clase de ESE día:
        //   Presente      : hasta el 15 % de la duración (clase de 60 min = primeros 15 min)
        //   Llegada tarde : del 15 % al 85 % de la duración
        //   Rechazado     : pasado el 85 % ("No se puede registrar, llegada tardía")
        // Como 15 % < 85 % siempre, los tramos nunca se pisan, sea la clase larga o corta.
        public const double PORCENTAJE_PRESENTE = 0.15;
        public const double PORCENTAJE_LIMITE_TARDE = 0.85;

        // false = si hoy no hay horario cargado, se sigue fichando como siempre (como antes de agregar
        //         esta función). true = sin horario no se puede fichar.
        public bool BloquearSinHorario { get; set; } = false;

        private volatile Dictionary<DayOfWeek, HorarioDia> horarios = new Dictionary<DayOfWeek, HorarioDia>();

        public HorarioDia Obtener(DayOfWeek dia)
        {
            HorarioDia h;
            return horarios.TryGetValue(dia, out h) ? h : null;
        }

        public void Establecer(HorarioDia horario)
        {
            Dictionary<DayOfWeek, HorarioDia> copia = new Dictionary<DayOfWeek, HorarioDia>(horarios);
            copia[horario.Dia] = horario;
            horarios = copia; // reemplazo atómico de la referencia
        }

        public void Reemplazar(IEnumerable<HorarioDia> lista)
        {
            Dictionary<DayOfWeek, HorarioDia> nuevo = new Dictionary<DayOfWeek, HorarioDia>();
            foreach (HorarioDia h in lista) nuevo[h.Dia] = h;
            horarios = nuevo;
        }

        public IEnumerable<HorarioDia> Todos()
        {
            return new List<HorarioDia>(horarios.Values);
        }

        // Decide qué pasa si alguien ficha en el momento "ahora".
        public ResultadoFichaje Evaluar(DateTime ahora)
        {
            HorarioDia h = Obtener(ahora.DayOfWeek);

            if (h == null || h.EstaVacio)
            {
                return new ResultadoFichaje
                {
                    Estado = EstadoFichaje.SinHorario,
                    Permitido = !BloquearSinHorario,
                    TextoSql = BloquearSinHorario ? null : "Presente",
                    Descripcion = "Sin horario configurado para hoy"
                };
            }

            TimeSpan t = ahora.TimeOfDay;
            TimeSpan inicio = h.Inicio.Value;
            TimeSpan fin = h.Fin.Value;
            TimeSpan duracion = fin - inicio;
            TimeSpan limitePresente = inicio + TimeSpan.FromTicks((long)(duracion.Ticks * PORCENTAJE_PRESENTE));
            TimeSpan limiteTarde = inicio + TimeSpan.FromTicks((long)(duracion.Ticks * PORCENTAJE_LIMITE_TARDE));

            if (t < inicio)
                return Rechazo(EstadoFichaje.TodaviaNoEmpezo,
                               "Aún no comienza el horario (" + Formato(inicio) + ")");

            if (t >= fin)
                return Rechazo(EstadoFichaje.ClasesTerminadas, "Las clases terminaron por hoy");

            if (t <= limitePresente)
                return new ResultadoFichaje
                {
                    Estado = EstadoFichaje.Presente,
                    Permitido = true,
                    TextoSql = "Presente",
                    Descripcion = "Horario de asistencia activo"
                };

            if (t <= limiteTarde)
                return new ResultadoFichaje
                {
                    Estado = EstadoFichaje.LlegadaTarde,
                    Permitido = true,
                    TextoSql = "Llegada tarde",
                    Descripcion = "Horario activo: se registra como llegada tarde"
                };

            return Rechazo(EstadoFichaje.LlegadaTardia, "Registro cerrado: llegada tardía");
        }

        private static ResultadoFichaje Rechazo(EstadoFichaje estado, string descripcion)
        {
            return new ResultadoFichaje { Estado = estado, Permitido = false, TextoSql = null, Descripcion = descripcion };
        }

        public static string Formato(TimeSpan t)
        {
            return string.Format("{0:00}:{1:00}", t.Hours, t.Minutes);
        }
    }
}
