using System.IO;
using ChicagoSTDriveMgr.Config;
using ChicagoSTDriveMgr.Helpers;
using NLog;

namespace ChicagoSTDriveMgr.Workers.Download
{
    abstract class BaseUpdater : IUpdater
    {
        protected readonly AppConfig AppConfig;

        protected BaseUpdater(AppConfig appConfig)
        {
            AppConfig = appConfig;
        }

        public ReturnValue SaveContent(byte[] content, Item item)
        {
            if (!Utils.IsImage(content, out var imageSiaze, out var imageFormat, out var fileNameExtension, out var error))
            {
                Core.WriteToLog(AppConfig.Logger, LogLevel.Warn, $"\"{item.Url}\" is not image {error}");
                return ReturnValue.ContentIsNotImage;
            }

            item.FileName = Path.GetFileName(item.Url) + fileNameExtension;

            return SaveContentCore(content, item);
        }

        public virtual ReturnValue UpdateOnlyDocTable(Item item, long newidFilePacket = 0)
        {
            return ReturnValue.Ok;
        }

        protected virtual ReturnValue SaveContentCore(byte[] content, Item item)
        {
            return ReturnValue.Ok;
        }
    }
}
