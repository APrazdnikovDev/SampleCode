using ChicagoSTDriveMgr.Config;
using ChicagoSTDriveMgr.Helpers;

namespace ChicagoSTDriveMgr.Workers.Upload
{
    public class UploadingItem
    {
        public UploadingItem(long fileId, string fileName, EnumInfo mimeType, byte[] body, string tableName)
        {
            id = fileId;
            FileName = fileName;
            MimeType = mimeType;
            Body = body;
            FileTags = DataBaseHelper.GetFileTagFromTableName(tableName);
        }

        public UploadingItem(string fileName)
        {
            FileName = fileName;
        }
        public long  id { get; set; } 
        public string FileName { get; set; }
        public int idMimeType { get; set; }
        public string Url;
        public string ErrorInfo = string.Empty;
        public byte[] Body { get; set; }
        public bool UploadSucsess;
        internal EnumInfo MimeType { get; set; }
        internal EnumInfo LoadType { get; set; }
        public string FileTags { get; }
    }
}
