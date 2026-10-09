namespace KnapsackChallenge.Common.Enums
{
    // Trạng thái nộp bài của từng người trong 1 ván multiplayer.
    public enum SubmissionState
    {
        NotStarted,     // ván chưa bắt đầu
        Playing,        // ván đang chạy, chưa nộp
        Submitted,      // user tự bấm nộp
        AutoSubmitted   // server tự nộp khi hết giờ (0 điểm)
    }
}