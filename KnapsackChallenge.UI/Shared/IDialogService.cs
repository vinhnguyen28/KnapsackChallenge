using System.Windows;

namespace KnapsackChallenge.UI.Shared
{
    public interface IDialogService
    {
        bool Confirm(string message, string title);
    }

    public class WpfDialogService : IDialogService
    {
        public bool Confirm(string message, string title) =>
            MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            == MessageBoxResult.Yes;
    }
}