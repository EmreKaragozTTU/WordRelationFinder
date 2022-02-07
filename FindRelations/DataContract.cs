using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FindRelations
{
    [Serializable]
    class DataContract
    {
        public string DOCNO { get; set; }
        public string TEXT { get; set; }

        public List<Sentence> Sentences { get; set; }
    }
}
