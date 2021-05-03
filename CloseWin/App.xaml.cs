using System;
using System.Windows;
using CloseWin.Core;

namespace CloseWin {
  /// <summary>
  /// Логика взаимодействия для App.xaml
  /// </summary>
  public partial class App : Application
    {
        public App()
        {
            this.Activated += StartElmish;
        }

        private void StartElmish(object sender, EventArgs e)
        {
            this.Activated -= StartElmish;
            Test.main(MainWindow, () => new ModalWindow ());
        }
    }
}
