﻿using Amazon.S3.Model;
using Android.OS;
using MaiFileManager.Classes.Aws;
using MaiFileManager.Services;
using Microsoft.Maui.Dispatching;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MaiFileManager.Classes;
using MaiFileManager.Pages;
using CommunityToolkit.Maui.Views;
using System.Text;
using System.Threading;

namespace MaiFileManager.Classes
{
    /// <summary>
    /// Class help the UI get the information to view the list of files
    /// </summary>
    public class FileList : INotifyPropertyChanged
    {
        public enum FileSelectOption
        {
            None,
            Cut,
            Copy,
        }

        public enum FileSortMode
        {
            NameAZ,
            NameZA,
            SizeSL,
            SizeLS,
            TypeAZ,
            TypeZA,
            DateNO,
            DateON
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<FileSystemInfoWithIcon> CurrentFileList { get; set; } = new ObservableCollection<FileSystemInfoWithIcon>();
        public FileManager CurrentDirectoryInfo { get; set; }
        public int BackDeep { get; set; } = 0;
        public static FileSelectOption OperatedOption { get; set; } = FileSelectOption.None;
        public static ObservableCollection<FileSystemInfoWithIcon> OperatedFileList { get; set; } = new ObservableCollection<FileSystemInfoWithIcon>();
        public bool IsSelectionMode { get; set; } = false;
        public int NumberOfCheked { get; set; } = 0;
        private bool isReloading = true;
        public FileSortMode SortMode = (FileSortMode)Preferences.Default.Get("Sort_by", 0);
        public ObservableCollection<FileSystemInfoWithIcon> OperatedFileListView { get; set; } = new ObservableCollection<FileSystemInfoWithIcon>();
        public ObservableCollection<FileSystemInfoWithIcon> OperatedCompletedListView { get; set; } = new ObservableCollection<FileSystemInfoWithIcon>();
        public ObservableCollection<FileSystemInfoWithIcon> OperatedErrorListView { get; set; } = new ObservableCollection<FileSystemInfoWithIcon>();
        public bool IsFavouriteMode { get; set; } = false;
        public bool IsNotFavouritePage { get; set; } = true;
        public string FavouriteFilePath { get; set; } = Path.Combine(FileSystem.Current.AppDataDirectory, "FavFile.txt");
        public string FavouriteFolderPath { get; set; } = Path.Combine(FileSystem.Current.AppDataDirectory, "FavFolder.txt");
        private double operatedPercent = 0;
        private string operatedStatusString = "";
        public Page NavigatedPage = null;
        public bool IsHomePage { get; set; } = false;
        internal StorageService awsStorageService = new StorageService(new AwsCredentials(), Preferences.Default.Get("Aws_Bucket_name", ""));
        private List<S3Object> currentS3List = new List<S3Object>();
        private List<FileSystemInfoWithIcon> currentS3ListFileWIcon = new List<FileSystemInfoWithIcon>();
        public string currentBucket = "";
        CancellationTokenSource prevToken = null;
        public string RecentFilePath { get; set; } = Path.Combine(FileSystem.Current.AppDataDirectory, "RecentFile.txt");
        public bool IsRecentMode {get; set;} = false;
        public bool IsNotRecentMode { get; set; } = true;

        public double OperatedPercent
        {
            get
            {
                return operatedPercent;
            }
            set
            {
                operatedPercent = value;
                operatedStatusString = string.Format("{0:0} %, {1} / {2}",
                                                     operatedPercent * 100,
                                                     OperatedFileList.Count - OperatedFileListView.Count,
                                                     OperatedFileList.Count);
                OnPropertyChanged(nameof(OperatedPercent));
                OnPropertyChanged(nameof(OperatedStatusString));
            }
        }
        public string OperatedStatusString
        {
            get
            {
                return operatedStatusString;
            }
        }
        public bool IsReloading
        {
            get
            {
                return isReloading;
            }
            set
            {
                isReloading = value;
                OnPropertyChanged(nameof(IsReloading));
                OnPropertyChanged(nameof(IsNotReloading));
            }
        }
        public bool IsNotReloading
        {
            get
            {
                return !isReloading;
            }
        }
        public FileList()
        {
            CurrentDirectoryInfo = new FileManager();
            IsHomePage = true;
        }
        public FileList(int type)
        {
            CurrentDirectoryInfo = new FileManager(type);
            if (type == 2)
            {
                IsFavouriteMode = true;
                IsNotFavouritePage = false;
            }
            if (type == 3)
            {
                IsRecentMode = true;
                IsNotRecentMode = false;
                IsFavouriteMode = true;
                IsNotFavouritePage = false;
            }
        }
        public FileList(string path)
        {
            CurrentDirectoryInfo = new FileManager(path);
        }

        public void OnPropertyChanged([CallerMemberName] string name = "") =>
             PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


        #region Permission
        private async Task<bool> RequestPermAsync()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                return CurrentDirectoryInfo.GetPerm();
            }
            else
            {
                var statusW = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                if (statusW != PermissionStatus.Granted)
                {
                    statusW = await Permissions.RequestAsync<Permissions.StorageWrite>();
                }
                if (statusW != PermissionStatus.Granted)
                {
                    return false;
                }
            }
            return true;
        }
        #endregion
        internal int SortFileComparisonFolderToFile(FileSystemInfoWithIcon x, FileSystemInfoWithIcon y)
        {
            return (x.fileInfo.GetType() == y.fileInfo.GetType() ? 0 : (x.fileInfo.GetType() == typeof(DirectoryInfo) ? -1 : 1));
        }
        internal int SortFileComparisonNameAZ(FileSystemInfoWithIcon x, FileSystemInfoWithIcon y)
        {
            int tmp = SortFileComparisonFolderToFile(x, y);
            if (tmp != 0) return tmp;

            return string.Compare(x.fileInfo.Name, y.fileInfo.Name, StringComparison.OrdinalIgnoreCase);
        }

        internal int SortFileComparisonNameZA(FileSystemInfoWithIcon x, FileSystemInfoWithIcon y)
        {
            int tmp = SortFileComparisonFolderToFile(x, y);
            if (tmp != 0) return tmp;

            return string.Compare(y.fileInfo.Name, x.fileInfo.Name, StringComparison.OrdinalIgnoreCase);
        }

        internal int SortFileComparisonSizeSL(FileSystemInfoWithIcon x, FileSystemInfoWithIcon y)
        {
            int tmp = SortFileComparisonFolderToFile(x, y);
            if (tmp != 0) return tmp;

            double sizeX = 0, sizeY = 0;
            if (x.fileInfo.GetType() == typeof(FileInfo))
            {
                sizeX = x.dualFileSize;
            }
            if (y.fileInfo.GetType() == typeof(FileInfo))
            {
                sizeY = y.dualFileSize;
            }
            return (sizeX < sizeY ? -1 : (sizeX > sizeY ? 1 : 0));
        }
        internal int SortFileComparisonSizeLS(FileSystemInfoWithIcon x, FileSystemInfoWithIcon y)
        {
            int tmp = SortFileComparisonFolderToFile(x, y);
            if (tmp != 0) return tmp;

            double sizeX = 0, sizeY = 0;
            if (x.fileInfo.GetType() == typeof(FileInfo))
            {
                sizeX = x.dualFileSize;
            }
            if (y.fileInfo.GetType() == typeof(FileInfo))
            {
                sizeY = y.dualFileSize;
            }
            return (sizeX > sizeY ? -1 : (sizeX < sizeY ? 1 : 0));
        }
        internal int SortFileComparisonTypeAZ(FileSystemInfoWithIcon x, FileSystemInfoWithIcon y)
        {
            int tmp = SortFileComparisonFolderToFile(x, y);
            if (tmp != 0) return tmp;

            return string.Compare(x.fileInfo.Extension, y.fileInfo.Extension, StringComparison.OrdinalIgnoreCase);
        }
        internal int SortFileComparisonTypeZA(FileSystemInfoWithIcon x, FileSystemInfoWithIcon y)
        {
            int tmp = SortFileComparisonFolderToFile(x, y);
            if (tmp != 0) return tmp;

            return string.Compare(y.fileInfo.Extension, x.fileInfo.Extension, StringComparison.OrdinalIgnoreCase);
        }
        internal int SortFileComparisonDateNO(FileSystemInfoWithIcon x, FileSystemInfoWithIcon y)
        {
            int tmp = SortFileComparisonFolderToFile(x, y);
            if (tmp != 0) return tmp;

            return DateTime.Compare(y.dualLastModified, x.dualLastModified);
        }
        internal int SortFileComparisonDateON(FileSystemInfoWithIcon x, FileSystemInfoWithIcon y)
        {
            int tmp = SortFileComparisonFolderToFile(x, y);
            if (tmp != 0) return tmp;

            return DateTime.Compare(x.dualLastModified, y.dualLastModified);
        }
        internal List<FileSystemInfoWithIcon> SortFileMode(List<FileSystemInfoWithIcon> fsi)
        {

                Comparison<FileSystemInfoWithIcon> compare = new Comparison<FileSystemInfoWithIcon>(SortFileComparisonNameAZ);

                switch (SortMode)
                {
                    case FileSortMode.NameAZ:
                        compare = new Comparison<FileSystemInfoWithIcon>(SortFileComparisonNameAZ);
                        break;
                    case FileSortMode.NameZA:
                        compare = new Comparison<FileSystemInfoWithIcon>(SortFileComparisonNameZA);
                        break;
                    case FileSortMode.SizeSL:
                        compare = new Comparison<FileSystemInfoWithIcon>(SortFileComparisonSizeSL);
                        break;
                    case FileSortMode.SizeLS:
                        compare = new Comparison<FileSystemInfoWithIcon>(SortFileComparisonSizeLS);
                        break;
                    case FileSortMode.TypeAZ:
                        compare = new Comparison<FileSystemInfoWithIcon>(SortFileComparisonTypeAZ);
                        break;
                    case FileSortMode.TypeZA:
                        compare = new Comparison<FileSystemInfoWithIcon>(SortFileComparisonTypeZA);
                        break;
                    case FileSortMode.DateNO:
                        compare = new Comparison<FileSystemInfoWithIcon>(SortFileComparisonDateNO);
                        break;
                    case FileSortMode.DateON:
                        compare = new Comparison<FileSystemInfoWithIcon>(SortFileComparisonDateON);
                        break;
                }
                fsi.Sort(compare);
                return fsi;
        }
        internal async Task InitialLoadAsync()
        {
            bool accepted = await RequestPermAsync();
            if (!accepted)
            {
                await Shell.Current.DisplayAlert("Permission not granted", "Need storage permission to use the app", "OK");
                Application.Current.Quit();
            }
            else
            {
                await Task.Run(async () => await UpdateFileListAsync());

            }

            //await Task.Run(UpdateFileListAsync);
        }

        internal void UpdateBackDeep(int val)
        {
            BackDeep += val;
            if (BackDeep < 0)
            {
                BackDeep = 0;
            }
        }


        internal async Task GenerateFileViewAsync(StorageService.GenerateFileViewMode mode = StorageService.GenerateFileViewMode.View, 
                                                    CancellationToken cancellationToken = default, 
                                                    string searchValue = "",
                                                    string currentDirectory = null)
        {
            currentBucket = awsStorageService.bucketName;
            awsStorageService = new StorageService(new AwsCredentials(), Preferences.Default.Get("Aws_Bucket_name", ""));
            DirectoryInfo viewDir = new DirectoryInfo(MaiConstants.HomePath); 
            cancellationToken.ThrowIfCancellationRequested();
            if (!viewDir.Exists)
            {
                viewDir.Create();
            }

            System.Diagnostics.Debug.WriteLine(MaiConstants.HomePath);

            cancellationToken.ThrowIfCancellationRequested();

            string awsPath = (currentDirectory ?? CurrentDirectoryInfo.CurrentDir).Remove(0,
                MaiConstants.HomePath.Length);
            if (awsPath.StartsWith("/")) { awsPath = awsPath.Remove(0, 1); }
            List<S3Object> s3Objects = new List<S3Object>(); 
            cancellationToken.ThrowIfCancellationRequested();
            s3Objects = await awsStorageService.ListAllFileInPath(awsPath, mode, searchValue, cancellationToken);
            currentS3List = s3Objects;

            DirectoryInfo currentDir = new DirectoryInfo(currentDirectory ?? CurrentDirectoryInfo.CurrentDir);
            DirectoryInfo parentDir = new DirectoryInfo(FileSystem.Current.CacheDirectory);

            System.Diagnostics.Debug.WriteLine(currentDir);
            System.Diagnostics.Debug.WriteLine(parentDir);

            bool isParent = currentDir.FullName == parentDir.FullName;
            while (currentDir.Parent != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (currentDir.Parent.FullName == parentDir.FullName)
                {
                    isParent = true;
                    break;
                }
                currentDir = currentDir.Parent;
            }

            if (isParent)
            {
                System.Diagnostics.Debug.WriteLine("IsParent");
                currentDir = new DirectoryInfo(currentDirectory ?? CurrentDirectoryInfo.CurrentDir);

                await Task.Run(() =>
                {
                    foreach (FileInfo file in currentDir.GetFiles())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        file.Delete();
                    }
                    foreach (DirectoryInfo dir in currentDir.GetDirectories())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        dir.Delete(true);
                    }
                });

                await Task.Run(() =>
                {
                    currentS3ListFileWIcon.Clear();
                    foreach (S3Object s3Object in s3Objects)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string path = Path.Combine(MaiConstants.HomePath, s3Object.Key);
                        System.Diagnostics.Debug.WriteLine("createing path: ", path);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (s3Object.Size == 0 && s3Object.Key.EndsWith('/'))
                        {
                            Directory.CreateDirectory(path);
                            currentS3ListFileWIcon.Add(new FileSystemInfoWithIcon(new DirectoryInfo(path), "folder.png", 45));
                            currentS3ListFileWIcon[currentS3ListFileWIcon.Count - 1].ConvertFileInfoSize(s3Object.Size);
                            currentS3ListFileWIcon[currentS3ListFileWIcon.Count - 1].bucketName = currentBucket;
                            currentS3ListFileWIcon[currentS3ListFileWIcon.Count - 1].ConvertFileLastModified(s3Object.LastModified);
                        }
                        else
                        {
                            File.Create(path);
                            currentS3ListFileWIcon.Add(new FileSystemInfoWithIcon(new FileInfo(path), MaiIcon.GetIcon(Path.GetExtension(path)), 40));
                            currentS3ListFileWIcon[currentS3ListFileWIcon.Count - 1].ConvertFileInfoSize(s3Object.Size);
                            currentS3ListFileWIcon[currentS3ListFileWIcon.Count - 1].bucketName = currentBucket;
                            currentS3ListFileWIcon[currentS3ListFileWIcon.Count - 1].ConvertFileLastModified(s3Object.LastModified);
                        }
                    }
                });
            }

        }
        internal int S3SortFileComparisonFolderToFile(S3Object x, S3Object y)
        {
            return (x.Key.EndsWith("/") == y.Key.EndsWith("/") ? 0 : (x.Key.EndsWith("/") ? -1 : 1));
        }
        internal int S3SortFileComparisonNameAZ(S3Object x, S3Object y)
        {
            int tmp = S3SortFileComparisonFolderToFile(x, y);
            if (tmp != 0) return tmp;

            return string.Compare(x.Key, y.Key, StringComparison.OrdinalIgnoreCase);
        }

        //internal void LoadSizeAndDateForS3(List<FileSystemInfoWithIcon> listFile, CancellationToken cancellationToken = default)
        //{
        //    currentS3List.Sort(S3SortFileComparisonNameAZ);
        //    listFile.Sort(SortFileComparisonNameAZ);
        //    if (currentS3List.Count != listFile.Count) return;
        //    for (int i = 0; i < listFile.Count; i++)
        //    {
        //        cancellationToken.ThrowIfCancellationRequested();
        //        listFile[i].ConvertFileInfoSize(currentS3List[i].Size);
        //        listFile[i].ConvertFileLastModified(currentS3List[i].LastModified);
        //    }
        //}

        internal async Task UpdateFileListAsync()
        {
            if (prevToken != null)
            {
                prevToken.Cancel();
            }
            CancellationTokenSource loadFileToken = new CancellationTokenSource();
            prevToken = loadFileToken;
            CancellationToken cancellationToken = loadFileToken.Token;

            IsReloading = true;
            if (BackDeep == 0 && IsRecentMode)
            {
                await Task.Run(async () =>
                {

                    List<FileSystemInfoWithIcon> tempFileList = new List<FileSystemInfoWithIcon>();
                    await GenerateFileViewAsync(StorageService.GenerateFileViewMode.Search, cancellationToken, currentDirectory: MaiConstants.HomePath);
                    CurrentFileList.Clear();
                    //file
                    if (File.Exists(RecentFilePath))
                    {
                        List<string> favList = (await File.ReadAllLinesAsync(RecentFilePath)).ToList();
                        for (int i = favList.Count - 1; i >= 0; i--)
                        {
                            if (favList[i].Split("[+]").Length < 2 || favList[i].Split("[+]")[0] != currentBucket)
                            {
                                favList.RemoveAt(i);
                            }
                            else
                            {
                                favList[i] = favList[i].Split("[+]")[1];
                            }
                        }
                        int cnt = 0;
                        for (int i = favList.Count - 1; i >= 0; i--)
                        {
                            await Task.Run(async () =>
                            {
                                var fav = favList[i];
                                if (File.Exists(fav) && (favList.Count - i + 1 < 30))
                                {
                                    FileSystemInfo fileInfo = new FileInfo(fav);
                                    tempFileList.Add(currentS3ListFileWIcon.Find(e => e.fileInfo.FullName == fileInfo.FullName));
                                }
                                else
                                {
                                    await AddOrRemoveFavouriteAsync(0, true, null, fav, 0, RecentFilePath);
                                }


                            });
                        }
                    }

                    foreach (FileSystemInfoWithIcon f in tempFileList)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Run(() =>
                        {
                            CurrentFileList.Add(f);
                        });
                    }
                });
            }
            else if (BackDeep == 0 && IsFavouriteMode)
            {
                await Task.Run(async () =>
                {

                    List<FileSystemInfoWithIcon> tempFileList = new List<FileSystemInfoWithIcon>();
                    await GenerateFileViewAsync(StorageService.GenerateFileViewMode.Search, cancellationToken, currentDirectory: MaiConstants.HomePath);
                    CurrentFileList.Clear();
                    //file
                    if (File.Exists(FavouriteFilePath))
                    {
                        List<string> favList = (await File.ReadAllLinesAsync(FavouriteFilePath)).ToList();
                        for (int i = favList.Count - 1; i >= 0; i--)
                        {
                            System.Diagnostics.Debug.WriteLine(currentBucket);
                            if (favList[i].Split("[+]").Length < 2 || favList[i].Split("[+]")[0] != currentBucket)
                            {
                                favList.RemoveAt(i);
                            }
                            else
                            {
                                favList[i] = favList[i].Split("[+]")[1];
                            }
                        }
                        foreach (string fav in favList)
                        {
                            await Task.Run(async () =>
                            {
                                if (File.Exists(fav))
                                {
                                    FileSystemInfo fileInfo = new FileInfo(fav);
                                    tempFileList.Add(currentS3ListFileWIcon.Find(e => e.fileInfo.FullName == fileInfo.FullName));
                                }
                                else
                                {
                                    await AddOrRemoveFavouriteAsync(0, true, null, fav, 0);
                                }

                            });
                        }
                    }
                    //folder
                    if (File.Exists(FavouriteFolderPath))
                    {
                        List<string> favList = (await File.ReadAllLinesAsync(FavouriteFolderPath)).ToList();
                        for (int i = favList.Count - 1; i >= 0; i--)
                        {
                            if (favList[i].Split("[+]").Length < 2 || favList[i].Split("[+]")[0] != currentBucket)
                            {
                                favList.RemoveAt(i);
                            }
                            else
                            {
                                favList[i] = favList[i].Split("[+]")[1];
                            }
                        }
                        foreach (string fav in favList)
                        {
                            await Task.Run(async () =>
                            {
                                if (Directory.Exists(fav))
                                {
                                    FileSystemInfo fileInfo = new DirectoryInfo(fav);
                                    tempFileList.Add(currentS3ListFileWIcon.Find(e => e.fileInfo.FullName == fileInfo.FullName));
                                }
                                else
                                {
                                    await AddOrRemoveFavouriteAsync(0, true, null, fav, 1);
                                }
                            });
                        }
                    }
                    tempFileList = SortFileMode(tempFileList);

                    foreach (FileSystemInfoWithIcon f in tempFileList)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        CurrentFileList.Add(f);
                    }

                    if (IsSelectionMode)
                    {
                        foreach (FileSystemInfoWithIcon f in CurrentFileList)
                        {
                            f.CheckBoxSelectVisible = true;
                        }
                    }
                    NumberOfCheked = 0;
                });
            }
            else
            {
                try
                {
                    await Task.Run(async () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        CurrentFileList.Clear();

                        List<FileSystemInfoWithIcon> tempFileList = new List<FileSystemInfoWithIcon>();
                        if (IsHomePage)
                        {
                            await GenerateFileViewAsync(cancellationToken: cancellationToken);
                            await Task.Run(async () =>
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                tempFileList = new List<FileSystemInfoWithIcon>(currentS3ListFileWIcon);

                            });
                        }
                        else
                        {
                            foreach (FileSystemInfo info in CurrentDirectoryInfo.GetListFile().ToList())
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                await Task.Run(() =>
                                {
                                    if (string.Equals(info.FullName, "/storage/emulated/0/android", StringComparison.CurrentCultureIgnoreCase)
                                        && Build.VERSION.SdkInt >= BuildVersionCodes.R)
                                    {

                                    }
                                    else if (info.GetType() == typeof(FileInfo))
                                    {
                                        tempFileList.Add(new FileSystemInfoWithIcon(info, MaiIcon.GetIcon(info.Extension), 40));
                                    }
                                    else if (info.GetType() == typeof(DirectoryInfo))
                                    {
                                        tempFileList.Add(new FileSystemInfoWithIcon(info, "folder.png", 45));
                                    }
                                });
                            }
                        }

                        tempFileList = SortFileMode(tempFileList);

                        foreach (FileSystemInfoWithIcon f in tempFileList)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            await Task.Run(() =>
                            {
                                CurrentFileList.Add(f);
                            });
                        }

                        if (IsSelectionMode)
                        {
                            foreach (FileSystemInfoWithIcon f in CurrentFileList)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                f.CheckBoxSelectVisible = true;
                            }
                        }
                        NumberOfCheked = 0;

                    });
                }
                catch (System.OperationCanceledException ex)
                {
                    System.Diagnostics.Debug.WriteLine("Canceled loading file");
                    return;
                }
                catch (Android.OS.OperationCanceledException ex)
                {
                    System.Diagnostics.Debug.WriteLine("Canceled loading file");
                    return;
                }
            }


            IsReloading = false;
        }

        internal async Task<int> PathSelectionAsync(object sender, SelectionChangedEventArgs e)
        {
            if (sender is null) return -1;
            if (e.CurrentSelection.Count == 0) return -1;
            FileSystemInfoWithIcon selectedWIcon = e.CurrentSelection.FirstOrDefault() as FileSystemInfoWithIcon;
            FileSystemInfo selected = selectedWIcon.fileInfo;
            if (selected.GetType() == typeof(FileInfo))
            {
                if (IsHomePage || IsFavouriteMode)
                {
                    Page tmp = Shell.Current.CurrentPage;
                    CancellationTokenSource source = new CancellationTokenSource();
                    FileInfo file = (FileInfo)selected;
                    StringBuilder path = new StringBuilder();

                    await Task.Run(() => File.Delete(file.FullName));

                    var popup = new SpinnerPopup(source, awsStorageService, file, ref path);
                    await tmp.Dispatcher.DispatchAsync(async () =>
                        await tmp.ShowPopupAsync(popup));

                    

                    if (path.ToString() != "")
                    {
                        selected = new FileInfo(path.ToString());
                        if (selected.Exists) await Launcher.OpenAsync(new OpenFileRequest("Open File", new ReadOnlyFile(selected.FullName)));
                    }
                }
                else
                {
                    if (selected.Exists) await Launcher.OpenAsync(new OpenFileRequest("Open File", new ReadOnlyFile(selected.FullName)));
                }
                await AddOrRemoveFavouriteAsync(1, isReloadOff: true , paths: selected.FullName, type: 0, favFilePath: RecentFilePath);
                return 0;
            }
            else if (selected.GetType() == typeof(DirectoryInfo))
            {
                if (selected == null)
                    return -1;
                int deep = 0;
                string fullPathName = selected.FullName.EndsWith('/') ? 
                    selected.FullName.Remove(selected.FullName.Length - 1) : selected.FullName;

                string tmp = fullPathName;
                if (BackDeep == 0 && IsFavouriteMode)
                {
                    deep++;
                    IsNotFavouritePage = true;
                    OnPropertyChanged(nameof(IsNotFavouritePage));
                }
                else
                {
                    while (tmp != CurrentDirectoryInfo.CurrentDir)
                    {
                        tmp = Path.GetDirectoryName(tmp);
                        deep++;
                    }
                }
                await Task.Run(() =>
                {
                    CurrentDirectoryInfo.UpdateDir(fullPathName);
                    UpdateBackDeep(deep);
                });
                await Task.Run(UpdateFileListAsync);
                return 1;
            }
            return -1;
        }


        internal async Task BackAsync(object sender, EventArgs e)
        {
            UpdateBackDeep(-1);
            if (BackDeep == 0 && IsFavouriteMode)
            {
                IsNotFavouritePage = false;
                OnPropertyChanged(nameof(IsNotFavouritePage));
                CurrentDirectoryInfo.CurrentDir = "Favourite";
            }
            else if (IsFavouriteMode)
            {
                IsNotFavouritePage = true;
                OnPropertyChanged(nameof(IsNotFavouritePage));
                CurrentDirectoryInfo.BackDir();
            }
            else
            {
                CurrentDirectoryInfo.BackDir();
            }
            await UpdateFileListAsync();
        }
        async void CopyDirectory(DirectoryInfo sourceDir, string destinationPath)
        {
            if (!sourceDir.Exists)
            {
                return;
            }

            List<DirectoryInfo> sourceDirTemp = new List<DirectoryInfo>();
            foreach (DirectoryInfo directory in sourceDir.GetDirectories())
            {
                sourceDirTemp.Add(directory);
            }

            string newSourcePath = Path.Combine(destinationPath, sourceDir.Name);
            DirectoryInfo newSourceDir = Directory.CreateDirectory(newSourcePath);
            foreach (FileInfo file in sourceDir.GetFiles())
            {
                string targetFilePath = Path.Combine(newSourcePath, file.Name);
                if (File.Exists(targetFilePath))
                {
                    if (await ArletForExisted(targetFilePath, file.Name))
                    {
                        int num = 0;
                        while (File.Exists(string.Format("{0}{1}", targetFilePath, num)))
                        {
                            num++;
                        }
                        file.CopyTo(string.Format("{0}{1}", targetFilePath, num));
                        File.Delete(targetFilePath);
                        File.Move(string.Format("{0}{1}", targetFilePath, num), targetFilePath);
                    }
                }
                else
                {
                    file.CopyTo(targetFilePath);
                }
            }

            foreach (DirectoryInfo directory in sourceDirTemp)
            {
                CopyDirectory(directory, newSourcePath);
            }

        }
        bool IsDirectoryContainDirectory(string dir1, string dir2)
        {
            while (dir2 != null)
            {
                if (dir2 == dir1) { return true; }
                DirectoryInfo tmp = Directory.GetParent(dir2);
                dir2 = (tmp != null) ? tmp.FullName : null;
            }
            return false;
        }
        async Task<bool> ArletForExisted(string dir, string dirName)
        {
            bool result = false;
            if (Directory.Exists(dir))
            {
                Page tmp;
                if (NavigatedPage == null)
                {
                    tmp = Shell.Current.CurrentPage;
                }
                else
                {
                    tmp = NavigatedPage;
                }
                await tmp.Dispatcher.DispatchAsync(async () =>
                {
                    await tmp.DisplayAlert("Existed", "Folder "
                                                                + dirName
                                                                + "already exists in this directory\n", "OK");
                    result = false;
                });
            }
            else if (File.Exists(dir))
            {
                Page tmp;
                if (NavigatedPage == null)
                {
                    tmp = Shell.Current.CurrentPage;
                }
                else
                {
                    tmp = NavigatedPage;
                }
                await tmp.Dispatcher.DispatchAsync(async () =>
                {
                    result = await tmp.DisplayAlert("Existed", "File "
                                                                + dirName
                                                                + "already exists in this directory\n"
                                                                + "Write new file or keep old file?",
                                                                "Write new", "Keep old");
                });

            }
            return result;
        }
        internal void ModifyMode(FileSelectOption mode)
        {
            OperatedOption = mode;
            OperatedFileList.Clear();
            foreach (FileSystemInfoWithIcon f in CurrentFileList)
            {
                if (f.CheckBoxSelected)
                {
                    OperatedFileList.Add(f);
                }
            }
        }

        internal async Task<int> DeleteModeAsync()
        {
            OperatedFileList.Clear();
            OperatedFileListView.Clear();
            OperatedCompletedListView.Clear();
            OperatedErrorListView.Clear();
            int noFIle = 1;
            foreach (FileSystemInfoWithIcon f in CurrentFileList)
            {
                if (f.CheckBoxSelected)
                {
                    noFIle = 0;
                    OperatedFileList.Add(f);
                    OperatedFileListView.Add(f);
                }
            }
            OperatedPercent = 0;
            int tmpInit = OperatedFileListView.Count;
            int tmpDone = 0;
            if (noFIle == 1) return 0;
            foreach (FileSystemInfoWithIcon f in CurrentFileList)
            {
                if (f.CheckBoxSelected)
                {
                    if (IsHomePage)
                    {
                        string sourceFilePath = f.fileInfo.FullName.Remove(0, MaiConstants.HomePath.Length + 1);
                        if (sourceFilePath.StartsWith("/")) { sourceFilePath = sourceFilePath.Remove(0, 1); }

                        var deleteResult = await awsStorageService.DeleteObjectAsync(sourceFilePath, f.bucketName);
                        if (!deleteResult)
                        {
                            tmpDone++;
                            OperatedFileListView.Remove(f);
                            OperatedErrorListView.Add(f);
                            OperatedPercent = (double)tmpDone / tmpInit;
                            continue;
                        }
                    }
                    else
                    {
                        if (f.fileInfo.GetType() == typeof(FileInfo))
                        {
                            (f.fileInfo as FileInfo).Delete();
                        }
                        else if (f.fileInfo.GetType() == typeof(DirectoryInfo))
                        {
                            (f.fileInfo as DirectoryInfo).Delete(true);
                        }
                    }
                    tmpDone++;
                    OperatedFileListView.Remove(f);
                    OperatedCompletedListView.Add(f);
                    OperatedPercent = (double)tmpDone / tmpInit;
                }
            }
            await UpdateFileListAsync();
            OperatedFileList.Clear();
            return 1;
        }
        internal async Task RenameModeAsync(string path, string newName)
        {
            bool isInFavourite = await IsInFavouriteAsync(path);
            if (Directory.Exists(path))
            {
                string newPath = Path.Combine(new DirectoryInfo(path).Parent.FullName, newName) + "/";
                if (Directory.Exists(newPath) || File.Exists(newPath))
                {
                    Page tmp;
                    if (NavigatedPage == null)
                    {
                        tmp = Shell.Current.CurrentPage;
                    }
                    else
                    {
                        tmp = NavigatedPage;
                    }
                    //maybe dont need
                    await tmp.Dispatcher.DispatchAsync(async () =>
                    {
                        await tmp.DisplayAlert("Duplicated", "Duplicate file/folder name, please choose another name", "OK");
                    });
                    return;
                }
                
                if (IsHomePage)
                {
                    FileSystemInfo fsi = new DirectoryInfo(path);
                    FileSystemInfoWithIcon f = new FileSystemInfoWithIcon(fsi, "folder.png", 45);
                    f.bucketName = currentBucket;
                    if (!await PasteFileAndFolderForS3(f, newPath, FileSelectOption.Cut))
                    {
                        return;
                    }
                }
                else
                {
                    Directory.Move(path, newPath);
                }

                if (isInFavourite)
                {
                    FileSystemInfo f = new DirectoryInfo(path);
                    await AddOrRemoveFavouriteAsync(0, true, f);
                }

                if (isInFavourite)
                {
                    FileSystemInfo f = new DirectoryInfo(newPath);
                    await AddOrRemoveFavouriteAsync(1, true, f);
                }
            }
            else if (File.Exists(path))
            {
                string newPath = Path.Combine(Directory.GetParent(path).FullName, newName) + Path.GetExtension(path);
                if (Directory.Exists(newPath) || File.Exists(newPath))
                {
                    Page tmp;
                    if (NavigatedPage == null)
                    {
                        tmp = Shell.Current.CurrentPage;
                    }
                    else
                    {
                        tmp = NavigatedPage;
                    }
                    //maybe dont need
                    await tmp.Dispatcher.DispatchAsync(async () =>
                    {
                        await tmp.DisplayAlert("Duplicated", "Duplicate file/folder name, please choose another name", "OK");
                    });
                    return;
                }
                if (IsHomePage)
                {
                    FileSystemInfo fsi = new FileInfo(path);
                    FileSystemInfoWithIcon f = new FileSystemInfoWithIcon(fsi, "folder.png", 45);
                    f.bucketName = currentBucket;
                    if (!await PasteFileAndFolderForS3(f, newPath, FileSelectOption.Cut))
                    {
                        return;
                    }
                }
                else
                {
                    File.Move(path, newPath);
                }
                if (isInFavourite)
                {
                    FileSystemInfo f = new FileInfo(path);
                    await AddOrRemoveFavouriteAsync(0, true, f);
                }
                if (isInFavourite)
                {
                    FileSystemInfo f = new FileInfo(newPath);
                    await AddOrRemoveFavouriteAsync(1, true, f);
                }
            }
        }

        internal async Task<bool> PasteFileAndFolderForS3(FileSystemInfoWithIcon f, string targetFilePath, FileSelectOption option)
        {

            string sourceFilePath = f.fileInfo.FullName.Remove(0, MaiConstants.HomePath.Length);
            if (sourceFilePath.StartsWith("/")) { sourceFilePath = sourceFilePath.Remove(0, 1); }


            targetFilePath = targetFilePath.Remove(0, MaiConstants.HomePath.Length);
            if (targetFilePath.StartsWith("/")) { targetFilePath = targetFilePath.Remove(0, 1); }


            var copyResult = await awsStorageService.CopyingObjectAsync(sourceFilePath, targetFilePath, f.bucketName, currentBucket);
            if (copyResult == null) return false;
            
            if (option.Equals(FileSelectOption.Cut))
            {
                var deleteResult = await awsStorageService.DeleteObjectAsync(sourceFilePath, f.bucketName);
                return deleteResult;
            }
            return true;
        }

        internal async Task PasteModeAsync()
        {
            OperatedFileListView.Clear();
            OperatedCompletedListView.Clear();
            OperatedErrorListView.Clear();
            foreach (FileSystemInfoWithIcon f in OperatedFileList)
            {
                OperatedFileListView.Add(f);
            }
            OperatedPercent = 0;
            foreach (FileSystemInfoWithIcon f in OperatedFileList)
            {
                switch (OperatedOption)
                {
                    case FileSelectOption.Cut:
                        {
                            bool isInFavourite = await IsInFavouriteAsync(f.fileInfo.FullName);
                            string fileFullName = "";
                            if (isInFavourite)
                            {
                                fileFullName = f.fileInfo.FullName;
                            }
                            bool canceled = false;
                            if (f.fileInfo.GetType() == typeof(FileInfo))
                            {
                                string targetFilePath = Path.Combine(CurrentDirectoryInfo.CurrentDir, f.fileInfo.Name);
                                if (File.Exists(targetFilePath) || Directory.Exists(targetFilePath))
                                {
                                    if (await ArletForExisted(targetFilePath, f.fileInfo.Name))
                                    {
                                        int num = 0;
                                        while (File.Exists(string.Format("{0}{1}", targetFilePath, num)))
                                        {
                                            num++;
                                        }
                                        if (IsHomePage)
                                        {

                                            if (!await PasteFileAndFolderForS3(f, string.Format("{0}{1}", targetFilePath, num), OperatedOption))
                                            {
                                                canceled = true;
                                                OperatedFileListView.Remove(f);
                                                OperatedErrorListView.Add(f);
                                                OperatedPercent = (double)(OperatedFileList.Count - OperatedFileListView.Count) / OperatedFileList.Count;
                                                continue;
                                            }

                                        }
                                        else
                                        {
                                            (f.fileInfo as FileInfo).MoveTo(string.Format("{0}{1}", targetFilePath, num));
                                            File.Delete(targetFilePath);
                                            File.Move(string.Format("{0}{1}", targetFilePath, num), targetFilePath);
                                        }
                                    }
                                    else
                                    {
                                        canceled = true;
                                        OperatedFileListView.Remove(f);
                                        OperatedErrorListView.Add(f);
                                        OperatedPercent = (double)(OperatedFileList.Count - OperatedFileListView.Count) / OperatedFileList.Count;
                                        continue;
                                    }
                                }
                                else
                                {
                                    if (IsHomePage)
                                    {
                                        if (!await PasteFileAndFolderForS3(f, targetFilePath, OperatedOption))
                                        {
                                            canceled = true;
                                            OperatedFileListView.Remove(f);
                                            OperatedErrorListView.Add(f);
                                            OperatedPercent = (double)(OperatedFileList.Count - OperatedFileListView.Count) / OperatedFileList.Count;
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        (f.fileInfo as FileInfo).MoveTo(targetFilePath);
                                    }
                                }
                                if (!canceled && isInFavourite)
                                {
                                    await AddOrRemoveFavouriteAsync(0, true, null, fileFullName, 0);
                                    await AddOrRemoveFavouriteAsync(1, true, null, targetFilePath, 0);
                                }
                            }
                            else if (f.fileInfo.GetType() == typeof(DirectoryInfo))
                            {
                                string targetFilePath = Path.Combine(CurrentDirectoryInfo.CurrentDir, f.fileInfo.Name) + "/";
                                if (IsDirectoryContainDirectory(f.fileInfo.FullName, CurrentDirectoryInfo.CurrentDir))
                                {
                                    Page tmp;
                                    if (NavigatedPage == null)
                                    {
                                        tmp = Shell.Current.CurrentPage;
                                    }
                                    else
                                    {
                                        tmp = NavigatedPage;
                                    }
                                    await tmp.Dispatcher.DispatchAsync(async () => { await tmp.DisplayAlert("Error", f.fileInfo.Name + "\nCannot move to itself", "OK"); });
                                    canceled = true;
                                    OperatedFileListView.Remove(f);
                                    OperatedErrorListView.Add(f);
                                    OperatedPercent = (double)(OperatedFileList.Count - OperatedFileListView.Count) / OperatedFileList.Count;
                                    continue;
                                }
                                if (Directory.Exists(targetFilePath) || File.Exists(targetFilePath))
                                {
                                    Page tmp;
                                    if (NavigatedPage == null)
                                    {
                                        tmp = Shell.Current.CurrentPage;
                                    }
                                    else
                                    {
                                        tmp = NavigatedPage;
                                    }
                                    await tmp.Dispatcher.DispatchAsync(async () => { await tmp.DisplayAlert("Error", f.fileInfo.Name + "\nDirectory/File with same name already exists", "OK"); });
                                    canceled = true;
                                    OperatedFileListView.Remove(f);
                                    OperatedErrorListView.Add(f);
                                    OperatedPercent = (double)(OperatedFileList.Count - OperatedFileListView.Count) / OperatedFileList.Count;
                                    continue;
                                }
                                if (IsHomePage)
                                {
                                    if (!await PasteFileAndFolderForS3(f, targetFilePath, OperatedOption))
                                    {
                                        canceled = true;
                                        OperatedFileListView.Remove(f);
                                        OperatedErrorListView.Add(f);
                                        OperatedPercent = (double)(OperatedFileList.Count - OperatedFileListView.Count) / OperatedFileList.Count;
                                        continue;
                                    }
                                }
                                else
                                {
                                    (f.fileInfo as DirectoryInfo).MoveTo(targetFilePath);
                                }
                                if (!canceled && isInFavourite)
                                {
                                    await AddOrRemoveFavouriteAsync(0, true, null, fileFullName, 1);
                                    await AddOrRemoveFavouriteAsync(1, true, null, targetFilePath, 1);
                                }
                            }
                            break;
                        }
                    case FileSelectOption.Copy:
                        {
                            if (f.fileInfo.GetType() == typeof(FileInfo))
                            {
                                string targetFilePath = Path.Combine(CurrentDirectoryInfo.CurrentDir, f.fileInfo.Name);
                                if (File.Exists(targetFilePath) || Directory.Exists(targetFilePath))
                                {
                                    if (await ArletForExisted(targetFilePath, f.fileInfo.Name))
                                    {
                                        int num = 0;
                                        while (File.Exists(string.Format("{0}{1}", targetFilePath, num)))
                                        {
                                            num++;
                                        }
                                        if (IsHomePage)
                                        {
                                            if (!await PasteFileAndFolderForS3(f, string.Format("{0}{1}", targetFilePath, num), OperatedOption))
                                            {
                                                OperatedFileListView.Remove(f);
                                                OperatedErrorListView.Add(f);
                                                OperatedPercent = (double)(OperatedFileList.Count - OperatedFileListView.Count) / OperatedFileList.Count;
                                                continue;
                                            }
                                        }
                                        else
                                        {
                                            (f.fileInfo as FileInfo).CopyTo(string.Format("{0}{1}", targetFilePath, num));
                                            File.Delete(targetFilePath);
                                            File.Move(string.Format("{0}{1}", targetFilePath, num), targetFilePath);
                                        }
                                    }
                                    else
                                    {
                                        OperatedFileListView.Remove(f);
                                        OperatedErrorListView.Add(f);
                                        OperatedPercent = (double)(OperatedFileList.Count - OperatedFileListView.Count) / OperatedFileList.Count;
                                        continue;
                                    }
                                }
                                else
                                {
                                    if (IsHomePage)
                                    {
                                        if (!await PasteFileAndFolderForS3(f, targetFilePath, OperatedOption))
                                        {
                                            OperatedFileListView.Remove(f);
                                            OperatedErrorListView.Add(f);
                                            OperatedPercent = (double)(OperatedFileList.Count - OperatedFileListView.Count) / OperatedFileList.Count;
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        (f.fileInfo as FileInfo).CopyTo(targetFilePath);
                                    }
                                }

                            }
                            else if (f.fileInfo.GetType() == typeof(DirectoryInfo))
                            {
                                if (IsDirectoryContainDirectory(f.fileInfo.FullName, CurrentDirectoryInfo.CurrentDir))
                                {
                                    Page tmp;
                                    if (NavigatedPage == null)
                                    {
                                        tmp = Shell.Current.CurrentPage;
                                    }
                                    else
                                    {
                                        tmp = NavigatedPage;
                                    }
                                    await tmp.Dispatcher.DispatchAsync(async () => { await tmp.DisplayAlert("Error", f.fileInfo.Name + "\nCannot copy to itself", "OK"); });
                                    OperatedFileListView.Remove(f);
                                    OperatedErrorListView.Add(f);
                                    OperatedPercent = (double)(OperatedFileList.Count - OperatedFileListView.Count) / OperatedFileList.Count;
                                    continue;
                                }
                                else
                                {
                                    string targetFilePath = Path.Combine(CurrentDirectoryInfo.CurrentDir, f.fileInfo.Name) + "/";
                                    if (File.Exists(targetFilePath))
                                    {
                                        Page tmp;
                                        if (NavigatedPage == null)
                                        {
                                            tmp = Shell.Current.CurrentPage;
                                        }
                                        else
                                        {
                                            tmp = NavigatedPage;
                                        }
                                        await tmp.Dispatcher.DispatchAsync(async () => { await tmp.DisplayAlert("Error", f.fileInfo.Name + "\nDirectory/File with same name already exists", "OK"); });
                                        OperatedFileListView.Remove(f);
                                        OperatedErrorListView.Add(f);
                                        OperatedPercent = (double)(OperatedFileList.Count - OperatedFileListView.Count) / OperatedFileList.Count;
                                        continue;
                                    }
                                    if (IsHomePage)
                                    {
                                        if (!await PasteFileAndFolderForS3(f, targetFilePath, OperatedOption))
                                        {
                                            OperatedFileListView.Remove(f);
                                            OperatedErrorListView.Add(f);
                                            OperatedPercent = (double)(OperatedFileList.Count - OperatedFileListView.Count) / OperatedFileList.Count;
                                            continue;
                                        }
                                    }
                                    else
                                    {
                                        CopyDirectory((f.fileInfo as DirectoryInfo), CurrentDirectoryInfo.CurrentDir);
                                    }
                                }
                            }
                            break;
                        }
                }
                OperatedFileListView.Remove(f);
                OperatedCompletedListView.Add(f);
                OperatedPercent = (double)(OperatedFileList.Count - OperatedFileListView.Count) / OperatedFileList.Count;
            }
            await UpdateFileListAsync();
        }
        internal async Task<bool> NewFolderAsync(string name)
        {
            if (!IsValidFileName(name))
            {
                await Shell.Current.DisplayAlert("Invalid", "Invalid folder name, please choose another name", "OK");
                return false;
            }
            string path = Path.Combine(CurrentDirectoryInfo.CurrentDir, name) + "/";
            if (Directory.Exists(path))
            {
                await Shell.Current.DisplayAlert("Duplicated", "Duplicate folder name, please choose another name", "OK");
                return false;
            }
            if (File.Exists(path))
            {
                await Shell.Current.DisplayAlert("Duplicated", "Duplicate with another file name, please choose another name", "OK");
                return false;
            }
            if (IsHomePage)
            {
                string awsFolderPath = path.Remove(0, MaiConstants.HomePath.Length + 1);
                if (awsFolderPath.StartsWith("/")) { awsFolderPath = awsFolderPath.Remove(0, 1); }
                await awsStorageService.CreateFolder(currentBucket, awsFolderPath);
            }
            else
            {
                Directory.CreateDirectory(path);
            }
            await UpdateFileListAsync();
            return true;
        }
        internal async Task SearchFileListAsync(string value)
        {
            IsReloading = true;

            if (prevToken != null)
            {
                prevToken.Cancel();
            }
            CancellationTokenSource loadFileToken = new CancellationTokenSource();
            prevToken = loadFileToken;
            CancellationToken cancellationToken = loadFileToken.Token;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                DirectoryInfo dir = new DirectoryInfo(CurrentDirectoryInfo.CurrentDir);
                IEnumerable<System.IO.DirectoryInfo> directoryList = null;
                IEnumerable<System.IO.FileInfo> fileList = null;
                await Task.Run(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    CurrentFileList.Clear();
                    List<FileSystemInfoWithIcon> tempFileList = new List<FileSystemInfoWithIcon>();


                    cancellationToken.ThrowIfCancellationRequested();
                    if (IsHomePage)
                    {
                        await GenerateFileViewAsync(StorageService.GenerateFileViewMode.Search, cancellationToken, value);
                    }

                    directoryList = dir.GetDirectories("**", System.IO.SearchOption.AllDirectories)
                                        .Where(dirInfo
                                        => dirInfo.Name.Contains(value, StringComparison.OrdinalIgnoreCase));
                    foreach (DirectoryInfo directoryInfo in directoryList)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        await Task.Run(() =>
                        {

                            tempFileList.Add(currentS3ListFileWIcon.Find(e => e.fileInfo.FullName == directoryInfo.FullName + "/"));
                        });
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    fileList = dir.GetFiles("**", System.IO.SearchOption.AllDirectories)
                                    .Where(fileInfo
                                    => fileInfo.Name.Contains(value, StringComparison.OrdinalIgnoreCase));

                    foreach (FileInfo fileInfo in fileList)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Run(() =>
                        {
                            tempFileList.Add(currentS3ListFileWIcon.Find(e => e.fileInfo.FullName == fileInfo.FullName));
                        });
                    }

                    SortFileMode(tempFileList);

                    foreach (FileSystemInfoWithIcon f in tempFileList)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await Task.Run(() =>
                        {
                            CurrentFileList.Add(f);
                        });
                    }

                    if (IsSelectionMode)
                    {
                        foreach (FileSystemInfoWithIcon f in CurrentFileList)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            f.CheckBoxSelectVisible = true;
                        }
                    }
                    NumberOfCheked = 0;
                });
            }
            catch (System.OperationCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine("Canceled loading file");
                return;
            }
            catch (Android.OS.OperationCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine("Canceled loading file");
                return;
            }
            IsReloading = false;
        }
        internal bool IsValidFileName(string name)
        {
            string invalidList = "|\\?*<\":>+[]/'";
            return (name.IndexOfAny(invalidList.ToCharArray()) == -1);
        }
        //1 Add 0 Remove
        private async Task FavouriteAR(FileSystemInfo f, int mode, List<string> oldFileList, List<string> oldFolderList)
        {
            string bucketAndPath = currentBucket + "[+]" + f.FullName;
            await Task.Run(() =>
            {
                if (f.GetType() == typeof(FileInfo))
                {
                    if (mode == 1)
                    {
                        if (!oldFileList.Exists(e => e == bucketAndPath))
                        {
                            oldFileList.Add(bucketAndPath);
                        }
                    }
                    else if (mode == 0)
                    {
                        oldFileList.Remove(bucketAndPath);
                    }
                }
                else if (f.GetType() == typeof(DirectoryInfo))
                {
                    if (mode == 1)
                    {
                        if (!oldFolderList.Exists(e => e == bucketAndPath))
                        {
                            oldFolderList.Add(bucketAndPath);
                        }
                    }
                    else if (mode == 0)
                    {
                        oldFolderList.Remove(bucketAndPath);
                    }
                }
            });
        }
        //0 file 1 folder
        private async Task FavouriteAR(string f, int type, int mode, List<string> oldFileList, List<string> oldFolderList)
        {
            f = currentBucket + "[+]" + f;
            await Task.Run(() =>
            {
                if (type == 0)
                {
                    if (mode == 1)
                    {
                        if (!oldFileList.Exists(e => e == f))
                        {
                            oldFileList.Remove(f);
                            oldFileList.Add(f);
                        }
                    }
                    else if (mode == 0)
                    {
                        oldFileList.Remove(f);
                    }
                }
                else if (type == 1)
                {
                    if (mode == 1)
                    {
                        if (!oldFolderList.Exists(e => e == f))
                        {
                            oldFileList.Remove(f);
                            oldFolderList.Add(f);
                        }
                    }
                    else if (mode == 0)
                    {
                        oldFolderList.Remove(f);
                    }
                }
            });
        }
        internal async Task AddOrRemoveFavouriteAsync(int mode, bool isReloadOff = false, FileSystemInfo path = null, string paths = null, int type = -1, 
                                                      string favFilePath = null, string favFolderPath = null)
        {
            favFilePath = favFilePath ?? FavouriteFilePath;
            favFolderPath = favFolderPath ?? FavouriteFolderPath;

            List<string> oldFileList = new List<string>();
            List<string> oldFolderList = new List<string>();
            if (!File.Exists(favFilePath))
            {
                oldFileList.Clear();
            }
            else
            {
                oldFileList = (await File.ReadAllLinesAsync(favFilePath)).ToList();
            }

            if (!File.Exists(favFolderPath))
            {
                oldFolderList.Clear();
            }
            else
            {
                oldFolderList = (await File.ReadAllLinesAsync(favFolderPath)).ToList();
            }

            if (path == null && paths == null)
            {
                foreach (FileSystemInfoWithIcon f in CurrentFileList.ToList())
                {
                    if (f.CheckBoxSelected)
                    {
                        await FavouriteAR(f.fileInfo, mode, oldFileList, oldFolderList);
                    }
                }
            }
            else if (path != null)
            {
                await FavouriteAR(path, mode, oldFileList, oldFolderList);
            }
            else if (paths != null)
            {
                await FavouriteAR(paths, type, mode, oldFileList, oldFolderList);
            }

            await File.WriteAllLinesAsync(favFilePath, oldFileList);
            await File.WriteAllLinesAsync(favFolderPath, oldFolderList);
            if (mode == 0 && (!isReloadOff))
            {
                await UpdateFileListAsync();
            }
        }
        private async Task<bool> IsInFavouriteAsync(string f, string favFilePath = null, string favFolderPath = null)
        {
            favFilePath = favFilePath ?? FavouriteFilePath;
            favFolderPath = favFolderPath ?? FavouriteFolderPath;
            List<string> oldFileList = new List<string>();
            List<string> oldFolderList = new List<string>();
            if (!File.Exists(favFilePath))
            {
                oldFileList.Clear();
            }
            else
            {
                oldFileList = (await File.ReadAllLinesAsync(favFilePath)).ToList();
            }

            if (!File.Exists(favFolderPath))
            {
                oldFolderList.Clear();
            }
            else
            {
                oldFolderList = (await File.ReadAllLinesAsync(favFolderPath)).ToList();
            }
            f = currentBucket + "[+]" + f;
            if (oldFileList.Contains(f) || oldFolderList.Contains(f))
            {
                return true;
            }
            return false;
        }
        internal async Task UploadFile()
        {
            string awsPath = CurrentDirectoryInfo.CurrentDir.Remove(0, MaiConstants.HomePath.Length);
            if (awsPath.StartsWith("/")) { awsPath = awsPath.Remove(0, 1); }
            if (awsPath.Length > 0) { awsPath += "/";}
            var result = await FilePicker.Default.PickMultipleAsync();
            if (result == null)
            {
                return;
            }
            foreach(FileResult fr in result)
            {
                await awsStorageService.UploadFileAsync(fr.FileName, fr.FullPath, awsPath);
            }
            await UpdateFileListAsync();
        }

    }
}
