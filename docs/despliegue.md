# Despliegue del piloto

Estado al 2026-11-17: **nada publicado ni aprovisionado.** Este documento dice qué ya está listo en el repositorio y
qué falta decidir y hacer cuando haya fecha de piloto. La arquitectura difiere todo lo operativo a ese momento
(sección Deferred de `architecture-appointment-booking.md`).

## Decidido

- **Azure Container Apps, publicado desde el AppHost con `azd`** (Gerardo, 2026-11-17). Es el camino de Aspire: el
  `MigrationService` corre, termina y después arranca la web, igual que en local (AD-25). App Service se descartó porque
  espera un proceso que siga vivo y la web no migra (`WebNoMigraTests`).
- **La web con una réplica mínima de 1.** La entrega del outbox y los envíos programados (Recordatorios, Agenda del
  día) corren dentro de la web: si se apaga, no salen. Además, un Paciente no espera un arranque en frío.

## Ya está en el repositorio

| Qué | Dónde |
| --- | --- |
| Encabezados reenviados: al publicar, el AppHost pone `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. La web toma `X-Forwarded-Proto` y `X-Forwarded-For` del proxy de entrada: el JSON-LD, `robots.txt` y el sitemap salen con `https` (que el proxy conserva el Host original es `[ASSUMPTION]`; se verifica al publicar), y el límite de frecuencia (FR-7) cuenta la IP del Paciente, no la del proxy. En local no se activa. | `src/DraSelina.Consultorio.AppHost/AppHost.cs`; prueba en `EncontrableTests` |
| Integración continua: en cada push a `main`, GitHub Actions compila (advertencias como errores) y corre todas las pruebas, las de arquitectura y las de integración contra PostgreSQL incluidas. No publica. | `.github/workflows/pruebas.yml` |
| Chequeos de salud: `/health` y `/alive` solo se exponen en desarrollo (plantilla de ServiceDefaults). Container Apps usa por omisión un sondeo TCP al puerto de la web, que no los necesita. | `src/DraSelina.Consultorio.ServiceDefaults/Extensions.cs` |

## Falta decidir antes de aprovisionar

Cada una se pregunta a Gerardo cuando haya fecha de piloto; ninguna está decidida.

1. **Base de datos.** PostgreSQL Flexible Server en Mexico Central; confirmar que ofrece la versión 18 (arquitectura,
   Open Question 6). Mexico Central no replica el respaldo a otra región: decidir la estrategia de respaldo antes del
   primer Paciente (Deferred, "Respaldo replicado a otra región"; PRD §6, "respaldo diario").
2. **Correo.** Hoy la web envía por SMTP (`CanalDeCorreoSmtp`) a la cadena de conexión `mailpit`. La arquitectura nombra
   Azure Communication Services: decidir si se usa su relevo SMTP (sin cambiar el canal) o su SDK, y el remitente real
   (`Notificaciones__Remitente`, hoy `citas@consultorio.local`).
3. **Dominio y HTTPS.** El dominio público y su certificado administrado en Container Apps. Con el dominio se fija
   `AllowedHosts` (hoy `*`).
4. **Secretos.** La clave del Enlace de gestión debe ser **fija** en producción: si cambia, todos los enlaces enviados
   dejan de servir (AD-19). Lo mismo la contraseña inicial del Administrador. Los dos son parámetros secretos del
   AppHost (`clave-enlace-de-gestion`, `clave-administrador`); `azd` los pide al aprovisionar y los guarda como secretos
   de Container Apps.
5. **Alertas.** Qué se vigila (la web caída, eventos del outbox en Fallido, correos que no salen) y a quién avisa.
6. **Entorno intermedio.** Sin él en el piloto; revisar antes de recibir pacientes reales (Deferred).

## Condiciones de lanzamiento que no son de código

- El contenido de Selina ya está en `ContenidoApi.cs`. Se lanza sin fotos: el retrato queda con las iniciales y no
  hace falta esperar la foto (Gerardo, 2026-11-24).
- El aviso de privacidad revisado por el abogado, con su versión (PRD §6). Selina aprobó el texto integral y el
  simplificado; hoy es `2026-11-24-borrador` (Gerardo, 2026-11-24). Antes de lanzar faltan el correo para las
  solicitudes y el código postal del consultorio, que el aviso muestra como Pendiente
  (`CorreoDePrivacidadDelConsultorio` y `Consultorio.CodigoPostal` en `ContenidoApi.cs`).
- La revisión de COFEPRIS sobre publicidad médica antes de publicar el sitio (PRD §6).
- El aviso de publicidad ante COFEPRIS, presentado antes de publicar el sitio, porque el sitio anuncia los servicios
  de la doctora (Gerardo, 2026-11-24).

## Cómo se publica, cuando toque

Esbozo; no se ha ejecutado. Requiere una suscripción de Azure y genera costo.

```bash
azd auth login
azd init            # detecta el AppHost de Aspire
azd up              # aprovisiona y despliega; pide los parámetros secretos
```

Antes de `azd up` hay que modelar en el AppHost lo que en local es contenedor: PostgreSQL (Flexible Server en lugar de
`AddPostgres`) y el correo (en lugar de Mailpit), según las decisiones 1 y 2.
