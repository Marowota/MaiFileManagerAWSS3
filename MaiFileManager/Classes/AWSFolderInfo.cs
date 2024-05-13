using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaiFileManager.Classes
{
    internal class AWSFolderInfo
    {
        private long _folderId;
        private string _folderName;
        private string _parentFolderId;

        [PrimaryKey, AutoIncrement]
        public long FolderId { get => _folderId; set => _folderId = value; }
        public string FolderName { get => _folderName; set => _folderName = value; }
        public string ParentFolderId { get => _parentFolderId; set => _parentFolderId = value; }
    }
}
