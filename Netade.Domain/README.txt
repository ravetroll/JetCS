
dotnet ef dbcontext scaffold "Provider=Microsoft.ACE.OLEDB.16.0;Data Source=C:\Users\brend\source\repos\Netade\Netade.Server\Netade.mdb" "EntityFrameworkCore.Jet" --context NetadeDbContext --context-dir="..\Netade.Persistence" --output-dir "../Netade.Domain" --namespace "Netade.Domain" --context-namespace "Netade.Persistence" --force --no-onconfiguring

