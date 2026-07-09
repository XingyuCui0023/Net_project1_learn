# Finance Tracker API

一个用于学习和简历展示的 .NET 8 后端项目：个人记账系统 API。

## Tech Stack

- .NET 8 / ASP.NET Core Minimal API
- Entity Framework Core / SQLite / Code-first Migrations
- JWT Bearer Authentication
- Swagger / OpenAPI
- xUnit / WebApplicationFactory
- Docker / Docker Compose
- GitHub Actions

## Features

- 用户注册和登录
- JWT 保护业务接口
- 账户 CRUD
- 分类 CRUD 和 Soft Delete
- 交易 CRUD
- 日期、账户、分类筛选交易
- 账户余额计算
- 月度预算 CRUD
- 月度收支报表
- 分类支出统计
- 超预算判断
- 用户数据隔离
- 统一错误响应
- 开发环境 Seed Data
- 自动化集成测试

## Demo Account

开发环境启动后会自动创建 demo 数据：

```text
Email: demo@example.com
Password: Password123!
```

## Local Run

如果 `dotnet` 命令不可用，先在 PowerShell 设置本地 SDK 路径：

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"
$env:PATH="$env:USERPROFILE\.dotnet;$env:PATH"
```

运行 API：

```powershell
dotnet restore FinanceTracker.sln --configfile NuGet.config
dotnet run --project src/FinanceTracker.Api
```

打开 Swagger：

```text
http://localhost:5250/swagger
```

端口可能根据本机环境不同而变化，请以终端输出为准。

## Database

项目使用 EF Core Code-first Migrations 管理 SQLite 数据库结构。

常用命令：

```powershell
dotnet ef migrations add <MigrationName> --project src/FinanceTracker.Api --startup-project src/FinanceTracker.Api
dotnet ef database update --project src/FinanceTracker.Api --startup-project src/FinanceTracker.Api
```

关键词：

- Migration: 数据库迁移
- Schema: 数据库结构
- Model Snapshot: 模型快照
- Migration History: `__EFMigrationsHistory`

## Docker

构建并启动：

```powershell
docker compose up --build
```

访问：

```text
http://localhost:8080/swagger
```

停止：

```powershell
docker compose down
```

删除数据卷并重建 demo 数据：

```powershell
docker compose down -v
docker compose up --build
```

## Tests

运行测试：

```powershell
dotnet test FinanceTracker.sln
```

当前测试覆盖：

- 未登录访问受保护接口返回 401
- 错误响应格式统一
- Seed Data 不重复创建
- 注册、登录、JWT
- 账户、分类、交易、预算 CRUD
- 分类 Soft Delete
- 账户余额计算
- 月度报表和超预算判断
- 用户数据隔离

## API Examples

注册：

```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "Password123!"
}
```

登录：

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "demo@example.com",
  "password": "Password123!"
}
```

创建账户：

```http
POST /api/accounts
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "Cash",
  "type": "Wallet",
  "openingBalance": 100
}
```

创建支出分类：

```http
POST /api/categories
Authorization: Bearer <token>
Content-Type: application/json

{
  "name": "Food",
  "type": 2
}
```

创建交易：

```http
POST /api/transactions
Authorization: Bearer <token>
Content-Type: application/json

{
  "accountId": "<account-id>",
  "categoryId": "<category-id>",
  "type": 2,
  "amount": 45.5,
  "note": "Lunch",
  "occurredOn": "2026-07-02"
}
```

查询月度报表：

```http
GET /api/reports/monthly?year=2026&month=7
Authorization: Bearer <token>
```

## Resume Summary

使用 .NET 8 构建个人记账系统后端 API，实现用户认证、JWT 授权、账户/分类/交易/预算管理、月度收支报表、分类支出统计、超预算判断和用户数据隔离。项目使用 EF Core + SQLite + Code-first Migrations 完成数据持久化和数据库结构管理，使用 xUnit + WebApplicationFactory 编写集成测试，并提供 Docker Compose 本地部署。

## Interview Talking Points

- JWT 如何实现无状态认证
- User Data Isolation 如何通过 `UserId` 和查询过滤实现
- Soft Delete 如何保留历史交易数据
- EF Core Migrations 如何管理数据库 schema
- DTO 如何区分请求模型和响应模型
- LINQ 如何实现筛选、求和和报表统计
- Integration Tests 如何验证真实 API 行为
- Docker 如何让项目更容易部署和演示
