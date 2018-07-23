using System;
using GalaSoft.MvvmLight;

namespace ArcgisPoC.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        #region Properties

        string title;
        
        public string Title
        {
            get
            {
                return title;
            }

            set
            {
                if (title == value)
                {
                    return;
                }
                title = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        public MainViewModel()
        {
            if (IsInDesignMode)
            {
                Title = "Hello MVVM Light (Design Mode)";
            }
            else
            {
                Title = "Hello MVVM Light";
            }
        }
    }
}