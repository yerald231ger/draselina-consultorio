using DraSelina.Consultorio.Agenda.Domain;
using DraSelina.Consultorio.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DraSelina.Consultorio.Agenda.Infrastructure;

/// <summary>Los datos de Agenda viven en el esquema <c>agenda</c> y solo este contexto los toca (AD-2).</summary>
public sealed class AgendaDbContext(DbContextOptions<AgendaDbContext> options) : DbContext(options)
{
    public const string Esquema = "agenda";

    /// <summary>La restricción de exclusión que impide dos Citas no canceladas traslapadas (AD-8, RN-1).</summary>
    public const string RestriccionSinTraslape = "ex_cita_sin_traslape";

    /// <summary>El índice único de <c>agenda.cita.solicitud_id</c>; los nulos, de las Citas del consultorio, no chocan (AD-27).</summary>
    public const string IndiceDeSolicitud = "ux_cita_solicitud_id";

    /// <summary>
    /// La única forma de configurar este contexto: la usan la aplicación, el MigrationService y las
    /// herramientas de EF Core, para que el historial de migraciones quede siempre en el esquema del módulo (AD-25).
    /// </summary>
    public static void Configurar(DbContextOptionsBuilder opciones, string cadenaDeConexion) =>
        opciones.UseNpgsql(cadenaDeConexion, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", Esquema));

    internal DbSet<Cita> Citas => Set<Cita>();

    internal DbSet<TipoDeVisita> TiposDeVisita => Set<TipoDeVisita>();

    internal DbSet<FilaDeTramoBase> TramosBase => Set<FilaDeTramoBase>();

    internal DbSet<FilaDeAgendaCustom> AgendasCustom => Set<FilaDeAgendaCustom>();

    internal DbSet<FilaDeTramoCustom> TramosCustom => Set<FilaDeTramoCustom>();

    internal DbSet<ReglasDeReserva> ReglasDeReserva => Set<ReglasDeReserva>();

    // int a smallint con verificación: un valor fuera de rango falla con OverflowException en lugar de guardarse
    // truncado (40000 no se vuelve -25536).
    private static readonly ValueConverter<int, short> ASmallint = new(v => Convert.ToInt16(v), v => v);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);

        // agenda.outbox: los eventos de integración que Agenda publica (AD-2, AD-3).
        modelBuilder.Entity<FilaDeOutbox>(fila =>
        {
            fila.ToTable(FilaDeOutbox.Tabla);
            fila.HasKey(f => f.Id);
            fila.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();
            fila.Property(f => f.Tipo).HasColumnName("tipo");
            fila.Property(f => f.Contenido).HasColumnName("contenido").HasColumnType("jsonb");
            fila.Property(f => f.OcurridoEn).HasColumnName("ocurrido_en");
            fila.Property(f => f.ClaveDeOrden).HasColumnName("clave_de_orden");
            fila.Property(f => f.Estado).HasColumnName("estado");
            fila.Property(f => f.Intentos).HasColumnName("intentos");
            fila.Property(f => f.ProximoIntentoEn).HasColumnName("proximo_intento_en");
            fila.Property(f => f.EntregadoEn).HasColumnName("entregado_en");
        });

        // agenda.bitacora: quién cambió qué y cuándo (RN-24). Solo se inserta, por BitacoraDeAgenda. Los CHECK son los
        // conjuntos de Actor, EntidadDeBitacora y AccionDeBitacora, guardados por su nombre.
        modelBuilder.Entity<FilaDeBitacora>(fila =>
        {
            fila.ToTable(FilaDeBitacora.Tabla, t =>
            {
                t.HasCheckConstraint("ck_bitacora_actor_tipo", "actor_tipo IN ('Paciente', 'Administrador', 'Sistema')");
                t.HasCheckConstraint(
                    "ck_bitacora_entidad",
                    "entidad IN ('Cita', 'AgendaBase', 'AgendaCustom', 'TipoDeVisita', 'ReglasDeReserva')");
                t.HasCheckConstraint(
                    "ck_bitacora_accion",
                    "accion IN ('Agendada', 'Reprogramada', 'Cancelada', 'NoSePresento', 'Registrada', 'Cambiada', 'Borrada')");
            });
            fila.HasKey(f => f.Id);
            fila.Property(f => f.Id).HasColumnName("id").ValueGeneratedNever();
            fila.Property(f => f.OcurridoEn).HasColumnName("ocurrido_en");
            fila.Property(f => f.ActorTipo).HasColumnName("actor_tipo");
            fila.Property(f => f.ActorId).HasColumnName("actor_id");
            fila.Property(f => f.Entidad).HasColumnName("entidad");
            fila.Property(f => f.EntidadId).HasColumnName("entidad_id");
            fila.Property(f => f.Accion).HasColumnName("accion");
            fila.Property(f => f.Detalle).HasColumnName("detalle").HasColumnType("jsonb");
            fila.HasIndex(f => new { f.Entidad, f.EntidadId, f.OcurridoEn }).HasDatabaseName("ix_bitacora_entidad_entidad_id_ocurrido_en");
        });

        // agenda.tipo_de_visita, con los dos tipos sembrados (AD-25).
        modelBuilder.Entity<TipoDeVisita>(tipo =>
        {
            tipo.ToTable("tipo_de_visita", t => t.HasCheckConstraint("ck_tipo_de_visita_duracion", "duracion_min > 0"));
            tipo.HasKey(t => t.Id);
            tipo.Property(t => t.Id)
                .HasColumnName("id")
                .HasConversion(id => id.Valor, valor => new TipoDeVisitaId(valor))
                .ValueGeneratedNever();
            tipo.Property(t => t.Nombre).HasColumnName("nombre");
            tipo.Property(t => t.Descripcion).HasColumnName("descripcion");
            tipo.Property(t => t.DuracionMin).HasColumnName("duracion_min");
            tipo.Property(t => t.ReservableEnLinea).HasColumnName("reservable_en_linea");
            tipo.Property(t => t.Orden).HasColumnName("orden");
            tipo.Property(t => t.Activo).HasColumnName("activo");
            tipo.Ignore(t => t.Duracion);
            tipo.Ignore(t => t.SeAgendaEnLinea);

            tipo.HasData(TiposDeVisitaSembrados.Filas);
        });

        // agenda.agenda_base_tramo: la semana tipo, sembrada de lunes a viernes (AD-25). Que los tramos de un día no se
        // encimen (INV-B2) y que en cada uno quepa el tipo más corto (INV-B5) lo garantiza AgendaAtencion.
        modelBuilder.Entity<FilaDeTramoBase>(tramo =>
        {
            tramo.ToTable(FilaDeTramoBase.Tabla, t =>
            {
                t.HasCheckConstraint("ck_agenda_base_tramo_rango", "fin > inicio");
                t.HasCheckConstraint("ck_agenda_base_tramo_dia", "dia_de_la_semana BETWEEN 1 AND 7");
            });
            tramo.HasKey(t => t.Id);
            tramo.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
            tramo.Property(t => t.DiaDeLaSemana).HasColumnName("dia_de_la_semana");
            tramo.Property(t => t.Inicio).HasColumnName("inicio");
            tramo.Property(t => t.Fin).HasColumnName("fin");
            tramo.HasIndex(t => new { t.DiaDeLaSemana, t.Inicio }).IsUnique().HasDatabaseName("ux_agenda_base_tramo_dia_inicio");

            tramo.HasData(AgendaDeAtencionSembrada.TramosBase);
        });

        // agenda.agenda_custom: a lo más una por fecha, porque la fecha es la llave (INV-A1, AD-5).
        modelBuilder.Entity<FilaDeAgendaCustom>(custom =>
        {
            custom.ToTable(FilaDeAgendaCustom.Tabla);
            custom.HasKey(c => c.Fecha);
            custom.Property(c => c.Fecha).HasColumnName("fecha").ValueGeneratedNever();
            custom.Property(c => c.Motivo).HasColumnName("motivo");
            custom.Property(c => c.CreadaEn).HasColumnName("creada_en");
            custom.Property(c => c.ActualizadaEn).HasColumnName("actualizada_en");
            custom.HasMany(c => c.Tramos).WithOne().HasForeignKey(t => t.Fecha).OnDelete(DeleteBehavior.Cascade);
        });

        // agenda.agenda_custom_tramo: se borra con su custom. INV-A2 e INV-A6 los garantiza AgendaAtencion.
        modelBuilder.Entity<FilaDeTramoCustom>(tramo =>
        {
            tramo.ToTable(FilaDeTramoCustom.Tabla, t => t.HasCheckConstraint("ck_agenda_custom_tramo_rango", "fin > inicio"));
            tramo.HasKey(t => t.Id);
            tramo.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();
            tramo.Property(t => t.Fecha).HasColumnName("fecha");
            tramo.Property(t => t.Inicio).HasColumnName("inicio");
            tramo.Property(t => t.Fin).HasColumnName("fin");
            tramo.HasIndex(t => new { t.Fecha, t.Inicio }).IsUnique().HasDatabaseName("ux_agenda_custom_tramo_fecha_inicio");
        });

        // agenda.reglas_de_reserva: una sola fila (AD-14), sembrada (AD-25). El id vive solo en la base.
        modelBuilder.Entity<ReglasDeReserva>(reglas =>
        {
            reglas.ToTable("reglas_de_reserva", t => t.HasCheckConstraint("ck_reglas_de_reserva_id", "id = 1"));
            reglas.Property<short>("Id").HasColumnName("id").ValueGeneratedNever();
            reglas.HasKey("Id");
            reglas.Property(r => r.AnticipacionMinimaDias).HasColumnName("anticipacion_minima_dias").HasConversion(ASmallint);
            reglas.Property(r => r.VentanaMeses).HasColumnName("ventana_meses").HasConversion(ASmallint);
            reglas.Property(r => r.UmbralCancelacionTardiaMin).HasColumnName("umbral_cancelacion_tardia_min").HasConversion(ASmallint);
            reglas.Property(r => r.TopeSemanalPorTelefono).HasColumnName("tope_semanal_por_telefono").HasConversion(ASmallint);

            reglas.HasData(AgendaDeAtencionSembrada.Reglas);
        });

        // agenda.cita, sin token_hash: llega con su historia. La restricción de exclusión la crea
        // la migración con SQL explícito (AD-25).
        modelBuilder.Entity<Cita>(cita =>
        {
            cita.ToTable("cita", t =>
            {
                t.HasCheckConstraint("ck_cita_rango", "fin > inicio");
                t.HasCheckConstraint("ck_cita_estado", "estado IN ('Agendada', 'NoSePresento', 'Cancelada')");
                t.HasCheckConstraint("ck_cita_origen", "origen IN ('en_linea', 'consultorio')");
                t.HasCheckConstraint("ck_cita_consentimiento_canal", "consentimiento_canal IN ('en_linea', 'externo')");
                t.HasCheckConstraint(
                    "ck_cita_motivo_de_cancelacion",
                    "motivo_de_cancelacion IS NULL OR motivo_de_cancelacion IN ('paciente', 'tarde', 'consultorio')");

                // INV-C8: motivo y fecha de cancelación si y solo si está cancelada.
                t.HasCheckConstraint(
                    "ck_cita_cancelacion",
                    "(estado = 'Cancelada') = (motivo_de_cancelacion IS NOT NULL) "
                    + "AND (estado = 'Cancelada') = (cancelada_en IS NOT NULL)");
            });

            cita.HasKey(c => c.Id);
            cita.Property(c => c.Id)
                .HasColumnName("id")
                .HasConversion(id => id.Valor, valor => new CitaId(valor))
                .ValueGeneratedNever();

            // Sin llave foránea: el Paciente vive en otro esquema (AD-2).
            cita.Property(c => c.PacienteId)
                .HasColumnName("paciente_id")
                .HasConversion(id => id.Valor, valor => new PatientId(valor));

            cita.Property(c => c.TipoDeVisitaId)
                .HasColumnName("tipo_de_visita_id")
                .HasConversion(id => id.Valor, valor => new TipoDeVisitaId(valor));
            cita.HasOne<TipoDeVisita>().WithMany().HasForeignKey(c => c.TipoDeVisitaId).OnDelete(DeleteBehavior.Restrict);

            cita.ComplexProperty(c => c.Rango, rango =>
            {
                rango.Property(r => r.Inicio).HasColumnName("inicio");
                rango.Property(r => r.Fin).HasColumnName("fin");
            });

            cita.Property(c => c.Estado)
                .HasColumnName("estado")
                .HasConversion(e => TextoDeEnumeraciones.Estado(e), texto => TextoDeEnumeraciones.Estado(texto));
            cita.Property(c => c.MotivoDeCancelacion)
                .HasColumnName("motivo_de_cancelacion")
                .HasConversion(m => TextoDeEnumeraciones.Motivo(m!.Value), texto => TextoDeEnumeraciones.Motivo(texto));
            cita.Property(c => c.CanceladaEn).HasColumnName("cancelada_en");
            cita.Property(c => c.Origen)
                .HasColumnName("origen")
                .HasConversion(o => TextoDeEnumeraciones.Origen(o), texto => TextoDeEnumeraciones.Origen(texto));
            cita.Property(c => c.Nota).HasColumnName("nota");
            cita.Property(c => c.MotivoDeConsulta).HasColumnName("motivo_de_consulta");

            cita.ComplexProperty(c => c.Consentimiento, consentimiento =>
            {
                consentimiento.Property(c => c.En).HasColumnName("consentimiento_en");
                consentimiento.Property(c => c.VersionDelAviso).HasColumnName("consentimiento_version_aviso");
                consentimiento.Property(c => c.Canal)
                    .HasColumnName("consentimiento_canal")
                    .HasConversion(c => TextoDeEnumeraciones.Canal(c), texto => TextoDeEnumeraciones.Canal(texto));
            });

            cita.Property(c => c.CreadaEn).HasColumnName("creada_en");

            // AD-27: nulo en las del consultorio; en PostgreSQL los nulos no chocan en un índice único.
            cita.Property(c => c.SolicitudId).HasColumnName("solicitud_id");
            cita.HasIndex(c => c.SolicitudId).IsUnique().HasDatabaseName(IndiceDeSolicitud);

            // Los índices (inicio) y (paciente_id, inicio) van sobre columnas del rango y los crea la migración con SQL
            // explícito, como la restricción de exclusión.
        });
    }
}
