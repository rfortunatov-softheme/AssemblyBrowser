// <copyright company="Dell Inc.">
//     Confidential and Proprietary
//     Copyright © 2015 Dell Inc. 
//     ALL RIGHTS RESERVED.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Media;
using Graphviz4Net.Graphs;

namespace AssemblyBrowser
{
    public class TypeInfoParser
    {
        private static readonly Brush[] _brushesToUse = {
            Brushes.DarkCyan,
            Brushes.MediumVioletRed,
            Brushes.Green,
            Brushes.Yellow,
            Brushes.Tomato,
            Brushes.Aqua,
            Brushes.DarkOrchid,
            Brushes.Navy
        };

        private readonly Dictionary<string, Brush>  _assemblyColors = new Dictionary<string, Brush>();

        public Graph<TypeInfo> GetTypeInfoGraph(Type type, List<LegendItem> legend,  bool makeFullDump = false)
        {
            var graph = new Graph<TypeInfo>();
            FillTypeInfoGraph(type, graph);
            legend.AddRange(_assemblyColors.Select(x => new LegendItem {Color = x.Value, AssemblyName = x.Key}));
            return graph;
        }

        private void FillTypeInfoGraph(Type type, Graph<TypeInfo> rootTypeInfo, TypeInfo parent = null)
        {
            var enumerableEntryType = IsTypeOfEnumerableKind(type) ? GetEnumerableEntryType(type) : null;
            if (!ShouldProcessType(type, enumerableEntryType))
            {
                return;
            }

            var typeInfo = GenerateTypeInfo(type, parent, enumerableEntryType);

            if (rootTypeInfo.Vertices.Any(x => x.Equals(typeInfo)) && parent != null)
            {
                var vert = rootTypeInfo.Vertices.First(x => x.Equals(typeInfo));
                var matchingEdge = rootTypeInfo.Edges.FirstOrDefault(x => x.Source.Equals(parent) && x.Destination.Equals(vert));
                if (matchingEdge == null && !parent.Equals(vert))
                {
                    rootTypeInfo.AddEdge(new Edge<TypeInfo>(parent, vert, new Arrow()));
                }
            }
            else
            {
                rootTypeInfo.AddVertex(typeInfo);
                if (parent != null)
                {
                    var matchingEdge = rootTypeInfo.Edges.FirstOrDefault(x => x.Source.Equals(parent) && x.Destination.Equals(typeInfo));
                    if (matchingEdge == null)
                    {
                        rootTypeInfo.AddEdge(new Edge<TypeInfo>(parent, typeInfo, new Arrow()));
                    }
                }

                if (typeInfo.DeepDigType)
                {
                    ProcessBaseTypes(type, rootTypeInfo, typeInfo);

                    ProcessCustomAttributes(type, rootTypeInfo, typeInfo);

                    ProcessConstructors(type, rootTypeInfo, typeInfo);

                    ProcessFields(type, rootTypeInfo, typeInfo);

                    ProcessMethods(type, rootTypeInfo, typeInfo);

                    ProcessEvents(type, rootTypeInfo, typeInfo);

                    ProcessGenericArguments(type, rootTypeInfo, typeInfo);

                    ProcessNetstedTypes(type, rootTypeInfo, typeInfo);

                    ProcessProperties(type, rootTypeInfo, typeInfo);

                    ProcessMembersAttributes(type.GetMembers(), rootTypeInfo, typeInfo);
                }

                if (enumerableEntryType != null)
                {
                    FillTypeInfoGraph(enumerableEntryType, rootTypeInfo, typeInfo);
                }
            }
        }

        private void ProcessProperties(Type type, Graph<TypeInfo> rootTypeInfo, TypeInfo typeInfo)
        {
            var properties = type.GetProperties();
            foreach (var property in properties)
            {
                FillTypeInfoGraph(property.PropertyType, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessNetstedTypes(Type type, Graph<TypeInfo> rootTypeInfo, TypeInfo typeInfo)
        {
            var nestedTypes = type.GetNestedTypes();
            foreach (var nestedType in nestedTypes)
            {
                FillTypeInfoGraph(nestedType, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessGenericArguments(Type type, Graph<TypeInfo> rootTypeInfo, TypeInfo typeInfo)
        {
            var genericArguments = type.GetGenericArguments();
            foreach (var argument in genericArguments)
            {
                FillTypeInfoGraph(argument, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessEvents(Type type, Graph<TypeInfo> rootTypeInfo, TypeInfo typeInfo)
        {
            var events = type.GetEvents();
            foreach (var eventInfo in events)
            {
                FillTypeInfoGraph(eventInfo.EventHandlerType, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessMethods(Type type, Graph<TypeInfo> rootTypeInfo, TypeInfo typeInfo)
        {
            var methods = type.GetMethods();
            foreach (var method in methods)
            {
                FillTypeInfoGraph(method.ReturnType, rootTypeInfo, typeInfo);
                var methodParameters = method.GetParameters();
                foreach (var parameter in methodParameters)
                {
                    FillTypeInfoGraph(parameter.ParameterType, rootTypeInfo, typeInfo);
                }

                var methodBody = method.GetMethodBody();
                if (methodBody == null)
                {
                    continue;
                }

                foreach (var localVariable in methodBody.LocalVariables)
                {
                    FillTypeInfoGraph(localVariable.LocalType, rootTypeInfo, typeInfo);
                }
            }
        }

        private void ProcessFields(Type type, Graph<TypeInfo> rootTypeInfo, TypeInfo typeInfo)
        {
            var fields = type.GetFields();
            foreach (var field in fields)
            {
                FillTypeInfoGraph(field.FieldType, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessBaseTypes(Type type, Graph<TypeInfo> rootTypeInfo, TypeInfo typeInfo)
        {
            if (type.BaseType?.Namespace != null && !type.BaseType.Namespace.StartsWith("System."))
            {
                FillTypeInfoGraph(type.BaseType, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessConstructors(Type type, Graph<TypeInfo> rootTypeInfo, TypeInfo typeInfo)
        {
            var constructors = type.GetConstructors();
            foreach (var parameter in constructors.Select(constructor => constructor.GetParameters()).SelectMany(parameters => parameters))
            {
                FillTypeInfoGraph(parameter.ParameterType, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessCustomAttributes(MemberInfo type, Graph<TypeInfo> rootTypeInfo, TypeInfo typeInfo)
        {
            foreach (var attribute in type.GetCustomAttributes<KnownTypeAttribute>().ToArray().Where(attribute => attribute.Type != null))
            {
                FillTypeInfoGraph(attribute.Type, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessMembersAttributes(IEnumerable<MemberInfo> members, Graph<TypeInfo> rootTypeInfo, TypeInfo typeInfo)
        {
            foreach (var member in members)
            {
                var otherAttributes = member.GetCustomAttributesData();
                foreach (var customAttributeData in otherAttributes.Where(customAttributeData => customAttributeData.AttributeType.Namespace?.StartsWith("Replay") == true))
                {
                    FillTypeInfoGraph(customAttributeData.AttributeType, rootTypeInfo, typeInfo);
                }
            }
        }

        private TypeInfo GenerateTypeInfo(Type type, TypeInfo parent, Type enumerableEntryType)
        {
            TypeInfo resultType;
            if (enumerableEntryType != null && type.Namespace?.StartsWith("System") == true || type.Name.Contains("["))
            {
                resultType = new TypeInfo
                {
                    Name = $"{enumerableEntryType?.Name}-(generated collection)",
                    Type = typeof (IEnumerable<>).MakeGenericType(enumerableEntryType),
                    ParentType = parent?.Type,
                    Parent = parent,
                    Assembly = enumerableEntryType?.Assembly.GetName().Name,
                    DeepDigType = false
                };
            }
            else
            {
                resultType = new TypeInfo
                {
                    Name = type.Name,
                    Type = type,
                    ParentType = parent?.Type,
                    Parent = parent,
                    Assembly = type.Assembly.GetName().Name,
                };
            }

            if (!_assemblyColors.ContainsKey(resultType.Assembly))
            {
                Brush brush = Brushes.Transparent;
                if (_assemblyColors.Count >= _brushesToUse.Length)
                {
                    while (Equals(brush, Brushes.Transparent) || Equals(brush, Brushes.White) || Equals(brush, Brushes.Black) || _assemblyColors.Values.Any(x => Equals(x, brush)))
                    {
                        brush = PickBrush();
                    }
                }
                else
                {
                    brush = _brushesToUse[_assemblyColors.Count];
                }

                _assemblyColors.Add(resultType.Assembly, brush);
            }

            resultType.Backgroud = _assemblyColors[resultType.Assembly];
            return resultType;
        }

        private Brush PickBrush()
        {
            var rnd = new Random();

            var brushesType = typeof(Brushes);

            var properties = brushesType.GetProperties();

            var random = rnd.Next(properties.Length);
            var result = (Brush)properties[random].GetValue(null, null);

            return result;
        }

        private bool ShouldProcessType(Type type, Type enumerableEntryType)
        {
            if (type.IsPrimitive || type == typeof(string) || type.Name.Contains("&") || type.Name.Contains("<"))
            {
                return false;
            }

            if (type.Namespace == null || !type.Namespace.StartsWith("System"))
            {
                return true;
            }

            return enumerableEntryType?.Namespace != null && !enumerableEntryType.Namespace.StartsWith("System");
        }

        private Type GetEnumerableEntryType(Type enumerable)
        {
            return enumerable.GetElementType() ?? (enumerable.GetGenericArguments().FirstOrDefault() ?? (enumerable.BaseType == null
                ? null
                : (enumerable.BaseType.GetElementType() ?? enumerable.BaseType.GetGenericArguments().FirstOrDefault())));
        }

        private bool IsTypeOfEnumerableKind(Type type)
        {
            return typeof(IEnumerable).IsAssignableFrom(type) || typeof(ICollection).IsAssignableFrom(type) && type != typeof(string);
        }
    }
}