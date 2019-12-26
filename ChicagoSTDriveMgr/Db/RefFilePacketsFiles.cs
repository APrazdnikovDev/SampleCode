using System;
using System.Data;
using System.Data.SqlClient;

namespace ChicagoSTDriveMgr.Db
{
    class RefFilePacketsFiles
    {
        public const string
            TableName = "reffilepacketsfiles";

        public static ReturnValue Insert(SqlConnection connection, SqlTransaction transaction, long idDistributor, long idPacket, long idFile, out int returnValue, out string errorMessage)
        {
            returnValue = 0;
            errorMessage = string.Empty;
            ReturnValue result;
            try
            {
                using (var cmd = connection.CreateCommand())
                { 
                    cmd.Transaction = transaction;

                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = "insert into dbo.refFilePacketsFiles (id, idPacket, idFile, Internal, deleted, Priority) values (dbo.fn_GetIdEx(@idDistributor, 1), @idPacket, @idFile, @Internal, 0, @Priority)";

                    cmd.Parameters.Add("@idDistributor", SqlDbType.BigInt).Value = idDistributor;
                    cmd.Parameters.Add("@idPacket", SqlDbType.BigInt).Value = idPacket;
                    cmd.Parameters.Add("@idFile", SqlDbType.BigInt).Value = idFile;
                    cmd.Parameters.Add("@Internal", SqlDbType.Bit).Value = false;
                    cmd.Parameters.Add("@Priority", SqlDbType.TinyInt).Value = 1;

                    returnValue = cmd.ExecuteNonQuery();
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
