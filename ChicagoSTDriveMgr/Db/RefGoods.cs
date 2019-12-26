using System;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace ChicagoSTDriveMgr.Db
{
    class RefGoods
    {
        public static ReturnValue Update(SqlConnection connection, SqlTransaction transaction, long id, string htmlBody, long idPacket, out int returnValue, out string errorMessage)
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
                    cmd.CommandText = @"
update
    dbo.refGoods
set
    ImageFileName = @ImageFileName,
    ImageBody = @ImageBody,
    ImageCRC = @ImageCRC,
    HtmlBody = @HtmlBody,
    HtmlCRC = @HtmlCRC,
    idPacket = @idPacket
where
    id = @id
";

                    cmd.Parameters.Add("@id", SqlDbType.BigInt).Value = id;
                    cmd.Parameters.Add("@ImageFileName", SqlDbType.NVarChar).Value = string.Empty;
                    cmd.Parameters.Add("@ImageBody", SqlDbType.VarBinary).Value = Array.Empty<byte>();
                    cmd.Parameters.Add("@ImageCRC", SqlDbType.BigInt).Value = 0;
                    cmd.Parameters.Add("@HtmlBody", SqlDbType.NVarChar).Value = htmlBody;
                    cmd.Parameters.Add("@HtmlCRC", SqlDbType.BigInt).Value = 0;
                    cmd.Parameters.Add("@idPacket", SqlDbType.BigInt).Value = idPacket;

                    returnValue = cmd.ExecuteNonQuery();

                    result = ReturnValue.Ok;
                }
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
                result = ReturnValue.RefFilePacketsInsertError;
            }
            return result;
        }

        public static string RemoveImgFromBody(string body)
        {
            return Regex.Replace(body, "<\\s*img.+?>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        }
    }
}
