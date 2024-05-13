using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaiFileManager.Classes
{
    internal class MaiConstants
    {
        public static string HomePath = Path.Combine(FileSystem.Current.CacheDirectory, "view");
    }
}
