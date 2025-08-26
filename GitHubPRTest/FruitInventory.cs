using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitHubPRTest
{
    /// <summary>
    /// Represents a fruit item in the inventory with name and count.
    /// </summary>
    public class FruitInventory
    {
        public string FruitName { get; set; }
        public int Count { get; set; }

        public FruitInventory(string fruitName, int count)
        {
            FruitName = fruitName;
            Count = count;
        }
    }
}