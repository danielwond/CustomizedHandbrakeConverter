using Handbrake.Models;
using SQLite;

namespace Handbrake.Data
{
    public class MyDatabase
    {
        private readonly string _path;
        private readonly string _db;
        private readonly string _logDb;

        public MyDatabase(string path)
        {
            _path = path;
            _db = Path.Combine(_path, "info.db3");
            _logDb = "C:\\LOGS\\log.db3";

            InitializeDatabase();
        }
        public SQLiteConnection InitializeDatabase()
        {
            var result = new SQLiteConnection(_db);
            var logDb = new SQLiteConnection(_logDb);


            if(!TableExists(result, "Configurations"))
            {
                result.CreateTable<Configurations>();

                var data = new List<Configurations>()
                {
                    new()
                    {
                        Name = "Finalized",
                        Value = 0
                    },
                    new()
                    {
                        Name = "Completed",
                        Value = 0
                    },
                    new()
                    {
                        Name = "BeforeFolderSize",
                        Value = 0
                    },
                    new()
                    {
                        Name = "AfterFolderSize",
                        Value = 0
                    }
                };

                result.InsertAll(data);
            }

            if (!TableExists(result, "ConvertedFiles"))
            {
                result.CreateTable<ConvertedFiles>();
            }

            if (!TableExists(logDb, "FolderLog"))
            {
                logDb.CreateTable<FolderLog>();
            }

            if (!File.Exists(_logDb))
            {
                File.SetAttributes(Directory.GetDirectoryRoot(_logDb), FileAttributes.Normal);

                result = new SQLiteConnection(_logDb, SQLiteOpenFlags.Create);
                result.CreateTable<FolderLog>();
            }

            return result;
        }
        public void FinishedConvertingFile(string path)
        {
            var file = new ConvertedFiles()
            {
                FullPath = path,
                File = Path.GetFileName(path)
            };
            var db = new SQLiteConnection(_db);
            db.Insert(file);
        }

        public void FinalizeFolderConversion()
        {
            var db = new SQLiteConnection(_db);
            var configTable = db.Table<Configurations>();
            var data = configTable.Where(x => x.ID == 1).FirstOrDefault();

            data.Value = 1;
            db.Update(data);


            //insert the path into the finished log db
            var logDb = new SQLiteConnection(_logDb);
            logDb.Insert(new FolderLog()
            {
                date = DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss"),
                Path = _path
            });
        }
        public void FolderCompletedConversion()
        {
            var db = new SQLiteConnection(_db);
            var configTable = db.Table<Configurations>();
            var data = configTable.Where(x => x.ID == 2).FirstOrDefault();

            data.Value = 1;
            db.Update(data);
        }

        public bool HasFinalizedConversion()
        {
            var db = new SQLiteConnection(_db);
            var configTable = db.Table<Configurations>();
            var data = configTable.Where(x => x.ID == 1).FirstOrDefault();
            if (data.Value == 0)
            {
                return false;
            }
            return true;
        }
        public bool HasFolderCompletedConversion()
        {
            var db = new SQLiteConnection(_db);
            var configTable = db.Table<Configurations>();
            var data = configTable.Where(x => x.ID == 2).FirstOrDefault();
            if (data.Value == 0)
            {
                return false;
            }
            return true;
        }

        public void SetBeforeFolderSize(long beforeSize, string sizeInfo)
        {
            try
            {
                var db = new SQLiteConnection(_db);
                var configTable = db.Table<Configurations>();
                var before = db.Query<Configurations>("select * from Configurations where ID = 3").FirstOrDefault();
                
                before!.Value = beforeSize;
                before!.Option = sizeInfo;

                db.RunInTransaction(() =>
                {
                    db.Update(before);
                });
            }
            catch (Exception)
            {

                throw;
            }
        }
        public void SetAfterFolderSize(long afterSize, string sizeInfo)
        {
            try
            {
                var db = new SQLiteConnection(_db);
                var configTable = db.Table<Configurations>();
                var after = db.Query<Configurations>("select * from Configurations where ID = 4").FirstOrDefault();

                after!.Value = afterSize;
                after!.Option = sizeInfo;

                db.RunInTransaction(() =>
                {
                    db.Update(after);
                });
            }
            catch (Exception)
            {

                throw;
            }
        }

        public bool IsBeforeFolderSizeSet()
        {
            var db = new SQLiteConnection(_db);
            var configTable = db.Table<Configurations>();
            var before = configTable.Where(x => x.ID == 3).FirstOrDefault();
            return before.Value == 0;
        }
        public bool IsAfterFolderSizeSet()
        {
            var db = new SQLiteConnection(_db);
            var configTable = db.Table<Configurations>();
            var after = configTable.Where(x => x.ID == 4).FirstOrDefault();
            return after.Value == 0;
        }




        public bool TableExists(SQLiteConnection _conn, string tableName)
        {
            string sql = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{tableName}'";
            var count = _conn.ExecuteScalar<int>(sql);
            return count > 0;
        }

    }
}
