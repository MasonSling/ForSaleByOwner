# Database deployment contract

This project reserves the PostgreSQL migration assembly and deployment boundary. The visual concept has no DbContext, executable migrations, connection string, or database startup dependency.

When persistence is introduced:

1. DAL will own the EF Core DbContext, mappings, Npgsql provider, and implementations of BLL interfaces.
2. DB will own migrations and the design-time context factory. Configure DAL's context with `MigrationsAssembly("ForSaleByOwner.DB")`.
3. DB will expose explicit script and migrate commands. Script produces reviewable idempotent SQL from the migrations; migrate applies only after the operator selects a target.
4. Configure the connection through environment variables or .NET user secrets, supporting local, Docker, and hosted PostgreSQL. Never commit credentials.
5. Provisioning a database/role is a separate administrative operation. The migration identity only needs schema deployment permissions.
6. Do not migrate production on Web startup. CI will test migrations against an ephemeral PostgreSQL instance before release.

EF migrations are the schema source of truth. Do not maintain a competing hand-edited schema or SQL Server DACPAC. Persistent entity design and executable deployment commands are intentionally deferred.
