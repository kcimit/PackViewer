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
        bool _folderInTrash, _autoTrashFolder, _autoRemoveFiles;
        bool _folderIsSaved, _isFileSaved, _isFileDeleted;
        private string statusBottom;
        private string statusTop;

        public int RightIconSize { get; set; } = 40;
        public int IconSize { get; set; } = 40;
        public int IconSpacing { get; set; } = 15;

        public bool IsFolderInTrash
        {
            get => _folderInTrash;
            set
            {
                if (_folderInTrash == value) return;

                _folderInTrash = value;
                OnPropertyChanged(nameof(IsFolderInTrash));
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
                if (_autoTrashFolder && !FolderIsSaved && !FolderInTrash)
                {
                    IsFolderInTrash = true;
                    _currentFolder.Status=Status.Delete;
                }
            }
        }

        public bool AutoRemoveFiles
        {
            get => _autoRemoveFiles;
            set
            {
                if (_autoRemoveFiles == value) return;

                _autoRemoveFiles = value;
                OnPropertyChanged(nameof(AutoRemoveFiles));
            }
        }

        public bool IsSaved
        {
            get  => _folderIsSaved;
            set
            {
                if (_folderIsSaved == value) return;

                _folderIsSaved = value;
                OnPropertyChanged(nameof(IsSaved));
            }
        }

        public bool IsFileDeleted
        {
            get => _isFileDeleted;
            set
            {
                if (_isFileDeleted == value) return;

                _isFileDeleted = value;
                OnPropertyChanged(nameof(IsFileDeleted));
            }
        }

        public bool IsFileSaved
        {
            get => _isFileSaved;
            set
            {
                if (_isFileSaved == value) return;

                _isFileSaved = value;
                OnPropertyChanged(nameof(IsFileSaved));
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
            IsFolderInTrash = false;
            AutoTrashFolder = false;
            AutoRemoveFiles = false;
        }
    }
}
