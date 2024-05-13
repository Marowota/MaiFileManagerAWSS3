using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaiFileManager.Classes.Aws
{
    internal class AwsCredentials
    {
        public AwsCredentials() { 
            AwsKey = Preferences.Default.Get("Aws_access_key", "");
            AwsSecretKey = Preferences.Default.Get("Aws_secret_key", "");
        }
        public AwsCredentials(string awsKey, string awsSecretKey)
        {
            AwsKey = awsKey;
            AwsSecretKey = awsSecretKey;
        }
        internal string AwsKey { get; set; }
        internal string AwsSecretKey { get; set; } 
    }
}
