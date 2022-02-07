using net.zemberek.erisim;
using net.zemberek.tr.yapi;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace FindRelations
{
    class ProgramWithoutDatabase
    {
        private static Dictionary<string, bool> stopWordsList = new Dictionary<string, bool>();
        private static Zemberek zemberek = new Zemberek(new TurkiyeTurkcesi());

        private static RelationsEntities context = new RelationsEntities();

        static void oldMain(string[] args)
        {

            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            GetStopWords(path);

            string filePath = Path.Combine(path, @"SampleData");

            CultureInfo ci = new CultureInfo("tr-TR");
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;

            foreach (var file in System.IO.Directory.GetFiles(filePath))
            {
                try
                {
                    string xmlPath = file;
                    FileDataContract fileDataContract = new FileDataContract() { DOCS = new List<DataContract>() };
                    XmlReaderSettings settings = new XmlReaderSettings();
                    settings.DtdProcessing = DtdProcessing.Parse;
                    settings.ConformanceLevel = System.Xml.ConformanceLevel.Fragment;
                    var v = System.IO.File.ReadAllText(xmlPath).Replace('&', ' ').Replace('\n', ' ');
                    var r = Encoding.UTF8.GetBytes(v);
                    using (var stream = new System.IO.MemoryStream(r))
                    {
                        using (var reader = XmlReader.Create(stream, settings))
                        {
                            reader.ReadToFollowing("DOCNO");

                          //  fileDataContract.DOCNO = reader.ReadElementContentAsString();

                            while (!reader.EOF)
                            {
                                DataContract dataContract = new DataContract();

                                #region hepsiburada için kapalı
                               /* reader.ReadToFollowing("DATE");

                                if (reader.EOF)
                                    break;

                                dataContract.DATE = DateTime.ParseExact(reader.ReadElementContentAsString(), "dd.MM.yyyy", new System.Globalization.CultureInfo("tr-TR"));

                                reader.ReadToFollowing("AUTHOR");
                                dataContract.AUTHOR = reader.ReadElementContentAsString();*/
                                #endregion

                                reader.ReadToFollowing("TEXT");

                                if (reader.EOF)
                                    break;

                                if (reader.NodeType != XmlNodeType.None)
                                {
                                    dataContract.TEXT = reader.ReadElementContentAsString();
                                    fileDataContract.DOCS.Add(dataContract);
                                }
                            }
                        }
                    }

                    foreach (var item in fileDataContract.DOCS)
                    {
                        string[] sentences = item.TEXT.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                        item.Sentences = new List<Sentence>();

                        for (int i = 0; i < sentences.Length; i++)
                        {
                            var sentence = new Sentence();
                            sentence.sentenceText = sentences[i];
                            sentence.RelatedWords = RemoveStopwords(sentence.sentenceText);
                            item.Sentences.Add(sentence);
                        }

                    }

                    #region Write to File
                   // filePath = path + "\\Results\\" + fileDataContract.DOCNO + ".txt";

                    if (System.IO.File.Exists(filePath))
                        System.IO.File.Delete(filePath);

                    StringBuilder sb = new StringBuilder();
                    var AIORelatedWords = new Dictionary<Tuple<string, string>, int>();

                    foreach (var item in fileDataContract.DOCS)
                    {
                        // sb.Append("\n--------yorum---------");
                        foreach (var currentSentenceItem in item.Sentences)
                        {
                            // sb.Append("\n--------cümle---------");
                            foreach (var myLookupItem in currentSentenceItem.RelatedWords)
                            {
                                // sb.Append("\n\nKey: " + myLookupItem.Key);
                                foreach (var myLookupValue in myLookupItem)
                                {
                                    // sb.Append("\nValue: " + myLookupValue);
                                    try
                                    {
                                        if (AIORelatedWords.ContainsKey(new Tuple<string, string>(myLookupItem.Key.Key, myLookupValue.Key)))
                                        {
                                            AIORelatedWords[new Tuple<string, string>(myLookupItem.Key.Key, myLookupValue.Key)] += myLookupItem.Key.Value * myLookupValue.Value;
                                        }

                                        else if (AIORelatedWords.ContainsKey(new Tuple<string, string>(myLookupValue.Key, myLookupItem.Key.Key)))
                                        {
                                            AIORelatedWords[new Tuple<string, string>(myLookupValue.Key, myLookupItem.Key.Key)] += myLookupItem.Key.Value * myLookupValue.Value;
                                        }
                                        else                                  
                                            AIORelatedWords.Add(new Tuple<string, string>(myLookupItem.Key.Key, myLookupValue.Key), myLookupItem.Key.Value * myLookupValue.Value);
                                    }
                                    catch (Exception exc)
                                    {

                                        Console.WriteLine(myLookupItem.Key.Key +" - " + myLookupValue.Key + "#" + exc.ToString());
                                        Console.ReadLine();
                                    }
                                    
                                }
                                // sb.Append("\n----------------------");
                            }
                        }
                        
                    }

                    sb.Append("\n\n--------sonuç---------\n");

                    var sonuc = AIORelatedWords.Where(a => a.Value > 6).OrderBy(b => b.Value);
                    foreach (var item in sonuc)
                        sb.Append("\n" + item.Key + " - " + item.Value);

                    string[] lines = { sb.ToString() };
                    System.IO.File.WriteAllLines(filePath, lines);
                    #endregion


                    
                    

                }
                catch (Exception exc)
                {
                    Console.WriteLine(file + "#" + exc.ToString());
                }
            }
        }

        private static void GetStopWords(string path)
        {
            if (System.IO.File.Exists(path + "\\stopwords.txt"))
            {
                foreach (var item in System.IO.File.ReadAllLines(path + "\\stopwords.txt"))
                {
                    if (!string.IsNullOrEmpty(item) && !string.IsNullOrWhiteSpace(item) && item != Environment.NewLine)
                    {
                        var kelime = item;
                        if (zemberek.kelimeDenetle(kelime))
                        {
                            var kelimeler = zemberek.kelimeCozumle(item);
                            if (kelimeler.Any())                                //TODO  on    ortaya  yapılan     yerine
                                kelime = kelimeler[0].kok().icerik();
                        }

                        if(!stopWordsList.ContainsKey(kelime))
                            stopWordsList.Add(kelime, true);
                    }
                }
            }
        }

        static char[] _delimiters = new char[] { ' ', ',', ';', '.' };

        public static ILookup<KeyValuePair<string, int>, KeyValuePair<string, int>> RemoveStopwords(string sentence)
        {
            var found = new Dictionary<String, int>();

            var words = sentence.Split(_delimiters, StringSplitOptions.RemoveEmptyEntries);

            foreach (string currentWord in words)
            {
                string lowerWord = currentWord.ToLower();

                if (!zemberek.kelimeDenetle(lowerWord))
                {
                    var asciiSuggestions = zemberek.asciidenTurkceye(lowerWord);
                    if (asciiSuggestions.Any())
                        lowerWord = asciiSuggestions[0];
                }
                
                var kelimeler = zemberek.kelimeCozumle(lowerWord);
                
                if (kelimeler.Any())
                    lowerWord = kelimeler[0].kok().icerik();

                if (!stopWordsList.ContainsKey(lowerWord) && !Regex.IsMatch(lowerWord, @"^\d+$"))
                    if (!found.ContainsKey(lowerWord))
                        found.Add(lowerWord, 1);
                    else
                        found[lowerWord] += 1;
            }

            return found.Combinations(2).ToLookup(t => t.ElementAt(0), t => t.ElementAt(1));

            /* var result_ = Enumerable
                 .Range(1, (1 << found.Count) - 1)
                 .Select(index => found.Where((item, idx) => ((1 << idx) & index) != 0).ToList());

             var result = result_.Where(k => k.Count == 2);
             var count_ = result_.Count();
             var count = result.Count();*/          
        }

    }
}
