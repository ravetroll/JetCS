
dotnet ef dbcontext scaffold "Provider=Microsoft.ACE.OLEDB.16.0;Data Source=C:\Users\brend\source\repos\JetCS\JetCS.Server\JetCS.mdb" "EntityFrameworkCore.Jet" --context JetCSDbContext --context-dir="..\JetCS.Persistence" --output-dir "../JetCS.Domain" --namespace "JetCS.Domain" --context-namespace "JetCS.Persistence" --force --no-onconfiguring

