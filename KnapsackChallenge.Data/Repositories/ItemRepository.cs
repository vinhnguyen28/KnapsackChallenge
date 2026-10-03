using System.Data;
using Microsoft.Data.SqlClient;
using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.Data.Repositories
{
    public class ItemRepository
    {
        private readonly DbConnectionHelper _dbHelper = new DbConnectionHelper();

        public List<ItemEntity> GetAll()
        {
            var list = new List<ItemEntity>();
            using (var connection = _dbHelper.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Id, Name, Weight, Value FROM Items ORDER BY Id";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new ItemEntity
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                Weight = reader.GetInt32(2),
                                Value = reader.GetInt32(3)
                            });
                        }
                    }
                }
            }
            return list;
        }

        // Thêm mới, trả về Id vừa được SQL Server sinh ra
        public int Create(string name, int weight, int value)
        {
            using (var connection = _dbHelper.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "INSERT INTO Items (Name, Weight, Value) OUTPUT INSERTED.Id VALUES (@Name, @Weight, @Value)";
                    AddParameter(command, "@Name", name);
                    AddParameter(command, "@Weight", weight);
                    AddParameter(command, "@Value", value);
                    return (int)command.ExecuteScalar()!;
                }
            }
        }

        public bool Update(int id, string name, int weight, int value)
        {
            using (var connection = _dbHelper.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "UPDATE Items SET Name = @Name, Weight = @Weight, Value = @Value WHERE Id = @Id";
                    AddParameter(command, "@Id", id);
                    AddParameter(command, "@Name", name);
                    AddParameter(command, "@Weight", weight);
                    AddParameter(command, "@Value", value);
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