using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AssemblyBrowser.Properties;
using Graphviz4Net.Graphs;

namespace AssemblyBrowser
{
    public partial class MainWindow : INotifyPropertyChanged
    {
        private readonly Dictionary<string, Assembly> _assemblies = new Dictionary<string, Assembly>();
        private readonly Dictionary<string, IEnumerable<ComboBoxItem>> _types = new Dictionary<string, IEnumerable<ComboBoxItem>>();
        private readonly List<LegendItem> _legend = new List<LegendItem>();
        private Dictionary<LegendItem, List<LegendItem>> _referencedAssemblies = new Dictionary<LegendItem, List<LegendItem>>();
        private Graph<TypeInfo> _currentGraph;
        private string _selectedAssembly;
        private Type _selectedType;

        public MainWindow()
        {
            InitializeComponent();
            LoadAssemblies();
            DataContext = this;
        }
        
        public Graph<TypeInfo> GraphItem { get; set; }

        public string AssemblyName { get; set; }

        public ObservableCollection<LegendItem> ReferencedAssemblies { get; set; }

        public ObservableCollection<string> AllAssemblies => new ObservableCollection<string>(_assemblies.Keys);

        public ObservableCollection<ComboBoxItem> AllTypes => new ObservableCollection<ComboBoxItem>(string.IsNullOrEmpty(_selectedAssembly) ? Enumerable.Empty<ComboBoxItem>() : _types[_selectedAssembly]);

        public ObservableCollection<LegendItem> Legend => new ObservableCollection<LegendItem>(_legend); 

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Scroll(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                Zoom.Zoom += 0.1;
            }
            else
            {
                Zoom.Zoom -= 0.1;
            }
        }

        private void CopyNameOrGoToTypes(object sender, MouseButtonEventArgs e)
        {
            var textBlock = sender as TextBlock;
            if (textBlock != null)
            {
                Clipboard.SetText(textBlock.Text);
                if (_assemblies.ContainsKey(textBlock.Text))
                {
                    ReferencedAssemblies = new ObservableCollection<LegendItem>(_referencedAssemblies.First(x => string.Equals(x.Key.AssemblyName, textBlock.Text)).Value);
                    OnPropertyChanged(nameof(ReferencedAssemblies));
                }
            }
        }

        private void AssemblySelected(object sender, EventArgs e)
        {
            if (Assemblies.SelectedItem == null)
            {
                return;
            }

            if (Assemblies.IsDropDownOpen)
            {
                return;
            }

            _selectedAssembly = (string)Assemblies.SelectedItem;
            Types.IsEnabled = true;
            OnPropertyChanged(nameof(AllTypes));
        }
        
        private void TypeSelected(object sender, EventArgs e)
        {
            if (Types.IsDropDownOpen)
            {
                return;
            }

            var typeWrapper = Types.SelectedItem as ComboBoxItem;
            if (typeWrapper == null)
            {
                return;
            }

            _selectedType = typeWrapper.Tag as Type;
            Dispatcher.Invoke(() => { Loading.Visibility = Visibility.Visible; MainGrid.IsEnabled = false; });
            Task.Factory.StartNew(() =>
            {
                try
                { 
                    var parser = new TypeInfoParser();
                    _legend.Clear();
                    _currentGraph = parser.GetTypeInfoGraph(_selectedType, _legend);
                    GraphItem = _currentGraph;
                    GraphItem.Rankdir = RankDirection.TopToBottom;
                    var assemblyGroups = GraphItem.Vertices.GroupBy(x => x.Assembly).ToList();
                    _referencedAssemblies = assemblyGroups.ToDictionary(
                        x =>
                            new LegendItem
                            {
                                AssemblyName = x.Key,
                                Color = _legend.First(y => y.AssemblyName.Equals(x.Key)).Color
                            },
                        x =>
                        {
                            var sortFunction = new Func<TypeInfo, IEnumerable<TypeInfo>>(info =>
                            {
                                return GraphItem.VerticesEdges
                                    .Where(z => z.Source.Equals(info) && assemblyGroups.Where(k => string.Equals(k.Key, x.Key)).SelectMany(v => v).Contains(z.Destination))
                                    .Select(z => z.Destination);
                            });

                            var itemsSource = Sort(x, sortFunction).Select(y =>
                            {
                                var tb = new LegendItem {AssemblyName = y.Name, Color = Brushes.Transparent};
                                return tb;
                            }).ToList();

                            return itemsSource;
                        });

                    ReferencedAssemblies = new ObservableCollection<LegendItem>(_referencedAssemblies.Keys);
                }
                catch(Exception ex)
                { 
                    Dispatcher.Invoke(() => MessageBox.Show(ex.Message, "Error!", MessageBoxButton.OK, MessageBoxImage.Error));
                }
                finally
                {
                    Dispatcher.Invoke(() =>
                    {
                        Loading.Visibility = Visibility.Collapsed;
                        MainGrid.IsEnabled = true;
                        OnPropertyChanged(nameof(GraphItem));
                        OnPropertyChanged(nameof(ReferencedAssemblies));
                        OnPropertyChanged(nameof(Legend));
                    });
                }
            });
        }
        
        private void OnAssembliesFilter(object sender, TextChangedEventArgs e)
        {
            if (!Assemblies.IsDropDownOpen)
            {
                Assemblies.IsDropDownOpen = true;
            }
        }

        private void OnTypesFilter(object sender, TextChangedEventArgs e)
        {
            if (!Types.IsDropDownOpen)
            {
                Types.IsDropDownOpen = true;
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(p => p != null);
            }
        }

        private static IEnumerable<T> Sort<T>(IEnumerable<T> source, Func<T, IEnumerable<T>> dependencies,
            bool throwOnCycle = false)
        {
            var sorted = new List<T>();
            var visited = new HashSet<T>();

            foreach (var item in source)
                Visit(item, visited, sorted, dependencies, throwOnCycle);

            return sorted;
        }

        private static void Visit<T>(T item, HashSet<T> visited, List<T> sorted, Func<T, IEnumerable<T>> dependencies,
            bool throwOnCycle)
        {
            if (!visited.Contains(item))
            {
                visited.Add(item);

                foreach (var dep in dependencies(item))
                    Visit(dep, visited, sorted, dependencies, throwOnCycle);

                sorted.Add(item);
            }
            else
            {
                if (throwOnCycle && !sorted.Contains(item))
                    throw new InvalidOperationException();
            }
        }

        private void GoBack(object sender, RoutedEventArgs e)
        {
            ReferencedAssemblies = new ObservableCollection<LegendItem>(_referencedAssemblies.Keys);
            OnPropertyChanged(nameof(ReferencedAssemblies));
        }

        private void BuildSmallGraph(object sender, MouseButtonEventArgs e)
        {
            var textBlock = sender as TextBlock;
            if (textBlock == null || _assemblies.ContainsKey(textBlock.Text))
            {
                return;
            }
            
            var newGraph = new Graph<TypeInfo>();
            TypeInfo previousType = null;
            var thisType = _currentGraph.Edges.Cast<Edge<TypeInfo>>().FirstOrDefault(x => string.Equals(x.Source.Name, textBlock.Text))?.Source 
                           ?? _currentGraph.Edges.Cast<Edge<TypeInfo>>().FirstOrDefault(x => string.Equals(x.Destination.Name, textBlock.Text))?.Destination;
            while (thisType != null && !string.Equals(thisType.Type.FullName, _selectedType.FullName))
            {
                var type = thisType;
                var parent = previousType;
                var edgesToUse = previousType == null 
                    ? _currentGraph.Edges.Cast<Edge<TypeInfo>>().Where(x => string.Equals(x.Source.Name, type.Name))
                    : _currentGraph.Edges.Cast<Edge<TypeInfo>>().Where(x => string.Equals(x.Destination.Name, parent.Name) && string.Equals(x.Source.Name, type.Name));
                foreach (var edge in edgesToUse)
                {
                    newGraph.AddEdge(edge);
                    if (!newGraph.Vertices.Contains(edge.Source))
                    {
                        newGraph.AddVertex(edge.Source);
                    }

                    if (!newGraph.Vertices.Contains(edge.Destination))
                    {
                        newGraph.AddVertex(edge.Destination);
                    }
                }

                previousType = thisType;
                thisType = thisType.Parent;
            }
            
            GraphItem = newGraph;
            OnPropertyChanged(nameof(GraphItem));
        }

        private void SetMainGraph(object sender, RoutedEventArgs e)
        {
            GraphItem = _currentGraph;
            OnPropertyChanged(nameof(GraphItem));
        }

        private void RefreshAssemblies(object sender, RoutedEventArgs e)
        {
            LoadAssemblies();
        }

        private void LoadAssemblies()
        {
            Dispatcher.Invoke(() => { Loading.Visibility = Visibility.Visible; MainGrid.IsEnabled = false; });
            var files = Directory.GetFiles(App.AssembliesPath, "*.dll");
            Task.Factory.StartNew(() =>
            {
                Parallel.ForEach(files, file =>
                {
                    try
                    {
                        var assembly = Assembly.LoadFile(file);
                        var types = GetLoadableTypes(assembly).Where(x => x.Namespace != null && x.Namespace.StartsWith("Replay")).ToList();
                        if (types.Any(x => x.Namespace?.StartsWith("Replay") == true))
                        {
                            _assemblies.Add(assembly.GetName().Name, assembly);
                            _types.Add(assembly.GetName().Name, types.OrderBy(x => x.Name).Select(x => new ComboBoxItem { Content = x.Name, Tag = x }));
                        }
                    }
                    catch (Exception e)
                    {
                    }
                });

                Dispatcher.Invoke(() =>
                {
                    Assemblies.IsEnabled = true;
                    Zoom.Visibility = Visibility.Visible;
                    OnPropertyChanged(nameof(AllAssemblies));
                    OnPropertyChanged(nameof(AllTypes));
                });
                Dispatcher.Invoke(() => { Loading.Visibility = Visibility.Collapsed; MainGrid.IsEnabled = true; });
            });
        }
    }
}