using MaiFileManager.Classes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace MaiFileManager.Services
{
    public partial class FileManager : INotifyPropertyChanged
    {
        string currentDir;
        public string CurrentDir 
        { 
            get => currentDir;
            set 
            {
                currentDir = value;
                OnPropertyChanged(nameof(CurrentDir));
                OnPropertyChanged(nameof(DirName));
                OnPropertyChanged(nameof(CurrentDirView));
            } 
        }
        public string DirName
        {
            get
            {
                if (currentDir == "Favourite") return "Favourite";
                if (currentDir == MaiConstants.HomePath)
                    return Preferences.Default.Get("Aws_Bucket_name", "");
                return Path.GetFileName(CurrentDir);
            }
        }
        public string CurrentDirView
        {
            get
            {
                if (currentDir == "Favourite") return "Favourite";
                if (currentDir.StartsWith(MaiConstants.HomePath))
                {
                    return Preferences.Default.Get("Aws_Bucket_name", "") + currentDir.Substring(MaiConstants.HomePath.Length);
                }
                return currentDir;
            }
            set
            {
                OnPropertyChanged(nameof(CurrentDirView));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string name = "") =>
             PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public partial ObservableCollection<FileSystemInfo> GetListFile();
        public partial void UpdateDir(string newDir);
        public partial void BackDir();
        public partial bool GetPerm();
    }
}
