using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace 雲箱二創
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly List<ButtonModel> CanPlay = new List<ButtonModel>();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            string[] Files = GetDll();
            if (Files.Length > 0)
            {
                List<ButtonModel> buttonList = GetText(Files);
                if (buttonList.Count > 0)
                {
                    CreateControl(buttonList);
                }
            }
        }

        private string[] GetDll()
        {
            string Path = AppDomain.CurrentDomain.BaseDirectory;
            string[] Files = Directory.GetFiles(Path, "*.dll", SearchOption.AllDirectories);
            return Files;
        }

        private List<ButtonModel> GetText(string[] Files)
        {
            List<ButtonModel> textList = new List<ButtonModel>();
            foreach (string path in Files)
            {
                try
                {
                    Assembly dll = Assembly.LoadFile(path);
                    AssemblyName assemName = dll.GetName();
                    List<Type> dllName = dll.GetExportedTypes().ToList();
                    Type Info = dllName.First(x => x.Name == "Info");

                    List<MethodInfo> methods = Info.GetMethods(BindingFlags.Public | BindingFlags.Static).ToList();
                    MethodInfo GetImage = methods.First(x => x.Name == "GetImage");
                    MethodInfo GetText = methods.First(x => x.Name == "GetText");

                    Image image = GetImage.Invoke(null, null) as Image;
                    TextBlock text = GetText.Invoke(null, null) as TextBlock;

                    textList.Add(new ButtonModel() { Dll = dll, Image = image, TextBlock = text });
                }
                catch { }
            }
            return textList;
        }

        private void CreateControl(List<ButtonModel> buttonList)
        {
            Style style = (Style)Application.Current.Resources["SimpleButton"];

            foreach (ButtonModel m in buttonList)
            {
                try
                {
                    ContentControl content = new ContentControl();
                    content.Style = style;
                    content.Tag = m.TextBlock.Text;
                    content.MouseLeftButtonUp += ContentControl_MouseLeftButtonUp;

                    Grid grid = new Grid();
                    content.Content = grid;

                    RowDefinition row1 = new RowDefinition();
                    RowDefinition row2 = new RowDefinition() { Height = GridLength.Auto };
                    grid.RowDefinitions.Add(row1);
                    grid.RowDefinitions.Add(row2);

                    Grid.SetRow(m.Image, 0);
                    Grid.SetRow(m.TextBlock, 1);

                    grid.Children.Add(m.Image);
                    grid.Children.Add(m.TextBlock);

                    _ = MainPanel.Children.Add(content);
                    CanPlay.Add(m);
                }
                catch { }
            }
        }

        private void ContentControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is ContentControl content)
                {
                    string Tag = content.Tag as string;

                    ButtonModel model = CanPlay.First(x => x.TextBlock.Text == Tag);
                    List<Type> dllName = model.Dll.GetExportedTypes().ToList();
                    Type Info = dllName.First(x => x.Name == "MainWindos");

                    UserControl inst = Activator.CreateInstance(Info) as UserControl;
                    if (inst != null)
                    {
                        OpenPopup(inst, Tag);
                    }
                }
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message);
            }
        }

        private void OpenPopup(UserControl form, string title)
        {
            Hide();
            Popup popup = new Popup
            {
                Title = title
            };
            _ = popup.MainGrid.Children.Add(form);

            if (popup.ShowDialog() != null)
            {
                Show();
            }
        }

        public class ButtonModel
        {
            public Assembly Dll { get; set; }
            public Image Image { get; set; }
            public TextBlock TextBlock { get; set; }
        }
    }
}
