using System.Text.RegularExpressions;
using ChicagoSTDriveMgr.Helpers;

namespace ChicagoSTDriveMgr.Workers.Download
{
    class Item
    {
        public long Id { get; }
        public string Url { get; set; }
        public long IdDistributor { get; }
        public long IdPacket { get; }
        public string TableName { get; }
        public string IdFieldName { get; }
        public string UrlFieldName { get; }
        public bool TriggerExists { get; }
        public string FileName { get; set; }
        public string FullFileName { get; set; }
        public string FileTags { get; }

        public Item(long id, long idDistributor, long idPacket, string url, string tableName, string idFieldName, string urlFieldName, bool triggerExists = false, string fileName = "", string fullFileName = "")
        {
            Id = id;
            IdDistributor = idDistributor;
            IdPacket = idPacket;
            Url = url;
            TableName = tableName;
            IdFieldName = idFieldName;
            UrlFieldName = urlFieldName;
            TriggerExists = triggerExists;
            FileName = fileName;
            FullFileName = fullFileName;
            FileTags = DataBaseHelper.GetFileTagFromTableName(tableName);
        }

        public Item(long id, string url, string tableName, string idFieldName, string urlFieldName, bool triggerExists = false, string fileName = "", string fullFileName = "")
        {
            Id = id;
            Url = url;
            TableName = tableName;
            IdFieldName = idFieldName;
            UrlFieldName = urlFieldName;
            TriggerExists = triggerExists;
            FileName = fileName;
            FullFileName = fullFileName;
            FileTags = DataBaseHelper.GetFileTagFromTableName(tableName);
        }
        readonly static Regex regex = new Regex("(?<=\\.\\[?)[^\\[\\]]+(?=]?)");
        public static string GetTableNameOnly(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                return tableName;

            var match = regex.Match(tableName);
            return match.Success ? match.Value : tableName;
        }
    }
}
