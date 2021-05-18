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
using GeneralCode;
using System.Data;

namespace CommodityMgr
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        MysqlOperator db = new MysqlOperator();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnTest_Click(object sender, RoutedEventArgs e)
        {
            //string sql = "insert into Table2 (Name) Values ('你我他')";
            //db.update(sql);

            string sql = "select * from table2";
            DataTable tbl = db.query(sql);
            if (CSTR.IsTableEmpty(tbl)) return;

            foreach (DataRow row in tbl.Rows)
            {
                MessageBox.Show(CSTR.ObjectTrim(row["Name"]));
            }
        }
    }
}
