var builder = DistributedApplication.CreateBuilder(args);

// La contraseña la genera Aspire en cada arranque; no hay secretos en el repositorio.
// Sin volumen: cada arranque parte de una base vacía.
var postgres = builder.AddPostgres("postgres");
var consultorio = postgres.AddDatabase("consultorio");

// Mailpit atrapa los correos que envía la web por SMTP y los muestra, con sus adjuntos, desde el dashboard (épica 4).
// Sin volumen, como PostgreSQL: cada arranque parte sin correos. La web lee el servidor de la cadena de conexión
// "mailpit" (Endpoint=smtp://host:puerto).
var mailpit = builder.AddMailPit("mailpit");

// La clave del Enlace de gestión (AD-19): un parámetro secreto, fuera de la base y del repositorio. Si no se da
// (Parameters:clave-enlace-de-gestion, por ejemplo con user-secrets), Aspire genera una en cada arranque: los enlaces
// cambian con ella, igual que la base, que también parte vacía.
var claveDeEnlace = builder.AddParameter(
    "clave-enlace-de-gestion",
    new GenerateParameterDefault { MinLength = 64, Special = false },
    secret: true);

// Aplica las migraciones de todos los módulos y termina (AD-25).
var migraciones = builder.AddProject<Projects.DraSelina_Consultorio_MigrationService>("migraciones")
    .WithReference(consultorio)
    .WaitFor(consultorio);

// La web arranca solo si las migraciones terminaron con código 0; si fallan, no arranca. El Enlace de gestión se arma
// con la dirección de la propia web.
var web = builder.AddProject<Projects.DraSelina_Consultorio_Web>("web")
    .WithReference(consultorio)
    .WithReference(mailpit)
    .WaitForCompletion(migraciones)
    .WithEnvironment("EnlaceDeGestion__Clave", claveDeEnlace)
    // El remitente de los correos en desarrollo. `[ASSUMPTION]` hasta que exista la dirección real.
    .WithEnvironment("Notificaciones__Remitente", "citas@consultorio.local");
web.WithEnvironment("EnlaceDeGestion__UrlBase", web.GetEndpoint("http"));

builder.Build().Run();
