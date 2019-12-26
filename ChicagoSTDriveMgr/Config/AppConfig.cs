using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using ChicagoSTDriveMgr.Db;
using ChicagoSTDriveMgr.Helpers;
using NLog;

namespace ChicagoSTDriveMgr.Config
{
    public class AppConfig
    {
        private const string
            ConnectionStringKey = "chicago",
            QueryGoodsKey = "queryGoods",
            QueryGoodsWithoutIdGoodsCOKey = "queryGoodsWithoutIdGoodsCO",
            QueryGoodsWithIdGoodsCOKey = "queryGoodsWithIdGoodsCO",
            QueryFilesKey = "queryFiles",
            QueryFilesByIdPacketKey = "queryFilesByIdPacket",
            QueryFilesByIdKey = "queryFilesById",
            QueryMarketingMaterialsKey = "queryMarketingMaterials";

        public Logger Logger { get; }
        public string ConnectionString { get; private set; }
        public string QueryGoods { get; private set; }
        public string QueryGoodsWithIdGoodsCO { get; private set; }
        public string QueryGoodsWithoutIdGoodsCO { get; private set; }
        public string QueryFiles { get; private set; }
        public string QueryMarketingMaterials { get; private set; }
        public bool ProcessRefGoods { get; set; }
        public bool ProcessRefFiles { get; private set; }
        public Dictionary<long, string> PhotoHostingUri { get; }
        public string CompanyId { get; private set; }
        public Dictionary<string, EnumInfo> MimeTypes { get; private set; }
        public Dictionary<string, EnumInfo> LoadTypes { get; private set; }
        public long ObjectSKUId { get; private set; }
        public bool MoveRefFilesToFileHosting { get; set; }
        public bool IsAboutSKUSplitField { get; set; }
        public bool Download { get; private set; }
        public string DestinationPath { get; set; }
        public DownloadSections DownloadSections { get; private set; }
        public bool Upload { get; set; }
        public bool UploadMM { get; set; }
        public bool UploadPromo { get; set; }
        public bool ClearDoubles { get; set; }
        public UploadSections UploadSections { get; private set; }
        public string QueryFilesByIdPacket { get; private set; }
        public string QueryFilesById { get; private set; }
        public DateTime DateBegin { get; set; }
        public DateTime DateEnd { get; set; }
        public int CountRows { get; set; }

        public AppConfig()
        {
            Logger = LogManager.GetCurrentClassLogger();
            ConnectionString = QueryGoods = QueryGoodsWithIdGoodsCO = QueryGoodsWithoutIdGoodsCO = QueryFiles = CompanyId = null;
            ProcessRefGoods = ProcessRefFiles = MoveRefFilesToFileHosting = false;
            PhotoHostingUri = new Dictionary<long, string>();
            MimeTypes = new Dictionary<string, EnumInfo>();
            LoadTypes = new Dictionary<string, EnumInfo>();
            ObjectSKUId = 0;
        }

        public void Load()
        {
            if (ConfigurationManager.ConnectionStrings.OfType<ConnectionStringSettings>().Any(cs => cs.Name == ConnectionStringKey))
                ConnectionString = ConfigurationManager.ConnectionStrings[ConnectionStringKey].ConnectionString;

            QueryGoods = ConfigurationManager.AppSettings[QueryGoodsKey];
            QueryGoodsWithoutIdGoodsCO = ConfigurationManager.AppSettings[QueryGoodsWithoutIdGoodsCOKey];
            QueryGoodsWithIdGoodsCO = ConfigurationManager.AppSettings[QueryGoodsWithIdGoodsCOKey];
            QueryFiles = ConfigurationManager.AppSettings[QueryFilesKey];

            DownloadSections = ConfigurationManager.GetSection(DownloadSections.SectionName) as DownloadSections;

            UploadSections = ConfigurationManager.GetSection(UploadSections.SectionName) as UploadSections;
            QueryFilesByIdPacket = ConfigurationManager.AppSettings[QueryFilesByIdPacketKey];
            QueryFilesById = ConfigurationManager.AppSettings[QueryFilesByIdKey];
            QueryMarketingMaterials = ConfigurationManager.AppSettings[QueryMarketingMaterialsKey];
        }

        public ReturnValue Configure()
        {
            ReturnValue result;

            if (Download = !string.IsNullOrWhiteSpace(DestinationPath))
                return SetLoadTypes();

            SetProcessRefGoods();
            SetProcessRefFiles();

            if ((result = SetCompanyId()) != ReturnValue.Ok
                || (result = SetMimeTypes()) != ReturnValue.Ok
                || (result = SetLoadTypes()) != ReturnValue.Ok
                || (result = SetObjectSKUId()) != ReturnValue.Ok)
                return result;

            return result;
        }

        void SetProcessRefGoods()
        {
            var requiredColumn = new[] { Core.RefGoodsImageFileNameFieldName, Core.RefGoodsImageBodyFieldName, Core.RefGoodsHtmlBodyFieldName, Core.RefGoodsIdPacketFieldName };
            var allRequiredColumnExists = requiredColumn.Aggregate(true, (current, item) => current & IsColumnExist(Core.RefGoodsTableName, item));

            ProcessRefGoods = allRequiredColumnExists;
        }

        void SetProcessRefFiles()
        {
            var requiredColumn = new[] { Core.RefFilesUriFieldName, /*Core.RefFilesPriorityFieldName,*/ Core.RefFilesIdLoadTypeFieldName };
            var allRequiredColumnExists = requiredColumn.Aggregate(true, (current, item) => current & IsColumnExist(Core.RefFilesTableName, item));

            ProcessRefFiles = allRequiredColumnExists;
        }

        bool IsColumnExist(string tableName, string columnName)
        {
            if (DataBaseHelper.IsColumnExist(this, tableName, columnName, out var errorMessage))
                return true;

            Core.WriteToLog(Logger, LogLevel.Warn, !string.IsNullOrWhiteSpace(errorMessage) ? nameof(IsColumnExist)+errorMessage : $"Column \"{tableName}.{columnName}\" doesn't exist");

            return false;
        }

        ReturnValue SetCompanyId()
        {
            CompanyId = Db.SettingsBase.GetCompanyId(this, out var result, out var errorMessage);

            if (!string.IsNullOrWhiteSpace(errorMessage))
                Core.WriteToLog(Logger, LogLevel.Warn, nameof(SetCompanyId)+errorMessage);

            return result;
        }

        ReturnValue SetMimeTypes()
        {
            MimeTypes = EnumsBase.GetMimeTypes(this, out var result, out var errorMessage);

            if (!string.IsNullOrWhiteSpace(errorMessage))
                Core.WriteToLog(Logger, LogLevel.Error, nameof(SetMimeTypes) + errorMessage);

            return result;
        }
        ReturnValue SetLoadTypes()
        {
            LoadTypes = EnumsBase.GetLoadTypes(this, out var result, out var errorMessage);

            if (!string.IsNullOrWhiteSpace(errorMessage))
                Core.WriteToLog(Logger, LogLevel.Error, nameof(SetMimeTypes) + errorMessage);

            return result;
        }

        ReturnValue SetObjectSKUId()
        {
            ObjectSKUId = EnumsBase.GetObjectSKUId(this, out var result, out var errorMessage);

            if (!string.IsNullOrWhiteSpace(errorMessage))
                Core.WriteToLog(Logger, LogLevel.Warn, nameof(SetObjectSKUId) + errorMessage);

            return result;
        }
    }
}
