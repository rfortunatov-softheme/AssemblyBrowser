using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AssemblyBrowser.Properties;
using GraphLayout;

namespace AssemblyBrowser
{
    public sealed partial class MainWindow : INotifyPropertyChanged
    {
        private const string CsvOutputFilePath = @"../../../../{0}.csv";
        private static readonly Regex _nameCheck = new Regex("^[a-zA-Z].*");
        private readonly Dictionary<string, IEnumerable<ComboBoxItem>> _types = new Dictionary<string, IEnumerable<ComboBoxItem>>();
        private readonly List<LegendItem> _legend = new List<LegendItem>();
        private readonly List<Type>  _allTypes = new List<Type>();
        private Dictionary<string, Assembly> _assemblies = new Dictionary<string, Assembly>();
        private Dictionary<string, List<MemberInfo>> _memberInfos = new Dictionary<string, List<MemberInfo>>();
        private Dictionary<LegendItem, List<LegendItem>> _referencedAssemblies = new Dictionary<LegendItem, List<LegendItem>>();
        private DependencyGraph _currentGraph;
        private string _selectedAssembly;
        private Type _selectedType;
        private string _layoutType;

        public MainWindow()
        {
            InitializeComponent();
            LoadAssemblies();
            DataContext = this;
            LayoutType = "Tree";
            Gear.ContextMenu.DataContext = this;
            OnPropertyChanged(nameof(LayoutType));
        }
        
        public DependencyGraph GraphItem { get; set; }

        public string AssemblyName { get; set; }

        public ObservableCollection<LegendItem> ReferencedAssemblies { get; set; }

        public ObservableCollection<LegendItem> AssemblyTypesSource { get; set; } 

        public ObservableCollection<string> AllAssemblies => new ObservableCollection<string>(_assemblies.Keys);

        public ObservableCollection<ComboBoxItem> AllTypes => new ObservableCollection<ComboBoxItem>(string.IsNullOrEmpty(_selectedAssembly) ? Enumerable.Empty<ComboBoxItem>() : _types[_selectedAssembly]);

        public ObservableCollection<ComboBoxItem> MemberInfos => new ObservableCollection<ComboBoxItem>(_selectedType == null ? Enumerable.Empty<ComboBoxItem>() : _memberInfos[_selectedType.FullName].Select(x => new ComboBoxItem
        {
            Content = x.Name,
            Tag = x.MetadataToken
        }));

        public ObservableCollection<LegendItem> Legend => new ObservableCollection<LegendItem>(_legend); 

        public event PropertyChangedEventHandler PropertyChanged;

        public string TotalVertices => GraphItem?.Vertices.Count().ToString() ?? "None";

        public ObservableCollection<string> LayoutTypes => new ObservableCollection<string>(new []
        {
            "BoundedFR",
            "Circular",
            "CompoundFDP",
            "EfficientSugiyama",
            "FR",
            "ISOM",
            "KK",
            "LinLog",
            "Tree"
        });

        public string LayoutType
        {
            get { return _layoutType; }
            set
            {
                _layoutType = value;
                OnPropertyChanged(nameof(LayoutType));
                OnPropertyChanged(nameof(GraphItem));
                OnPropertyChanged(nameof(TotalVertices));
            }
        }

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Scroll(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                Zoom.Zoom += 0.01;
            }
            else
            {
                Zoom.Zoom -= 0.01;
            }
        }

        private void ShowTypes(object sender, MouseButtonEventArgs e)
        {
            var textBlock = sender as TextBlock;
            if (textBlock == null) return;
            if (!_assemblies.ContainsKey(textBlock.Text)) return;

            AssemblyTypesSource = new ObservableCollection<LegendItem>(_referencedAssemblies.First(x => string.Equals(x.Key.AssemblyName, textBlock.Text)).Value);
            References.Visibility = Visibility.Collapsed;
            AssemblyTypes.Visibility = Visibility.Visible;
            OnPropertyChanged(nameof(AssemblyTypesSource));
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
            Members.IsEnabled = false;
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
            if (_selectedType == null)
            {
                return;
            }
            
            if (_memberInfos.ContainsKey(_selectedType.FullName))
            {
                _memberInfos[_selectedType.FullName].Clear();
                _memberInfos[_selectedType.FullName].AddRange(_selectedType.GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance));
            }
            else
            {
                _memberInfos.Add(_selectedType.FullName, _selectedType.GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance).ToList());
            }
            
            Members.IsEnabled = true;
            Members.SelectedItem = null;
            OnPropertyChanged(nameof(MemberInfos));
        }

        private void BuildGraph(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(BuildGraphInternal);
        }

        private void BuildGraphInternal()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    Loading.Visibility = Visibility.Visible;
                    MainGrid.IsEnabled = false;
                });

                var parser = new TypeInfoParser();
                _legend.Clear();
                MemberInfo selectedMember = null;
                Dispatcher.Invoke(() =>
                {
                    if (Members.SelectedItem != null)
                    {
                        selectedMember = _memberInfos[_selectedType.FullName].FirstOrDefault(x => x.MetadataToken == (int) (Members.SelectedItem as ComboBoxItem).Tag);
                    }
                });

                _currentGraph = selectedMember == null
                    ? parser.GetTypeInfoGraph(_selectedType, _legend, _allTypes)
                    : parser.GetMemberInfoGraph(selectedMember, _legend, _allTypes);

                GraphItem = _currentGraph;

                WriteDependenciesToCsvFile(_selectedType.Name, GraphItem.Vertices);

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
                            return GraphItem.Edges
                                .Where(
                                    z =>
                                        z.Source.Equals(info) &&
                                        assemblyGroups.Where(k => string.Equals(k.Key, x.Key))
                                            .SelectMany(v => v)
                                            .Contains(z.Target))
                                .Select(z => z.Target);
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
            catch (Exception ex)
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
                    OnPropertyChanged(nameof(TotalVertices));
                });
            }
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

        private void OnMembersFilter(object sender, TextChangedEventArgs e)
        {
            if (!Members.IsDropDownOpen)
            {
                Members.IsDropDownOpen = true;
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
            References.Visibility = Visibility.Visible;
            AssemblyTypes.Visibility = Visibility.Collapsed;
        }
        
        private void SetMainGraph(object sender, RoutedEventArgs e)
        {
            GraphItem = _currentGraph;
            OnPropertyChanged(nameof(GraphItem));
            OnPropertyChanged(nameof(TotalVertices));
        }

        private void RefreshAssemblies(object sender, RoutedEventArgs e)
        {
            LoadAssemblies();
        }

        private void LoadAssemblies()
        {
            _assemblies.Clear();
            _types.Clear();
            _allTypes.Clear();
            _legend.Clear();
            _referencedAssemblies.Clear();
            _currentGraph = null;
            ReferencedAssemblies?.Clear();
            AssemblyTypesSource?.Clear();
            Assemblies.SelectedItem = null;
            Types.SelectedItem = null;
            Types.IsEnabled = false;
            Dispatcher.Invoke(() => { Loading.Visibility = Visibility.Visible; MainGrid.IsEnabled = false; });
            var files = Directory.GetFiles(App.AssembliesPath, "*.dll");
            Task.Factory.StartNew(() =>
            {
                Parallel.ForEach(files, file =>
                {
                    try
                    {
                        Assembly assembly;
                        using (Stream stream = File.OpenRead(file))
                        {
                            byte[] rawAssembly = new byte[stream.Length];
                            stream.Read(rawAssembly, 0, (int)stream.Length);
                            assembly = Assembly.Load(rawAssembly);
                        }

                        var types = GetLoadableTypes(assembly).Where(x => _nameCheck.IsMatch(x.Name)).OrderBy(x => x.Name).ToList();
                        _allTypes.AddRange(types.Except(_allTypes));

                        _assemblies.Add(assembly.GetName().Name, assembly);
                        _types.Add(assembly.GetName().Name, types.OrderBy(x => x.Name).Select(x => new ComboBoxItem { Content = x.Name, Tag = x }));
                    }
                    catch (Exception)
                    {
                    }
                });

                _assemblies = _assemblies.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
                Dispatcher.Invoke(() =>
                {
                    Assemblies.IsEnabled = true;
                    Zoom.Visibility = Visibility.Visible;
                    OnPropertyChanged(nameof(AllAssemblies));
                    OnPropertyChanged(nameof(AllTypes));
                    OnPropertyChanged(nameof(GraphItem));
                    OnPropertyChanged(nameof(TotalVertices));
                });
                Dispatcher.Invoke(() => { Loading.Visibility = Visibility.Collapsed; MainGrid.IsEnabled = true; });
            });
        }

        private void OpenContextMenu(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null)
            {
                return;
            }

            button.ContextMenu.IsOpen = !button.ContextMenu.IsOpen;
        }

        private void UpdateGraph(object sender, RoutedEventArgs e)
        {
            var newGraph = new DependencyGraph();
            var button = sender as Button;
            if (button != null)
            {
                FilterByType(button.Tag.ToString(), newGraph);
            }
            else if (sender is CheckBox)
            {
                FilterByAssemblies(newGraph);
            }
            else
            {
                return;
            }
            
            GraphItem = newGraph;
            OnPropertyChanged(nameof(GraphItem));
            OnPropertyChanged(nameof(TotalVertices));
        }

        private void FilterByAssemblies(DependencyGraph newGraph)
        {
            var matchingEdges = _currentGraph.Edges.Where(x => ReferencedAssemblies.Any(y => y.ShouldShow && x.Source.Assembly == y.AssemblyName) && ReferencedAssemblies.Any(y => y.ShouldShow && x.Target.Assembly == y.AssemblyName));
            foreach (var edge in matchingEdges)
            {
                if (!newGraph.Vertices.Contains(edge.Source))
                {
                    newGraph.AddVertex(edge.Source);
                }

                if (!newGraph.Vertices.Contains(edge.Target))
                {
                    newGraph.AddVertex(edge.Target);
                }

                newGraph.AddEdge(edge);
            }
        }

        private void FilterByType(string typeName, DependencyGraph newGraph)
        {
            TypeInfo previousType = null;
            var thisType = _currentGraph.Edges.FirstOrDefault(x => string.Equals(x.Source.Name, typeName))?.Source
                           ?? _currentGraph.Edges.FirstOrDefault(x => string.Equals(x.Target.Name, typeName))?.Target;
            while (thisType != null && !string.Equals(thisType.Type.FullName, _selectedType.FullName))
            {
                var type = thisType;
                var parent = previousType;
                var edgesToUse = previousType == null
                    ? _currentGraph.Edges.Where(x => string.Equals(x.Source.Name, type.Name))
                    : _currentGraph.Edges.Where(x => string.Equals(x.Target.Name, parent.Name) && string.Equals(x.Source.Name, type.Name));
                foreach (var edge in edgesToUse)
                {
                    if (!newGraph.Vertices.Contains(edge.Source))
                    {
                        newGraph.AddVertex(edge.Source);
                    }

                    if (!newGraph.Vertices.Contains(edge.Target))
                    {
                        newGraph.AddVertex(edge.Target);
                    }

                    newGraph.AddEdge(edge);
                }

                previousType = thisType;
                thisType = thisType.Parent;
            }
        }

        private void WriteDependenciesToCsvFile(string selectedTypeName, IEnumerable<TypeInfo> typeInfos)
        {
            using (var csvFileStream = new StreamWriter(string.Format(CsvOutputFilePath, selectedTypeName), false))
            {
                csvFileStream.WriteLine("{0},{1}", "Name", "Type");
                int i = 0;

                foreach (var typeInfo in typeInfos)
                {
                    var name = typeInfo.ParentType != null ? string.Format("{0}.{1}", typeInfo.ParentType.Name, typeInfo.Name) : typeInfo.Name;
                    var type = typeInfo.Name;

                    csvFileStream.WriteLine("{0},{1}", name, type);

                    i++;

                    foreach (var field in typeInfo.Type.GetProperties())
                    {
                        name = string.Format("{0}.{1}", typeInfo.Type.Name, field.Name);
                        type = field.PropertyType.Name;

                        csvFileStream.WriteLine("{0},{1}", name, type);

                        i++;

                        if (i >= 50)
                        {
                            csvFileStream.Flush();
                            i = 0;
                        }
                    }
                }

                csvFileStream.Flush();
            }
        }
    }
}