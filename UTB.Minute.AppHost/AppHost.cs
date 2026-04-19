using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Add PostgreSQL database
var postgres = builder.AddPostgres("postgres")
    .AddDatabase("minute-db", "minute");

// Add WebAPI with PostgreSQL reference
var webApi = builder.AddProject<Projects.UTB_Minute_WebApi>("webapi")
    .WithHttpHealthCheck("/health")
    .WithReference(postgres)
    .WaitFor(postgres);

// Add CanteenClient (for students and cooks)
builder.AddProject<Projects.UTB_Minute_CanteenClient>("canteenclient")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(webApi)
    .WaitFor(webApi);

// Add AdminClient (for canteen management)
builder.AddProject<Projects.UTB_Minute_AdminClient>("adminclient")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(webApi)
    .WaitFor(webApi);

// Add DbManager for database management
builder.AddProject<Projects.UTB_Minute_DbManager>("dbmanager")
    .WithReference(webApi)
    .WithReference(postgres)
    .WaitFor(postgres)
    .WaitFor(webApi);

builder.Build().Run();
