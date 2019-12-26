using System.Configuration;

namespace ChicagoSTDriveMgr.Config
{

    public class UploadSections : ConfigurationSection
    {
        public const string SectionName = "upload";
        private const string ItemsCollectionName = "items";

        [ConfigurationProperty(ItemsCollectionName)]
        [ConfigurationCollection(typeof(UploadConfigurationElementCollection), AddItemName = "add")]
        public UploadConfigurationElementCollection Items
        {
            get => (UploadConfigurationElementCollection)base[ItemsCollectionName];
            set => base[ItemsCollectionName] = value;
        }
    }

    [ConfigurationCollection(typeof(UploadConfigurationElement))]
    public class UploadConfigurationElementCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new UploadConfigurationElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((UploadConfigurationElement)element).Name;
        }
    }

    public class UploadConfigurationElement : DownloadConfigurationElement
    {
        [ConfigurationProperty("idDistributorFIeldName", DefaultValue = "", IsRequired = false)]
        public string IdDistributorFieldName
        {
            get => (string)base["idDistributorFIeldName"];
            set => base["idDistributorFIeldName"] = value;
        }

        [ConfigurationProperty("idPacketFieldNameFieldName", DefaultValue = "", IsRequired = false)]
        public string IdPacketFieldNameFieldName
        {
            get => (string)base["idPacketFieldNameFieldName"];
            set => base["idPacketFieldNameFieldName"] = value;
        }

    }
}
