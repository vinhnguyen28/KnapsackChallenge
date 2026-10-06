namespace KnapsackChallenge.Core.Algorithms
{
    // Quy hoạch động 0/1 cho bài toán cái túi.
    // - Public + static để dễ unit test.
    // - Xử lý biên: danh sách rỗng, W <= 0, vật phẩm nặng hơn W.
    public static class KnapsackSolver
    {
        // Trả về (giá trị tối ưu, danh sách ItemId của MỘT đáp án tối ưu).
        public static (int OptimalValue, List<int> SelectedItemIds) Solve(
            IReadOnlyList<(int Id, int Weight, int Value)> items,
            int capacity)
        {
            // Biên: không có gì để chọn.
            if (items == null || items.Count == 0 || capacity <= 0)
                return (0, new List<int>());

            // Lọc bỏ vật phẩm không hợp lệ (weight <= 0, value <= 0, nặng hơn capacity).
            var valid = new List<(int Id, int Weight, int Value)>(items.Count);
            foreach (var it in items)
            {
                if (it.Weight <= 0 || it.Value <= 0) continue;
                if (it.Weight > capacity) continue;
                valid.Add(it);
            }
            if (valid.Count == 0) return (0, new List<int>());

            int n = valid.Count;
            // dp[i, w] = giá trị tối ưu dùng i vật đầu, sức chứa w.
            var dp = new int[n + 1, capacity + 1];

            for (int i = 1; i <= n; i++)
            {
                var (_, w, v) = valid[i - 1];
                for (int c = 0; c <= capacity; c++)
                {
                    dp[i, c] = dp[i - 1, c];
                    if (w <= c)
                    {
                        int candidate = dp[i - 1, c - w] + v;
                        if (candidate > dp[i, c]) dp[i, c] = candidate;
                    }
                }
            }

            // Truy vết ngược để lấy danh sách ItemId của đáp án tối ưu.
            var selected = new List<int>();
            int cur = capacity;
            for (int i = n; i >= 1; i--)
            {
                if (dp[i, cur] != dp[i - 1, cur])
                {
                    var (id, w, _) = valid[i - 1];
                    selected.Add(id);
                    cur -= w;
                }
            }

            return (dp[n, capacity], selected);
        }
    }
}