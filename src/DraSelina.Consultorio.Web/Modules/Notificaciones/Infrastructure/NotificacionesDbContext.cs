using Microsoft.EntityFrameworkCore;

namespace DraSelina.Consultorio.Notificaciones.Infrastructure;

/// <summary>Los datos de Notificaciones viven en el esquema <c>notificaciones</c> y solo este contexto los toca (AD-2).</summary>
public sealed class NotificacionesDbContext(DbContextOptions<NotificacionesDbContext> options) : DbContext(options)
{
    public const string Esquema = "notificaciones";

    /// <summary>
    /// La única forma de configurar este contexto: la usan la aplicación, el MigrationService y las
    /// herramientas de EF Core, para que el historial de migraciones quede siempre en el esquema del módulo (AD-25).
    /// </summary>
    public static void Configurar(DbContextOptionsBuilder opciones, string cadenaDeConexion) =>
        opciones.UseNpgsql(cadenaDeConexion, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", Esquema));

    internal DbSet<FilaDeMensaje> Mensajes => Set<FilaDeMensaje>();

    internal DbSet<FilaDeConfiguracionDeEnvio> ConfiguracionDeEnvio => Set<FilaDeConfiguracionDeEnvio>();

    internal DbSet<FilaDeRecordatorioProgramado> RecordatoriosProgramados => Set<FilaDeRecordatorioProgramado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);

        // notificaciones.configuracion_de_envio: una sola fila (AD-14), sembrada (AD-25).
        modelBuilder.Entity<FilaDeConfiguracionDeEnvio>(configuracion =>
        {
            configuracion.ToTable(FilaDeConfiguracionDeEnvio.Tabla, t => t.HasCheckConstraint("ck_configuracion_de_envio_id", "id = 1"));
            configuracion.HasKey(c => c.Id);
            configuracion.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            configuracion.Property(c => c.HoraRecordatorio).HasColumnName("hora_recordatorio");
            configuracion.Property(c => c.HoraAgendaDelDia).HasColumnName("hora_agenda_del_dia");
            configuracion.Property(c => c.CorreoDoctora).HasColumnName("correo_doctora");
            configuracion.Property(c => c.CorreoAdministrador).HasColumnName("correo_administrador");

            configuracion.HasData(FilaDeConfiguracionDeEnvio.Semilla);
        });

        // notificaciones.mensaje: lo enviado, sin dirección ni contenido (AD-23). Sin llaves foráneas. Los CHECK son los
        // conjuntos de TipoDeMensaje, CanalDeMensaje y EstadoDeMensaje, guardados por su nombre.
        modelBuilder.Entity<FilaDeMensaje>(mensaje =>
        {
            mensaje.ToTable(FilaDeMensaje.Tabla, t =>
            {
                t.HasCheckConstraint(
                    "ck_mensaje_tipo",
                    "tipo IN ('Confirmacion', 'Recordatorio', 'Reprogramada', 'Cancelada', 'CanceladaPorConsultorio', 'AlertaTardia', 'AgendaDelDia')");
                t.HasCheckConstraint("ck_mensaje_canal", "canal IN ('correo')");
                t.HasCheckConstraint("ck_mensaje_estado", "estado IN ('Enviado', 'Fallido', 'Omitido')");
            });
            mensaje.HasKey(m => m.Id);
            mensaje.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
            mensaje.Property(m => m.EventoId).HasColumnName("evento_id");
            mensaje.Property(m => m.CitaId).HasColumnName("cita_id");
            mensaje.Property(m => m.Tipo).HasColumnName("tipo");
            mensaje.Property(m => m.Canal).HasColumnName("canal");
            mensaje.Property(m => m.Estado).HasColumnName("estado");
            mensaje.Property(m => m.RegistradoEn).HasColumnName("registrado_en");
            mensaje.HasIndex(m => new { m.EventoId, m.Tipo }).IsUnique().HasDatabaseName(FilaDeMensaje.IndiceDeEventoYTipo);
        });

        // notificaciones.recordatorio_programado: el Recordatorio pendiente de cada Cita (AD-20). Sin llaves foráneas.
        modelBuilder.Entity<FilaDeRecordatorioProgramado>(recordatorio =>
        {
            recordatorio.ToTable(FilaDeRecordatorioProgramado.Tabla);
            recordatorio.HasKey(r => r.CitaId);
            recordatorio.Property(r => r.CitaId).HasColumnName("cita_id").ValueGeneratedNever();
            recordatorio.Property(r => r.PacienteId).HasColumnName("paciente_id");
            recordatorio.Property(r => r.Inicio).HasColumnName("inicio");
            recordatorio.Property(r => r.ProgramadoPara).HasColumnName("programado_para");
            recordatorio.HasIndex(r => r.ProgramadoPara).HasDatabaseName(FilaDeRecordatorioProgramado.IndiceDeProgramadoPara);
        });
    }
}
