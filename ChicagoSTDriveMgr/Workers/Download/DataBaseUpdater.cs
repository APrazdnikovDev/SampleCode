using ChicagoSTDriveMgr.Config;
using ChicagoSTDriveMgr.Db;

namespace ChicagoSTDriveMgr.Workers.Download
{
    class DataBaseUpdater : BaseUpdater
    {
        public DataBaseUpdater(AppConfig appConfig) : base(appConfig)
        {}

        protected override ReturnValue SaveContentCore(byte[] content, Item item)
        {
            return RefFiles.Update(AppConfig, item.Id, string.Empty, RefFiles.EmptyValue, content, string.Empty, AppConfig.LoadTypes[RefFiles.LoadTypeSTReplicationKey].Id, out var returnValue, out var errorMessage);
        }
    }
}
