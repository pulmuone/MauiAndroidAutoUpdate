using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MauiAndroidAutoUpdate
{
    public class GlobalSetting
    {

        public GlobalSetting()
        {
            //ApkUri = "http://app.test.com/com.gwise.test.apk"; //웹서버에 MIME형식 추가 (.apk, application/vnd.android.package-archive)
            //ApkVerUri = "http://app.test.com/version.txt";

            ApkUri = "http://219.254.35.79:20000/com.gwise.test.apk";
            ApkVerUri = "http://219.254.35.79:20000/version.txt";
        }

        public static GlobalSetting Instance { get; } = new GlobalSetting();

        public string ApiUri { get; set; }
        //public string ApiKey { get; set; }

        public string ApkUri { get; set; }
        public string ApkVerUri { get; set; }

    }
}
