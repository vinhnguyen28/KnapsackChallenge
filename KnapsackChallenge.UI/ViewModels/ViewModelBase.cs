//using System;
//using System.Collections.Generic;
//using System.Text;

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KnapsackChallenge.UI.ViewModels.Base
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}