using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaiFileManager.Classes
{
    internal class AWSFileInfo
    {
        private int _fileId;
        private string _fileName;
        private string _fileSize;
        private string _fileType;
        private string _fileLink;
        private long _folderId;

        [PrimaryKey, AutoIncrement]
        public int FileId { get => _fileId; set => _fileId = value; }
        public string FileName { get => _fileName; set => _fileName = value; }
        public string FileSize { get => _fileSize; set => _fileSize = value; }
        public string FileType { get => _fileType; set => _fileType = value; }
        public string FileLink { get => _fileLink; set => _fileLink = value; }
        public long FolderId { get => _folderId; set => _folderId = value; }
    }
}
