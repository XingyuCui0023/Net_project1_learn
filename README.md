# Finance Tracker API

一个适合写进简历的 .NET 8 后端学习项目：个人记账系统 API。

## 技术栈

- .NET 8 / ASP.NET Core Minimal API
- Entity Framework Core
- SQLite
- JWT Bearer Authentication
- Swagger / OpenAPI
- xUnit + WebApplicationFactory
- Docker / Docker Compose
- GitHub Actions

## 已完成功能

- 健康检查：`GET /api/health`
- 用户注册：`POST /api/auth/register`
- 用户登录：`POST /api/auth/login`
- JWT 保护业务接口
- 账户管理：创建、查询
- 分类管理：创建、查询
- 交易记录 CRUD
- 交易筛选：日期、账户、分类
- 月度预算设置
- 月度收支报表
- 按分类统计支出
- 超预算判断
- 用户数据隔离测试

## 本地运行

当前项目使用用户目录安装的 .NET SDK。若 `dotnet --version` 找不到 SDK，可先在 PowerShell 中设置：

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"
$env:PATH="$env:USERPROFILE\.dotnet;$env:PATH"
```

运行项目：

```powershell
dotnet restore FinanceTracker.sln --configfile NuGet.config
dotnet run --project src/FinanceTracker.Api
```

打开 Swagger：

```text
https://localhost:5001/swagger
http://localhost:5000/swagger
```

## 测试

```powershell
dotnet test FinanceTracker.sln
```

测试覆盖：

- 未登录访问受保护接口返回 401
- 注册后可获得 JWT
- 可创建账户、分类、交易、预算
- 月度统计金额正确
- 超预算判断正确
- 用户无法读取其他用户交易

## API 示例

注册：

```http
POST /api/auth/register
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

## Docker

```powershell
docker compose up --build
```

服务地址：

```text
http://localhost:8080
```

## 简历描述

使用 .NET 8 构建个人记账系统后端，实现用户注册登录、JWT 鉴权、账户和分类管理、交易记录 CRUD、月度预算、分类支出统计和超预算判断。项目使用 EF Core + SQLite 完成数据持久化，使用 xUnit 和 WebApplicationFactory 编写集成测试，并提供 Docker Compose 和 GitHub Actions 构建测试流程。

## 面试讲解要点

- 为什么使用 JWT：API 无状态，适合前后端分离和移动端调用。
- 如何做用户数据隔离：所有业务表保存 `UserId`，查询和修改都按当前 JWT 用户过滤。
- EF Core 建模：用户、账户、分类、交易、预算是核心实体，预算按用户、分类、年月唯一。
- 测试重点：认证保护、核心业务流程、预算统计、跨用户访问控制。
- 后续可扩展：刷新令牌、分页、审计日志、导入账单 CSV、前端管理页面。
