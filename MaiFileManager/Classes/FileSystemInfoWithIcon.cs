using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MaiFileManager.Classes
{
    public class FileSystemInfoWithIcon : INotifyPropertyChanged
    {
        public FileSystemInfo fileInfo { get; set; }
        public string iconPath { get; set; }
        public int iconSize { get; set; }
        public string fileInfoSize { get; set; }
        public string[] fileSizeMeasure { get; set; } = { "B", "K", "M", "G", "T" };
        public string lastModified { get; set; }
        public double awsCustomSize { get; set; } = -1;
        public DateTime awsCustomLastModified { get; set; }

        public double dualFileSize
        {
            get
            {
                if (awsCustomSize != -1)
                {
                    return awsCustomSize;
                }
                return (fileInfo as FileInfo).Length;
            }
        }
        public DateTime dualLastModified {
            get
            {
                if (awsCustomLastModified != default)
                {
                    return awsCustomLastModified;
                }
                return fileInfo.LastWriteTime;
            }
        }

        private bool checkBoxSelectVisible = false;
        public bool CheckBoxSelectVisible
        {
            get => checkBoxSelectVisible;
            set
            {
                checkBoxSelectVisible = value;
                GridFileListViewPadding = value ? new Thickness(0, 8) : new Thickness(12, 8);
                OnPropertyChanged(nameof(CheckBoxSelectVisible));
            }
        }
        private bool checkBoxSelected = false;
        public bool CheckBoxSelected
        {
            get => checkBoxSelected;
            set
            {
                checkBoxSelected = value;
                OnPropertyChanged(nameof(CheckBoxSelected));
            }
        }
        Thickness gridFileListViewPadding;
        public Thickness GridFileListViewPadding
        {
            get
            {
                return gridFileListViewPadding;
            }
            set
            {
                gridFileListViewPadding = value;
                OnPropertyChanged(nameof(GridFileListViewPadding));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string name = "") =>
             PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public FileSystemInfoWithIcon(FileSystemInfo fileData, string iconPathData, int iconSizeData)
        {
            fileInfo = fileData;
            iconPath = iconPathData;
            iconSize = iconSizeData;
            gridFileListViewPadding = new Thickness(12, 8);
            ConvertFileInfoSize();
            ConvertFileLastModified();
            
        }
        public void ConvertFileInfoSize(double customSize = - 1)
        {

            if (fileInfo.GetType() == typeof(FileInfo))
            {
                double tmp;
                if (customSize == -1)
                {

                    tmp = (fileInfo as FileInfo).Length;
                }
                else
                {
                    tmp = customSize;
                }
                int i = 0;
                while (tmp > 1024)
                {
                    i++;
                    tmp /= 1024;
                    if (i == 4) break;
                }
                fileInfoSize = string.Format(" {0:0.#} {1}", tmp, fileSizeMeasure[i]);
                awsCustomSize = customSize;
                
            }
            else
            {
                fileInfoSize = "";
            }
        }

        public void ConvertFileLastModified(DateTime? customLastModified = null)
        {
            if (customLastModified != null)
            {
                DateTime custom = customLastModified ?? DateTime.Now;
                lastModified = custom.ToString("G", CultureInfo.GetCultureInfo("vi-VN"));
                awsCustomLastModified = custom;
                return;
            }
            lastModified = fileInfo.LastWriteTime.ToString("G", CultureInfo.GetCultureInfo("vi-VN"));
        }
    }
}
