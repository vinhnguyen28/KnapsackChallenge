//using System;
//using System.Collections.Generic;
//using System.Text;
using System.Data;
using Microsoft.Data.SqlClient;
using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.Data.Repositories
{
    public class UserRepository
    {
        private readonly DbConnectionHelper _dbHelper;

        public UserRepository()
        {
            // Khởi tạo helper để lấy chuỗi kết nối
            _dbHelper = new DbConnectionHelper();
        }

        // Hàm tìm user theo Usernametieeps
        public UserEntity? GetUserByUsername(string username)
        {
            using (var connection = _dbHelper.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, Username, PasswordHash, Role FROM Users WHERE Username = @Username";

                    // Tránh SQL Injection
                    var param = command.CreateParameter();
                    param.ParameterName = "@Username";
                    param.Value = username;
                    command.Parameters.Add(param);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new UserEntity
                            {
                                Id = reader.GetInt32(0),
                                Username = reader.GetString(1),
                                PasswordHash = reader.GetString(2),
                                Role = reader.GetString(3)
                            };
                        }
                    }
                }
            }
            return null; // Trả về null nếu không tìm thấy user
        } 

        // Thêm user mới vào bảng Users.
        // Trả về true nếu thêm thành công, false nếu Username đã tồn tại (vi phạm UNIQUE).
        public bool CreateUser(string username, string passwordHash, string role)
        {
            using (var connection = _dbHelper.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "INSERT INTO Users (Username, PasswordHash, Role) VALUES (@Username, @PasswordHash, @Role)";

                    AddParameter(command, "@Username", username);
                    AddParameter(command, "@PasswordHash", passwordHash);
                    AddParameter(command, "@Role", role);

                    try
                    {
                        command.ExecuteNonQuery(); // INSERT/UPDATE/DELETE dùng ExecuteNonQuery (không trả dòng dữ liệu)
                        return true;
                    }
                    catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                    {
                        // 2627 / 2601 = lỗi trùng khóa UNIQUE -> Username đã có người dùng
                        return false;
                    }
                }
            }
        }

        // Hàm phụ để thêm tham số nhanh, tránh lặp code
        private static void AddParameter(IDbCommand command, string name, object value)
        {
            var param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            command.Parameters.Add(param);
        }
    }
}