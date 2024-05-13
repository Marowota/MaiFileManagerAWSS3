using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaiFileManager.Classes.Aws
{
    internal class S3ResponseDto
    {
        public int StatusCode { get; set; } = 200;
        public string Message { get; set; } = "";
    }
}
