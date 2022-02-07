using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FindRelations
{
    [Serializable]
    class Sentence
    {
        public string sentenceText { get; set; }

        public ILookup<KeyValuePair<string, int>, KeyValuePair<string, int>> RelatedWords { get; set; }

    }
}
