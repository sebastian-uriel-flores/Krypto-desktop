using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace FilesEncryptor.helpers
{
    public abstract class BaseCodifier
    {   
        public abstract Task<bool> ReadFileContent(FilesHelper filesHelper);
    }
}
