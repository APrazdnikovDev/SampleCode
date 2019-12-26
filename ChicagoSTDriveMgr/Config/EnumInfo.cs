namespace ChicagoSTDriveMgr.Config
{
    public class EnumInfo
    {
        public long Id { get; set; }
        public string Value { get; set; }

        public EnumInfo(long id = 0, string value = "")
        {
            Id = id;
            Value = value;
        }
    }
}
