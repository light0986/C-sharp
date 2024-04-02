using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DataGrid測試
{
    /// <summary>
    /// DataGrid Binding List
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<Data> datas = new List<Data>();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //製作DataGrid
            CreateDataGrid();
            //製作資料
            CreateData();
        }

        private void CreateDataGrid()
        {
            DataGrid dataGrid = new DataGrid();
            dataGrid.ItemsSource = datas;
            dataGrid.AutoGenerateColumns = false;

            DataGridTextColumn First = new DataGridTextColumn();
            First.Header = "First";
            First.Binding = new Binding("Second");
            dataGrid.Columns.Add(First);

            DataGridTextColumn Second = new DataGridTextColumn();
            Second.Header = "Second";
            Second.Binding = new Binding("Second");
            dataGrid.Columns.Add(Second);

            dataGrid.ItemsSource = datas;
            MainGrid.Children.Add(dataGrid);
        }

        private void CreateData()
        {
            datas.Add(new Data()
            {
                First = "01",
                Second = "02",
                Third = "03",
                Fourth = "04",
            });

            DataGrid dataGrid = MainGrid.Children[0] as DataGrid;
            if (dataGrid != null)
            {
                dataGrid.Items.Refresh();
            }
        }

        public class Data
        {
            public string First { get; set; }

            public string Second { get; set; }

            public string Third { get; set; }

            public string Fourth { get; set; }
        }
    }
}
