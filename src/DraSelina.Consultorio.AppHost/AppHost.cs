var builder = DistributedApplication.CreateBuilder(args);

// La contraseña la genera Aspire en cada arranque; no hay secretos en el repositorio.
// Sin volumen: cada arranque parte de una base vacía.
var postgres = builder.AddPostgres("postgres");
var consultorio = postgres.AddDatabase("consultorio");

// Aplica las migraciones de todos los módulos, crea el almacenamiento de Wolverine y termina (AD-25).
var migraciones = builder.AddProject<Projects.DraSelina_Consultorio_MigrationService>("migraciones")
    .WithReference(consultorio)
    .WaitFor(consultorio);

// La web arranca solo si las migraciones terminaron con código 0; si fallan, no arranca.
builder.AddProject<Projects.DraSelina_Consultorio_Web>("web")
    .WithReference(consultorio)
    .WaitForCompletion(migraciones);

builder.Build().Run();
