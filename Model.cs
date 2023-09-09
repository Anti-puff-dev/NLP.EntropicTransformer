using MySQL;
using NLP.Models;
using System.Collections;
using System.Data;

namespace NLP
{
    public class Model
    {
        public static async Task ClearDb(int experiment_id)
        {
            Console.WriteLine("Clearing Database");


            DataSet ds = Data.Query("SELECT intent_id FROM nlp_experiments WHERE experiment_id=?experiment_id", new string[] { experiment_id.ToString() });
            Console.WriteLine("Categories: " + ds.Tables[0].Rows.Count);
            if (ds.Tables[0].Rows.Count > 0)
            {
                foreach (DataRow row in ds.Tables[0].Rows)
                {
                    string category_id = row[0].ToString();
                    MySQL.Json.Delete.From("nlp_questions", "category_id", category_id).Run();
                    MySQL.Json.Delete.From("nlp_answers", "category_id", category_id).Run();
                }
            }

            MySQL.Json.Delete.From("nlp_agents", "experiment_id", experiment_id.ToString()).Run();
            MySQL.Json.Delete.From("nlp_greetings", "experiment_id", experiment_id.ToString()).Run();
            MySQL.Json.Delete.From("nlp_intents", "experiment_id", experiment_id.ToString()).Run();
            MySQL.Json.Delete.From("nlp_dataset", "experiment_id", experiment_id.ToString()).Run();
        }


        public static async Task TruncateDb()
        {
            Console.WriteLine("Truncating Database");
            Data.Query("TRUNCATE TABLE nlp_experiments;", new string[] { });
            Data.Query("TRUNCATE TABLE nlp_greetings;", new string[] { });
            Data.Query("TRUNCATE TABLE nlp_categories;", new string[] { });
            Data.Query("TRUNCATE TABLE nlp_questions;", new string[] { });
            Data.Query("TRUNCATE TABLE nlp_answers;", new string[] { });
            Data.Query("TRUNCATE TABLE nlp_dataset;", new string[] { });
        }


        public static async Task TrainDb(string experiment_id)
        {
            Console.WriteLine("Truncating Dataset");
            //Data.Query("TRUNCATE TABLE nlp_dataset;", new string[] { });
            ClearDataset(experiment_id);

            string category_id = "";
            DataSet ds = Data.Query("SELECT intent_id, phrase FROM nlp_questions WHERE experiment_id=?experiment_id ORDER BY category_id ASC", new string[] { experiment_id });

            Hashtable hashtable = new Hashtable();

            foreach (DataRow dr in ds.Tables[0].Rows)
            {
                if (category_id != dr[0].ToString())
                {
                    category_id = dr[0].ToString();
                    hashtable[dr[0].ToString()] = new List<string>();
                }

                ((List<string>)hashtable[dr[0].ToString()]).Add(dr[1].ToString());
            }

            NLP.Classify.word_pooling = 0.7d;
            NLP.Classify.ExperimentId = experiment_id;

            foreach (DictionaryEntry ht in hashtable)
            {
                NLP.Classify.TrainCategoryGroup(((List<string>)hashtable[ht.Key.ToString()]).ToArray(), new string[] { ht.Key.ToString() }, true, 10);
            }

            Console.WriteLine("TrainDb Finished");
        }


        public static async Task TrainDb(string experiment_id, string category_id)
        {
            //Console.WriteLine("Truncating Dataset");
            //Data.Query("TRUNCATE TABLE nlp_dataset;", new string[] { });

            DataSet ds = Data.Query("SELECT category_id, phrase FROM nlp_questions WHERE experiment_id=?experiment_id AND category_id=?category_id ORDER BY category_id ASC", new string[] { experiment_id, category_id });

            Hashtable hashtable = new Hashtable();

            string _intent_id = "";
            foreach (DataRow dr in ds.Tables[0].Rows)
            {
                if (_intent_id != dr[0].ToString())
                {
                    _intent_id = dr[0].ToString();
                    hashtable[dr[0].ToString()] = new List<string>();
                }

                ((List<string>)hashtable[dr[0].ToString()]).Add(dr[1].ToString());
            }

            NLP.Classify.word_pooling = 0.7d;
            NLP.Classify.ExperimentId = experiment_id;

            foreach (DictionaryEntry ht in hashtable)
            {
                NLP.Classify.TrainCategoryGroup(((List<string>)hashtable[ht.Key.ToString()]).ToArray(), new string[] { ht.Key.ToString() }, true, 10);
            }

            //Console.WriteLine("TrainDb Finished");
        }


        public static async Task ClearDataset(string experiment_id)
        {
            Data.Query("DELETE FROM nlp_dataset WHERE experiment_id=?experiment_id;", new string[] { experiment_id });
        }
    }
}
