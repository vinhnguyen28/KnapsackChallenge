using KnapsackChallenge.Data.Entities;
using Microsoft.Data.SqlClient;
using System.Data;

namespace KnapsackChallenge.Data.Repositories
{
    public class ItemRepository
    {
        private readonly DbConnectionHelper _dbHelper = new DbConnectionHelper();

        // v10: đọc thêm IconKey. Dùng IsDBNull để tương thích ngược:
        // hàng cũ IconKey = NULL vẫn map ra ItemEntity.IconKey = null.
        public List<ItemEntity> GetAll()
        {
            var list = new List<ItemEntity>();
            using (var connection = _dbHelper.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, Name, Weight, Value, IconKey FROM Items ORDER BY Id";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new ItemEntity
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Weight = reader.GetInt32(2),
                                Value = reader.GetInt32(3),
                                IconKey = reader.IsDBNull(4) ? null : reader.GetString(4) // v10
                            });
                        }
                    }
                }
            }
            return list;
        }

        // Thêm mới, trả về Id vừa được SQL Server sinh ra.
        // v10: nhận thêm iconKey (null = icon "default").
        public int Create(string name, int weight, int value, string? iconKey)
        {
            using (var connection = _dbHelper.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "INSERT INTO Items (Name, Weight, Value, IconKey) " +
                        "OUTPUT INSERTED.Id VALUES (@Name, @Weight, @Value, @IconKey)";
                    AddParameter(command, "@Name", name);
                    AddParameter(command, "@Weight", weight);
                    AddParameter(command, "@Value", value);
                    // DBNull.Value khi iconKey null — tránh lỗi tham số null.
                    AddParameter(command, "@IconKey", (object?)iconKey ?? DBNull.Value);
                    return (int)command.ExecuteScalar()!;
                }
            }
        }

        // v10: nhận thêm iconKey. Truyền null để xoá icon (về "default").
        public bool Update(int id, string name, int weight, int value, string? iconKey)
        {
            using (var connection = _dbHelper.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "UPDATE Items SET Name = @Name, Weight = @Weight, Value = @Value, " +
                        "IconKey = @IconKey WHERE Id = @Id";
                    AddParameter(command, "@Id", id);
                    AddParameter(command, "@Name", name);
                    AddParameter(command, "@Weight", weight);
                    AddParameter(command, "@Value", value);
                    AddParameter(command, "@IconKey", (object?)iconKey ?? DBNull.Value);
                    return command.ExecuteNonQuery() > 0;
                }
            }
        }

        // Trả về false nếu vật phẩm đang được dùng trong SetItems / SelectedItems (vi phạm khóa ngoại)
        public bool Delete(int id)
        {
            using (var connection = _dbHelper.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM Items WHERE Id = @Id";
                    AddParameter(command, "@Id", id);
                    try
                    {
                        return command.ExecuteNonQuery() > 0;
                    }
                    catch (SqlException ex) when (ex.Number == 547) // 547 = vi phạm FOREIGN KEY
                    {
                        return false;
                    }
                }
            }
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            var param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            command.Parameters.Add(param);
        }
    }
}