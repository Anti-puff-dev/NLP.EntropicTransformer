

using MySQL;
using NLP.Models;
using System;

namespace NLP
{
    public class AutoClassify
    {

        public static string Experiment = "default";
        public static string ExperimentId = "";
        public static string DbTable = "nlp_dataset";
        public static double TrainingRate = 1.1;
        public static double TrainingRateDecay = 1.1;
        public static double MinWeight = 0.0001;
        public static double MaxWeight = 10000;
        public static double MinSimilarity = 0.23;
        public static double MinTokens = 20;

        public static string DbConnection
        {
            get => MySQL.DbConnection.ConnString;
            set { MySQL.DbConnection.ConnString = value; }
        }

        public static double word_pooling = 1d;
        public static int maxlength = 0;
        public static bool soundex = false;
        public static List<Models.Token> mem_tokens = new List<Models.Token>();
        public static List<Models.CategoryAuto> mem_categories = new List<Models.CategoryAuto>();
        public static List<Models.Queue> mem_queue = new List<Models.Queue>();





        public AutoClassify()
        {

        }

        public static AutoClassify Instance()
        {
            return new AutoClassify();
        }

        public static AutoClassify Instance(double word_pooling, int maxlength, bool sondex = false)
        {
            AutoClassify.word_pooling = word_pooling;
            AutoClassify.maxlength = maxlength;
            AutoClassify.soundex = sondex;
            return new AutoClassify();
        }



        #region Train
        public static void TrainAuto(int id, string text, bool ignore)
        {
            if(ignore) {
                text = Sanitize.HardApply(text);
            }
            else
            {
                text = Sanitize.Apply(text);
            }

            Models.Token[] tokensArr = Tokenize.Instance(word_pooling, maxlength, soundex).Apply(text);
            Models.Token[] tokens = Relevances(Weights(tokensArr));


            if(mem_categories.Count == 0) {
                List<int> ids = new List<int>();
                ids.Add(id);
                mem_categories.Add(new Models.CategoryAuto() { category_id = mem_categories.Count+1, name = Guid.NewGuid().ToString(), tokens = tokens.ToList(), ids = ids });
            } 
            else if(tokens.Length >= MinTokens)
            {
                int _max_cat_id = 0;
                double _max_similarity = 0d;


                foreach(Models.CategoryAuto cat in mem_categories)
                {
                    double _similarity = Similarity(tokens, cat.tokens.ToArray());
                    //Console.WriteLine(_similarity);
                    if(_similarity > MinSimilarity)
                    {
                        if(_similarity > _max_similarity)
                        {
                            _max_cat_id = cat.category_id;
                            _max_similarity = _similarity;
                        }
                    }
                }

                if(_max_cat_id > 0)
                {
                    int index = mem_categories.FindIndex(t => t.category_id == _max_cat_id);
                    mem_categories[index].ids.Add(id);
                    CategoriesJoin(tokens, index);
                } 
                else
                {
                    List<int> ids = new List<int>();
                    ids.Add(id);
                    mem_categories.Add(new Models.CategoryAuto() { category_id =  mem_categories.Count + 1, name = Guid.NewGuid().ToString(), tokens = tokens.ToList(), ids = ids });
                }
            }

            Console.SetCursorPosition(0, 0);
            Console.WriteLine($"Categories Count: {mem_categories.Count}                      ");
        }
        #endregion Train



        #region Save
        public static void Save(string table, string field_name, string field_id, int start_index = 0)
        {
            int c = 1;
            foreach (Models.CategoryAuto ca in mem_categories)
            {
                foreach(int id in ca.ids)
                {
                    Data.Query($"UPDATE {table} SET {field_id}={(ca.category_id+ start_index)}, {field_name}='{ca.name}' WHERE id={id}");
                }
                c++;
            }
        }


        public static void Clear()
        {
            mem_categories.Clear();
        }
        #endregion Save



        #region Functions
        public static Models.Token[] Weights(Models.Token[] tokens)
        {
            int maxCount = tokens.OrderByDescending(i => i.count).First().count;

            for (int i = 0; i < tokens.Length; i++)
            {
                tokens[i].weight = ((double)tokens[i].count / (1 + (double)maxCount));
            }

            return tokens;
        }


        public static Models.Token[] Relevances(Models.Token[] tokens, int maxDistance = 10)
        {
            Models.Token[] _tokens = tokens.OrderByDescending(i => i.weight).ToArray();
            int[] position = new int[5];

            for (int i = 0; i < _tokens.Length; i++)
            {


                double sum = 0d;
                for (int j = 0; j < position.Length; j++)
                {
                    try
                    {
                        position[j] = tokens.IndexOf(item => item.word.Equals(_tokens[i + j].word));
                    }
                    catch (Exception ex)
                    {
                        position[j] = 0;
                    }


                    if (j > 0 && position[j] > -1)
                    {
                        int distance = Math.Abs(position[j] - position[0]);
                        sum += 1d / (double)(Math.Pow(j * 2, 2) + Math.Pow(distance, 2));
                    }
                }

                _tokens[i].relevance = (double)_tokens[i].weight * sum;
            }
            return _tokens;
        }


        public static int IntersectCount(Models.Token[] arr1, Models.Token[] arr2)
        {
            int c = 0;

            List<Models.Token> result = new List<Models.Token>();
            foreach (Models.Token token1 in arr1)
            {
                foreach (Models.Token token2 in arr2)
                {
                    if (token1.word == token2.word)
                    {
                        c++;
                    }
                }
            }

            return c;
        }


        public static double Similarity(Models.Token[] arr1, Models.Token[] arr2)
        {
            int intersect = IntersectCount(arr1, arr2);
            return (2d * (double)intersect) / (double)(arr1.Length + arr2.Length);
        }

        public static void CategoriesJoin(Models.Token[] tokens, int index)
        {
            List<Models.Token> _l = mem_categories[index].tokens.Union(tokens.ToList()).ToList();
            mem_categories[index].tokens = _l;
        }
        #endregion Functions
    }
}
