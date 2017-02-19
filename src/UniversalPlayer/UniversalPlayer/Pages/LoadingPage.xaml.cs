using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using UniversalPlayer.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace UniversalPlayer.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class LoadingPage : Page
    {
        public LoadingViewModel LoadingViewModel { get; set; } = new LoadingViewModel();
        public LoadingPage()
        {
            this.InitializeComponent();

            Loaded += LoadingPage_Loaded;
        }

        private async void LoadingPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadingViewModel.LoadInitialMusic();
            Frame.Navigate(typeof(MainPage), LoadingViewModel.Files);
        }
    }
}
