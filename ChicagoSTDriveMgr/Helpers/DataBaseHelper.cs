using System;
using System.Data;
using System.Data.SqlClient;
using ChicagoSTDriveMgr.Config;
using NLog;

namespace ChicagoSTDriveMgr.Helpers
{
    class DataBaseHelper
    {
        public const long SystemId = 0L;

        public const string
            DrPhotoReport = "drphotoreport",
            DrSurvey = "drsurvey",
            DrSurveyPhotos = "drsurveyphotos",
            DrWorkingDayOfAgent = "drworkingdayofagent",
            DrWorkingDayOfAgentPhotos = "drworkingdayofagentphotos",
            DhGoodsReturns = "dhgoodsreturns",
            DrGoodsReturns = "drgoodsreturns",
            DhMerchandises = "dhmerchandises",
            DrMerchandises = "drmerchandises",
            IdPacketFieldName = "idPacket",
            IdParamName = "@id",
            FileNameParamName = "@fileName",
            UriFileNameParamName = "@uriFileName",
            RefGoods = "refgoods",
            RefMarketingMaterials = "refmarketingmaterials",
            RefEmployees = "refphysicalpersons",
            RefPromo = "refpromo",
            RefOutlets = "refoutlets";

        public static bool IsColumnExist(AppConfig appConfig, string tableName, string columnName, out string errorMessage)
        {
            const string
                tableNameParameterName = "@tableName",
                columnNameParameterName = "@columnName";

            var parameters = new[] { new SqlParameter(tableNameParameterName, SqlDbType.NVarChar), new SqlParameter(columnNameParameterName, SqlDbType.NVarChar) };
            
            parameters[0].Value = tableName;
            parameters[1].Value = columnName;

            return ExecuteScalar(appConfig, $"select 1 from sys.columns where object_id = object_id({tableNameParameterName}, N'u') and name = {columnNameParameterName}", parameters, out var result, out errorMessage) == ReturnValue.Ok
                && result != null
                && !Convert.IsDBNull(result)
                && Convert.ToInt32(result) == 1;
        }

        public static ReturnValue ExecuteNonQuery(AppConfig appConfig, string statement, SqlParameter[] parameters, out int result, out string errorMessage)
        {
            result = 0;
            errorMessage = null;
            ReturnValue returnValue;
            var connectionString = appConfig.ConnectionString;
            try
            {
                using (var connection = new SqlConnection(connectionString))
                { 
                    connection.Open();

                    using (var cmd = connection.CreateCommand())
                    { 
                        cmd.CommandTimeout = connection.ConnectionTimeout;
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandText = statement;

                        if (parameters != null && parameters.Length > 0)
                            cmd.Parameters.AddRange(parameters);

                        result = cmd.ExecuteNonQuery();

                    }

                    returnValue = ReturnValue.Ok;
                }
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
                returnValue = e is SqlException ? ReturnValue.SqlError : ReturnValue.UnknownError;
                Core.WriteToLog(appConfig.Logger, LogLevel.Warn, nameof(ExecuteNonQuery) + errorMessage);
                Core.WriteToLog(appConfig.Logger, LogLevel.Warn, statement + errorMessage);
            }
            return returnValue;
        }

        public static ReturnValue ExecuteScalar(AppConfig appConfig, string statement, SqlParameter[] parameters, out object result, out string errorMessage)
        {
            result = null;
            errorMessage = null;
            ReturnValue returnValue;
            var connectionString = appConfig.ConnectionString;
            try
            {
                using (var connection = new SqlConnection(connectionString))
                { 
                    connection.Open();
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandTimeout = connection.ConnectionTimeout;
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandText = statement;

                        if (parameters != null && parameters.Length > 0)
                            cmd.Parameters.AddRange(parameters);

                        result = cmd.ExecuteScalar();

                    }

                    returnValue = ReturnValue.Ok;
                }
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
                returnValue = e is SqlException ? ReturnValue.SqlError : ReturnValue.UnknownError;
                Core.WriteToLog(appConfig.Logger, LogLevel.Warn, nameof(ExecuteScalar) + errorMessage);
                Core.WriteToLog(appConfig.Logger, LogLevel.Warn, nameof(ExecuteScalar) + statement);
            }

            return returnValue;
        }

        public static ReturnValue ExecuteSQL(AppConfig appConfig, string statement, SqlParameter[] parameters, out DataTable result, out string errorMessage)
        {
            result = new DataTable();
            errorMessage = null;
            ReturnValue returnValue;
            var connectionString = appConfig.ConnectionString;
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandTimeout = connection.ConnectionTimeout;
                        cmd.CommandType = CommandType.Text;
                        cmd.CommandText = statement;

                        if (parameters != null && parameters.Length > 0)
                            cmd.Parameters.AddRange(parameters);

                        using (var da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(result);

                        }
                    }
                }
                returnValue = ReturnValue.Ok;
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
                returnValue = e is SqlException ? ReturnValue.SqlError : ReturnValue.UnknownError;
                Core.WriteToLog(appConfig.Logger, LogLevel.Warn, nameof(ExecuteSQL) + errorMessage);
                Core.WriteToLog(appConfig.Logger, LogLevel.Warn, statement);
            }
            return returnValue;
        }

        /// <summary>
        /// по имени таблицы получаем тег для хостинга
        /// </summary>
        /// <param name="tableNameWithDbo"></param>
        /// <returns></returns>
        public static string GetFileTagFromTableName(string tableNameWithDbo)
        {
            // отрабатываем "refGoods" или "dbo.refGoods"
            var test = tableNameWithDbo.Split('.');
            var tableName = test.Length > 1 ? test[1] : test[0];

            switch (tableName.ToLower())
            {
                case DrSurveyPhotos:
                case DrSurvey:
                    return "survey";
                case DrPhotoReport:
                    return "photoreport";
                case DrWorkingDayOfAgent:
                case DrWorkingDayOfAgentPhotos:
                    return "taskfact";
                case RefEmployees:
                    return "employee";
                case RefGoods:
                    return "sku";
                case RefMarketingMaterials:
                    return "marketing";
                case RefPromo:
                    return "promo";
                case RefOutlets:
                    return "outletphoto";
                case DhGoodsReturns:
                case DrGoodsReturns:
                    return "goodsreturn";
                case DhMerchandises:
                case DrMerchandises:
                    return "merchandise";
                default:
                    return string.Empty;
            }
        }
    }
}
