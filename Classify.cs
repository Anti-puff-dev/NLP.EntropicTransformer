using MySQL;
using Newtonsoft.Json.Linq;
using NLP.Models;
using System.Data;
using System.Linq;
using System.Reflection;


namespace NLP
{
    public static class Extensions
    {
        public static int IndexOf<T>(this IEnumerable<T> list, Predicate<T> condition)
        {
            int i = -1;
            return list.Any(x => { i++; return condition(x); }) ? i : -1;
        }

        public static IEnumerable<TSource> DistinctBy<TSource>(this IEnumerable<TSource> source, params Func<TSource, object>[] keySelectors)
        {
            // initialize the table
            var seenKeysTable = keySelectors.ToDictionary(x => x, x => new HashSet<object>());

            // loop through each element in source
            foreach (var element in source)
            {
                // initialize the flag to true
                var flag = true;

                // loop through each keySelector a
                foreach (var (keySelector, hashSet) in seenKeysTable)
                {
                    // if all conditions are true
                    flag = flag && hashSet.Add(keySelector(element));
                }

                // if no duplicate key was added to table, then yield the list element
                if (flag)
                {
                    yield return element;
                }
            }
        }
    }


    struct TokenTensor
    {
        public string word;
        public int category_id;
        public int level;
        public double weigth;
        public double relevance;
        public int count;
    }


    public class Classify
    {
        public static string Experiment = "default";
        public static string ExperimentId = "";
        public static string DbTable = "nlp_dataset";
        public static double TrainingRate = 1.1;
        public static double TrainingRateDecay = 1.1;
        public static double MinWeight = 0.001;
        public static double MaxWeight = 10000;
        static TokenTensor[] tensors;



        public static string DbConnection
        {
            get => MySQL.DbConnection.ConnString;
            set { MySQL.DbConnection.ConnString = value; }
        }

        public static double word_pooling = 1d;
        public static int maxlength = 0;
        public static bool soundex = false;
     


        public Classify()
        {

        }

        public static Classify Instance()
        {
            return new Classify();
        }

        public static Classify Instance(double word_pooling, int maxlength, bool sondex = false)
        {
            Classify.word_pooling = word_pooling;
            Classify.maxlength = maxlength;
            Classify.soundex = sondex;

            DataSet ds = Data.Query($"SELECT nlp_dataset.word, nlp_dataset.category_id, nlp_dataset.level, nlp_dataset.weight, nlp_dataset.relevance, nlp_dataset.count FROM nlp_dataset WHERE nlp_dataset.experiment_id={ExperimentId} AND nlp_dataset.weight>{MinWeight.ToString().Replace(",", ".")} ORDER BY nlp_dataset.word ASC, nlp_dataset.weight DESC", new string[] { });

            tensors = new TokenTensor[ds.Tables[0].Rows.Count];

            int i = 0;
            foreach (DataRow row in ds.Tables[0].Rows)
            {
                tensors[i].word = row[0].ToString();
                tensors[i].category_id = Convert.ToInt32(row[1]);
                tensors[i].level = Convert.ToInt32(row[2]);
                tensors[i].weigth = Convert.ToDouble(row[3]);
                tensors[i].relevance = Convert.ToDouble(row[4]);
                tensors[i].count = Convert.ToInt32(row[5]);
                i++;
            }


            return new Classify();
        }



        #region Train
        #region Train.Category

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
                int category_id = IsCategoriesIds ? Convert.ToInt32(word) : Convert.ToInt32(Data.Query($"SELECT category_id FROM {DbTable}_categories WHERE name=?name", new string[] { word }).Tables[0].Rows[0][0]);

                foreach (Models.Token _token in tokens)
                {
                    _token.category_id = category_id;
                    word_tokens = MySQL.Json.Select.Fill($"SELECT * FROM {DbTable} WHERE word=?word AND experiment_id=?experiment_id AND level=?level AND weight>{MinWeight} ORDER BY word ASC", new string[] { _token.word, ExperimentId, level.ToString() }).Multiple<Models.Token>();
                    //word_tokens = MySQL.Json.Select.Fill($"SELECT * FROM {DbTable} WHERE word=?word AND experiment_id=?experiment_id ORDER BY word ASC", new string[] { token.word, ExperimentId }).Multiple<Models.Token>();


                    acquired += tokens.Count();
                    computed += word_tokens.Count();

                    //Console.SetCursorPosition(0, 1);
                    //Console.WriteLine($"Category: {category_id} / Acquired {acquired} / Computed {computed}                       ");

                    if (word_tokens.Count() > 0)
                    {
                        int maxCount = word_tokens.OrderByDescending(i => i.count).First().count;

                        foreach (Models.Token wtoken in word_tokens)
                        {
                            string query = "";
                            List<string> parms = new List<string>();
                            int c = 0;

                            Models.Token token = MySQL.Json.Select.Fill(Data.Query($"SELECT * FROM {DbTable} WHERE word=?word AND category_id=?category_id AND experiment_id=?experiment_id AND level=?level ORDER BY word ASC LIMIT 1", new string[] { _token.word, category_id.ToString(), ExperimentId, level.ToString() })).Single<Models.Token>();
                            if (token.id > 0)
                            {
                                Data.Query($"UPDATE {DbTable} SET {DbTable}.count={DbTable}.count+1 WHERE id=?id", new string[] { token.id.ToString() });
                            }
                            else
                            {
                                token = _token;
                            }

                            string query_insert = $"INSERT IGNORE INTO {DbTable} (experiment_id, word, word_original, category_id, level, {DbTable}.count, weight, relevance) VALUES (?experiment_i{c}, ?word_i{c}, ?word_original_i{c}, ?category_i{c}, ?level{c}, ?count_i{c}, ?weight_i{c}, ?relevance_i{c})";
                            List<string> parms_insert = new List<string>();
                            parms_insert.Add(ExperimentId);
                            parms_insert.Add(_token.word);
                            parms_insert.Add(_token.word_original);
                            parms_insert.Add(category_id.ToString());
                            parms_insert.Add(level.ToString());
                            parms_insert.Add(_token.count.ToString());
                            parms_insert.Add(_token.weight.ToString().Replace(",", "."));
                            parms_insert.Add(_token.relevance.ToString().Replace(",", "."));
                            Data.Query(query_insert, parms_insert.ToArray());


                            if (wtoken.category_id == category_id)
                            {
                                double weight = token.weight * (TrainingRate) * (1 + (token.count / 1000));
                                if(weight > MaxWeight) weight = MaxWeight;

                                double relevance = token.relevance * (TrainingRate) * (1 + (token.count / 1000));
                                if (relevance > MaxWeight) relevance = MaxWeight;

                                int count = token.count + 1;
                                query += $"UPDATE {DbTable} SET weight=?weight{c}, relevance=?relevance{c} WHERE word=?word{c} AND level={level} AND category_id={category_id};";
                                parms.Add(weight.ToString().Replace(",", "."));
                                parms.Add(relevance.ToString().Replace(",", "."));
                                parms.Add(wtoken.word);
                            }
                            else
                            {
                                double weight = token.weight / (TrainingRateDecay);
                                if (weight < MinWeight) weight = MinWeight;

                                double relevance = token.relevance / (TrainingRateDecay);;
                                if (relevance < MinWeight) relevance = MinWeight;


                                int count = token.count + 1;
                                query += $"UPDATE {DbTable} SET weight=?weight{c}, relevance=?relevance{c} WHERE word=?word{c} AND level={level};";
                                parms.Add(weight.ToString().Replace(",", "."));
                                parms.Add(relevance.ToString().Replace(",", "."));
                                parms.Add(wtoken.word);

                            }
                            c++;


                            if (query != "") Data.Query(query, parms.ToArray());

                        }
                    }
                    else
                    {
                        string query = "";
                        List<string> parms = new List<string>();
                        int c = 0;

                        query += $"INSERT IGNORE INTO {DbTable} (experiment_id, word, word_original, category_id, level, {DbTable}.count, weight, relevance) VALUES (?experiment_i{c}, ?word_i{c}, ?word_original_i{c}, ?category_i{c}, ?level{c}, ?count_i{c}, ?weight_i{c}, ?relevance_i{c}) ON DUPLICATE KEY UPDATE {DbTable}.count=?count_u{c}, weight=?weight_u{c}, relevance=?relevance_u{c};";
                        parms.Add(ExperimentId);
                        parms.Add(_token.word);
                        parms.Add(_token.word_original);
                        parms.Add(category_id.ToString());
                        parms.Add(level.ToString());
                        parms.Add(_token.count.ToString());
                        parms.Add(_token.weight.ToString().Replace(",", "."));
                        parms.Add(_token.relevance.ToString().Replace(",", "."));
                        parms.Add(_token.count.ToString());
                        parms.Add(_token.weight.ToString().Replace(",", "."));
                        parms.Add(_token.relevance.ToString().Replace(",", "."));
                        c++;
                        Data.Query(query, parms.ToArray());
                    }
                }

                level++;

                #region Debug
                /*Console.WriteLine(">>> ");
                foreach (Models.Token token in tokens.OrderByDescending(o => o.weight))
                {
                    Console.WriteLine($"word: {token.word} \t category: {token.category_id} \t count: {token.count} \t weight: {token.weight} \t relevance: {token.relevance}");
                }*/
                #endregion Debug
            }

            //Console.Write($" {acquired} tokens acquired / {computed} tokens computed");
            //Console.WriteLine();
        }


        #region old TrainCategoryv1
        /*
        public static void TrainCategory(string text, string[] words)
        {
            Models.Token[] category_tokens = null;
            Models.Token[] tokensArr = Tokenize.Instance(word_pooling, maxlength, soundex).Apply(text);
            Models.Token[] tokens = Relevances(Weights(tokensArr));


            
            foreach(string word in words)
            {
                int category_id = Convert.ToInt32(Data.Query($"SELECT category_id FROM {DbTable}_categories WHERE name=?name", new string[] { word }).Tables[0].Rows[0][0]);

                foreach (Models.Token token in tokens)
                {
                    token.category_id = category_id;
                }

                category_tokens = MySQL.Json.Select.Fill($"SELECT * FROM {DbTable} WHERE category_id=?category_id AND experiment_id=?experiment_id ORDER BY word ASC", new string[] { category_id.ToString(), ExperimentId }).Multiple<Models.Token>();

                bool isfirst = false;

                if (category_tokens != null)
                {
                    if (category_tokens.Count() > 0)
                    {
                        isfirst = false;
                        Models.Token[] intersect_tokens = Intersect(tokens, category_tokens);
                        Models.Token[] different_tokens = Diference(tokens, category_tokens);

                        int maxCount = intersect_tokens.OrderByDescending(i => i.count).First().count;

                        foreach (Models.Token token in intersect_tokens)
                        {
                            //Console.WriteLine($"intersect word: {token.word}");
                            token.weight *= (TrainingRate) * (1 + (token.count / maxCount));
                            token.relevance *= (TrainingRate) * (1 + (token.count / maxCount));
                        }

                        foreach (Models.Token token in different_tokens)
                        {
                            //Console.WriteLine($"different word: {token.word}");
                            token.weight /= (TrainingRateDecay);
                            token.relevance /= (TrainingRateDecay);
                        }


                        string query = "";
                        List<string> parms = new List<string>();

                        int c = 0;

                        foreach (Models.Token token in intersect_tokens.Concat(different_tokens))
                        {
                            query += $"INSERT INTO {DbTable} (experiment_id, word, category_id, {DbTable}.count, weight, relevance) VALUES (?experiment_i{c}, ?word_i{c}, ?category_i{c}, ?count_i{c}, ?weight_i{c}, ?relevance_i{c}) ON DUPLICATE KEY UPDATE {DbTable}.count=?count_u{c}, weight=?weight_u{c}, relevance=?relevance_u{c};";
                            parms.Add(ExperimentId);
                            parms.Add(token.word);
                            parms.Add(category_id.ToString());
                            parms.Add(token.count.ToString());
                            parms.Add(token.weight.ToString().Replace(",", "."));
                            parms.Add(token.relevance.ToString().Replace(",", "."));
                            parms.Add(token.count.ToString());
                            parms.Add(token.weight.ToString().Replace(",", "."));
                            parms.Add(token.relevance.ToString().Replace(",", "."));
                            c++;
                        }

                        Data.Query(query, parms.ToArray());
                    }
                    else
                    {
                        isfirst = true;
                    }
                }
                else
                {
                    isfirst = true;
                }

                //Console.WriteLine("First: " + isfirst);


                if (isfirst)
                {
                    string query = "";
                    List<string> parms = new List<string>();

                    int c = 0;
                    foreach (Models.Token token in tokens)
                    {
                        query += $"INSERT INTO {DbTable} (experiment_id, word, category_id, {DbTable}.count, weight, relevance) VALUES (?experiment_i{c}, ?word_i{c}, ?category_i{c}, ?count_i{c}, ?weight_i{c}, ?relevance_i{c}) ON DUPLICATE KEY UPDATE {DbTable}.count=?count_u{c}, weight=?weight_u{c}, relevance=?relevance_u{c};";
                        parms.Add(ExperimentId);
                        parms.Add(token.word);
                        parms.Add(category_id.ToString());
                        parms.Add(token.count.ToString());
                        parms.Add(token.weight.ToString().Replace(",", "."));
                        parms.Add(token.relevance.ToString().Replace(",", "."));
                        parms.Add(token.count.ToString());
                        parms.Add(token.weight.ToString().Replace(",", "."));
                        parms.Add(token.relevance.ToString().Replace(",", "."));
                        c++;
                    }

                    Data.Query(query, parms.ToArray());
                }

                #region Debug
                Console.WriteLine(">>> ");
                foreach (Models.Token token in tokens.OrderByDescending(o => o.weight))
                {     
                    Console.WriteLine($"word: {token.word} \t category: { token.category_id} \t count: {token.count} \t weight: {token.weight} \t relevance: {token.relevance}");
                }
                #endregion Debug
            }
        }
        */
        #endregion old TrainCategoryv1

        public static void TrainCategory(string text, string[] words, string[] ignore, bool IsCategoriesIds = false)
        {
            text = Sanitize.CustomApply(text, ignore);
            TrainCategory(text, words, IsCategoriesIds);
        }


        public static void TrainCategory(string text, string[] words, bool ignore, bool IsCategoriesIds = false)
        {
            if (ignore)
            {
                text = Sanitize.HardApply(text);
            }
            else
            {
                text = Sanitize.Apply(text);
            }

            TrainCategory(text, words, IsCategoriesIds);
        }


        public static void TrainCategoryGroup(string[] texts, string[] words, int epochs = 10, bool IsCategoriesIds = false)
        {
            Console.WriteLine("Start Training Group...");
            TrainingRate = 1 + ((TrainingRate - 1) / epochs);

            if (!IsCategoriesIds) DbPopulateExperimentsCategory(words);

            for (int j = 0; j < epochs; j++)
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    texts[i] = Sanitize.Apply(texts[i]);
                    TrainCategory(texts[i], words, IsCategoriesIds);
                }
            }
        }


        public static void TrainCategoryGroup(string[] texts, string[] words, string[] ignore, int epochs = 1, bool IsCategoriesIds = false)
        {
            Console.WriteLine("Start Training Group...");
            TrainingRate = 1 + ((TrainingRate - 1) / epochs);

            if(!IsCategoriesIds) DbPopulateExperimentsCategory(words);

            for (int j = 0; j < epochs; j++)
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    texts[i] = Sanitize.CustomApply(texts[i], ignore);
                    TrainCategory(texts[i], words, IsCategoriesIds);
                }
            }
        }


        public static void TrainCategoryGroup(string[] texts, string[] words, bool ignore, int epochs = 10, bool IsCategoriesIds = false)
        {
            //Console.WriteLine("Start Training Group...");
            TrainingRate = 1 + ((TrainingRate - 1) / epochs);

            if (!IsCategoriesIds) DbPopulateExperimentsCategory(words);


            for (int j = 0; j < epochs; j++)
            {
                //Console.WriteLine(">> Epoch " + j);
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
                }
            }
        }

        #endregion Train.Category
        #endregion Train



        #region Predict
        public static Models.Category[] Predict(string text, int subcategories_levels = 1, int results = 10)
        {
            Models.Token[] tokens = Relevances(Weights(Tokenize.Instance(word_pooling, maxlength, soundex).Apply(text)));
            List<Models.Token[]> list = new List<Models.Token[]>();
            List<Models.Category> list_categories = new List<Models.Category>();


            Models.Token[] _tokens = tokens.OrderByDescending(i => i.weight).ToArray();


            if (subcategories_levels > 0)
            {
                subcategories_levels--;

                foreach (Models.Token token in _tokens)
                {
                    Models.Token[] word_tokens = TensorsToTokens(token.word, 0);//MySQL.Json.Select.Fill($"SELECT nlp_dataset.*  FROM {DbTable}  WHERE {DbTable}.experiment_id=?experiment_id AND word=?word AND {DbTable}.level=0 AND weight>{MinWeight.ToString().Replace(",", ".")} ORDER BY weight DESC LIMIT 30", new string[] { ExperimentId, token.word }).Multiple<Models.Token>();
                    if (word_tokens != null && word_tokens.Length > 0) list.Add(word_tokens);
                }

               
                int c = 0;
                foreach (Models.Token[] token_list in list)
                {
                    foreach (Models.Token token in token_list)
                    {
                        int? index = list_categories.FindIndex(v => v.category_id == token.category_id);
                        if (index != null && index > -1)
                        {
                            list_categories[(int)index].weigths_avg = (list_categories[(int)index].weigths_avg + token.weight) / 2;
                            list_categories[(int)index].weigths_sum += token.weight;
                            list_categories[(int)index].relevance_avg = (list_categories[(int)index].relevance_avg + token.relevance) / 2;
                            list_categories[(int)index].relevance_sum += token.relevance;
                            list_categories[(int)index].count++;
                        }
                        else
                        {
                            string category_name = ""; // Data.Query($"SELECT name FROM {DbTable}_categories WHERE category_id=?category_id AND level=0 AND experiment_id=?experiment_id", new string[] { token.category_id.ToString(), ExperimentId }).Tables[0].Rows[0][0].ToString();
                            list_categories.Add(new Models.Category() { category_id = token.category_id, name = category_name, count = 1, weigths_sum = token.weight, weigths_avg = token.weight, relevance_sum = token.relevance, relevance_avg = token.relevance/*, subcategories = PredictSubCategory(_tokens, token.category_id, subcategories_levels, results)*/ });
                        }
                    }
                }

                list_categories = list_categories.OrderByDescending(item => (item.weigths_sum * item.relevance_sum)).Take(results).ToList();


                if (subcategories_levels > 0)
                {
                    for (int i = 0; i < list_categories.Count; i++)
                    {
                        list_categories[i].subcategories = PredictSubCategory(_tokens, list_categories[i].category_id, subcategories_levels, results);
                    }
                }
            }


            return NLP.Result.Normalize(list_categories.ToArray());
        }


        private static Models.Category[] PredictSubCategory(Models.Token[] tokens, int parent_id, int subcategories_levels, int results)
        {
            List<Models.Token[]> list = new List<Models.Token[]>();
            List<Models.Category> list_categories = new List<Models.Category>();
            int level = 1;

            //Console.WriteLine($"### {subcategories_levels}");

            if (subcategories_levels > 0)
            {
                subcategories_levels--;

                foreach (Models.Token token in tokens)
                {
                    Models.Token[] word_tokens = TensorsToTokens(token.word, level);//MySQL.Json.Select.Fill($"SELECT nlp_dataset.*  FROM {DbTable} INNER JOIN {DbTable}_categories ON {DbTable}_categories.category_id={DbTable}.category_id AND {DbTable}_categories.experiment_id={DbTable}.experiment_id AND {DbTable}_categories.parent_id=?parent_id WHERE {DbTable}.experiment_id=?experiment_id AND word=?word AND {DbTable}.level=?level AND weight>{MinWeight.ToString().Replace(",", ".")} ORDER BY weight DESC LIMIT 30", new string[] { parent_id.ToString(), ExperimentId, token.word, level.ToString() }).Multiple<Models.Token>();
                    //Trace.Message($"SELECT nlp_dataset.*  FROM {DbTable} INNER JOIN {DbTable}_categories ON {DbTable}_categories.category_id={DbTable}.category_id AND {DbTable}_categories.experiment_id={DbTable}.experiment_id AND {DbTable}_categories.parent_id={parent_id} WHERE {DbTable}.experiment_id={ExperimentId} AND word={token.word} AND {DbTable}.level={level} AND weight>{MinWeight.ToString().Replace(",", ".")} ORDER BY weight DESC LIMIT 30");
                    if (word_tokens != null && word_tokens.Length > 0) list.Add(word_tokens);
                }

               

                int c = 1;
                foreach (Models.Token[] token_list in list)
                {

                    foreach (Models.Token token in token_list)
                    {
                        //Console.WriteLine(token.word + " " + token.weight + " " + token.relevance);

                        int? index = list_categories.FindIndex(v => v.category_id == token.category_id);
                        if (index != null && index > -1)
                        {
                            list_categories[(int)index].weigths_avg = (list_categories[(int)index].weigths_avg + token.weight) / 2;
                            list_categories[(int)index].weigths_sum += token.weight;
                            list_categories[(int)index].relevance_avg = (list_categories[(int)index].relevance_avg + token.relevance) / 2;
                            list_categories[(int)index].relevance_sum += token.relevance;
                            list_categories[(int)index].count++;

                            //Console.WriteLine(">> " + cat.name);

                            //Console.WriteLine($">>> weigths_sum: " + cat.weigths_sum + " cat.relevance_sum: " + cat.relevance_sum);
                            //Console.WriteLine($">>> token.weight " + token.weight + " token.relevance: " + token.relevance);

                            //list_categories.Add(new Models.Category() { category_id = token.category_id, name = list_categories[(int)index].name, count = 1, weigths_sum = token.weight, weigths_avg = token.weight, relevance_sum = token.relevance, relevance_avg = token.relevance, subcategories = PredictSubCategory(tokens, token.category_id, subcategories_levels, results) });
                        }
                        else
                        {
                            string category_name = ""; // Data.Query($"SELECT name FROM {DbTable}_categories WHERE category_id=?category_id AND level=?level AND experiment_id=?experiment_id", new string[] { token.category_id.ToString(), level.ToString(), ExperimentId }).Tables[0].Rows[0][0].ToString();
                            //Console.WriteLine(">> " + category_name);
                            list_categories.Add(new Models.Category() { category_id = token.category_id, name = category_name, count = 1, weigths_sum = token.weight, weigths_avg = token.weight, relevance_sum = token.relevance, relevance_avg = token.relevance/*, subcategories = PredictSubCategory(tokens, token.category_id, subcategories_levels, results)*/ });
                        }

                        //Trace.Message($"\t subcategory_id: {token.category_id} \tword: {token.word} \t count: {token.count} \t weight: {token.weight} \t relevance: {token.relevance}");
                    }
                }

                //list_categories = list_categories.OrderByDescending(item => item.count).ThenByDescending(item => item.relevance_avg).Take(results).ToList();
                //Models.Category[]? mc = list_categories.OrderByDescending(item => item.count).Take(1).ToArray();
                //int MaxCount = mc != null && mc.Length > 0 ? mc[0].count : 1;
                list_categories = list_categories.OrderByDescending(item => (item.weigths_sum * item.relevance_sum)).Take(results).ToList();
                level++;

                //Trace.Message($"{subcategories_levels}");

                if (subcategories_levels > 0)
                {
                    for (int i = 0; i < list_categories.Count; i++)
                    {
                        list_categories[i].subcategories = PredictSubCategory(tokens, list_categories[i].category_id, subcategories_levels, results);
                    }
                }
            }

            return NLP.Result.Normalize(list_categories.ToArray());
        }



        public static Models.Category[] Predict(string text, bool ignore = true, int subcategories_levels = 1, int results = 10)
        {
            if (ignore)
            {
                text = Sanitize.HardApply(text);
            }
            else
            {
                text = Sanitize.Apply(text);
            }

            return Predict(text, subcategories_levels, results);
        }


        public static Models.Category[] Predict(string text, string[] ignore, int subcategories_levels = 1, int results = 10)
        {
            text = Sanitize.CustomApply(text, ignore);
            return Predict(text, subcategories_levels, results);
        }
        #endregion Predict



        #region Functions
        public static Models.Token[] TensorsToTokens(string word, int level)
        {
            TokenTensor[] wordTensors = tensors.Where<TokenTensor>(t => t.word == word && t.level == level).ToArray();
            Models.Token[] wordTokens = new Models.Token[wordTensors.Length];
            for(int i = 0; i < wordTensors.Length; i++)
            {
                wordTokens[i] = new Models.Token() {
                    word = wordTensors[i].word,
                    category_id = wordTensors[i].category_id,
                    weight = wordTensors[i].weigth,
                    relevance = wordTensors[i].relevance,
                    count = wordTensors[i].count
                };
            }

            return wordTokens;
        }



        public static Models.Token[] Weights(Models.Token[] tokens)
        {
            int maxCount = tokens.OrderByDescending(i => i.count).First().count;
            //maxCount = maxCount < 7 ? 7 : maxCount;


            for (int i = 0; i < tokens.Length; i++)
            {
                tokens[i].weight = ((double)tokens[i].count / (1 + (double)maxCount));
                //Console.WriteLine($"{tokens[i].word}|{tokens[i].weight}|{tokens[i].count}|{maxCount}");
            }
            //Console.WriteLine("--------------------------------------------------------");

            return tokens;
        }


        public static Models.Token[] Relevances(Models.Token[] tokens, int maxDistance = 10)
        {
            Models.Token[] _tokens = tokens.OrderByDescending(i => i.weight).ToArray();
            int[] position = new int[5];

            //Tokenize.PrintLine(tokens);
            //Tokenize.PrintLine(_tokens);

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

                    //Console.Write(j + " " + position[j]);

                    if (j > 0 && position[j] > -1)
                    {
                        int distance = Math.Abs(position[j] - position[0]);
                        //_tokens[i].relevance += (double)_tokens[i].weight * (1 + ((double)maxDistance / (double)distance) / j);
                        sum += 1d / (double)(Math.Pow(j * 2, 2) + Math.Pow(distance, 2));
                        //sum += 1 / (double)((distance + j));

                        try
                        {
                            //Console.WriteLine($"{_tokens[i].word} {_tokens[i + j].word} / {j} - {distance.ToString()} / {sum.ToString()}");
                        }
                        catch (Exception ex)
                        {

                        }


                        try
                        {
                            //tokens[i + 1].relevance += (double)_tokens[i].weight * sum;
                            //Console.WriteLine($"{_tokens[i+1].word} / {j} - {distance.ToString()} / {sum.ToString()}");
                        }
                        catch (Exception ex)
                        {

                        }
                        //Console.WriteLine(" dist(" + distance + ") relevance(" + _tokens[0].relevance + ")");
                    }
                }

                _tokens[i].relevance = (double)_tokens[i].weight * sum;
            }
            return _tokens;
        }


        public static Models.Token[] Intersect(Models.Token[] arr1, Models.Token[] arr2)
        {
            List<Models.Token> result = new List<Models.Token>();
            foreach (Models.Token token1 in arr1)
            {
                foreach (Models.Token token2 in arr2)
                {
                    if (token1.word == token2.word)
                    {
                        token1.count += token2.count;
                        result.Add(token1);
                    }
                }
            }

            return result.ToArray();
        }


        public static Models.Token[] Diference(Models.Token[] arr1, Models.Token[] arr2)
        {
            List<Models.Token> result = new List<Models.Token>();


            foreach (Models.Token token1 in arr1)
            {
                bool has = false;
                Models.Token tmp = null;
                foreach (Models.Token token2 in arr2)
                {
                    if (token1.word == token2.word)
                    {
                        has = true;
                        tmp = null;
                        break;
                    }
                    else
                    {
                        tmp = token2;
                    }
                }

                if (!has)
                {
                    result.Add(token1);
                    if (tmp != null) result.Add(tmp);
                }
            }

            foreach (Models.Token token2 in arr2)
            {
                bool has = false;
                Models.Token tmp = null;

                foreach (Models.Token token1 in arr1)
                {
                    if (token1.word == token2.word)
                    {
                        has = true;
                        tmp = null;
                        break;
                    }
                    else
                    {
                        tmp = token1;
                    }
                }

                if (!has)
                {
                    result.Add(token2);
                    if (tmp != null) result.Add(tmp);
                }
            }




            return result.DistinctBy(t => t.word).ToArray();
        }


        public static void ClearDb()
        {
            Data.Query($"TRUNCATE TABLE {DbTable};/*TRUNCATE TABLE {DbTable}_experiments;TRUNCATE TABLE {DbTable}_categories;*/");
        }


        public static void CleaExperiment()
        {
            if (String.IsNullOrEmpty(ExperimentId)) DbPopulateExperiment();
            Data.Query($"DELETE FROM {DbTable} WHERE experiment_id=?experiment_id", new string[] { ExperimentId });
            Data.Query($"DELETE FROM {DbTable}_experiments WHERE experiment_id=?experiment_id", new string[] { ExperimentId });
            Data.Query($"DELETE FROM {DbTable}_categories WHERE experiment_id=?experiment_id", new string[] { ExperimentId });
        }


        private static void DbPopulateExperimentsCategory(string[] words)
        {
            if (String.IsNullOrEmpty(ExperimentId))
            {
                Data.Query($"INSERT IGNORE INTO {DbTable}_experiments (name) VALUES (?name)", new string[] { Experiment });
                ExperimentId = Data.Query($"SELECT experiment_id FROM {DbTable}_experiments WHERE name=?name", new string[] { Experiment }).Tables[0].Rows[0][0].ToString();
            }


            string last_id = "0";
            int level = 0;
            foreach (string word in words)
            {
                Data.Query($"INSERT IGNORE INTO {DbTable}_categories (experiment_id, parent_id, name, level) VALUES (?experiment_id, ?last_id, ?name, ?level)", new string[] { ExperimentId, last_id, word, level.ToString() });
                last_id = Data.Query($"SELECT category_id FROM {DbTable}_categories WHERE experiment_id=?experiment_id AND name=?word", new string[] { ExperimentId, word }).Tables[0].Rows[0][0].ToString();
                level++;
            }

        }


        private static void DbPopulateExperiment()
        {
            if (String.IsNullOrEmpty(ExperimentId))
            {
                Data.Query($"INSERT IGNORE INTO {DbTable}_experiments (name) VALUES (?name)", new string[] { Experiment });
                ExperimentId = Data.Query($"SELECT experiment_id FROM {DbTable}_experiments WHERE name=?name", new string[] { Experiment }).Tables[0].Rows[0][0].ToString();
            }
        }
        #endregion Functions
    }
}