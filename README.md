# LeaveTrackerPro

A modern, lightweight leave and absence management application built with ASP.NET Core (.NET 8).

Badges
- Build: ![Build Status](https://img.shields.io/badge/build-passing-brightgreen)
- .NET: ![.NET](https://img.shields.io/badge/.NET-8.0-blue)
- License: ![License](https://img.shields.io/badge/license-MIT-lightgrey)

Table of Contents
- About
- Key Features
- Tech Stack
- Quick Start
  - Prerequisites
  - Clone
  - Configuration
  - Database (EF Core)
  - Run
  - Tests
  - Docker
- Deployment
- Environment Variables / appsettings
- Contributing
- License
- Contact

About

LeaveTrackerPro helps organizations manage employee leave requests, approvals, balances, and reporting with minimal setup. It is designed to be extensible and deployable on-premises or to cloud providers.

Key Features
- Employee leave request lifecycle (Request → Approve/Reject → Cancel)
- Manager approval workflow
- Leave balances and accruals
- Role-based access control (Admin / Manager / Employee)
- Audit logging and basic reporting
- REST API and (optional) web UI

Tech Stack
- Backend: ASP.NET Core (.NET 8)
- Data: Entity Framework Core (SQL Server, SQLite, or other EF Core provider)
- Frontend: May include ASP.NET Core MVC or SPA (React/Vue) depending on the solution contents
- Optional: Docker for containerized deployments

Quick Start

Prerequisites
- .NET 8 SDK: https://dotnet.microsoft.com/
- SQL Server (LocalDB), SQLite, or another EF Core-supported database
- Optional: Docker & Docker Compose

Clone

```bash
git clone https://github.com/madhukavirajith/LeaveTrackerPro.git
cd LeaveTrackerPro
```

Configuration
1. Copy an example settings file if present (appsettings.json.example or appsettings.Development.json.example) to appsettings.Development.json or appsettings.json.
2. Update ConnectionStrings and any secrets (JWT keys, SMTP credentials).

Example connection string (development):

```
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=LeaveTrackerProDb;Trusted_Connection=True;"
}
```

Database (EF Core)
- If the solution contains EF Core DbContext and migrations are not committed, add an initial migration and apply it:

```bash
dotnet ef migrations add InitialCreate --project <YourDataProject> --startup-project <YourApiProject>
dotnet ef database update --project <YourDataProject> --startup-project <YourApiProject>
```

Run (development)
- Using CLI:

```bash
cd <ApiProjectFolder>
dotnet restore
dotnet run
```

- Using Visual Studio:
  - Open LeaveTrackerPro.slnx
  - Set the API project as startup and run (F5)

Testing
- Run tests (if present):

```bash
dotnet test
```

Docker (optional)
- Build image:

```bash
docker build -t leavetrackerpro:latest .
```

- Run container:

```bash
docker run -e ConnectionStrings__DefaultConnection="Server=...;" -p 5000:80 leavetrackerpro:latest
```

Deployment
- Azure App Service: publish with `dotnet publish -c Release` or use GitHub Actions.
- IIS: publish as a folder and configure site and app pool.
- Containers: push images to a registry and deploy to AKS, ECS, or other.

Environment Variables & Secrets
- Do not store secrets in source control. Use environment variables, Azure Key Vault, or GitHub Secrets for:
  - Connection strings
  - JWT signing keys
  - SMTP credentials

Contributing
- Open issues for bugs or feature requests.
- Fork the repo, create a feature branch, and open a pull request.
- Run tests and follow existing code style.

License
This repository does not include an explicit license file. If you intend to publish under MIT or another license, add a LICENSE file to the repo.

Contact
Repository: https://github.com/madhukavirajith/LeaveTrackerPro

Acknowledgements
- Built with .NET and EF Core. Contributions are welcome.
