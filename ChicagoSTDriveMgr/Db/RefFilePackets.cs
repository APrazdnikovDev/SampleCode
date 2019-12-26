using System;
using System.Data;
using System.Data.SqlClient;

namespace ChicagoSTDriveMgr.Db
{
    class RefFilePackets
    {
        public const string
            TableName = "reffilepackets";

        public static ReturnValue Insert(SqlConnection connection, SqlTransaction transaction, long idDistributor, long idBaseObject, out int returnValue, out long newId, out string errorMessage)
        {
            const string
                returnValueParamName = "@RETURN_VALUE",
                newIdParamName = "@idOut";

            returnValue = 0;
            newId = 0;
            errorMessage = string.Empty;

            ReturnValue result;


            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;

                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = "sp_executesql";

                    cmd.Parameters.Add(returnValueParamName, SqlDbType.Int).Direction = ParameterDirection.ReturnValue;

                    cmd.Parameters.Add("@stmt", SqlDbType.NVarChar).Value = @"
set @idOut = dbo.fn_GetIdEx(@idDistributor, 1)
insert into dbo.refFilePackets (id, idBaseObject, deleted) values (@idOut, @idBaseObject, 0)
";

                    cmd.Parameters.Add("@params", SqlDbType.NVarChar).Value = "@idOut bigint output, @idDistributor bigint, @idBaseObject bigint";
                    cmd.Parameters.Add("@idDistributor", SqlDbType.BigInt).Value = idDistributor;
                    cmd.Parameters.Add("@idBaseObject", SqlDbType.BigInt).Value = idBaseObject;
                    cmd.Parameters.Add(newIdParamName, SqlDbType.BigInt).Direction = ParameterDirection.Output;

                    cmd.ExecuteNonQuery();

                    if (!Convert.IsDBNull(cmd.Parameters[returnValueParamName].Value))
                        returnValue = Convert.ToInt32(cmd.Parameters[returnValueParamName].Value);
                    if (!Convert.IsDBNull(cmd.Parameters[newIdParamName].Value))
                        newId = Convert.ToInt64(cmd.Parameters[newIdParamName].Value);
                }
                result = ReturnValue.Ok;
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
                result = ReturnValue.RefFilePacketsInsertError;
            }
            return result;
        }
    }
}
