using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using ChicagoSTDriveMgr.Config;
using ChicagoSTDriveMgr.Helpers;
using ChicagoSTDriveMgr.Workers.Download;
using NLog;

namespace ChicagoSTDriveMgr.Workers
{
    abstract class BaseLoader
    {
        protected AppConfig AppConfig;
        protected SqlParameter[] SQLParameters;

        protected BaseLoader(AppConfig appConfig)
        {
            AppConfig = appConfig;
            SQLParameters = null;
        }

        public ReturnValue Run()
        {
            return CheckConditions() != ReturnValue.Ok ? ReturnValue.NotRun : DoOperations();
        }

        protected virtual ReturnValue DoOperations()
        {
            return ReturnValue.Ok;
        }

        protected virtual ReturnValue CheckConditions()
        {
            return ReturnValue.Ok;
        }

        protected void ProcessQuery(ConfigurationElement item)
        {
            if (!(item is DownloadConfigurationElement element))
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Error, "Config error");
                return;
            }
            Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"Process start {element.TableName}");

            if (GetData(element, out var items) != ReturnValue.Ok)
                return;
            if (items.Length == 0)
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Error, "Query return 0 rows");
            }
            else
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"Process start {element.TableName} item {items.Count()} ");
                ProcessItems(items);
            }
            Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"Process finisned");
        }


        protected virtual void ProcessItems(Item[] items)
        {
            Parallel.ForEach(items, ProcessItem);
        }

        protected virtual void ProcessItem(Item item)
        {

        }

        protected virtual ReturnValue GetData(DownloadConfigurationElement item, out Item[] items)
        {
            items = null;
            DataTable dataTable = null;
            ReturnValue result;

            var param = new SqlParameter[SQLParameters.Length];
            for (var i = 0; i < SQLParameters.Length; i++)
            {
                param[i] = new SqlParameter
                {
                    DbType = SQLParameters[i].DbType,
                    ParameterName = SQLParameters[i].ParameterName,
                    Value = SQLParameters[i].Value
                };
            }

            Core.WriteToLog(AppConfig.Logger, LogLevel.Info, $"Query {item.Query} {string.Join(",", param.Select(p => p.ParameterName + " " + p.Value))}");
            try
            {
                if ((result = DataBaseHelper.ExecuteSQL(AppConfig, item.Query, param, out dataTable,
                        out var errorMessage)) != ReturnValue.Ok)
                {
                    Core.WriteToLog(AppConfig.Logger, LogLevel.Error, nameof(GetData) + errorMessage);
                    return result;
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

                items = GetItemsFromQuery(item, dataTable);
            }
            finally
            {
                dataTable?.Dispose();
            }
            return ReturnValue.Ok;
        }

        protected virtual Item[] GetItemsFromQuery(ConfigurationElement baseitem, DataTable dataTable)
        {
            return null;
        }
    }
}
