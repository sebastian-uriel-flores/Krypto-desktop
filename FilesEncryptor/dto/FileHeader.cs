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
        public string FileName { get; set; }
        public string FileDisplayType { get; set; }

        public override string ToString()
        {
            return string.Format("File Name: {0} - File Extension: {1} - File Display Type: {2}", FileName, FileExtension, FileDisplayType);                
        }
    }
}
