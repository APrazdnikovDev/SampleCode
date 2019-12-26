using ChicagoSTDriveMgr.Config;
using ChicagoSTDriveMgr.Helpers;
using NLog;


namespace ChicagoSTDriveMgr.Db
{
internal class ClearDoubles
    {
        public static ReturnValue ClearDoublesInRefFiles(AppConfig appConfig, out string errorMessage)
        {
            Core.WriteToLog(appConfig.Logger, LogLevel.Info, $"ClearDoubles: start  exec dbo.sp_SplitDuplicateFiles");
            string sqlscript = @"exec dbo.sp_SplitDuplicateFiles";
            var outputExecuteNonQuery =
                DataBaseHelper.ExecuteNonQuery(appConfig, sqlscript, null, out var  _, out errorMessage);
            if (!string.IsNullOrEmpty(errorMessage))
                Core.WriteToLog(appConfig.Logger, LogLevel.Error, $"Error during exec dbo.sp_SplitDuplicateFiles: " + errorMessage);
            Core.WriteToLog(appConfig.Logger, LogLevel.Info, $"ClearDoubles: end  exec dbo.sp_SplitDuplicateFiles");

            return outputExecuteNonQuery;
        }
    }
}
