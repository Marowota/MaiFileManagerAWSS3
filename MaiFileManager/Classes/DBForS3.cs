using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaiFileManager.Classes
{
    internal class DBForS3
    {
        SQLiteAsyncConnection Database;

        public DBForS3()
        {
        }

        async Task Init()
        {
            if (Database is not null)
                return;

            Database = new SQLiteAsyncConnection(DBConstants.DatabasePath, DBConstants.Flags);
            var result = await Database.CreateTableAsync<AWSFileInfo>();
            var result2 = await Database.CreateTableAsync<AWSFolderInfo>();
        }

        public async Task<List<AWSFolderInfo>> GetFolderlist()
        {
            await Init();
            return await Database.Table<AWSFolderInfo>().ToListAsync();
        }

        public async Task<List<AWSFileInfo>> GetFileListByFolderId(long folderId)
        {
            await Init();
            return await Database.Table<AWSFileInfo>().Where(f => f.FolderId == folderId).ToListAsync();
        }
        public async Task<List<AWSFolderInfo>> getFolderListByFolderId(long folderId)
        {
            await Init();
            return await Database.Table<AWSFolderInfo>().Where(f => f.FolderId == folderId).ToListAsync();
        }

        public async Task<AWSFileInfo> GetFileById(int id)
        {
            await Init();
            return await Database.Table<AWSFileInfo>().Where(i => i.FileId == id).FirstOrDefaultAsync();
        }

        public async Task<int> SaveFileAsync(AWSFileInfo fileInfo)
        {
            await Init();
            if (fileInfo.FileId != 0)
                return await Database.UpdateAsync(fileInfo);
            else
                return await Database.InsertAsync(fileInfo);
        }

        public async Task<int> DeleteFileAsync(AWSFileInfo fileInfo)
        {
            await Init();
            return await Database.DeleteAsync(fileInfo);
        }

        public async Task<int> SaveFolderAsync(AWSFolderInfo folderInfo)
        {
            await Init();
            if (folderInfo.FolderId != 0)
                return await Database.UpdateAsync(folderInfo);
            else
                return await Database.InsertAsync(folderInfo);
        }

        public async Task<int> DeleteFolderAsync(AWSFolderInfo folderInfo)
        {
            await Init();
            return await Database.DeleteAsync(folderInfo);
        }

    }
}
