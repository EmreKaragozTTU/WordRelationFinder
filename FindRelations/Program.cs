using HtmlAgilityPack;
using net.zemberek.erisim;
using net.zemberek.tr.yapi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    class Program
    {
        private static Dictionary<string, bool> stopWordsList = new Dictionary<string, bool>();
        private static Zemberek zemberek = new Zemberek(new TurkiyeTurkcesi());

        static void Main(string[] args)
        {
            //deneme();

            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            GetStopWords(path);

            //string filePath = Path.Combine(path, @"SampleData");

            CultureInfo ci = new CultureInfo("tr-TR");
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;

            FileDataContract fileDataContract = new FileDataContract() { DOCS = new List<DataContract>() };

            //  C:\temp\WebLinks    -       C:\Users\malkan\Desktop\WebLinks
            string[] filePaths = Directory.GetFiles(@"C:\Users\malkan\Desktop\WebLinks", "*.html", SearchOption.AllDirectories);
            Console.WriteLine("total file : " + filePaths.Length);

            var stopWatch_loadHtmlFiles = Stopwatch.StartNew();

            Parallel.ForEach(filePaths, file =>
                {
                    try
                    {
                        string htmlPath = file;

                        var v = System.IO.File.ReadAllText(htmlPath);
                        var r = Encoding.UTF8.GetBytes(v);
                        using (var stream = new System.IO.MemoryStream(r))
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                HtmlDocument document = new HtmlDocument();

                                document.LoadHtml(reader.ReadToEnd());

                                IEnumerable<HtmlNode> textNodes = document.DocumentNode.Descendants().Where(n =>
                                    n.NodeType == HtmlNodeType.Text &&
                                    n.ParentNode.Name != "a" &&
                                    !n.InnerText.Trim().Equals(string.Empty) &&
                                    !n.XPath.Contains("/a[") &&
                                    n.ParentNode.Name != "script" &&
                                    n.ParentNode.Name != "style");

                                DataContract dataContract = new DataContract();
                                dataContract.DOCNO = htmlPath;

                                while (textNodes.ToArray().Length > 0)
                                {
                                    var node = textNodes.First();

                                    var innerText = node.InnerText.Trim();

                                    try
                                    {
                                        HtmlNodeCollection siblingNodes = node.ParentNode.ChildNodes;

                                        var includeInternalLink = siblingNodes.Where(a => a.Name == "a").ToArray().Length;

                                        if (includeInternalLink > 1)    //TODO !!
                                            innerText = node.ParentNode.InnerText.Trim();

                                        node.ParentNode.ChildNodes.Clear();
                                    }
                                    catch (Exception exc)
                                    {
                                        Console.WriteLine("\n ** \n" + exc + "\n **");
                                    }

                                    if (!innerText.Equals(string.Empty) && innerText.Contains(" ") && !innerText.Contains("=\""))
                                    {
                                        innerText = innerText.Replace("&#304;", "İ").Replace("&#305;", "ı").Replace("&#214;", "Ö")
                                            .Replace("&#246;", "ö").Replace("&#220;", "Ü").Replace("&#252;", "ü")
                                            .Replace("&#199;", "Ç").Replace("&#231;", "ç").Replace("&#286;", "Ğ")
                                            .Replace("&#287;", "ğ").Replace("&#350;", "Ş").Replace("&#351;", "ş");

                                        dataContract.TEXT += innerText + "\n";
                                    }
                                }
                                fileDataContract.DOCS.Add(dataContract);
                            }
                        }
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine(file + "#" + exc.ToString());
                    }
                });

            stopWatch_loadHtmlFiles.Stop();

            TimeSpan ts = stopWatch_loadHtmlFiles.Elapsed;            
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);

            Console.WriteLine("stopWatch_loadHtmlFiles - toplam zaman : " + elapsedTime + " - dosya sayısı : " + filePaths.Length);

            Console.ReadLine();


            #region Find Related Words

            var stopWatch_findRelatedWords = Stopwatch.StartNew();

            Parallel.ForEach(fileDataContract.DOCS, item =>
                {
                    string[] sentences = item.TEXT.Split(new char[] { '.', '?', '\n', '!' }, StringSplitOptions.RemoveEmptyEntries);
                    item.Sentences = new List<Sentence>();

                    for (int i = 0; i < sentences.Length; i++)
                    {
                        if (sentences[i] == " " || !sentences[i].Contains(" "))
                            continue;

                        var sentence = new Sentence();
                        sentence.sentenceText = sentences[i].Trim();
                        sentence.RelatedWords = RemoveStopwords(sentence.sentenceText);
                        item.Sentences.Add(sentence);
                    }
                });

            stopWatch_findRelatedWords.Stop();

            ts = stopWatch_findRelatedWords.Elapsed;
            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);

            Console.WriteLine("stopWatch_findRelatedWords - toplam zaman : " + elapsedTime + " - dosya sayısı : " + filePaths.Length);

            Console.ReadLine();
          
            #endregion


            #region Write to DB

            int count_relatedWords = 0;
            var stopWatch_writeToDB = Stopwatch.StartNew();

            Parallel.ForEach(fileDataContract.DOCS, item =>
                {
                    var DocumentGUID = Guid.NewGuid();
                    addDB_Documents(DocumentGUID, item.DOCNO);

                    foreach (var currentSentenceItem in item.Sentences)
                    {
                        var SentenceGUID = Guid.NewGuid();
                        addDB_Sentences(SentenceGUID, DocumentGUID, currentSentenceItem.sentenceText);

                        foreach (var myLookupItem in currentSentenceItem.RelatedWords)
                        {
                            foreach (var myLookupValue in myLookupItem)
                            {
                                var RelatedWordsGUID = Guid.Empty;
                                try
                                {
                                    var Count = myLookupItem.Key.Value * myLookupValue.Value;

                                    RelatedWordsGUID = Guid.NewGuid();

                                    if (string.Compare(myLookupItem.Key.Key, myLookupValue.Key) > 0)
                                        addDB_RelatedWords(RelatedWordsGUID, SentenceGUID, myLookupValue.Key, myLookupItem.Key.Key, Count);
                                    else
                                        addDB_RelatedWords(RelatedWordsGUID, SentenceGUID, myLookupItem.Key.Key, myLookupValue.Key, Count);

                                    count_relatedWords++;
                                }
                                catch (Exception exc)
                                {
                                    Console.WriteLine(myLookupItem.Key.Key + " - " + myLookupValue.Key + "#" + exc.ToString());
                                    Console.ReadLine();
                                }
                            }
                        }
                    }
                });

            stopWatch_writeToDB.Stop();

            ts = stopWatch_writeToDB.Elapsed;
            elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);

            Console.WriteLine("stopWatch_writeToDB - toplam zaman : " + elapsedTime + " - dosya sayısı : " + count_relatedWords);

            Console.ReadLine();

            #endregion

        }

        private static void deneme()
        {
            while (true)
            {
                string currentWord = Console.ReadLine();

                string lowerWord = currentWord.ToLower();

                if (!zemberek.kelimeDenetle(lowerWord))
                {
                    try
                    {
                        var asciiSuggestions = zemberek.asciidenTurkceye(lowerWord);
                        if (asciiSuggestions.Any())
                            lowerWord = asciiSuggestions[0];
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine("\n*word*" + lowerWord + "\n##" + exc);
                    }
                }
            }           
        }

        private static Guid controlDB_RelatedWords(string key, string value)
        {
            using (WordPairsEntities context = new WordPairsEntities())
            {
                try
                {
                    var control = context.RelatedWords.Where(entry => (entry.word1 == key) & (entry.word2 == value)).Select(s => s.RelatedWords_ID).First();

                    if (control != null)
                        return control;
                }
                catch(Exception exc)
                {
                    Console.WriteLine(key + " - " + value + " # " + exc.ToString());
                }

                try
                {
                    var control = context.RelatedWords.Where(entry => (entry.word1 == value) & (entry.word2 == key)).Select(s => s.RelatedWords_ID).First();
                  
                    if (control != null)
                        return control;
                }
                catch(Exception exc)
                {
                    Console.WriteLine(value + " - " + key + " # " + exc.ToString());
                }            
            }
            return new Guid();
        }

        private static void addDB_RelatedWords(Guid RelatedWordsGUID, Guid SentenceGUID, string word1, string word2, int Count)
        {
            using (WordPairsEntities context = new WordPairsEntities())
            {
                RelatedWords newEntry = new RelatedWords
                {
                    RelatedWords_ID = RelatedWordsGUID,
                    Sentence_FID = SentenceGUID,
                    word1 = word1,
                    word2 = word2,
                    Count = Count
                };
                context.RelatedWords.Add(newEntry);
                context.SaveChanges();
            }
        }

        private static void addDB_Sentences(Guid SentenceGUID, Guid DocumentGUID, string Sentence)
        {
            using (WordPairsEntities context = new WordPairsEntities())
            {
                Sentences newEntry = new Sentences
                {
                    Sentence_ID = SentenceGUID,
                    Document_FID = DocumentGUID,
                    Sentence = Sentence
                };
                context.Sentences.Add(newEntry);
                context.SaveChanges();
            }
        }

        private static void addDB_Documents(Guid DocumentGUID, string Link)
        {
            using (WordPairsEntities context = new WordPairsEntities())
            {
                Documents newEntry = new Documents
                {
                    Document_ID = DocumentGUID,
                    Link = Link
                };
                context.Documents.Add(newEntry);
                context.SaveChanges();
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

        static char[] _delimiters = new char[] { ' ', ',', ';', ':', '.', '"', '\'', '/', '“', '”', '(', ')', '*', '-', '’' };

        public static ILookup<KeyValuePair<string, int>, KeyValuePair<string, int>> RemoveStopwords(string sentence)
        {                        
            var found = new Dictionary<String, int>();

            var words = sentence.Split(_delimiters, StringSplitOptions.RemoveEmptyEntries);

            foreach (string currentWord in words)
            {
                string lowerWord = currentWord.ToLower();

                if (!zemberek.kelimeDenetle(lowerWord))
                {
                    try
                    {
                        var asciiSuggestions = zemberek.asciidenTurkceye(lowerWord);
                        if (asciiSuggestions.Any())
                            lowerWord = asciiSuggestions[0];
                    }
                    catch (Exception exc)
                    {
                       // Console.WriteLine("\n*sentence*" + sentence + "\n*word*" + lowerWord + "\n##" + exc);
                    }           
                }
                
                var kelimeler = zemberek.kelimeCozumle(lowerWord);
                
                if (kelimeler.Any())
                    lowerWord = kelimeler[0].kok().icerik();

                if (lowerWord.Length > 1)
                {
                    if (!stopWordsList.ContainsKey(lowerWord) && !Regex.IsMatch(lowerWord, @"^\d+$"))
                        if (!found.ContainsKey(lowerWord))
                            found.Add(lowerWord, 1);
                        else
                            found[lowerWord] += 1;
                }              
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
