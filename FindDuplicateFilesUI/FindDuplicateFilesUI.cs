using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

internal class FindDuplicateFilesUI
{
    [STAThread]
    private static void Main(string[] args)
    {
        new FindDuplicateFilesUI().ShowUI();
    }

    public void ShowUI()
    {
        var app = new Application();
        var window = new Window();
        window.Content = GetContent();
        window.Background = SystemColors.ControlBrush;
        window.Title = "Find duplicate files";
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.Loaded += Window_Loaded;
        app.Run(window);
    }

    public class File
    {
        public File(string path, string key)
        {
            Path = path;
            Sha = key;
        }

        public string Path { get; set; }
        public string Sha { get; set; }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PopulateDuplicateList();
    }

    private void PopulateDuplicateList(string root = null)
    {
        root = root ?? folderPathText.Text;

        string[] allFiles = GetFiles(root);
        var filesByHash = FindDuplicateFiles(allFiles);

        var list = new List<File>();

        foreach (var currentBucket in filesByHash)
        {
            if (currentBucket.Value.Count > 1)
            {
                foreach (var dupe in currentBucket.Value)
                {
                    list.Add(new File(dupe, currentBucket.Key));
                }
            }
        }

        PopulateList(list);
    }

    private void PopulateList(List<File> list)
    {
        var view = new GridView();
        view.Columns.Add(new GridViewColumn() { Header = "Path", DisplayMemberBinding = new Binding("Path") });
        listBox.View = view;

        var viewSource = CollectionViewSource.GetDefaultView(list);
        viewSource.GroupDescriptions.Add(new PropertyGroupDescription("Sha"));

        listBox.GroupStyle.Add(new GroupStyle());

        listBox.ItemsSource = viewSource;
    }

    ListView listBox;
    TextBox folderPathText;

    private object GetContent()
    {
        var dockPanel = new DockPanel();
        folderPathText = new TextBox()
        {
            Margin = new Thickness(8),
            MinHeight = 23,
            Text = Environment.CurrentDirectory
        };

        listBox = new ListView()
        {
            Margin = new Thickness(8, 0, 8, 8)
        };
        listBox.KeyUp += ListBox_KeyUp;

        DockPanel.SetDock(folderPathText, Dock.Top);

        dockPanel.Children.Add(folderPathText);
        dockPanel.Children.Add(listBox);

        return dockPanel;
    }

    private void ListBox_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            if (listBox.SelectedItem is File selectedFile)
            {
                System.IO.File.Delete(selectedFile.Path);
                PopulateDuplicateList();
                e.Handled = true;
            }
        }
    }

    private static string[] GetFiles(string root)
    {
        var pattern = "*.*";
        var allFiles = Directory.GetFiles(root, pattern, SearchOption.AllDirectories);
        return allFiles;
    }

    public static Dictionary<string, HashSet<string>> FindDuplicateFiles(string[] allFiles)
    {
        var filesByHash = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in allFiles)
        {
            var hash = Utilities.SHA1Hash(file);
            if (!filesByHash.TryGetValue(hash, out HashSet<string> bucket))
            {
                bucket = new HashSet<string>();
                filesByHash[hash] = bucket;
            }

            bucket.Add(file);
        }

        return filesByHash;
    }
}