using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using ChicagoSTDriveMgr.Config;
using ChicagoSTDriveMgr.Helpers;
using ChicagoSTDriveMgr.Workers.Download;
using NLog;

namespace ChicagoSTDriveMgr.Workers
{
    internal class Downloader:BaseLoader
    {
        public Downloader(AppConfig appConfig):base(appConfig)
        {
        }
        
        protected override ReturnValue CheckConditions()
        {
            return CheckDestination(AppConfig);
        }

        protected override ReturnValue DoOperations()
        {
            foreach (var query in AppConfig.DownloadSections.Items.OfType<DownloadConfigurationElement>())
                ProcessQuery(query);
            return ReturnValue.Ok;
        }

        internal static ReturnValue CheckDestination(AppConfig appConfig)
        {
            ReturnValue result;

            try
            {
                var directoryInfo = new DirectoryInfo(appConfig.DestinationPath);
                if (!directoryInfo.Exists)
                    directoryInfo = Directory.CreateDirectory(appConfig.DestinationPath);

                directoryInfo.GetAccessControl();

                result = ReturnValue.Ok;
            }
            catch (Exception e)
            {
                Core.WriteToLog(appConfig.Logger, LogLevel.Fatal, $"{e.GetType().Name}: \"{e.Message}\" (\"{appConfig.DestinationPath}\")");
                result = ReturnValue.DestinationDirectoryError;
            }

            return result;
        }
        
        protected override Item[] GetItemsFromQuery(ConfigurationElement baseitem, DataTable dataTable)
        {
            if (!(baseitem is DownloadConfigurationElement item))
                return null;
            return dataTable.AsEnumerable()
                .Where(row => !row.IsNull(item.IdFieldName) && !row.IsNull(item.UrlFieldName))
                .Select(row => new Item(Convert.ToInt64(row[item.IdFieldName]), Convert.ToString(row[item.UrlFieldName]),
                    item.TableName, item.IdFieldName, item.UrlFieldName, item.TriggerExists))
                .ToArray();
        }

        protected override void ProcessItem(Item item)
        {
            Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"Process with {item.Url}");

            byte[] content;
            if ((content = DownloadContent(item.Url)) == null || content.Length == 0)
                return;

            IUpdater updater;
            if ((updater = UpdaterFactory.GetUpdater(AppConfig, item.TableName)) == null)
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Fatal, $"Invalid tableName: \"{item.TableName}\"");
                return;
            }

            updater.SaveContent(content, item);
        }

        internal byte[] DownloadContent(string url)
        {
            var result = PhotoHostingHelper.HttpDownloadFile(AppConfig, url, out var errorMessage);
            if (!string.IsNullOrWhiteSpace(errorMessage))
                Core.WriteToLog(AppConfig.Logger, LogLevel.Error, nameof(DownloadContent) + errorMessage);

            return result;
        }
    }
}
