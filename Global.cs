namespace PackViewer
{
    public static class Global
    {
        public const bool BackupDeleted = true;
        public const string FolderDeletedName = "_DeletedBackup";
        public const string FolderSavedName = "_Saved";
        public const string FolderFavName = "_FavPackViewer";
        public const string FolderAutoRemoveName = "_DeletedPackViewer";

        // Do not save folder structue if number of folders below the limit
        public const int MinNumberOfFoldersToCache = 20;
    }
}
