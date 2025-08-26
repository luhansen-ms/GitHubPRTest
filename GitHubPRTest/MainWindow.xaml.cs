using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GitHubPRTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<FruitInventory> FruitItems { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            InitializeFruitInventory();
            FruitInventoryGrid.ItemsSource = FruitItems;
        }

        private void InitializeFruitInventory()
        {
            FruitItems = new ObservableCollection<FruitInventory>
            {
                new FruitInventory("Apples", 150),
                new FruitInventory("Bananas", 89),
                new FruitInventory("Oranges", 76),
                new FruitInventory("Strawberries", 42),
                new FruitInventory("Grapes", 124),
                new FruitInventory("Pineapples", 18),
                new FruitInventory("Mangoes", 33),
                new FruitInventory("Blueberries", 67),
                new FruitInventory("Peaches", 51),
                new FruitInventory("Watermelons", 12)
            };
        }
    }
}
