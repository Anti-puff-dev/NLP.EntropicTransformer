
namespace NLP.Models
{
    public class Token
    {
        public int id { get; set; }
        public string experiment_id { get; set; }
        public string word { get; set; }
        public string word_original { get; set; }
        public int category_id { get; set; }
        public int level { get; set; }
        public int count { get; set; }
        public double weight { get; set; }
        public double relevance { get; set; }
    }

    public class DbToken
    {
        public int id { get; set; }
        public string word { get; set; }
        public string word_original { get; set; }
        public string soundex_t { get; set; }
        public string soundex_full { get; set; }
        public string grammar { get; set; }
        public string gender { get; set; }
        public int verbo { get; set; }
        public float weight { get; set; }
        public int isname { get; set; }
    }

    public class DataSetItem
    {
        public int id { get; set; }
        public int experiment_id { get; set; }
        public string word { get; set; }
        public string word_original { get; set; }
        public int category_id { get; set; }
        public int level { get; set; }
        public int count { get; set; }
        public double weight { get; set; }
        public double relevance { get; set;}
    }

}
