using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using ChicagoSTDriveMgr.Config;
using ChicagoSTDriveMgr.Db;
using ChicagoSTDriveMgr.Helpers;
using ChicagoSTDriveMgr.Workers.Download;
using ChicagoSTDriveMgr.Workers.Upload;
using NLog;

namespace ChicagoSTDriveMgr.Workers
{
    internal class ExtendedUploader : Uploader
    {
        private readonly string _tableName;
        public ExtendedUploader(AppConfig appConfig, string tableName) : base(appConfig)
        {
            _tableName = tableName;
        }
        protected override ReturnValue DoOperations()
        {
            var query = AppConfig.UploadSections.Items.OfType<UploadConfigurationElement>().FirstOrDefault(x => x.Name.ToLower() == _tableName.ToLower());
            if (query == null)
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Error, $"Config doesn't settings for {_tableName}.");
                return ReturnValue.DataEquNull;
            }
            ProcessQuery(query);
            return ReturnValue.Ok;
        }

        protected override ReturnValue GetData(DownloadConfigurationElement item, out Item[] items)
        {
            items = null;
            DataTable dataTable = null;
            ReturnValue result;
            try
            {
                if ((result = DataBaseHelper.ExecuteSQL(AppConfig, item.Query, SQLParameters, out dataTable, out string errorMessage)) != ReturnValue.Ok)
                {
                    Core.WriteToLog(AppConfig.Logger, LogLevel.Error, errorMessage);
                    return result;
                }
                if (dataTable?.Columns == null || dataTable?.Columns.Count < 1)
                {
                    Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"Columns of table aren't exist");
                    return ReturnValue.DataEquNull;
                }
                if (!dataTable.Columns.Contains(item.IdFieldName))
                {
                    Core.WriteToLog(AppConfig.Logger, LogLevel.Fatal, $"\"{item.IdFieldName}\" doesn't exist");
                    return ReturnValue.IdFieldDoesntExist;
                }
                if (!dataTable.Columns.Contains(item.UrlFieldName))
                {
                    Core.WriteToLog(AppConfig.Logger, LogLevel.Fatal, $"\"{item.UrlFieldName}\" doesn't exist");
                    return ReturnValue.UrlFieldDoesntExist;
                }
                if (dataTable?.Rows == null || dataTable?.Rows.Count<1)
                {
                    Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"Query of refMarketingMaterials is empty.");
                    return ReturnValue.DataEquNull;
                }
                items = GetItemsFromQuery(item, dataTable);
            }
            finally
            {
                dataTable?.Dispose();
            }
            return ReturnValue.Ok;
        }
        protected override void ProcessItems(Item[] uploadItems)
        {
            if (uploadItems == null || uploadItems.Length == 0)
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Error, "Query return 0 rows");
                return;
            }
            var items = uploadItems.Where(x => Core.IsPhotoHostingUsed(AppConfig, x.IdDistributor)).ToArray();
            if (items.Length <= AppConfig.CountRows)
                AppConfig.CountRows = items.Length;
            for (var i = 0; i < items.Length; i += AppConfig.CountRows)
            {
                var skip = i;
                Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"Query with row number {skip}");
                ProcessedPartCollection(items, skip);
            }
        }

        protected internal void ProcessedPartCollection(Item[] items, int skip)
        {
            try
            {
                var collectionIds = items.Skip(skip).Take(AppConfig.CountRows).Select(x => x.Id).ToList();
                if (collectionIds?.Count == 0)
                {
                    Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"For Query {GetAppConfigQuery()} parameters are empty");
                    return;
                }
                var commandText = GetAppConfigQuery() + $" in ({string.Join(", ", collectionIds)}) ";
                Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"Start Query {commandText}");
                var result = ProcessedEnd(commandText);
                Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"Query {commandText} with result {result}");
            }
            catch (Exception ex)
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Error, nameof(ProcessedPartCollection) + ex.Message);
            }
        }

        private string GetAppConfigQuery()
        {
            return AppConfig.QueryMarketingMaterials;
        }

        protected internal ReturnValue ProcessedEnd(string cmdText) 
        {
            ReturnValue result;
            DataTable dataTableResult = null;
            if ((result = DataBaseHelper.ExecuteSQL(AppConfig, cmdText, new SqlParameter[0], out dataTableResult, out string errorMessage)) != ReturnValue.Ok)
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Error, errorMessage);
            }
            else
            {
                if (dataTableResult?.Rows == null || dataTableResult.Rows.Count < 1)
                    Core.WriteToLog(AppConfig.Logger, LogLevel.Info, "Has no data in refFiles");
                else
                {
                    dataTableResult.TableName = Core.RefFilesTableName;
                    Parallel.ForEach(dataTableResult.AsEnumerable(), ProcessRow);
                }
            }
            return result;
        }

        protected virtual void ProcessRow(DataRow row)
        {
            var item = GetItemsFromRow(row, _tableName);
            if (item == null)
                return;
            var result = UploadFileFromRefFiles(item);
            if(string.IsNullOrEmpty(result))
                Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"Query {GetAppConfigQuery()} returned empty result");
        }

        private string UploadFileFromRefFiles(UploadingItem item)
        {
            var newUri = string.Empty;
            var message = $"(refFiles -> FileHosting) Processing {item.id}, filename -  ({item.FileName})";
            Core.WriteToLog(AppConfig.Logger, LogLevel.Info, message);
            UploadBodyFromFileStream(item);
            if (!item.UploadSucsess)
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Warn, $"FileHosting finised with error {item.ErrorInfo}");
                return string.Empty;
            }
            else
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"FileHosting finised");
                if(!UpdateRefFiles(item))
                    Core.WriteToLog(AppConfig.Logger, LogLevel.Warn, $"Failed update  refFiles  {item.id}");
            }
            return item.Url;
        }

        private bool UpdateRefFiles(UploadingItem item)
        {
            if (RefFiles.Update(AppConfig, item.id, string.Empty, item.MimeType.Id, GetBody(), item.Url, item.LoadType.Id, out _, out var errorMessage) != ReturnValue.Ok)
            {
                if (!string.IsNullOrWhiteSpace(errorMessage))
                    Core.WriteToLog(AppConfig.Logger, LogLevel.Error, nameof(UpdateRefFiles) + errorMessage);
                return false;
            }
            return true;
        }

        private byte[] GetBody()
        {
            switch (_tableName.ToLower())
            {
                case DataBaseHelper.RefMarketingMaterials:
                case DataBaseHelper.RefPromo:
                    return null;
                default:
                    return new byte[] { 0 }; 
            }
        }

        private void UploadBodyFromFileStream(UploadingItem item)
        {
            var fileName = string.Empty;
            item.UploadSucsess = false;

            if (Utils.IsImage(item.Body, out _, out var imageFormat, out var _, out var error))
            {
                item.MimeType = (AppConfig.MimeTypes.ContainsKey(imageFormat) ? AppConfig.MimeTypes[imageFormat] : null) ??
                                    AppConfig.MimeTypes["unknown"];
            }
            if (!PhotoHostingHelper.CopyContentIntoFileOnServerByUri(AppConfig, 0, fileName, item.Body, item.MimeType.Value, out var url, out item.ErrorInfo, true, item.FileTags))
                return;
            item.LoadType = (AppConfig.LoadTypes.ContainsKey(RefFiles.LoadTypeSTDriveKey)
                                         ? AppConfig.LoadTypes[RefFiles.LoadTypeSTDriveKey]
                                         : null) ?? AppConfig.LoadTypes[RefFiles.LoadTypeUnknownKey];
            item.Url = url;
            item.UploadSucsess = true;
            return;
        }
        private EnumInfo GetMimeTypeOnId(long idMimeType)
        {
            return AppConfig.MimeTypes.Where(x => x.Value?.Id == idMimeType).Select(x => x.Value).FirstOrDefault();
        }
        public UploadingItem GetItemsFromRow(DataRow row, string filetag)
        {
            if (row == null)
                return null;
            if (row.IsNull(nameof(UploadingItem.id)) || row.IsNull(nameof(UploadingItem.Body)))
                return null;
            if (row[nameof(UploadingItem.Body)] is byte[] body && body != null && body.Length > 1)
            {
                var fileId = Convert.ToInt64(row[nameof(UploadingItem.id)]);
                var fileName = Convert.ToString(row[nameof(UploadingItem.FileName)]);
                var mimeTypeId = Convert.ToInt64(row[nameof(UploadingItem.idMimeType)]);
                return new UploadingItem(fileId, fileName, GetMimeTypeOnId(mimeTypeId), body, filetag);
            }
            return null;
        }
    }
}
