namespace ChicagoSTDriveMgr.Workers.Download
{
    interface IUpdater
    {
        ReturnValue SaveContent(byte[] content, Item item);
        ReturnValue UpdateOnlyDocTable(Item item, long newidFilePacket = 0);
    }
}
