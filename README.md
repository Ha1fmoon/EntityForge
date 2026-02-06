# EntityForge

CRM/ERP platform foundation with automatic microservice generation. Define an entity (name, fields, types) and get a fully functional microservice running in Docker.

## Features

- Code generation using Clean Architecture (Domain, Application, Infrastructure, API layers)
- 10-step pipeline with automatic rollback on failure
- PostgreSQL database with auto-generated schema
- Docker containerization out of the box
- API Gateway for routing and entity relationship management
- Service registry via Redis

## Stack

- .NET 8, ASP.NET Core
- Scriban (code templating)
- PostgreSQL
- Docker
- Redis
- Serilog, Polly, Swagger

## Project Structure

- EntityForge - Main Web API: controllers, models, templates, generation pipeline, Docker/CLI services
- EntityForge.Gateway - API Gateway: request routing to generated services, entity relations management
- EntityForge.Shared - Shared models (ServiceInfo, ServiceStatus, ServicePaths)

## How Generation Works

1. Initialization - port allocation, versioning
2. Project structure creation
3. Domain layer generation (aggregates, value objects, repositories)
4. Application layer generation (use cases, DTOs, mapping)
5. Infrastructure layer generation (database, SQL schemas)
6. API layer generation (controllers, middleware, Dockerfile)
7. Project test build (dotnet build)
8. Docker image build
9. Container launch via docker compose
10. PostgreSQL availability check

Any step failure triggers automatic rollback.

## API

### Entity Management

```
POST   /api/gateway/entities                               create entity definition
GET    /api/gateway/entities                               list all entities
GET    /api/gateway/entities/{name}                        get entity definition
PUT    /api/gateway/entities/{name}                        update entity definition
DELETE /api/gateway/entities/{name}                        delete entity definition
POST   /api/gateway/entities/{name}/generate               generate and run microservice
```

### Data Operations (generated services)

```
POST   /api/gateway/{entity}                               create record
GET    /api/gateway/{entity}                               list records
GET    /api/gateway/{entity}/{id}                          get record by id
PUT    /api/gateway/{entity}/{id}                          update record
DELETE /api/gateway/{entity}/{id}                          delete record
```

### Service Management

```
GET    /api/gateway/services                               list running services
GET    /api/gateway/services/{serviceName}                 service info
DELETE /api/gateway/services/{serviceName}                 stop and remove service
GET    /api/gateway/services/{serviceName}/dependencies    service dependencies
```

### Types

```
GET    /api/gateway/types                                  list available field types
GET    /api/gateway/types/{id}                             get type info
```

### Health

```
GET    /health                                             health check
```

## Quick Start (requires Docker)

```
docker-compose up -d
dotnet run --project EntityForge
dotnet run --project EntityForge.Gateway
```

Swagger UI available at each application URL.

## Frontend link
[Frontend](https://github.com/Ha1fmoon/EntityForge.Frontend)
