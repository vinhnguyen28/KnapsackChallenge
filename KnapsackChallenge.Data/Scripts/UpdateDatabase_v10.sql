/* =============================================================
   Update Database v10 - Icon cho vật phẩm (Item Icons)
   ------------------------------------------------------------
   - Thêm cột Items.IconKey NVARCHAR(50) NULL.
   - Tương thích ngược: dữ liệu cũ IconKey = NULL sẽ dùng
     icon "default" ở phía client.
   - Client cũ (không biết cột này) đọc SELECT cột tường minh
     nên vẫn chạy bình thường.
   - Idempotent: chạy lại nhiều lần vẫn an toàn.
   ============================================================= */

/* ---------- 1. Cột IconKey ---------- */
IF COL_LENGTH('dbo.Items', 'IconKey') IS NULL
    ALTER TABLE Items ADD IconKey NVARCHAR(50) NULL;
GO


/* ---------- 2. GỢI Ý gán IconKey cho vật phẩm mẫu ----------
   Khối UPDATE dưới đây đang BỊ COMMENT sẵn. Muốn chạy, bạn tự bỏ
   comment. Nếu tên vật phẩm trong DB khác, sửa lại cho khớp.

   Quy ước: Key phải khớp tên file PNG (không phần mở rộng) trong
   thư mục KnapsackChallenge.UI/Assets/ItemIcons/.
-------------------------------------------------------------------
UPDATE Items SET IconKey = N'book'          WHERE Name = N'Sách';
UPDATE Items SET IconKey = N'laptop'        WHERE Name = N'Laptop';
UPDATE Items SET IconKey = N'flashlight'    WHERE Name = N'Đèn pin';
UPDATE Items SET IconKey = N'food_can'      WHERE Name = N'Đồ ăn';
UPDATE Items SET IconKey = N'camera'        WHERE Name = N'Máy ảnh';
UPDATE Items SET IconKey = N'coat'          WHERE Name = N'Áo khoác';
UPDATE Items SET IconKey = N'tent'          WHERE Name = N'Lều nhỏ';
UPDATE Items SET IconKey = N'water_bottle'  WHERE Name = N'Bình nước';
UPDATE Items SET IconKey = N'dried_food'    WHERE Name = N'Thức ăn khô';
UPDATE Items SET IconKey = N'sleeping_bag'  WHERE Name = N'Túi ngủ';
UPDATE Items SET IconKey = N'camping_stove' WHERE Name = N'Bếp gas mini';
UPDATE Items SET IconKey = N'raincoat'      WHERE Name = N'Áo mưa';
UPDATE Items SET IconKey = N'rope'          WHERE Name = N'Dây thừng';
UPDATE Items SET IconKey = N'first_aid_kit' WHERE Name = N'Bộ sơ cứu';
GO
*/