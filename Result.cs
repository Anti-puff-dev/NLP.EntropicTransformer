
namespace NLP
{
    public class Result
    {


        public static Models.Category[] Normalize(Models.Category[] categories)
        {

            double sum = 0d;

            foreach (Models.Category category in categories)
            {
                category.confidence = category.weigths_sum * category.relevance_sum;
                sum += category.confidence;
            }

            categories = Confidences(categories, sum);

            return categories;
        }


        public static Models.Category[] Confidences(Models.Category[] categories, double sum)
        {
            foreach (Models.Category category in categories)
            {
                category.confidence = category.confidence / sum;
            }

            return categories;
        }


        public static void Print(Models.Category[] categories, int level = 0)
        {


            foreach (Models.Category category in categories)
            {
                for (int i = 0; i < level; i++)
                {
                    Console.Write(">>");
                }
                Console.WriteLine(category.category_id + " " + category.name + " " + category.confidence + " " + category.count);

                if (category.subcategories.Length > 0)
                {
                    Print(category.subcategories, level + 1);
                }
            }
            Console.WriteLine();
        }
    }
}
