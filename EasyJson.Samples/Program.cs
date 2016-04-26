using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyJson.Samples
{
    class Program
    {
        static void Main(string[] args)
        {
            var pathList = new string[] {
                "Data/data.json",
                "Data/data1.json"
            };

            var json = File.ReadAllText(pathList[1]);

            //Usage 1:The usual way 
            Console.WriteLine("\nUsage 1:The usual way");

            var jsonItem1 = Json.Parse(json);
            Console.WriteLine(jsonItem1["friends"][2]["name"]);

            //Usage 2:The dynamic way
            Console.WriteLine("\nUsage 2:The dynamic way");
            dynamic jsonItem2 = Json.Parse(json);
            Console.WriteLine(jsonItem2.friends[2].name);

            //Usage 3:The Linq Way
            Console.WriteLine("\nUsage 3:The Linq way");
            var jsonItem3 = Json.Parse(json);
            var query = from q in jsonItem3.Items
                        from p in q.Items
                        select p.Value;
            foreach (var item in query)
            {
                Console.WriteLine(item);
            }

            Console.WriteLine("\nBenchmark:\n");

            foreach (var path in pathList)
            {
                CodeTimer.Execute($"EasyJson test {path}", 20, () =>
                  {
                      Json.Parse(json);
                  });
            }

            foreach (var path in pathList)
            {
                CodeTimer.Execute($"Json.net text {path}", 20, () =>
                {
                    JObject.Parse(json);
                });
            }
            Console.ReadKey();
        }
    }
}