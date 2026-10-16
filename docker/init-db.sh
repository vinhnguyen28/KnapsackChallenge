#!/bin/bash
# Tạo database KnapsackChallenge + chạy toàn bộ script theo thứ tự + seed dữ liệu mẫu.
# An toàn khi chạy lại nhiều lần (Init chỉ chạy khi chưa có bảng Users).

SQLCMD=$(ls /opt/mssql-tools18/bin/sqlcmd /opt/mssql-tools/bin/sqlcmd 2>/dev/null | head -1)
HOST="${SQL_HOST:-sql}"
PW="${SA_PASSWORD:?thiếu SA_PASSWORD}"
DB="KnapsackChallenge"
SCRIPTS="/scripts"

if [ -z "$SQLCMD" ]; then
  echo "[init] LỖI: không tìm thấy sqlcmd trong image."; exit 1
fi

q() { "$SQLCMD" -C -S "$HOST" -U sa -P "$PW" -b "$@"; }

# Bỏ BOM UTF-8 + CRLF rồi chạy bằng UTF-8 (script có tiếng Việt).
run_file() {
  local f="$1"
  if [ ! -f "$f" ]; then echo "[init] LỖI: thiếu file $f"; exit 1; fi
  echo "[init] chạy $(basename "$f")"
  sed '1s/^\xEF\xBB\xBF//' "$f" | tr -d '\r' > /tmp/current.sql
  q -d "$DB" -f 65001 -i /tmp/current.sql || { echo "[init] LỖI khi chạy $f"; exit 1; }
}

echo "[init] tạo database $DB nếu chưa có..."
q -Q "IF DB_ID('$DB') IS NULL CREATE DATABASE [$DB];" || { echo "[init] LỖI tạo database"; exit 1; }

has_users=$(q -d "$DB" -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables WHERE name='Users'" | tr -d '[:space:]')
if [ "$has_users" = "0" ]; then
  run_file "$SCRIPTS/InitDatabase.sql"
else
  echo "[init] đã có schema, bỏ qua InitDatabase.sql"
fi

for f in $(ls "$SCRIPTS"/UpdateDatabase_v*.sql | sort -V); do
  run_file "$f"
done

run_file "/docker/seed-local.sql"

echo "[init] XONG. Tài khoản mẫu: admin / Admin@123  và  player1 / Player@123"
