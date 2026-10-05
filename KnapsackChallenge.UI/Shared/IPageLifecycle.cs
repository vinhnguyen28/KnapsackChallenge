namespace KnapsackChallenge.UI.Shared
{
    // ViewModel nào cần dọn dẹp khi trang bị thay khỏi vùng nội dung thì implement interface này.
    public interface IPageLifecycle
    {
        void OnNavigatedFrom();
    }
}