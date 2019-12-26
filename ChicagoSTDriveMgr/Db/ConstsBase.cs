using System;
using System.Data;
using System.Data.SqlClient;
using ChicagoSTDriveMgr.Config;
using ChicagoSTDriveMgr.Helpers;

namespace ChicagoSTDriveMgr.Db
{
    class ConstsBase
    {

        public static string GetPhotoExchangeAddress(AppConfig appConfig, long idDistributor, out string errorMessage, bool useOnlyGlobalConstant = true)
        {

            const string
                photoExchangeAddressCodeKeyValue = "PhotoExchangeAddress",
                codeKeyParameterName = "@codeKey",
                idDistrParameterName = "@idDistr";

            string
                sql = !useOnlyGlobalConstant
                ?
@"
with cte (Value)
as
(
    select
	    Value
    from
	    dbo.ConstsBase constsBase
    where
	    constsBase.CodeKey = @codeKey
	    and constsBase.Deleted = 0
	    and constsBase.IsLocal = 1 
	    and constsBase.idDistr = @idDistr
    union
    select
	    Value
    from
	    dbo.ConstsBase constsBase
    where
	    constsBase.CodeKey = @codeKey
	    and constsBase.Deleted = 0
	    and constsBase.IsLocal = 1
	    and constsBase.idDistr = 0 
    union
    select
	    Value
    from
	    dbo.ConstsBase constsBase
    where
	    constsBase.CodeKey = @codeKey
	    and constsBase.Deleted = 0
	    and constsBase.IsLocal = 0
	    and constsBase.idDistr = 0
)
select top 1
	Value
from
	cte
"
:
@"
select
	Value
from
	dbo.ConstsBase constsBase
where
	constsBase.CodeKey = @codeKey
	and constsBase.Deleted = 0
	and constsBase.IsLocal = 0
	and constsBase.idDistr = 0
"
;

            var parameters = new SqlParameter[useOnlyGlobalConstant ? 1 : 2];

            parameters[0] =new SqlParameter(codeKeyParameterName, SqlDbType.NVarChar)
            { Value = photoExchangeAddressCodeKeyValue};

            if (!useOnlyGlobalConstant)
            {
                parameters[1] = new SqlParameter(idDistrParameterName, SqlDbType.BigInt)
                { Value = idDistributor};
            }

            return DataBaseHelper.ExecuteScalar(appConfig, sql, parameters, out var result,
                       out errorMessage) == ReturnValue.Ok
                   && result != null
                   && !Convert.IsDBNull(result)
                ? Convert.ToString(result).Trim().ToLower()
                : null;
        }

    }
}
