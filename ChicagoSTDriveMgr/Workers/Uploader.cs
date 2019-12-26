using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using ChicagoSTDriveMgr.Config;
using ChicagoSTDriveMgr.Db;
using ChicagoSTDriveMgr.Helpers;
using ChicagoSTDriveMgr.Workers.Download;
using ChicagoSTDriveMgr.Workers.Upload;
using NLog;

namespace ChicagoSTDriveMgr.Workers
{
    internal class Uploader : BaseLoader
    {
        public Uploader(AppConfig appConfig) : base(appConfig)
        {
        }

        protected override ReturnValue CheckConditions()
        {
            return !Core.IsPhotoHostingUsed(AppConfig, 0) ? ReturnValue.PhotoHostingNotUsed : ReturnValue.Ok;
        }

        protected override ReturnValue DoOperations()
        {
            var listOfDocs = new string[]
                {DataBaseHelper.DrPhotoReport.ToLower(), DataBaseHelper.DrWorkingDayOfAgentPhotos.ToLower(), DataBaseHelper.DrSurveyPhotos.ToLower()};
            foreach (var query in AppConfig.UploadSections.Items.OfType<UploadConfigurationElement>())
            {
                if (!listOfDocs.Contains(query.Name.ToLower()))
                    continue;
                var startDate = AppConfig.DateBegin;
                while (startDate < AppConfig.DateEnd)
                {
                    var endDate = startDate.AddDays(1) > AppConfig.DateEnd ? AppConfig.DateEnd : startDate.AddDays(1);

                    SQLParameters = new SqlParameter[2];
                    SQLParameters[0] = new SqlParameter("@datebegin", SqlDbType.DateTime) { Value = startDate };
                    SQLParameters[1] = new SqlParameter("@dateend", SqlDbType.DateTime) { Value = endDate };

                    ProcessQuery(query);
                    startDate = endDate;
                }
            }
            return ReturnValue.Ok;
        }

        protected override void ProcessItem(Item item)
        {
            Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"Process with table {item.TableName}, id =  {item.Id} ({item.Url})");

            var idFilePacket = DataBaseHelper.SystemId;
            var newUri = UploadImages(item);
            if (string.IsNullOrEmpty(newUri) && item.IdPacket == DataBaseHelper.SystemId && IsPhotoFilesInPacket(item.TableName))
                newUri = AddNewRefFiles(item, out idFilePacket);
            if (string.IsNullOrEmpty(newUri))
                return;
            item.Url = newUri;
            item.FullFileName = newUri;
            IUpdater updater;
            if ((updater = UpdaterFactory.GetUpdater(AppConfig, item.TableName)) == null)
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Fatal, $"Invalid tableName: \"{item.TableName}\"");
                return;
            }
            updater.UpdateOnlyDocTable(item, idFilePacket);
            Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"Finised process with table {item.TableName}, id =  {item.Id} ({item.Url})");
        }

        protected override Item[] GetItemsFromQuery(ConfigurationElement baseitem, DataTable dataTable)
        {
            if (!(baseitem is UploadConfigurationElement item))
                return null;
            return dataTable.AsEnumerable()
                .Where(row =>
                    !row.IsNull(item.IdFieldName) && !row.IsNull(item.UrlFieldName) &&
                    !row.IsNull(item.IdDistributorFieldName))
                .Select(row => new Item(Convert.ToInt64(row[item.IdFieldName]),
                    Convert.ToInt64(row[item.IdDistributorFieldName]),
                    Convert.ToInt64(row[item.IdPacketFieldNameFieldName]),
                    Convert.ToString(row[item.UrlFieldName]), item.TableName, item.IdFieldName,
                    item.UrlFieldName))
                .ToArray();
        }

        private string AddNewRefFiles(Item item, out long refFilePacketsId)
        {
            refFilePacketsId = 0;
            Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"({item.TableName} -> PhotoHosting) Processing {item.Id}, filename -  ({item.Url})");

            var uploadingItem = UploadFileToFileStream(item.Url, item.FileTags);

            if (!uploadingItem.UploadSucsess)
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Error, $"Can`t load file to Filestream {uploadingItem.ErrorInfo}");
                return string.Empty;
            }
            else
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"PhotoHosting finised Filestream");
            }

            var newFileName = GetFileNameFromUri(uploadingItem.Url);

            var errorMessage = string.Empty;

            try
            {
                using (var connection = new SqlConnection(AppConfig.ConnectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        RefFilePackets.Insert(connection, transaction, item.IdDistributor, AppConfig.ObjectSKUId, out _,
                            out var refFilePacketsNewId, out errorMessage);
                        if (!string.IsNullOrEmpty(errorMessage))
                        {
                            transaction.Rollback();
                            return string.Empty;
                        }
                        refFilePacketsId = refFilePacketsNewId;
                        var body = Array.Empty<byte>();
                        RefFiles.Insert(connection, transaction, item.IdDistributor, newFileName, uploadingItem.MimeType.Id, body, uploadingItem.Url,
                            uploadingItem.LoadType.Id, out _, out var refFilesNewId, out errorMessage);
                        if (!string.IsNullOrEmpty(errorMessage))
                        {
                            transaction.Rollback();
                            return string.Empty;
                        }
                        RefFilePacketsFiles.Insert(connection, transaction, item.IdDistributor, refFilePacketsNewId,
                            refFilesNewId, out _, out errorMessage);
                        if (!string.IsNullOrEmpty(errorMessage))
                        {
                            transaction.Rollback();
                            return string.Empty;
                        }
                        transaction.Commit();
                        Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"Create in db");
                    }
                }

            }
            catch (Exception e)
            {
                errorMessage = e.Message;
                return string.Empty;
            }
            finally
            {
                if (!string.IsNullOrEmpty(errorMessage))
                    Core.WriteToLog(AppConfig.Logger, LogLevel.Error, errorMessage);
            }

            return uploadingItem.Url;
        }

        private string UploadImages(Item item)
        {

            var newUri = string.Empty;
            DataTable data = null;
            try
            {
                if (IsPhotoFilesInPacket(item.TableName))
                {
                    if (DataBaseHelper.ExecuteSQL(AppConfig, AppConfig.QueryFilesByIdPacket + item.IdPacket, null, out data, out var errorMessage) != ReturnValue.Ok)
                    {
                        Core.WriteToLog(AppConfig.Logger, LogLevel.Error, errorMessage);
                        return string.Empty;
                    }
                }
                else
                {
                    if (DataBaseHelper.ExecuteSQL(AppConfig, AppConfig.QueryFilesById + item.Id, null, out data, out var errorMessage) != ReturnValue.Ok)
                    {
                        Core.WriteToLog(AppConfig.Logger, LogLevel.Error, errorMessage);
                        return string.Empty;
                    }
                }

                if (data == null)
                {
                    Core.WriteToLog(AppConfig.Logger, LogLevel.Error, "DataTable == null");
                    return string.Empty;
                }
                data.TableName = Core.RefFilesTableName;

                if (Core.CheckQueryFiles(AppConfig, data) != ReturnValue.Ok)
                    return string.Empty;

                if (data.Rows.Count == 0)
                {
                    Core.WriteToLog(AppConfig.Logger, LogLevel.Info, "Has no data in refFiles");
                    return string.Empty;
                }
                foreach (DataRow row in data.Rows)
                {
                    var tempUri = UploadImagesfromRefFiles(row, item.FileTags);
                    if (!string.IsNullOrEmpty(tempUri))
                        newUri = tempUri;
                }
            }
            finally
            {
                data?.Dispose();
            }
            return newUri;
        }

        private string UploadImagesfromRefFiles(DataRow data, string fileTag)
        {
            var id = Convert.ToInt64(data[Core.IdFieldName]);
            var fileName = !data.IsNull(Core.RefFilesFileNameFieldName)
                ? Convert.ToString(data[Core.RefFilesFileNameFieldName])
                : string.Empty;

            var message = $"(refFiles -> PhotoHosting) Processing {id}, filename -  ({fileName})";
            Core.WriteToLog(AppConfig.Logger, LogLevel.Info, message);
            if (fileName.Contains(AppConfig.PhotoHostingUri[0]))
            {
                message = $"Already Loaded ({fileName})";
                Core.WriteToLog(AppConfig.Logger, LogLevel.Warn, message);
                return fileName;
            }

            var uploadingItem = UploadFileToFileStream(fileName, fileTag);
            if (!uploadingItem.UploadSucsess)
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Warn, $"PhotoHosting {id} finished with error {uploadingItem.ErrorInfo}");
                return string.Empty;
            }
            else
                Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"PhotoHosting upload {id}, filename - ({uploadingItem.Url})");

            if (RefFiles.Update(AppConfig, id, string.Empty, uploadingItem.MimeType.Id, null, uploadingItem.Url, uploadingItem.LoadType.Id, out _, out var errorMessage) != ReturnValue.Ok)
            {
                if (!string.IsNullOrWhiteSpace(errorMessage))
                    Core.WriteToLog(AppConfig.Logger, LogLevel.Error, errorMessage);
            }
            else
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"Update {id} in db filename -  ({uploadingItem.Url}) finished");
            }
            return uploadingItem.Url;
        }


        private string UploadImagesfromRefFilesBody(DataRow data, string fileTag)
        {
            var id = Convert.ToInt64(data[Core.IdFieldName]);
            if (data.IsNull(Core.RefFilesBodyFieldName))
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Warn, $"Can`t find body from record id = {id} ");
                return string.Empty;
            }
            var body = (byte[])data[Core.RefFilesBodyFieldName];
            var message = $"(refFiles -> PhotoHosting) Processing load body from record id = {id} ";
            Core.WriteToLog(AppConfig.Logger, LogLevel.Info, message);

            var fileName = !data.IsNull(Core.RefFilesFileNameFieldName)
                ? Convert.ToString(data[Core.RefFilesFileNameFieldName])
                : string.Empty;
            if (fileName.Contains(AppConfig.PhotoHostingUri[0]))
            {
                message = $"Already Loaded ({fileName})";
                Core.WriteToLog(AppConfig.Logger, LogLevel.Warn, message);
                return fileName;
            }

            var uploadingItem = UploadBodyToFileStream(body, fileTag);
            if (!uploadingItem.UploadSucsess)
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Warn, $"PhotoHosting finised with error {uploadingItem.ErrorInfo}");
                return string.Empty;
            }
            else
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"PhotoHosting finised");
            }

            if (RefFiles.Update(AppConfig, id, string.Empty, uploadingItem.MimeType.Id, null, uploadingItem.Url, uploadingItem.LoadType.Id, out _, out var errorMessage) != ReturnValue.Ok)
            {
                if (!string.IsNullOrWhiteSpace(errorMessage))
                    Core.WriteToLog(AppConfig.Logger, LogLevel.Error, errorMessage);
            }
            else
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"Update in db filename -  ({uploadingItem.Url}) finished");
            }
            return uploadingItem.Url;
        }

        private UploadingItem UploadBodyToFileStream(byte[] body, string fileTag)
        {
            var fileName = string.Empty;
            var uploadingItem = new UploadingItem(fileName) { UploadSucsess = false };

            if (!Utils.IsImage(body, out _, out var imageFormat, out var _, out var error))
            {
                uploadingItem.ErrorInfo = "Can not convert [body] to Image {error}";
                return uploadingItem;
            }
            uploadingItem.MimeType = (AppConfig.MimeTypes.ContainsKey(imageFormat) ? AppConfig.MimeTypes[imageFormat] : null) ??
                                     AppConfig.MimeTypes["unknown"];

            if (!PhotoHostingHelper.CopyContentIntoFileOnServerByUri(AppConfig, 0, fileName, body, uploadingItem.MimeType.Value, out var url, out uploadingItem.ErrorInfo, true, fileTag))
                return uploadingItem;

            uploadingItem.LoadType = (AppConfig.LoadTypes.ContainsKey(RefFiles.LoadTypeSTDriveKey)
                                         ? AppConfig.LoadTypes[RefFiles.LoadTypeSTDriveKey]
                                         : null) ?? AppConfig.LoadTypes[RefFiles.LoadTypeUnknownKey];
            uploadingItem.Url = url;
            uploadingItem.UploadSucsess = true;
            return uploadingItem;
        }

        private UploadingItem UploadFileToFileStream(string fileName,string fileTag)
        {
            var uploadingItem = new UploadingItem(fileName) { UploadSucsess = false };
            try
            {

                if (fileName.Contains("http"))
                {
                    uploadingItem.ErrorInfo = $"Can't read file {fileName} it is http";
                    return uploadingItem;
                }
                if (string.IsNullOrEmpty(fileName))
                {
                    uploadingItem.ErrorInfo = "File name is empty";
                    return uploadingItem;
                }

                var fi = new FileInfo(fileName);

                if (!fi.Exists)
                {
                    uploadingItem.ErrorInfo = $"File {fileName} not exist";
                    return uploadingItem;
                }
                byte[] body;
                try
                {
                    body = ReadContentFromFile(fileName);
                }
                catch (Exception ex)
                {
                    uploadingItem.ErrorInfo = $"File {fileName} not open {ex.Message}";
                    return uploadingItem;
                }

                if (!Utils.IsImage(body, out _, out var imageFormat, out var _, out var error))
                {
                    uploadingItem.ErrorInfo = $"File {fileName} is not image {error}";
                    return uploadingItem;
                }

                uploadingItem.MimeType =
                    (AppConfig.MimeTypes.ContainsKey(imageFormat) ? AppConfig.MimeTypes[imageFormat] : null) ??
                    AppConfig.MimeTypes["unknown"];

                if (!PhotoHostingHelper.CopyContentIntoFileOnServerByUri(AppConfig, 0, fileName, body,
                    uploadingItem.MimeType.Value, out var url, out uploadingItem.ErrorInfo, true, fileTag))
                    return uploadingItem;

                uploadingItem.LoadType = (AppConfig.LoadTypes.ContainsKey(RefFiles.LoadTypeSTDriveKey)
                                             ? AppConfig.LoadTypes[RefFiles.LoadTypeSTDriveKey]
                                             : null) ?? AppConfig.LoadTypes[RefFiles.LoadTypeUnknownKey];
                uploadingItem.Url = url;
            }

            catch (Exception ex)
            {
                uploadingItem.ErrorInfo = $"Work with file {fileName} " + ex.Message;
                return uploadingItem;
            }
            uploadingItem.UploadSucsess = true;
            return uploadingItem;
        }

        public byte[] ReadContentFromFile(string filePath)
        {
            var fi = new FileInfo(filePath);
            var numBytes = fi.Length;
            byte[] buffer = null;
            if (numBytes > 0)
            {
                try
                {
                    using (var fs = new FileStream(filePath, FileMode.Open))
                    {
                        using (var br = new BinaryReader(fs))
                        {
                            buffer = br.ReadBytes((int)numBytes);
                            br.Close();
                        }
                        fs.Close();
                    }
                }
                catch (Exception ex)
                {
                    Core.WriteToLog(AppConfig.Logger, LogLevel.Error, $"File {filePath} not open" + ex.Message);
                }
            }
            return buffer;
        }

        private static bool IsPhotoFilesInPacket(string tableName)
        {
            var result = Item.GetTableNameOnly(tableName).ToLower(CultureInfo.InvariantCulture);
            switch (result)
            {
                case DataBaseHelper.DrSurveyPhotos:
                case DataBaseHelper.DrWorkingDayOfAgentPhotos:
                    {
                        return false;
                    }
            }
            return true;
        }

        private static string GetFileNameFromUri(string uri)
        {
            return uri.Substring(uri.LastIndexOf(Path.AltDirectorySeparatorChar) + 1);
        }

    }

    
}
