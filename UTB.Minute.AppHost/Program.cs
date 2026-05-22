using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var isTesting = builder.Environment.IsEnvironment("Testing");

var postgres = isTesting
    ? builder.AddPostgres("postgres-testing")
    : builder.AddPostgres("postgres")
        .WithDataVolume();

var database = postgres.AddDatabase("minute-db");

if (isTesting)
{
    builder.AddProject<Projects.UTB_Minute_WebApi>("webapi")
        .WithReference(database)
        .WaitFor(database);
}
else
{
    var keycloak = builder.AddContainer("keycloak", "quay.io/keycloak/keycloak:26.0.7")
        .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
        .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", "admin")
        .WithBindMount(Path.Combine(AppContext.BaseDirectory, "Keycloak"), "/opt/keycloak/data/import", isReadOnly: true)
        .WithArgs("start-dev", "--import-realm")
        .WithHttpEndpoint(targetPort: 8080, name: "http");

    var dbManager = builder.AddProject<Projects.UTB_Minute_DbManager>("db-manager")
        .WithReference(database)
        .WaitFor(database)
        .WithHttpCommand("/commands/reset-database", "Reset database", commandOptions: new HttpCommandOptions { Method = HttpMethod.Post });

    var webApi = builder.AddProject<Projects.UTB_Minute_WebApi>("webapi")
        .WithReference(database)
        .WithReference(keycloak.GetEndpoint("http"))
        .WaitFor(database)
        .WaitFor(dbManager)
        .WaitFor(keycloak);

    builder.AddProject<Projects.UTB_Minute_AdminClient>("admin-client")
        .WithReference(webApi)
        .WithReference(keycloak.GetEndpoint("http"))
        .WaitFor(webApi);

    builder.AddProject<Projects.UTB_Minute_CanteenClient>("canteen-client")
        .WithReference(webApi)
        .WithReference(keycloak.GetEndpoint("http"))
        .WaitFor(webApi);
}

builder.Build().Run();

public partial class Program;
