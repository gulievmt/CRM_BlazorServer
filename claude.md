# CRMBlazorServerRBS.csproj

## Stack
- C# / .NET 10
- ASP.NET Core / Blazor Server
- EF Core + SQL Server (AZBAKSVRDBS02D.fincaint.local)
- Radzen UI components

## Build & Run
- Build: `dotnet build CRMBlazorServerRBS.csproj`
- Run:   `dotnet run --project CRMBlazorServerRBS.csproj
- Tests: `dotnet test`

## Conventions
- Язык кода: C#, комментарии: русский
- Async/await везде где возможно
- Repository pattern для доступа к данным
- Dapper для сырых SQL запросов, EF Core для CRUD

## Important
- Connection string в appsettings.json (не менять)
- Domain auth: fincaint.local (Windows Authentication)
- Не менять файлы миграций без явного запроса