var builder = DistributedApplication.CreateBuilder(args);

// La contraseña la genera Aspire en cada corrida; no hay secretos en el repositorio.
var postgres = builder.AddPostgres("postgres");
var arranque = postgres.AddDatabase("arranque");

builder.AddProject<Projects.Arranque_Web>("web")
    .WithReference(arranque)
    .WaitFor(arranque)
    // propio (por defecto) | nativo. Se elige al arrancar: Validacion__Modo=nativo dotnet run ...
    .WithEnvironment("Validacion__Modo", builder.Configuration["Validacion:Modo"] ?? "propio");

builder.Build().Run();
