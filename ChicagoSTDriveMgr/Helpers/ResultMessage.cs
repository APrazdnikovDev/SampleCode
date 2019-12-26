using System.Net;

namespace ChicagoSTDriveMgr.Helpers
{
    public class ResultMessage
    {
        public bool Result { get; set; }
        public string DestFileName { get; set; }
        public string ErrorMessage { get; set; }
        public HttpStatusCode StatusCode { get; set; }

    }
}