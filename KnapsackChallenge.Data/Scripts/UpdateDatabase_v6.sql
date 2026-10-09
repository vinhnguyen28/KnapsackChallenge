/* =============================================================
   Update Database v6 - Hỗ trợ dọn phiên Multiplayer treo
   ------------------------------------------------------------
   - Thêm giá trị 'Abandoned' vào CHECK constraint của cột
     GameSessions.Status NẾU DB thật có constraint đó.
   - Script TỰ DÒ tên constraint qua sys.check_constraints
     (không hard-code) để chạy được trên mọi môi trường.
   - Nếu không có CHECK constraint trên cột Status -> không làm gì.
   - Idempotent: chạy lại nhiều lần vẫn an toàn.
   - Mọi timestamp dùng SYSUTCDATETIME() (UTC).
   ============================================================= */

USE KnapsackChallenge;
GO

SET NOCOUNT ON;
GO

DECLARE @constraintName sysname = NULL;
DECLARE @definition     nvarchar(max) = NULL;

/* ---------- 1. Tìm CHECK constraint (nếu có) trên GameSessions.Status ---------- */
SELECT TOP 1
    @constraintName = cc.name,
    @definition     = cc.definition
FROM sys.check_constraints AS cc
INNER JOIN sys.columns AS c
    ON  c.object_id  = cc.parent_object_id
    AND c.column_id  = cc.parent_column_id
WHERE cc.parent_object_id = OBJECT_ID('dbo.GameSessions')
  AND c.name              = 'Status';

/* ---------- 2. Nếu có constraint mà chưa cho phép 'Abandoned' -> drop + tạo lại ---------- */
IF @constraintName IS NOT NULL AND @definition NOT LIKE '%Abandoned%'
BEGIN
    DECLARE @sqlDrop nvarchar(max) =
        N'ALTER TABLE dbo.GameSessions DROP CONSTRAINT ' + QUOTENAME(@constraintName);
    EXEC sp_executesql @sqlDrop;

    -- Giữ nguyên mọi giá trị đang được code dùng + thêm 'Abandoned'.
    DECLARE @sqlAdd nvarchar(max) =
        N'ALTER TABLE dbo.GameSessions ADD CONSTRAINT ' + QUOTENAME(@constraintName)
      + N' CHECK ([Status] IN (N''Waiting'', N''Playing'', N''Finished'', N''Abandoned''))';
    EXEC sp_executesql @sqlAdd;

    PRINT N'[v6] Đã nới CHECK constraint "' + @constraintName + N'" để cho phép Status = ''Abandoned''.';
END
ELSE IF @constraintName IS NULL
BEGIN
    PRINT N'[v6] Không có CHECK constraint trên GameSessions.Status — không cần thay đổi.';
END
ELSE
BEGIN
    PRINT N'[v6] CHECK constraint "' + @constraintName + N'" đã cho phép ''Abandoned'' — bỏ qua.';
END
GO