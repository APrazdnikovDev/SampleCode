using System.Configuration;

namespace ChicagoSTDriveMgr.Config
{
    public class DownloadSections : ConfigurationSection
    {
        public const string SectionName = "download";
        private const string ItemsCollectionName = "items";

        [ConfigurationProperty(ItemsCollectionName)]
        [ConfigurationCollection(typeof(DownloadConfigurationElementCollection), AddItemName = "add")]
        public DownloadConfigurationElementCollection Items
        {
            get => (DownloadConfigurationElementCollection)base[ItemsCollectionName];
            set => base[ItemsCollectionName] = value;
        }
    }

    [ConfigurationCollection(typeof(DownloadConfigurationElement))]
    public class DownloadConfigurationElementCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new DownloadConfigurationElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((DownloadConfigurationElement)element).Name;
        }
    }

    public class DownloadConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("name", DefaultValue = "", IsKey = true, IsRequired = true)]
        public string Name
        {
            get => (string)base["name"];
            set => base["name"] = value;
        }

        [ConfigurationProperty("query", DefaultValue = "", IsRequired = true)]
        public string Query
        {
            get => (string)base["query"];
            set => base["query"] = value;
        }

        [ConfigurationProperty("idFieldName", DefaultValue = "", IsRequired = true)]
        public string IdFieldName
        {
            get => (string)base["idFieldName"];
            set => base["idFieldName"] = value;
        }

        [ConfigurationProperty("urlFieldName", DefaultValue = "", IsRequired = true)]
        public string UrlFieldName
        {
            get => (string)base["urlFieldName"];
            set => base["urlFieldName"] = value;
        }

        [ConfigurationProperty("tableName", DefaultValue = "", IsRequired = true)]
        public string TableName
        {
            get => (string)base["tableName"];
            set => base["tableName"] = value;
        }

        [ConfigurationProperty("triggerExists", DefaultValue = false)]
        public bool TriggerExists
        {
            get => (bool)base["triggerExists"];
            set => base["triggerExists"] = value;
        }
    }
}
