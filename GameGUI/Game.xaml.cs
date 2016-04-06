using System;
using System.Collections.Generic;
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

namespace GameGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class GameWindow : Window
    {
        private Rectangle[] gameSquares;

        public GameWindow()
        {
            gameSquares = new Rectangle[20];
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Loop through and add squares that are only going to be used for the game
            for (int row = 0; row < gameGrid.RowDefinitions.Count; row++)
            {
                for (int column = 0; column < gameGrid.ColumnDefinitions.Count; column++)
                {
                    if (row.Equals(0) || column.Equals(0) || row.Equals(gameGrid.RowDefinitions.Count - 1) || column.Equals( gameGrid.ColumnDefinitions.Count - 1))
                    {
                        Rectangle gameSquare = new Rectangle();
                        gameSquare.Style = this.FindResource("gameSquare") as Style;
                        gameSquare.Visibility = Visibility.Visible;
                        gameGrid.Children.Add(gameSquare);
                        Grid.SetRow(gameSquare, row);
                        Grid.SetColumn(gameSquare, column);
                    }
                    
                }
            }
        }
    }
}
