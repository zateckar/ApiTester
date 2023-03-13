using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApiTester
{
    public class ServerCertificate
    {
        public string RequestUri { get; set; }
        public string ValidFrom { get; set; }
        public string ValidTo { get; set; }
        public string Subject { get; set; }
        public string Issuer { get; set; }
        public bool IsValid { get; set; }
    }

    public class Setting
    {       
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Endpoint { get; set; }
        public bool Selected { get; set; }
        public string ProfileName { get; set; }

        public int splitContainer6 { get; set; } = 90;
        public int splitContainer5 { get; set; } = 520;
        public int splitContainer3 { get; set; } = 520;
        public int splitContainer2 { get; set; } = 520;
        public int splitContainer1 { get; set; } = 520;
        public int LocationX { get; set; } = 150;
        public int LocationY { get; set; } = 150;
        public int SizeWidth { get; set; } = 1536;
        public int SizeHeight { get; set; } = 1024;

    }

    public class Session
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string DateTime { get; set; }
        public string UriAbsoluteUri { get; set; }
        public string UriAbsolutePath { get; set; }
        public string UriQuery { get; set; }
        public string UriHost { get; set; }
        public string Method { get; set; }
        public string RequestHeaders { get; set; }
        public string RequestBody { get; set; }
        public string ResponseHeaders { get; set; }
        public string ResponseBody { get; set; }
        public int ResponseStatusCode { get; set; }
        public string ResponseHttpVersion { get; set; }
        public string RequestHttpVersion { get; set; }
        public int ResponseLength { get; set; }
        public int ResponseTime { get; set; }
        public string Note { get; set; }
        public string Application { get; set; }
        public string Group { get; set; }
        public bool ServerCertIsValid { get; set; }
        public string ServerCertValidFrom { get; set; }
        public string ServerCertValidTo { get; set; }
        public string ServerCertSubject { get; set; }
        public string ServerCertIssuer { get; set; }
        public string ClientCertSubject { get; set; }
    }
}
