using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using ChicagoSTDriveMgr.Config;
using ChicagoSTDriveMgr.Helpers;

namespace ChicagoSTDriveMgr.Db
{
    class EnumsBase
    {
        const string
            TableName = "dbo.EnumsBase",
            IdFieldName = "id",
            CodeKeyFieldName = "CodeKey",
            ValueFieldName = "Value";

        public static Dictionary<string, EnumInfo> GetMimeTypes(AppConfig appConfig, out ReturnValue returnValue, out string errorMessage)
        {
            return GetEnumInfos(appConfig, "MimeType!_%", new Regex("(?<=MimeType_).+", RegexOptions.IgnoreCase), out returnValue, out errorMessage);
        }

        public static Dictionary<string, EnumInfo> GetLoadTypes(AppConfig appConfig, out ReturnValue returnValue, out string errorMessage)
        {
            return GetEnumInfos(appConfig, "File!_LoadType!_%", new Regex("(?<=File_LoadType_).+", RegexOptions.IgnoreCase), out returnValue, out errorMessage);
        }

        public static Dictionary<string, EnumInfo> GetEnumInfos(AppConfig appConfig, string codeKeyValue, Regex codeKeyRegex, out ReturnValue returnValue, out string errorMessage)
        {
            const string codeKeyParameterName = "@codeKey";

            var result = new Dictionary<string, EnumInfo>();
            var parameters = new[] { new SqlParameter(codeKeyParameterName, SqlDbType.NVarChar) };

            parameters[0].Value = codeKeyValue;

            if ((returnValue = DataBaseHelper.ExecuteSQL(appConfig,  string.Format("select {0}, {1}, {2} from {3} where {1} like {4} escape N'!'", IdFieldName, CodeKeyFieldName, ValueFieldName, TableName, codeKeyParameterName), parameters, out var data, out errorMessage)) != ReturnValue.Ok)
                return result;

            foreach (DataRow row in data.Rows)
            {
                string
                    codeKey,
                    value;

                if (string.IsNullOrWhiteSpace(codeKey = !row.IsNull(CodeKeyFieldName) ? Convert.ToString(row[CodeKeyFieldName]) : null))
                    continue;

                var match = codeKeyRegex.Match(codeKey);
                if (!match.Success
                    || string.IsNullOrWhiteSpace(codeKey = match.Value.Trim().ToLower())
                    || result.ContainsKey(codeKey)
                    || string.IsNullOrWhiteSpace(value = !row.IsNull(ValueFieldName) ? Convert.ToString(row[ValueFieldName]) : null))
                    continue;

                result[codeKey] = new EnumInfo(Convert.ToInt64(row[IdFieldName]), value);
            }

            return result;
        }

        public static long GetObjectSKUId(AppConfig appConfig, out ReturnValue returnValue, out string errorMessage)
        {
            const string
                codeKeyParameterName = "@codeKey",
                codeKeyCompanyIdValue = "Object_Name_SKU";

            var parameters = new[] { new SqlParameter(codeKeyParameterName, SqlDbType.NVarChar) };

            parameters[0].Value = codeKeyCompanyIdValue;

            return (returnValue = DataBaseHelper.ExecuteScalar(appConfig, 
                       $"select {IdFieldName} from {TableName} where {CodeKeyFieldName} = {codeKeyParameterName}", parameters, out var result, out errorMessage)) == ReturnValue.Ok
                && result != null
                && !Convert.IsDBNull(result)
                    ? Convert.ToInt64(result)
                    : 0;
        }

        public static long AboutSKUSplitField(AppConfig appConfig, out ReturnValue returnValue, out string errorMessage)
        {
            return (returnValue = DataBaseHelper.ExecuteScalar(appConfig,  "SELECT COUNT(*) FROM EnumsBase e left join refSplitFields s ON e.id = s.idField WHERE e.CodeKey LIKE '*SplitField_refGoods_AboutSKUFake*' AND s.deleted = 0", null, out var result, out errorMessage)) == ReturnValue.Ok
                && result != null
                && !Convert.IsDBNull(result)
                    ? Convert.ToInt64(result)
                    : 0;
        }
    }
}
