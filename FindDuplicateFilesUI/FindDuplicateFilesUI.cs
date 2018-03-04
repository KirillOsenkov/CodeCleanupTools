using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
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

    ObservableCollection<File> Files = new ObservableCollection<File>();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PopulateDuplicateList();
    }

    private void PopulateDuplicateList(string root = null)
    {
        root = root ?? folderPathText.Text;

        Files.Clear();

        if (!Directory.Exists(root))
        {
            return;
        }

        string[] allFiles = GetFiles(root);
        var filesByHash = FindDuplicateFiles(allFiles);

        foreach (var currentBucket in filesByHash)
        {
            if (currentBucket.Value.Count > 1)
            {
                foreach (var dupe in currentBucket.Value)
                {
                    Files.Add(new File(dupe, currentBucket.Key));
                }
            }
        }
    }

    private void InitializeList()
    {
        var view = new GridView();
        view.Columns.Add(new GridViewColumn() { Header = "Path", DisplayMemberBinding = new Binding("Path") });
        listBox.View = view;

        var viewSource = CollectionViewSource.GetDefaultView(Files);
        viewSource.GroupDescriptions.Add(new PropertyGroupDescription("Sha"));

        listBox.GroupStyle.Add(new GroupStyle());

        listBox.ItemsSource = viewSource;
    }

    ListView listBox;
    TextBox folderPathText;
    Button rescan;

    private object GetContent()
    {
        var dockPanel = new DockPanel();
        folderPathText = new TextBox()
        {
            Margin = new Thickness(8),
            MinHeight = 23,
            Text = Environment.CurrentDirectory
        };

        rescan = new Button()
        {
            Margin = new Thickness(0, 8, 8, 8),
            Content = "Scan",
            Padding = new Thickness(4),
            MinWidth = 75
        };
        rescan.Click += (s, e) => PopulateDuplicateList();
        DockPanel.SetDock(rescan, Dock.Right);

        var topPanel = new DockPanel();
        topPanel.Children.Add(rescan);
        topPanel.Children.Add(folderPathText);

        listBox = new ListView()
        {
            Margin = new Thickness(8, 0, 8, 8)
        };
        listBox.KeyUp += ListBox_KeyUp;

        InitializeList();

        DockPanel.SetDock(topPanel, Dock.Top);

        dockPanel.Children.Add(topPanel);
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
                Files.Remove(selectedFile);
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

        Parallel.ForEach(allFiles, file =>
        {
            var hash = Utilities.SHA1Hash(file);

            HashSet<string> bucket;
            lock (filesByHash)
            {
                if (!filesByHash.TryGetValue(hash, out bucket))
                {
                    bucket = new HashSet<string>();
                    filesByHash[hash] = bucket;
                }
            }

            lock (bucket)
            {
                bucket.Add(file);
            }
        });

        return filesByHash;
    }
}