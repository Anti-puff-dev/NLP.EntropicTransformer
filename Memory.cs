using MySQL;
using NLP.Models;


namespace NLP
{

    public class Memory
    {
        public static string Experiment = "default";
        public static string ExperimentId = "";
        public static string DbTable = "nlp_dataset";
        public static double TrainingRate = 1.1;
        public static double TrainingRateDecay = 1.1;
        public static double MinWeight = 0.0001;
        public static double MaxWeight = 10000;

        public static string DbConnection
        {
            get => MySQL.DbConnection.ConnString;
            set { MySQL.DbConnection.ConnString = value; }
        }

        public static double word_pooling = 1d;
        public static int maxlength = 0;
        public static bool soundex = false;
        public static List<Models.Token> mem_tokens = new List<Models.Token>();
        public static List<Models.CategoryTiny> mem_categories = new List<Models.CategoryTiny>();
        public static List<Models.Queue> mem_queue = new List<Models.Queue>();



        public Memory()
        {

        }

        public static Memory Instance()
        {
            return new Memory();
        }

        public static Memory Instance(double word_pooling, int maxlength, bool sondex = false)
        {
            Memory.word_pooling = word_pooling;
            Memory.maxlength = maxlength;
            Memory.soundex = sondex;
            return new Memory();
        }


        #region Train
        public static void TrainCategory(string text, string[] words, bool IsCategoriesIds = false)
        {
            Models.Token[] word_tokens = null;
            Models.Token[] tokensArr = Tokenize.Instance(word_pooling, maxlength, soundex).Apply(text);
            Models.Token[] tokens = Relevances(Weights(tokensArr));

            int level = 0;
            int acquired = 0;
            int computed = 0;

            foreach (string word in words)
            {
                int? category_id = IsCategoriesIds ? Convert.ToInt32(word) : mem_categories.FirstOrDefault(c => c.name == word)?.category_id;


                /*if (category_id == null && !IsCategoriesIds) {
                    category_id = mem_categories.Count+1;
                    mem_categories.Add(new Models.CategoryTiny() { category_id = (int)category_id, name = word }); 
                }*/
   

                foreach (Models.Token _token in tokens)
                {
                    _token.category_id = (int)category_id;


                    word_tokens = mem_tokens.Where(t => t.word == _token.word && t.experiment_id == ExperimentId && t.level == level).ToArray();


                    //Console.WriteLine($"{mem_tokens.Count()} / {word_tokens.Count()}");

                    acquired += tokens.Count();
                    computed += word_tokens.Count();

                    //Console.SetCursorPosition(0, 1);
                    //Console.WriteLine($"Category: {category_id} / Acquired {acquired} / Computed {computed}                       ");

                    if (word_tokens.Count() > 0)
                    {
                        int maxCount = word_tokens.OrderByDescending(i => i.count).First().count;

                        foreach (Models.Token wtoken in word_tokens)
                        {
                            Models.Token? token = mem_tokens.FirstOrDefault(t => t.word == _token.word && t.category_id == category_id && t.experiment_id == ExperimentId && t.level == level);
                            if (token != null && token.id > 0)
                            {
                                token.count++;
                            }
                            else
                            {
                                token = _token;
                            }

                            int _index = mem_tokens.FindIndex(t => t.word == wtoken.word && t.level == level && t.category_id == category_id && t.experiment_id == ExperimentId);
                            if (_index < 0)
                            {
                                
                                mem_tokens.Add(new Models.Token() { id = mem_tokens.Count+1, experiment_id = ExperimentId, word = _token.word, word_original = _token.word_original, category_id = (int)category_id, level = level, count = _token.count, weight = _token.weight, relevance = _token.relevance });
                            }

                   
                            if (wtoken.category_id == category_id)
                            {
                                double weight = token.weight * (TrainingRate) * (1 + (token.count / 100000));
                                if (weight > MaxWeight) weight = MaxWeight;

                                double relevance = token.relevance * (TrainingRate) * (1 + (token.count / 100000));
                                if (relevance > MaxWeight) relevance = MaxWeight;

                                //int count = token.count + 1;
                                int index = mem_tokens.FindIndex(t => t.word == wtoken.word && t.level == level && t.category_id == category_id && t.experiment_id == ExperimentId);
                                mem_tokens[index].weight = weight;
                                mem_tokens[index].relevance = relevance;
                            }
                            else
                            {
                                double weight = token.weight / (TrainingRateDecay);
                                if (weight < MinWeight) weight = MinWeight;

                                double relevance = token.relevance / (TrainingRateDecay); ;
                                if (relevance < MinWeight) relevance = MinWeight;

                                //int count = token.count + 1;
                                //int index = mem_tokens.FindIndex(t => t.word == wtoken.word && t.level == level);

                                for (int i = 0; i < mem_tokens.Count; i++)
                                {
                                    if (mem_tokens[i].word == wtoken.word && mem_tokens[i].level == level && mem_tokens[i].experiment_id == ExperimentId)
                                    {
                                        mem_tokens[i].weight = weight;
                                        mem_tokens[i].relevance = relevance;
                                    }
                                }


                              

                            }
                        }
                    } 
                    else
                    {
                        int? index = mem_tokens.FindIndex(t => t.experiment_id == ExperimentId && t.word == _token.word && t.category_id == category_id);

                        if (index == null || index < 0)
                        {
                            mem_tokens.Add(new Models.Token() { id = mem_tokens.Count + 1, experiment_id = ExperimentId, word = _token.word, word_original = _token.word_original, category_id = (int)category_id, level = level, count = _token.count, weight = _token.weight, relevance = _token.relevance });
                        }
                        else
                        {
                            mem_tokens[(int)index] = new Models.Token() { id = mem_tokens.Count + 1, experiment_id = ExperimentId, word = _token.word, word_original = _token.word_original, category_id = (int)category_id, level = level, count = _token.count, weight = _token.weight, relevance = _token.relevance };
                        } 
                    }
                }

                level++;
            }
            
        }


        #region Train.Category
        public static void TrainCategoryGroup(string[] texts, string[] words, bool ignore, int epochs = 10, bool IsCategoriesIds = false)
        {
            //Console.WriteLine("Start Training Group...");
            //TrainingRate = 1 + ((TrainingRate - 1) / epochs);

            if (mem_categories.Count == 0) LoadCategories();

            for (int j = 0; j < epochs; j++)
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    if (ignore)
                    {
                        texts[i] = Sanitize.HardApply(texts[i]);
                    }
                    else
                    {
                        texts[i] = Sanitize.Apply(texts[i]);
                    }
                    TrainCategory(texts[i], words, IsCategoriesIds);
                    //mem_queue.Add(new Queue() { texts = texts[i], words = words, is_cat_ids = IsCategoriesIds });
                    //Console.WriteLine($"men_queue {mem_queue.Count()}");
                    
                }
            }

            /*for (int j = 0; j < epochs; j++)
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    tm.AddFunction(Proc, "");
                }
            }*/

        }


        public static bool Proc(string args)
        {
            Queue _queue = null;

            try
            {
                lock(mem_queue) 
                {
                    _queue = mem_queue[0];
                    mem_queue.RemoveAt(0);
                    Thread.Sleep(100);
                }

                if (_queue != null)
                {
                    TrainCategory(_queue.texts, _queue.words, _queue.is_cat_ids);
                }

                if(mem_queue.Count == 0)
                {
                    Save(); 
                }
            } 
            catch(Exception err)
            {
                
            }


            return true;
        }
        #endregion Train.Category
        #endregion Train


        #region Save
        public static void Save()
        {
            Console.WriteLine($"{mem_tokens.Count}");

            /*
            foreach(CategoryTiny cat in mem_categories)
            {

            }
            */

            int c = 1;
            foreach (Token tk in mem_tokens)
            {
                Data.Query($"INSERT INTO {DbTable} (experiment_id, word, word_original, category_id, level, {DbTable}.count, weight, relevance) VALUES (?experiment_id, ?word, ?word_original, ?category_id, ?level, ?count, ?weight, ?relevance)", new string[] { tk.experiment_id, tk.word, tk.word_original, tk.category_id.ToString(), tk.level.ToString(), tk.count.ToString(), tk.weight.ToString().Replace(",","."), tk.relevance.ToString().Replace(",",".") });
                //Console.WriteLine($"INSERT INTO {DbTable} (experiment_id, word, word_original, category_id, level, {DbTable}.count, weight, relevance) VALUES ({tk.experiment_id}, '{tk.word}', '{tk.word_original}', {tk.category_id.ToString()}, {tk.level.ToString()}, {tk.count.ToString()}, {tk.weight.ToString().Replace(",",".")}, {tk.relevance.ToString().Replace(",", ".")})");
                Console.SetCursorPosition(0, 1);
                Console.WriteLine($"{c}/{mem_tokens.Count}");
                Thread.Sleep(1);
                c++;
            }

            Console.WriteLine("Training Finished");
        }
        #endregion Save


        #region Debug 
        public static void Report()
        {
            foreach(Models.Token t in mem_tokens)
            {
                Console.WriteLine($"{t.id} {t.experiment_id} {t.word} {t.word_original} {t.category_id} {t.level} {t.count} {t.weight} {t.relevance}");
            }
        }
        #endregion Debug 


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



        public static void LoadCategories()
        {
            mem_categories = MySQL.Json.Select.Fill(Data.Query($"SELECT category_id, name, experiment_id, level FROM {DbTable}_categories WHERE experiment_id={ExperimentId}", new string[] { })).Multiple<Models.CategoryTiny>().ToList();
        }
        #endregion Functions
    }
}
