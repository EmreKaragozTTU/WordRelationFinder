using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FindRelations
{
    [Serializable]
    class FileDataContract
    {

        public DateTime DATE { get; set; }
        public string USER { get; set; }
        public string KEYWORD { get; set; }

        public List<DataContract> DOCS { get; set; }

    }
}
