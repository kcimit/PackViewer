using Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PackViewer
{
    public class ViewModel : ViewModelBase
    {
        bool _inThrash;
        bool _isSaved;
        private string status;
        private string status2;

        public bool IsInThrash
        {
            get { return _inThrash; }
            set
            {
                if (_inThrash == value) return;

                _inThrash = value;
                OnPropertyChanged(nameof(IsInThrash));
            }
        }

        public bool IsSaved
        {
            get { return _isSaved; }
            set
            {
                if (_isSaved == value) return;

                _isSaved = value;
                OnPropertyChanged(nameof(IsSaved));
            }
        }

        public string Status
        {
            get => status;
            set
            {
                if (status == value) return;

                status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public string Status2
        {
            get => status2;
            set
            {
                if (status2 == value) return;

                status2 = value;
                OnPropertyChanged(nameof(Status2));
            }
        }

        public ViewModel()
        {
            uiSynchronizationContext = SynchronizationContext.Current;
            ControlsEnabled = true;
            IsInThrash = false;
        }
    }
}
