---
title: "Diagramas C4 — Sistema de citas, Dra. Selina Medina"
status: final
created: 2026-10-04
updated: 2026-10-05
source: architecture-appointment-booking.md
---

# Diagramas C4 — Sistema de citas, Dra. Selina Medina

Vista del sistema en cuatro niveles, derivada del documento de arquitectura (`architecture-appointment-booking.md`). Todavía no hay código: los diagramas muestran lo decidido, no lo construido. Si un diagrama y el documento de arquitectura se contradicen, gana el documento de arquitectura.

## 1. Contexto

Quién usa el sistema y con qué servicios externos habla.

```mermaid
C4Context
  title Contexto — Sistema de citas del consultorio

  Person(paciente, "Paciente", "Agenda, reprograma y cancela su cita desde el teléfono, sin cuenta")
  Person(admin, "Administrador", "Gerardo. Atiende las excepciones de la agenda")
  Person(selina, "Dra. Selina", "No usa el sistema. Recibe sus citas del día por correo")

  System(citas, "Sistema de citas", "Sitio del consultorio, reserva en línea y administración de la agenda")

  System_Ext(correo, "Proveedor de correo", "Azure Communication Services Email")
  System_Ext(antibots, "Verificación anti-bots", "Cloudflare Turnstile")
  System_Ext(mapas, "Mapa", "Google Maps, abierto por enlace")

  Rel(paciente, citas, "Reserva y gestiona su cita en", "HTTPS")
  Rel(admin, citas, "Administra la agenda en", "HTTPS")
  Rel(citas, correo, "Envía confirmaciones, recordatorios y avisos con", "HTTPS")
  Rel(correo, paciente, "Entrega correos a")
  Rel(correo, selina, "Entrega la agenda del día a")
  Rel(correo, admin, "Entrega alertas de cancelación tardía a")
  Rel(citas, antibots, "Valida que la reserva es humana con", "HTTPS")
  Rel(paciente, mapas, "Abre la ubicación en")

  UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="1")
```

## 2. Contenedores

Lo que se despliega. Es un monolito: una aplicación, una base de datos y un proceso de migraciones.

```mermaid
C4Container
  title Contenedores — Sistema de citas

  Person(paciente, "Paciente", "Reserva desde el teléfono")
  Person(admin, "Administrador", "Administra la agenda")

  System_Boundary(citas, "Sistema de citas") {
    Container(web, "Aplicación web", "Blazor Web App, .NET 11", "Sitio y reserva en SSR estático; administración en Interactive Server; módulos y mensajería en el mismo proceso")
    Container(mig, "Servicio de migraciones", ".NET 11, EF Core", "Aplica migraciones y datos iniciales; corre antes que la aplicación")
    ContainerDb(db, "Base de datos", "PostgreSQL con btree_gist", "Esquemas agenda, pacientes, notificaciones, identidad y wolverine")
  }

  System_Ext(correo, "Proveedor de correo", "Azure Communication Services Email")
  System_Ext(antibots, "Verificación anti-bots", "Cloudflare Turnstile")

  Rel(paciente, web, "Navega y envía formularios a", "HTTPS")
  Rel(admin, web, "Opera con conexión viva en", "HTTPS, SignalR")
  Rel(web, db, "Lee y escribe en", "Npgsql, EF Core")
  Rel(mig, db, "Crea y actualiza el esquema de", "Npgsql, EF Core")
  Rel(web, correo, "Envía correos con", "HTTPS")
  Rel(web, antibots, "Verifica el token de reserva con", "HTTPS")

  UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="1")
```

En desarrollo, el AppHost de Aspire levanta los tres contenedores y un panel con registros y trazas. El despliegue a Azure (Mexico Central) está diferido.

## 3. Componentes de la aplicación web

Los módulos dentro del proceso y cómo se hablan. Las flechas continuas son llamadas por contrato; la de eventos pasa por el outbox.

```mermaid
C4Component
  title Componentes — Aplicación web

  ContainerDb(db, "Base de datos", "PostgreSQL", "Un esquema por módulo")
  System_Ext(correo, "Proveedor de correo", "Azure Communication Services Email")

  Container_Boundary(web, "Aplicación web") {
    Component(uiPub, "Interfaz pública", "Blazor SSR estático", "Sitio y pasos de la reserva; sin estado en el servidor")
    Component(uiAdm, "Interfaz de administración", "Blazor Interactive Server", "Agenda, calendario de atención, pacientes, configuración")
    Component(host, "Host", "ASP.NET Core Identity, Wolverine", "Sesión con segundo factor, actor, límite de frecuencia, despacho de comandos")
    Component(agenda, "Agenda", "Módulo núcleo", "Citas, agenda de atención, disponibilidad, bitácora")
    Component(pacientes, "Pacientes", "Módulo", "Identidad y contacto del paciente")
    Component(notif, "Notificaciones", "Módulo", "Correos, recordatorio y agenda del día")
    Component(contenido, "Contenido", "Módulo", "Textos del sitio y datos del consultorio")
    ComponentQueue(outbox, "Eventos de cita", "Outbox de Wolverine", "CitaAgendada, CitaReprogramada, CitaCancelada")
  }

  Rel(uiPub, host, "Envía comandos y consultas por", "ISender")
  Rel(uiAdm, host, "Envía comandos y consultas por", "ISender")
  Rel(host, agenda, "Despacha a")
  Rel(host, pacientes, "Despacha a")
  Rel(host, contenido, "Despacha a")
  Rel(agenda, pacientes, "Busca o crea al paciente en", "IPacientesApi")
  Rel(agenda, contenido, "Pide el lugar para el archivo de calendario a", "IContenidoApi")
  Rel(agenda, outbox, "Publica en")
  Rel(notif, outbox, "Se suscribe a")
  Rel(notif, pacientes, "Pide contacto a", "IPacientesApi")
  Rel(notif, agenda, "Pide enlace y calendario a", "IAgendaApi")
  Rel(notif, contenido, "Pide datos del consultorio a", "IContenidoApi")
  Rel(notif, correo, "Envía correos con", "INotificationChannel")
  Rel(agenda, db, "Lee y escribe el esquema agenda de")
  Rel(pacientes, db, "Lee y escribe el esquema pacientes de")

  UpdateLayoutConfig($c4ShapeInRow="4", $c4BoundaryInRow="1")
```

## 4. Componentes del módulo Agenda

El núcleo. La disponibilidad se calcula; nunca se guardan slots.

```mermaid
C4Component
  title Componentes — Módulo Agenda

  Component_Ext(host, "Host", "Wolverine", "Despacha comandos y consultas ya validados")
  Component_Ext(pacientes, "Pacientes", "IPacientesApi", "Identidad del paciente")
  ContainerDb(db, "Esquema agenda", "PostgreSQL", "Citas, agenda base, agenda custom, tipos de visita, bitácora")

  Container_Boundary(agenda, "Agenda") {
    Component(handlers, "Casos de uso", "Features", "Agendar, reprogramar, cancelar, guardar base y custom, consultar disponibilidad")
    Component(cita, "Cita", "Agregado", "Agendar, agendar por consultorio, reprogramar, cancelar; tres estados guardados")
    Component(atencion, "Agenda de atención", "Agregado", "Agenda base y agendas custom; resuelve los tramos vigentes de una fecha")
    Component(calc, "Calculadora de disponibilidad", "Servicio de dominio puro", "Tramos vigentes menos citas activas, dentro de la ventana")
    Component(puertos, "Puertos", "Interfaces", "Escritura por agregado; lectura por caso de uso")
    Component(infra, "Infraestructura", "EF Core, Npgsql", "Implementa los puertos; restricción de exclusión y bitácora")
    Component(api, "API de Agenda", "IAgendaApi", "Enlace de gestión, archivo de calendario, citas del día")
  }

  Rel(host, handlers, "Entrega comandos y consultas a")
  Rel(handlers, cita, "Crea y modifica")
  Rel(handlers, atencion, "Modifica y consulta")
  Rel(handlers, calc, "Calcula libres con")
  Rel(handlers, pacientes, "Busca o crea al paciente en")
  Rel(handlers, puertos, "Persiste y consulta por")
  Rel(infra, puertos, "Implementa")
  Rel(infra, db, "Lee y escribe en", "SQL")
  Rel(api, handlers, "Se resuelve con")

  UpdateLayoutConfig($c4ShapeInRow="4", $c4BoundaryInRow="1")
```

## 5. Flujo: reserva en línea

Lo que pasa cuando un paciente confirma su cita.

```mermaid
C4Dynamic
  title Flujo — Un paciente confirma su cita

  Person(paciente, "Paciente", "En el paso Tus datos")
  ContainerDb(db, "Base de datos", "PostgreSQL", "Esquemas por módulo")
  System_Ext(correo, "Proveedor de correo", "Azure Communication Services Email")

  Container_Boundary(web, "Aplicación web") {
    Component(ui, "Interfaz pública", "Blazor SSR estático", "Formulario de confirmación")
    Component(host, "Host", "Wolverine", "Anti-bots, validación, despacho")
    Component(agenda, "Agenda", "Módulo", "AgendarCita")
    Component(pacientes, "Pacientes", "Módulo", "BuscarOCrear")
    Component(notif, "Notificaciones", "Módulo", "Confirmación y recordatorio")
  }

  Rel(paciente, ui, "1. Envía el formulario con su SolicitudId")
  Rel(ui, host, "2. Envía AgendarCita", "ISender")
  Rel(host, agenda, "3. Verifica anti-bots, valida la forma y despacha")
  Rel(agenda, pacientes, "4. Busca o crea al paciente y pide los que comparten teléfono")
  Rel(agenda, db, "5. Toma el candado de la fecha, lee tramos vigentes, guarda cita, bitácora y evento")
  Rel(agenda, ui, "6. Devuelve la cita, o conflicto si el horario se ocupó")
  Rel(notif, agenda, "7. Recibe CitaAgendada y pide el enlace de gestión")
  Rel(notif, correo, "8. Envía la confirmación y programa el recordatorio")

  UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="1")
```

Los pasos 7 y 8 ocurren después de responder al paciente: la reserva nunca espera al correo.

## Lo que no se muestra

- **Despliegue.** No hay diagrama de despliegue porque el aprovisionamiento de Azure está diferido.
- **Proyectos y capas.** La tabla de referencias permitidas entre proyectos está en el documento de arquitectura.
