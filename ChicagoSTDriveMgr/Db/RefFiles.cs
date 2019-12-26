using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using ChicagoSTDriveMgr.Config;
using ChicagoSTDriveMgr.Helpers;

namespace ChicagoSTDriveMgr.Db
{
    class RefFiles
    {
        public const string
            TableName = "reffiles",
            LoadTypeUnknownKey = "unknown",
            LoadTypeSTDriveKey = "stdrive",
            LoadTypeSTReplicationKey = "streplication";

        public const long EmptyValue = -1;

        public static ReturnValue Insert(SqlConnection connection, SqlTransaction transaction, long idDistributor, string fileName, long idMimeType, byte[] body, string uri, /*int priority,*/ long idLoadType, out int returnValue, out long newId, out string errorMessage)
        {
            const string
                returnValueParamName = "@RETURN_VALUE",
                newIdParamName = "@idOut";

            returnValue = 0;
            newId = 0;
            errorMessage = string.Empty;

            ReturnValue result;

            SqlCommand cmd = null;

            try
            {
                cmd = connection.CreateCommand();
                cmd.Transaction = transaction;

                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "sp_executesql";

                cmd.Parameters.Add(returnValueParamName, SqlDbType.Int).Direction = ParameterDirection.ReturnValue;

                cmd.Parameters.Add("@stmt", SqlDbType.NVarChar).Value = @"
set @idOut = dbo.fn_GetIdEx(@idDistributor, 1)
insert into dbo.refFiles (id, FileName, idMimeType, Body, Uri, idLoadType, deleted) values (@idOut, @FileName, @idMimeType, @Body, @Uri, @idLoadType, 0)
";

                cmd.Parameters.Add("@params", SqlDbType.NVarChar).Value = "@idOut bigint output, @idDistributor bigint, @FileName nvarchar(255), @idMimeType bigint, @Body varbinary(max), @Uri nvarchar(2048),  @idLoadType bigint";
                cmd.Parameters.Add("@idDistributor", SqlDbType.BigInt).Value = idDistributor;
                cmd.Parameters.Add("@FileName", SqlDbType.NVarChar).Value = fileName;
                cmd.Parameters.Add("@idMimeType", SqlDbType.BigInt).Value = idMimeType;
                cmd.Parameters.Add("@Body", SqlDbType.VarBinary).Value = body;
                cmd.Parameters.Add("@Uri", SqlDbType.NVarChar).Value = !string.IsNullOrWhiteSpace(uri) ? uri : Convert.DBNull;
                cmd.Parameters.Add("@idLoadType", SqlDbType.BigInt).Value = idLoadType;
                cmd.Parameters.Add(newIdParamName, SqlDbType.BigInt).Direction = ParameterDirection.Output;

                cmd.ExecuteNonQuery();

                if (!Convert.IsDBNull(cmd.Parameters[returnValueParamName].Value))
                    returnValue = Convert.ToInt32(cmd.Parameters[returnValueParamName].Value);
                if (!Convert.IsDBNull(cmd.Parameters[newIdParamName].Value))
                    newId = Convert.ToInt64(cmd.Parameters[newIdParamName].Value);

                result = ReturnValue.Ok;
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
                result = ReturnValue.RefFilePacketsInsertError;
            }
            finally
            {
                cmd?.Dispose();
            }

            return result;
        }

        public static ReturnValue Update(AppConfig appConfig, long id, string fileName, long idMimeType, byte[] body, string uri, long idLoadType, out int returnValue, out string errorMessage)
        {
            const string
                idParameterName = "@id",
                fileNameParameterName = "@FileName",
                idMimeTypeParameterName = "@idMimeType",
                bodyParameterName = "@Body",
                idLoadTypeParameterName = "@idLoadType",
                uriParameterName = "@Uri";

            var sql = @"
update
    dbo.refFiles
set "
+ (!string.IsNullOrWhiteSpace(fileName) ? $"FileName = {fileNameParameterName}," : string.Empty)
+ (idMimeType != EmptyValue ? $"idMimeType = {idMimeTypeParameterName}," : string.Empty)
+ (body != null && body.Length != 0 ? $"Body = {bodyParameterName}," : string.Empty) + $@"
    idLoadType = {idLoadTypeParameterName},
    Uri = {uriParameterName}
where
    id = {idParameterName}
";

            var parameters = new List<SqlParameter>
            {
                new SqlParameter(idParameterName, SqlDbType.BigInt),
                new SqlParameter(idLoadTypeParameterName, SqlDbType.BigInt),
                new SqlParameter(uriParameterName, SqlDbType.NVarChar)
            };

            SqlParameter parameter;

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                parameters.Add(parameter = new SqlParameter(fileNameParameterName, SqlDbType.NVarChar));
                parameter.Value = fileName;
            }

            if (idMimeType != EmptyValue)
            {
                parameters.Add(parameter = new SqlParameter(idMimeTypeParameterName, SqlDbType.BigInt));
                parameter.Value = idMimeType;
            }

            if (body != null && body.Length != 0)
            {
                parameters.Add(parameter = new SqlParameter(bodyParameterName, SqlDbType.VarBinary));
                parameter.Value = body;
            }

            parameters.First(p => p.ParameterName == idParameterName).Value = id;
            parameters.First(p => p.ParameterName == idLoadTypeParameterName).Value = idLoadType;
            parameters.First(p => p.ParameterName == uriParameterName).Value = uri;

            return DataBaseHelper.ExecuteNonQuery(appConfig,  sql, parameters.ToArray(), out returnValue, out errorMessage);
        }
    }
}
