using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

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

    private async void PopulateDuplicateList(string root = null)
    {
        root = root ?? folderPathText.Text;

        Files.Clear();

        if (!Directory.Exists(root))
        {
            return;
        }

        try
        {
            EnableUI(false);
            var filesByHash = await Task.Run(() => Scan(root));
            FillList(filesByHash);
        }
        catch
        {
        }
        finally
        {
            EnableUI(true);
        }
    }

    private void FillList(Dictionary<string, HashSet<string>> filesByHash)
    {
        foreach (var currentBucket in filesByHash.OrderBy(kvp => kvp.Key))
        {
            if (currentBucket.Value.Count > 1)
            {
                foreach (var dupe in currentBucket.Value.OrderBy(s => s))
                {
                    Files.Add(new File(dupe, currentBucket.Key));
                }
            }
        }
    }

    private Dictionary<string, HashSet<string>> Scan(string root)
    {
        var allFiles = GetFiles(root);
        var filesByHash = FindDuplicateFiles(allFiles);
        return filesByHash;
    }

    private void InitializeList()
    {
        var view = new GridView();
        view.Columns.Add(new GridViewColumn() { Header = "Path", DisplayMemberBinding = new Binding("Path") });
        listBox.View = view;

        var viewSource = CollectionViewSource.GetDefaultView(Files);
        viewSource.GroupDescriptions.Add(new PropertyGroupDescription("Sha"));

        var groupStyle = new GroupStyle();
        var headerTemplate = new DataTemplate();
        var frameworkElementFactory = new FrameworkElementFactory(typeof(TextBlock));
        frameworkElementFactory.SetValue(TextBlock.ForegroundProperty, Brushes.Gray);
        frameworkElementFactory.SetValue(TextBlock.TextProperty, new Binding("Name"));
        headerTemplate.VisualTree = frameworkElementFactory;
        groupStyle.HeaderTemplate = headerTemplate;
        listBox.GroupStyle.Add(groupStyle);

        listBox.ItemsSource = viewSource;
    }

    void EnableUI(bool enable)
    {
        listBox.IsEnabled = enable;
        folderPathText.IsEnabled = enable;
        rescan.IsEnabled = enable;
        progressBar.Visibility = enable ? Visibility.Collapsed : Visibility.Visible;
    }

    ListView listBox;
    TextBox folderPathText;
    Button rescan;
    ProgressBar progressBar;
    ContextMenu contextMenu;

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

        progressBar = new ProgressBar()
        {
            Margin = new Thickness(8),
            MinHeight = 23,
            Opacity = 0.5
        };
        progressBar.Visibility = Visibility.Collapsed;
        progressBar.IsIndeterminate = true;
        var grid = new Grid();
        grid.Children.Add(folderPathText);
        grid.Children.Add(progressBar);

        var topPanel = new DockPanel();
        topPanel.Children.Add(rescan);
        topPanel.Children.Add(grid);

        listBox = new ListView()
        {
            Margin = new Thickness(8, 0, 8, 8)
        };
        listBox.KeyUp += ListBox_KeyUp;
        listBox.IsSynchronizedWithCurrentItem = true;

        contextMenu = new ContextMenu();
        var menuDelete = new MenuItem() { Header = "Delete" };
        menuDelete.Click += (s, e) => DeleteSelectedItem();
        var menuCopy = new MenuItem { Header = "Copy" };
        menuCopy.Click += (s, e) => CopySelectedItem();
        var menuOpenFolder = new MenuItem { Header = "Open in Folder" };
        menuOpenFolder.Click += (s, e) => OpenSelectedFileInFolder();
        var menuOpen = new MenuItem { Header = "Open" };
        menuOpen.Click += (s, e) => OpenSelectedFile();

        contextMenu.Items.Add(menuOpen);
        contextMenu.Items.Add(menuOpenFolder);
        contextMenu.Items.Add(menuCopy);
        contextMenu.Items.Add(menuDelete);

        listBox.ContextMenu = contextMenu;

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
            DeleteSelectedItem();
            e.Handled = true;
        }
        else if (e.Key == Key.C && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        {
            CopySelectedItem();
            e.Handled = true;
        }
        else if (e.Key == Key.O && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        {
            OpenSelectedFileInFolder();
            e.Handled = true;
        }
        else if (e.Key == Key.Return)
        {
            OpenSelectedFile();
            e.Handled = true;
        }
    }

    private void OpenSelectedFile()
    {
        var selectedFile = listBox.SelectedItem as File;
        if (selectedFile == null)
        {
            return;
        }

        Process.Start(selectedFile.Path, null);
    }

    private void OpenSelectedFileInFolder()
    {
        var selectedFile = listBox.SelectedItem as File;
        if (selectedFile == null)
        {
            return;
        }

        Process.Start("explorer", "/select, \"" + selectedFile.Path + "\"");
    }

    private void CopySelectedItem()
    {
        var selectedFile = listBox.SelectedItem as File;
        if (selectedFile == null)
        {
            return;
        }

        try
        {
            Clipboard.SetText(selectedFile.Path);
        }
        catch
        {
        }
    }

    private void DeleteSelectedItem()
    {
        var selectedFile = listBox.SelectedItem as File;
        if (selectedFile == null)
        {
            return;
        }

        var view = CollectionViewSource.GetDefaultView(Files);
        view.MoveCurrentToNext();

        // https://stackoverflow.com/q/7363777/37899
        var container = (UIElement)listBox.ItemContainerGenerator.ContainerFromItem(listBox.SelectedItem);
        if (container != null)
        {
            container.Focus();
        }

        System.IO.File.Delete(selectedFile.Path);
        Files.Remove(selectedFile);
    }

    private static FileInfo[] GetFiles(string root)
    {
        var pattern = "*.*";
        var allFiles = new DirectoryInfo(root).GetFiles(pattern, SearchOption.AllDirectories);
        return allFiles;
    }

    public static Dictionary<string, HashSet<string>> FindDuplicateFiles(FileInfo[] allFiles)
    {
        var candidates = ExcludeUniqueFilesBySize(allFiles);

        var filesByHash = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        Parallel.ForEach(candidates, file =>
        {
            var hash = Utilities.SHA1Hash(file);

            HashSet<string> bucket;
            lock (filesByHash)
            {
                if (!filesByHash.TryGetValue(hash, out bucket))
                {
                    bucket = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

    private static IEnumerable<string> ExcludeUniqueFilesBySize(IEnumerable<FileInfo> allFiles)
    {
        var filesBySize = new Dictionary<long, HashSet<string>>();

        foreach (var file in allFiles)
        {
            if (!filesBySize.TryGetValue(file.Length, out var bucket))
            {
                bucket = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                filesBySize[file.Length] = bucket;
            }

            bucket.Add(file.FullName);
        }

        var result = new List<string>();
        foreach (var kvp in filesBySize)
        {
            if (kvp.Value.Count > 1)
            {
                result.AddRange(kvp.Value);
            }
        }

        return result;
    }
}