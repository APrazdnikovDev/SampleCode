using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using ChicagoSTDriveMgr.Db;
using ChicagoSTDriveMgr.Helpers;
using NLog;
using ChicagoSTDriveMgr.Config;
using ChicagoSTDriveMgr.Workers;
using System.Threading.Tasks;

namespace ChicagoSTDriveMgr
{
    class Core
    {
        public const string
            RefGoodsTableName = "refGoods",
            RefFilesTableName = "refFiles",
            IdFieldName = "id",
            RefGoodsIdDistributorFieldName = "idDistributor",
            RefGoodsImageBodyFieldName = "ImageBody",
            RefGoodsImageFileNameFieldName = "ImageFileName",
            RefGoodsHtmlBodyFieldName = "HtmlBody",
            RefGoodsIdPacketFieldName = "idPacket",
            RefFilesFileNameFieldName = "FileName",
            RefFilesIdMimeTypeFieldName = "idMimeType",
            RefFilesBodyFieldName = "Body",
            RefFilesUriFieldName = "Uri",
            RefFilesPriorityFieldName = "Priority",
            RefFilesIdLoadTypeFieldName = "idLoadType",
            RefFilesIdDistributorFieldName = "idDistributor";

        protected static AppConfig AppConfig;

        public static ReturnValue Execute(AppConfig appConfig)
        {
            AppConfig = appConfig;
            ReturnValue result;

            if ((result = CheckPreRequirement(appConfig)) != ReturnValue.Ok
                || (result = CheckConnectionString(appConfig)) != ReturnValue.Ok
                || (result = appConfig.Configure()) != ReturnValue.Ok)
                return result;

            if (appConfig.Download)
            {
                var downloader = new Downloader(appConfig);
                return downloader.Run();
            }

            if (appConfig.Upload)
            {
                var uploader = new Uploader(appConfig);
                return uploader.Run();
            }
            if (appConfig.UploadMM)
            {
                var uploader = new ExtendedUploader(appConfig, DataBaseHelper.RefMarketingMaterials);
                return uploader.Run();
            }
            if (appConfig.ClearDoubles)
            {
                var res =  ClearDoubles.ClearDoublesInRefFiles(appConfig, out var error);
                if (!string.IsNullOrWhiteSpace(error))
                    WriteToLog(appConfig.Logger, LogLevel.Warn, error);
                return res;
            }
            if (appConfig.UploadPromo)
            {
                var uploader = new ExtendedUploader(appConfig, DataBaseHelper.RefPromo);
                return uploader.Run();
            }
            ReturnValue
                resultProcessRefGoods = ReturnValue.Ok,
                resultProcessRefFiles = ReturnValue.Ok;


            appConfig.IsAboutSKUSplitField = EnumsBase.AboutSKUSplitField(appConfig, out result, out var errorMessage) > 0;

            if (!string.IsNullOrWhiteSpace(errorMessage))
                WriteToLog(appConfig.Logger, LogLevel.Warn, errorMessage);

            if (appConfig.MoveRefFilesToFileHosting)
            {
                if (!IsPhotoHostingUsed(appConfig, 0))
                { 
                    WriteToLog(appConfig.Logger, LogLevel.Error, "Can`t find PhotoExchangeAddress in ConstBase");
                    return ReturnValue.PhotoHostingNotUsed;
                }

                return MoveImageFromRefFilesToPhotohosting(appConfig, true);
            }

            if (appConfig.ProcessRefGoods)
            {
                resultProcessRefGoods = MoveImageFromRefGoodsToRefFiles(appConfig);
                if (appConfig.IsAboutSKUSplitField)
                    resultProcessRefGoods = MoveImageFromRefGoodsToRefFilesWithIdGoodsCO(appConfig);
            }
            // if (appConfig.CheckRefFiles && appConfig.ProcessRefFiles)
            //     resultProcessRefFiles = MoveImageFromRefFilesToPhotohosting(appConfig);

            if (resultProcessRefGoods == ReturnValue.Ok && resultProcessRefFiles == ReturnValue.Ok)
                result = ReturnValue.Ok;
            else if (resultProcessRefGoods != ReturnValue.Ok && resultProcessRefFiles == ReturnValue.Ok)
                result = ReturnValue.RefGoodsError;
            else if (resultProcessRefGoods == ReturnValue.Ok && resultProcessRefFiles != ReturnValue.Ok)
                result = ReturnValue.RefFilesError;
            else
                result = ReturnValue.RefGoodsRefFilesError;

            return result;
        }

        static ReturnValue CheckPreRequirement(AppConfig appConfig)
        {
            if (string.IsNullOrWhiteSpace(appConfig.ConnectionString))
            {
                const string errorMessage = "Connection string is undefined";
                WriteToLog(appConfig.Logger, LogLevel.Fatal, errorMessage);

                return ReturnValue.ConnectionStringUndefined;
            }

            return ReturnValue.Ok;
        }

        static ReturnValue CheckConnectionString(AppConfig appConfig)
        {
            ReturnValue result;
            try
            {
                using (var connection = new SqlConnection(appConfig.ConnectionString))
                {
                    connection.Open();
                }
                result = ReturnValue.Ok;
            }
            catch (SqlException e)
            {
                var errorMessage = e.Message;

                result = ReturnValue.SqlError;

                if (e.ErrorCode == -2146232060)
                {
                    switch (e.Number)
                    {
                        case 53: result = ReturnValue.ConnectionStringInvalidServer; errorMessage = "Invalid server in connection string"; break;
                        case 18456: result = ReturnValue.ConnectionStringInvalidUserOrPassword; errorMessage = "Invalid user or password in connection string"; break;
                        case 4060: result = ReturnValue.ConnectionStringInvalidDatabase; errorMessage = "Invalid database in connection string"; break;
                    }
                }

                WriteToLog(appConfig.Logger, LogLevel.Fatal, errorMessage);
            }
            catch (Exception e)
            {
                var errorMessage = e.Message;

                result = ReturnValue.UnknownError;
                WriteToLog(appConfig.Logger, LogLevel.Fatal, errorMessage);
            }

            return result;
        }

        static ReturnValue MoveImageFromRefGoodsToRefFiles(AppConfig appConfig)
        {
            ReturnValue result;

            if ((result = DataBaseHelper.ExecuteSQL(appConfig, appConfig.IsAboutSKUSplitField ? appConfig.QueryGoodsWithoutIdGoodsCO : appConfig.QueryGoods, null, out DataTable data, out string errorMessage)) != ReturnValue.Ok)
            {
                WriteToLog(appConfig.Logger, LogLevel.Error, errorMessage);
                return result;
            }

            if (data == null)
            {
                WriteToLog(appConfig.Logger, LogLevel.Error, "DataTable == null");
                return ReturnValue.DataEquNull;
            }

            data.TableName = RefGoodsTableName;

            if ((result = CheckQueryGoods(appConfig, data)) != ReturnValue.Ok)
                return result;

            foreach (DataRow row in data.Rows)
                if ((result = MoveImageFromRefGoodsToRefFiles(appConfig, row)) != ReturnValue.Ok)
                    return result;

            return result;
        }

        static ReturnValue MoveImageFromRefGoodsToRefFilesWithIdGoodsCO(AppConfig appConfig)
        {
            ReturnValue result;

            if ((result = DataBaseHelper.ExecuteSQL(appConfig, appConfig.QueryGoodsWithIdGoodsCO, null, out DataTable data, out string errorMessage)) != ReturnValue.Ok)
            {
                WriteToLog(appConfig.Logger, LogLevel.Error, errorMessage);
                return result;
            }

            if (data == null)
            {
                WriteToLog(appConfig.Logger, LogLevel.Error, "DataTable == null");
                return ReturnValue.DataEquNull;
            }

            data.TableName = RefGoodsTableName;

            if ((result = CheckQueryGoodsWithIdGoodsCO(appConfig, data)) != ReturnValue.Ok)
                return result;

            foreach (DataRow row in data.Rows)
                if ((result = MoveImageFromRefGoodsWithIdGoodsCOToRefFiles(appConfig, row)) != ReturnValue.Ok)
                    return result;

            return result;
        }
       
        static ReturnValue MoveImageFromRefFilesToPhotohosting(AppConfig appConfig, bool moveImageFromRefFilesToPhotohosting = false)
        {
            WriteToLog(AppConfig.Logger, LogLevel.Info, $"Process start movereffilestofilehosting");
            ReturnValue result;
            DataTable data = null;
            var id = 0L;
            try
            {
                while (true)
                { 
                    var SQLParameters = new SqlParameter[1];
                    SQLParameters[0] = new SqlParameter("@lastID", SqlDbType.BigInt) { Value = id };

                    WriteToLog(AppConfig.Logger, LogLevel.Info, $"Query {appConfig.QueryFiles} with {string.Join(",", SQLParameters.Select(p => p.ParameterName + " " + p.Value))}");

                    if ((result = DataBaseHelper.ExecuteSQL(appConfig, appConfig.QueryFiles, SQLParameters, out data, out string errorMessage)) != ReturnValue.Ok)
                    {
                        WriteToLog(appConfig.Logger, LogLevel.Error, errorMessage);
                        return result;
                    }

                    if (data == null)
                    {
                        WriteToLog(appConfig.Logger, LogLevel.Error, "DataTable == null");
                        return ReturnValue.DataEquNull;
                    }
                    if (data.Rows.Count == 0)
                    {
                        WriteToLog(appConfig.Logger, LogLevel.Info, "Query empty");
                        return ReturnValue.Ok;
                    }
                    data.TableName = RefFilesTableName;

                    if ((result = CheckQueryFiles(appConfig, data)) != ReturnValue.Ok)
                        return result;

                    
                    var items = data.AsEnumerable().Select(d =>
                       new RefFilesToPhotohostingItem()
                       {
                           IdFieldName = Convert.ToInt64(d[IdFieldName]),
                           RefFilesIdDistributorFieldName = Convert.ToInt64(d[RefFilesIdDistributorFieldName]),
                           RefFilesFileNameFieldName = !d.IsNull(RefFilesFileNameFieldName) ? Convert.ToString(d[RefFilesFileNameFieldName]) : string.Empty,
                           RefFilesBodyFieldName = !d.IsNull(RefFilesBodyFieldName) ? (byte[])d[RefFilesBodyFieldName] : Array.Empty<byte>()
                       });
                    Parallel.ForEach(items, (row) => MoveImageFromRefFilesToPhotohosting(appConfig, row));
                    id = items.Last().IdFieldName;
                    if (data != null)
                    {
                        data.Dispose();
                        data = null;
                    }
                }
            }
            finally
            {
                WriteToLog(AppConfig.Logger, LogLevel.Info, $"Process finisned");
                if (data != null)
                    data.Dispose();
            }
        }

        static ReturnValue CheckQueryGoods(AppConfig appConfig, DataTable dataTable)
        {
            var requiredColumn = new[] { IdFieldName, RefGoodsIdDistributorFieldName, RefGoodsImageFileNameFieldName, RefGoodsImageBodyFieldName, RefGoodsHtmlBodyFieldName };
            return requiredColumn.Aggregate(true, (current, item) => current & IsColumnExist(appConfig, dataTable, item)) ? ReturnValue.Ok : ReturnValue.ColumnDoesntExist;
        }

        static ReturnValue CheckQueryGoodsWithIdGoodsCO(AppConfig appConfig, DataTable dataTable)
        {
            var requiredColumn = new[] { IdFieldName, RefGoodsIdDistributorFieldName, RefGoodsImageFileNameFieldName, RefGoodsImageBodyFieldName, RefGoodsHtmlBodyFieldName, RefGoodsIdPacketFieldName };
            return requiredColumn.Aggregate(true, (current, item) => current & IsColumnExist(appConfig, dataTable, item)) ? ReturnValue.Ok : ReturnValue.ColumnDoesntExist;
        }

        public static ReturnValue CheckQueryFiles(AppConfig appConfig, DataTable dataTable)
        {
            var requiredColumn = new[] { IdFieldName, RefFilesFileNameFieldName, RefFilesIdMimeTypeFieldName, RefFilesBodyFieldName, RefFilesIdDistributorFieldName };
            return requiredColumn.Aggregate(true, (current, item) => current & IsColumnExist(appConfig, dataTable, item)) ? ReturnValue.Ok : ReturnValue.ColumnDoesntExist;
        }

        static bool IsColumnExist(AppConfig appconfig, DataTable dataTable, string columnName)
        {
            bool result;

            if (dataTable == null)
            {
                WriteToLog(appconfig.Logger, LogLevel.Error, "DataTable == null");
                return false;
            }

            if (!(result = dataTable.Columns.Contains(columnName)))
                WriteToLog(appconfig.Logger, LogLevel.Error, string.Format("\"{0}.{1}\" doesn't exist", dataTable.TableName, columnName));

            return result;
        }

        static ReturnValue MoveImageFromRefGoodsToRefFiles(AppConfig appConfig, DataRow data)
        {
            var result = ReturnValue.Ok;

            long
                id = Convert.ToInt64(data[IdFieldName]),
                idDistributor = Convert.ToInt64(data[RefGoodsIdDistributorFieldName]);

            Console.WriteLine("(refGoods -> refFiles) Processing {0}", id);

            string
                htmlBody = !data.IsNull(RefGoodsHtmlBodyFieldName) ? RefGoods.RemoveImgFromBody(Convert.ToString(data[RefGoodsHtmlBodyFieldName])) : string.Empty,
                fileName = !data.IsNull(RefGoodsImageFileNameFieldName) ? Convert.ToString(data[RefGoodsImageFileNameFieldName]) : string.Empty,
                fileNameExtension = !string.IsNullOrWhiteSpace(fileName) ? Path.GetExtension(fileName) : null,
                uri = string.Empty,
                errorMessage = string.Empty;

            var content = !data.IsNull(RefGoodsImageBodyFieldName) ? (byte[])data[RefGoodsImageBodyFieldName] : Array.Empty<byte>();
            var isContentImage = Utils.IsImage(content, out _, out var imageFormat, out var imageFileNameExtension, out var error);
            var mimeType = (isContentImage && appConfig.MimeTypes.ContainsKey(imageFormat) ? appConfig.MimeTypes[imageFormat] : null) ?? appConfig.MimeTypes["unknown"];
            var isPhotoHostingUsed = IsPhotoHostingUsed(appConfig, idDistributor);

            if (!string.IsNullOrWhiteSpace(fileNameExtension) && fileNameExtension.ToLower() != imageFileNameExtension)
                fileName = Path.ChangeExtension(fileName, imageFileNameExtension);
            var filetag = DataBaseHelper.GetFileTagFromTableName(RefGoodsTableName);

            if (isContentImage && appConfig.ProcessRefFiles && isPhotoHostingUsed)
            {
                //вернул - клиент окончательно перейдет на работу с ST-Drive, сейчас нужно сохранят body в любом случае
                if (isPhotoHostingUsed = PhotoHostingHelper.CopyContentIntoFileOnServerByUri(appConfig, idDistributor, fileName, content, mimeType.Value, out uri, out errorMessage, true, filetag))
                    content = Array.Empty<byte>();
                else
                    uri = string.Empty;
                isPhotoHostingUsed = PhotoHostingHelper.CopyContentIntoFileOnServerByUri(appConfig, idDistributor, fileName, content, mimeType.Value, out uri, out errorMessage, true, filetag);
                if (!isPhotoHostingUsed)
                    uri = string.Empty;
            }

            var loadType = appConfig.LoadTypes[RefFiles.LoadTypeSTReplicationKey];

            try
            {
                using (var connection = new SqlConnection(appConfig.ConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {

                        var refFilePacketsNewId = 0L;

                        if (isContentImage)
                        {

                            if ((result = RefFilePackets.Insert(connection, transaction, idDistributor, appConfig.ObjectSKUId, out _, out refFilePacketsNewId, out errorMessage)) != ReturnValue.Ok
                                || (result = RefFiles.Insert(connection, transaction, idDistributor, fileName, mimeType.Id, content, uri, /*1,*/ loadType.Id, out _, out long refFilesNewId, out errorMessage)) != ReturnValue.Ok
                                || (result = RefFilePacketsFiles.Insert(connection, transaction, idDistributor, refFilePacketsNewId, refFilesNewId, out _, out errorMessage)) != ReturnValue.Ok)
                            {
                                transaction.Rollback();
                                return result;
                            }
                        }

                        if ((result = RefGoods.Update(connection, transaction, id, htmlBody, refFilePacketsNewId, out _, out errorMessage)) != ReturnValue.Ok)
                        {
                            transaction.Rollback();
                            return result;
                        }

                        transaction.Commit();
                    }
                }
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
            }
            finally
            {
                if (!string.IsNullOrEmpty(errorMessage))
                    WriteToLog(appConfig.Logger, LogLevel.Error, errorMessage);
            }

            return result;
        }

        static ReturnValue MoveImageFromRefGoodsWithIdGoodsCOToRefFiles(AppConfig appConfig, DataRow data)
        {
            var result = ReturnValue.Ok;

            long
                id = Convert.ToInt64(data[IdFieldName]),
                idDistributor = Convert.ToInt64(data[RefGoodsIdDistributorFieldName]);

            Console.WriteLine("(refGoods -> refFiles) Processing {0}", id);

            string
                htmlBody = !data.IsNull(RefGoodsHtmlBodyFieldName) ? RefGoods.RemoveImgFromBody(Convert.ToString(data[RefGoodsHtmlBodyFieldName])) : string.Empty,
                fileName = !data.IsNull(RefGoodsImageFileNameFieldName) ? Convert.ToString(data[RefGoodsImageFileNameFieldName]) : string.Empty,
                fileNameExtension = !string.IsNullOrWhiteSpace(fileName) ? Path.GetExtension(fileName) : null, uri = string.Empty,
                errorMessage = string.Empty;

            var content = !data.IsNull(RefGoodsImageBodyFieldName) ? (byte[])data[RefGoodsImageBodyFieldName] : Array.Empty<byte>();
            var isContentImage = Utils.IsImage(content, out Size imageSize, out string imageFormat, out string imageFileNameExtension, out var error);
            var mimeType = (isContentImage && appConfig.MimeTypes.ContainsKey(imageFormat) ? appConfig.MimeTypes[imageFormat] : null) ?? appConfig.MimeTypes["unknown"];

            if (!string.IsNullOrWhiteSpace(fileNameExtension) && fileNameExtension.ToLower() != imageFileNameExtension)
                fileName = Path.ChangeExtension(fileName, imageFileNameExtension);

            try
            {
                using (var connection = new SqlConnection(appConfig.ConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        var refFilePacketsId = Convert.ToInt64(data[RefGoodsIdPacketFieldName]);

                        if ((result = RefGoods.Update(connection, transaction, id, htmlBody, refFilePacketsId, out int returnValue, out errorMessage)) != ReturnValue.Ok)
                        {
                            transaction.Rollback();
                            return result;
                        }

                        transaction.Commit();
                    }
                }
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
            }
            finally
            {
                if (!string.IsNullOrEmpty(errorMessage))
                    WriteToLog(appConfig.Logger, LogLevel.Error, errorMessage);
            }

            return result;
        }

        class RefFilesToPhotohostingItem
        {
            public long IdFieldName { get; set; }
            public long RefFilesIdDistributorFieldName { get; set; }
            public string RefFilesFileNameFieldName { get; set; }
            public byte[] RefFilesBodyFieldName { get; set; }

        }

        static ReturnValue MoveImageFromRefFilesToPhotohosting(AppConfig appConfig, RefFilesToPhotohostingItem data)
        {
            ReturnValue result;

            var id = data.IdFieldName;
            var idDistributor = data.RefFilesIdDistributorFieldName;

            WriteToLog(appConfig.Logger, LogLevel.Info, $"(refFiles -> PhotoHosting) Processing {id}");

            if (!IsPhotoHostingUsed(appConfig, idDistributor))
            {
                WriteToLog(appConfig.Logger, LogLevel.Info, $"(refFiles -> PhotoHosting) {idDistributor} not use StDrive {id}");
                return ReturnValue.Ok;
            }

            var fileName = data.RefFilesFileNameFieldName;
            var fileNameExtension = !string.IsNullOrWhiteSpace(fileName) ? Path.GetExtension(fileName) : null;

            byte[] body;

            if (!Utils.IsImage(body = data.RefFilesBodyFieldName, out Size imageSize, out string imageFormat, out string imageFileNameExtension, out string error))
            {
                WriteToLog(appConfig.Logger, LogLevel.Error, $"(refFiles -> PhotoHosting) error {id} {error}");
                return ReturnValue.Ok;
            }

            var mimeType = (appConfig.MimeTypes.ContainsKey(imageFormat) ? appConfig.MimeTypes[imageFormat] : null) ?? appConfig.MimeTypes["unknown"];

            if (!string.IsNullOrWhiteSpace(fileNameExtension) && fileNameExtension.ToLower() != imageFileNameExtension)
                fileName = Path.ChangeExtension(fileName, imageFileNameExtension);
            var filetag = DataBaseHelper.GetFileTagFromTableName(RefGoodsTableName);
            if (!PhotoHostingHelper.CopyContentIntoFileOnServerByUri(appConfig, idDistributor, fileName, body, mimeType.Value, out string uri, out string errorMessage, true, filetag))
            {
                WriteToLog(appConfig.Logger, LogLevel.Error, $"(refFiles -> PhotoHosting) Error {id} {errorMessage}");
                return ReturnValue.CopyContentIntoFileOnServerByUriError;
            }

            var loadType = (appConfig.LoadTypes.ContainsKey(RefFiles.LoadTypeSTDriveKey) ? appConfig.LoadTypes[RefFiles.LoadTypeSTDriveKey] : null) ?? appConfig.LoadTypes[RefFiles.LoadTypeUnknownKey];

            if ((result = RefFiles.Update(appConfig, id, string.Empty, mimeType.Id, null, uri, loadType.Id, out int returnValue, out errorMessage)) != ReturnValue.Ok)
            {
                if (!string.IsNullOrWhiteSpace(errorMessage))
                    WriteToLog(appConfig.Logger, LogLevel.Error, $"(refFiles -> PhotoHosting) update in db {id} {errorMessage}");
            }
            WriteToLog(appConfig.Logger, LogLevel.Info, $"(refFiles -> PhotoHosting) finished {id}");

            return result;
        }

        public static bool IsPhotoHostingUsed(AppConfig appConfig, long idDistributor)
        {
            if (appConfig.PhotoHostingUri.ContainsKey(idDistributor))
                return IsPhotoHostingUsed(appConfig.PhotoHostingUri[idDistributor]);

            var result =
                IsPhotoHostingUsed(appConfig.PhotoHostingUri[idDistributor] =
                    ConstsBase.GetPhotoExchangeAddress(appConfig, idDistributor, out var errorMessage)) &&
                string.IsNullOrWhiteSpace(errorMessage);

            if (!string.IsNullOrWhiteSpace(errorMessage))
                WriteToLog(appConfig.Logger, LogLevel.Warn, errorMessage);

            return result;
        }

        static bool IsPhotoHostingUsed(string uri)
        {
            return !string.IsNullOrWhiteSpace(uri) && uri.Trim().ToLower().StartsWith(PhotoHostingHelper.UriProtocol, StringComparison.Ordinal);
        }

        public static void WriteToLog(Logger logger, LogLevel logLevel, string message)
        {
            if (logger == null)
                return;

            switch (logLevel.Name)
            {
                case nameof(LogLevel.Trace): logger.Trace(message); break;
                case nameof(LogLevel.Debug): logger.Debug(message); break;
                case nameof(LogLevel.Info): logger.Info(message); break;
                case nameof(LogLevel.Warn): logger.Warn(message); break;
                case nameof(LogLevel.Error): logger.Error(message); break;
                case nameof(LogLevel.Fatal): logger.Fatal(message); break;
                default: logger.Fatal(message); break;
            }
        }
    }
}
