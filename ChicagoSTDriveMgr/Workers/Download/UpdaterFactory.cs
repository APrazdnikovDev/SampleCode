using System.Globalization;
using ChicagoSTDriveMgr.Config;
using ChicagoSTDriveMgr.Db;
using ChicagoSTDriveMgr.Helpers;

namespace ChicagoSTDriveMgr.Workers.Download
{
    class UpdaterFactory
    {
        public static IUpdater GetUpdater(AppConfig appConfig, string tableName)
        {
            IUpdater result = null;

            switch (Item.GetTableNameOnly(tableName).ToLower(CultureInfo.InvariantCulture))
            {
                case DataBaseHelper.DrPhotoReport:
                case DataBaseHelper.DrSurveyPhotos:
                case DataBaseHelper.DrWorkingDayOfAgentPhotos:
                {
                    result = new FileSystemUpdater(appConfig);
                    break;
                }
                case RefFiles.TableName:
                {
                    result = new DataBaseUpdater(appConfig);
                    break;
                }
            }

            return result;
        }
    }
}
