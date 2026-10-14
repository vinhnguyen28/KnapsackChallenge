//using System;
//using System.Collections.Generic;
//using System.Text;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace KnapsackChallenge.UI.Shared
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            CommandManager.InvalidateRequerySuggested();
        }
    }
}