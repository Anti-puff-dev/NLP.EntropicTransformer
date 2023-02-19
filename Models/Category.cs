
namespace NLP.Models
{
    public class Category
    {
        public int category_id { get; set; }
        public string name { get; set; }
        public int count { get; set; }
        public double weigths_sum { get; set; }
        public double weigths_avg { get; set; }
        public double relevance_sum { get; set; }
        public double relevance_avg { get; set; }
        public Category[] subcategories { get; set; }
        public double confidence { get; set; }
    }


    public class CategoryTiny
    {
        public int category_id { get; set; }
        public string name { get; set; }
        public int experiment_id { get; set; }
        public int level { get; set; }
    }


    public class CategoryAuto
    {
        public int category_id { get; set; }
        public int experiment_id { get; set; }
        public int level { get; set; }
        public string name { get; set; }
        public List<Models.Token> tokens { get; set; }
        public List<int> ids { get; set; }
    }
}
