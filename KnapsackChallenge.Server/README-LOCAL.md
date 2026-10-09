# Chạy local — KnapsackChallenge.Server

## 1. Chuẩn bị DB
Chạy đủ 5 script trong `KnapsackChallenge.Data/Scripts/`:
`InitDatabase.sql` → `UpdateDatabase_v2.sql` → `v3` → `v4` → `v5`.

## 2. Cấu hình
`appsettings.Development.json` (mặc định ASPNETCORE_ENVIRONMENT=Development khi `dotnet run`):
- `Jwt:Secret` — chuỗi ≥ 32 ký tự.
- `ConnectionStrings:KnapsackDb` — chỉnh nếu SQL Server không ở `localhost`.

## 3. Chạy
```bash
cd KnapsackChallenge.Server
dotnet run