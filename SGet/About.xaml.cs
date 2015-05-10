using System;
using System.Windows;

namespace SGet
{
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();

            tbVersionAuthor.Text = "Version 1.0\n\nCopyright \u00A9 " + DateTime.Now.Year + "\nMarko Dominik Topić";
            tbUser.Text = String.Format("Used by {0} on {1}", Environment.UserName, Environment.MachineName);
        }
    }
}
