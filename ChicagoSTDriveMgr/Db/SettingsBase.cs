using System;
using System.Data;
using System.Data.SqlClient;
using ChicagoSTDriveMgr.Config;
using ChicagoSTDriveMgr.Helpers;

namespace ChicagoSTDriveMgr.Db
{
    class SettingsBase
    {
        const string
            TableName = "dbo.SettingsBase",
            CodeKeyFieldName = "CodeKey",
            ValueFieldName = "Value";

        public static string GetCompanyId(AppConfig appConfig, out ReturnValue returnValue, out string errorMessage)
        {
            const string
                codeKeyParameterName = "@codeKey",
                codeKeyCompanyIdValue = "CompanyId";

            var parameters = new[] { new SqlParameter(codeKeyParameterName, SqlDbType.NVarChar) };

            parameters[0].Value = codeKeyCompanyIdValue;

            return (returnValue = DataBaseHelper.ExecuteScalar(appConfig, $"select {ValueFieldName} from {TableName} where {CodeKeyFieldName} = {codeKeyParameterName}", parameters, out var result, out errorMessage)) == ReturnValue.Ok
                && result != null
                && !Convert.IsDBNull(result)
                    ? Convert.ToString(result)
                    : null;
        }
    }
}
