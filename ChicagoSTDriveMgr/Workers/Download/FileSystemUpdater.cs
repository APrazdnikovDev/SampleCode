using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using ChicagoSTDriveMgr.Config;
using ChicagoSTDriveMgr.Db;
using ChicagoSTDriveMgr.Helpers;
using NLog;

namespace ChicagoSTDriveMgr.Workers.Download
{
    class FileSystemUpdater : BaseUpdater
    {
        public FileSystemUpdater(AppConfig appConfig) : base(appConfig)
        {}

        protected override ReturnValue SaveContentCore(byte[] content, Item item)
        {
            ReturnValue result;
            return (result = SaveContentToFile(content, item)) != ReturnValue.Ok ? result : UpdateTable(item);
        }

        ReturnValue SaveContentToFile(byte[] content, Item item)
        {
            ReturnValue result;

            try
            {
                if (File.Exists(item.FullFileName = Path.Combine(AppConfig.DestinationPath, item.FileName)))
                    File.Delete(item.FullFileName);

                File.WriteAllBytes(item.FullFileName, content);

                result = ReturnValue.Ok;
            }
            catch (Exception e)
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Fatal, $"{e.GetType().Name}: \"{e.Message}\" (\"{item.Url}\")");
                result = ReturnValue.WriteContentError;
            }

            return result;
        }

        ReturnValue UpdateTable(Item item, bool updaterefFiles = true, long newidFilePacket = DataBaseHelper.SystemId)
        {
            const string
                filesPacketIdParameterName = "@filesPacketId",
                uriParameterName = "@Uri";
            var errorMessage = string.Empty;
            var idPacket = DataBaseHelper.SystemId;
            if (newidFilePacket == DataBaseHelper.SystemId)
            {
                if (!item.TriggerExists && DataBaseHelper.IsColumnExist(AppConfig, GetTableNameWithIdPacket(item.TableName), DataBaseHelper.IdPacketFieldName, out errorMessage))
                    idPacket = GetIdPacketValue(item.TableName, item.Id);
            }
            else
                idPacket = newidFilePacket;

                var parameters = new List<SqlParameter>
            {
                new SqlParameter(DataBaseHelper.IdParamName, SqlDbType.BigInt),
                new SqlParameter(DataBaseHelper.UriFileNameParamName, SqlDbType.NVarChar),
            };

            parameters.First(parameter => parameter.ParameterName == DataBaseHelper.IdParamName).Value = item.Id;
            parameters.First(parameter => parameter.ParameterName == DataBaseHelper.UriFileNameParamName).Value = item.FullFileName;

            if (idPacket != DataBaseHelper.SystemId)
            {
                SqlParameter parameter;
                parameters.Add(parameter = new SqlParameter(filesPacketIdParameterName, SqlDbType.BigInt));
                parameter.Value = idPacket;

                parameters.Add(parameter = new SqlParameter(DataBaseHelper.FileNameParamName, SqlDbType.NVarChar));
                parameter.Value = item.FileName;

                parameters.Add(parameter = new SqlParameter(uriParameterName, SqlDbType.NVarChar));
                parameter.Value = item.Url;
            }

            var query = 
(idPacket != DataBaseHelper.SystemId ? "begin transaction;" : string.Empty)
+ $"update {item.TableName} set {item.UrlFieldName} = {DataBaseHelper.UriFileNameParamName} where {item.IdFieldName} = {DataBaseHelper.IdParamName};"
+ (idPacket != DataBaseHelper.SystemId && updaterefFiles ? 
$@"update
    files
set
    FileName = {DataBaseHelper.FileNameParamName},
    Uri = {DataBaseHelper.UriFileNameParamName},
    idLoadType = {AppConfig.LoadTypes[RefFiles.LoadTypeSTReplicationKey].Id}
from
    dbo.{RefFiles.TableName} files
    join dbo.{RefFilePacketsFiles.TableName} filePacketsFiles on filePacketsFiles.idFile = files.id
    join dbo.{RefFilePackets.TableName} filePackets on filePackets.id = filePacketsFiles.idPacket
where
    filePackets.id = {filesPacketIdParameterName}
    and files.Uri = {uriParameterName}; ": string.Empty) + 
    " commit transaction;";

            var returnValue = DataBaseHelper.ExecuteNonQuery(AppConfig, query, parameters.ToArray(), out var result, out errorMessage);

            return returnValue;
        }

        public override ReturnValue UpdateOnlyDocTable(Item item, long newidFilePacket = 0)
        {
            return UpdateTable(item, false, newidFilePacket);
        }

        long GetIdPacketValue(string tableName, long id)
        {
            string query;

            if (string.IsNullOrWhiteSpace(query = GetIdPacketQuery(tableName)))
                return DataBaseHelper.SystemId;

            var parameters = new[]
            {
                new SqlParameter(DataBaseHelper.IdParamName, SqlDbType.BigInt)
            };

            parameters.First(parameter => parameter.ParameterName == DataBaseHelper.IdParamName).Value = id;

            var returnValue = DataBaseHelper.ExecuteScalar(AppConfig, query, parameters, out var result, out var errorMessage);

            return returnValue == ReturnValue.Ok && result != null && !Convert.IsDBNull(result) ? Convert.ToInt64(result) : DataBaseHelper.SystemId;
        }

        static string GetIdPacketQuery(string tableName)
        {
            string
                result = string.Empty,
                tableNameOnly = Item.GetTableNameOnly(tableName).ToLower(CultureInfo.InvariantCulture);

            switch (tableNameOnly)
            {
                case DataBaseHelper.DrPhotoReport:
                {
                    result = $@"
select
    {DataBaseHelper.IdPacketFieldName}
from
    dbo.{tableNameOnly}
where
    id = {DataBaseHelper.IdParamName}
";
                    break;
                }
                case DataBaseHelper.DrSurveyPhotos:
                case DataBaseHelper.DrWorkingDayOfAgentPhotos:
                {
                    result = $@"
select
    dr.{DataBaseHelper.IdPacketFieldName}
from
    dbo.{tableNameOnly} drPhotos
    join dbo.{GetTableNameWithIdPacket(tableName)} dr on dr.id = drPhotos.idDocRow
where
    drPhotos.id = {DataBaseHelper.IdParamName}
";
                    break;
                }
            }

            return result;
        }

        static string GetTableNameWithIdPacket(string tableName)
        {
            var result = Item.GetTableNameOnly(tableName).ToLower(CultureInfo.InvariantCulture);

            switch (result)
            {
                case DataBaseHelper.DrSurveyPhotos:
                {
                    result = DataBaseHelper.DrSurvey;
                    break;
                }
                case DataBaseHelper.DrWorkingDayOfAgentPhotos:
                {
                    result = DataBaseHelper.DrWorkingDayOfAgent;
                    break;
                }
            }

            return result;
        }
    }
}
