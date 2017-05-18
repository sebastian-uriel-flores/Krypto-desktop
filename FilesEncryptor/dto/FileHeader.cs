using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilesEncryptor.dto
{
    public class FileHeader
    {
        public string FileExtension { get; set; }
        public uint FileExtensionLength { get; set; }

        public string FileName { get; set; }
        public uint FileNameLength { get; set; }

        public string FileDisplayType { get; set; }
        public uint FileDisplayTypeLength { get; set; }
    }
}
