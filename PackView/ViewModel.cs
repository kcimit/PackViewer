using Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PackViewer
{
    public partial class ViewModel : ViewModelBase
    {
        bool _inThrash, _autoTrashFolder;
        bool _isSaved, _isFav;
        private string statusBottom;
        private string statusTop;

        public bool IsInThrash
        {
            get => _inThrash;
            set
            {
                if (_inThrash == value) return;

                _inThrash = value;
                OnPropertyChanged(nameof(IsInThrash));
            }
        }

        public bool AutoTrashFolder
        {
            get => _autoTrashFolder;
            set
            {
                if (_autoTrashFolder == value) return;

                _autoTrashFolder = value;
                OnPropertyChanged(nameof(AutoTrashFolder));
                if (_autoTrashFolder && !FolderIsSaved && !FolderInThrash)
                {
                    IsInThrash = true;
                    _foldersToDelete.Add(_currentFolder);
                }
            }
        }


        public bool IsSaved
        {
            get  => _isSaved;
            set
            {
                if (_isSaved == value) return;

                _isSaved = value;
                OnPropertyChanged(nameof(IsSaved));
            }
        }
        public bool IsFav
        {
            get => _isFav;
            set
            {
                if (_isFav == value) return;

                _isFav = value;
                OnPropertyChanged(nameof(IsFav));
            }
        }

        public string StatusBottom
        {
            get => statusBottom;
            set
            {
                if (statusBottom == value) return;

                statusBottom = value;
                OnPropertyChanged(nameof(StatusBottom));
            }
        }

        public string StatusTop
        {
            get => statusTop;
            set
            {
                if (statusTop == value) return;

                statusTop = value;
                OnPropertyChanged(nameof(StatusTop));
            }
        }

        public ViewModel()
        {
            uiSynchronizationContext = SynchronizationContext.Current;
            ControlsEnabled = true;
            IsInThrash = false;
            AutoTrashFolder = false;
        }
    }
}
