using System.Data;
using Microsoft.Data.SqlClient;
using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.Data.Repositories
{
    public class SetRepository
    {
        private readonly DbConnectionHelper _dbHelper = new();

        public List<KnapsackSetEntity> GetAll()
        {
            var list = new List<KnapsackSetEntity>();
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT Id, SetName, MaxWeight, Difficulty FROM KnapsackSets ORDER BY Id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new KnapsackSetEntity
                {
                    Id = reader.GetInt32(0),
                    SetName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    MaxWeight = reader.GetInt32(2),
                    Difficulty = reader.IsDBNull(3) ? "" : reader.GetString(3),
                });
            }
            return list;
        }

        public int Create(string setName, int maxWeight, string difficulty)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO KnapsackSets (SetName, MaxWeight, Difficulty)
                OUTPUT INSERTED.Id
                VALUES (@SetName, @MaxWeight, @Difficulty)";
            AddParameter(command, "@SetName", setName);
            AddParameter(command, "@MaxWeight", maxWeight);
            AddParameter(command, "@Difficulty", difficulty);
            return (int)command.ExecuteScalar()!;
        }

        public bool Update(int id, string setName, int maxWeight, string difficulty)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE KnapsackSets
                SET SetName = @SetName, MaxWeight = @MaxWeight, Difficulty = @Difficulty
                WHERE Id = @Id";
            AddParameter(command, "@Id", id);
            AddParameter(command, "@SetName", setName);
            AddParameter(command, "@MaxWeight", maxWeight);
            AddParameter(command, "@Difficulty", difficulty);
            return command.ExecuteNonQuery() > 0;
        }

        // Trả false nếu Set đang được GameSessions dùng (FK 547) hoặc không tồn tại.
        // Xóa SetItems trước (bảng nối) trong cùng transaction.
        public bool Delete(int id)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();
            try
            {
                using (var delItems = connection.CreateCommand())
                {
                    delItems.Transaction = tx;
                    delItems.CommandText = "DELETE FROM SetItems WHERE SetId = @Id";
                    AddParameter(delItems, "@Id", id);
                    delItems.ExecuteNonQuery();
                }

                int affected;
                using (var delSet = connection.CreateCommand())
                {
                    delSet.Transaction = tx;
                    delSet.CommandText = "DELETE FROM KnapsackSets WHERE Id = @Id";
                    AddParameter(delSet, "@Id", id);
                    affected = delSet.ExecuteNonQuery();
                }

                tx.Commit();
                return affected > 0;
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                // Set đang được GameSessions.SetId trỏ tới
                tx.Rollback();
                return false;
            }
        }

        public List<int> GetItemIdsInSet(int setId)
        {
            var list = new List<int>();
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT ItemId FROM SetItems WHERE SetId = @Id";
            AddParameter(command, "@Id", setId);
            using var reader = command.ExecuteReader();
            while (reader.Read()) list.Add(reader.GetInt32(0));
            return list;
        }

        // Thay thế toàn bộ danh sách vật phẩm của Set bằng danh sách mới.
        public void ReplaceSetItems(int setId, IEnumerable<int> itemIds)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            using (var del = connection.CreateCommand())
            {
                del.Transaction = tx;
                del.CommandText = "DELETE FROM SetItems WHERE SetId = @Id";
                AddParameter(del, "@Id", setId);
                del.ExecuteNonQuery();
            }

            foreach (var itemId in itemIds.Distinct())
            {
                using var ins = connection.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = "INSERT INTO SetItems (SetId, ItemId) VALUES (@SetId, @ItemId)";
                AddParameter(ins, "@SetId", setId);
                AddParameter(ins, "@ItemId", itemId);
                ins.ExecuteNonQuery();
            }

            tx.Commit();
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