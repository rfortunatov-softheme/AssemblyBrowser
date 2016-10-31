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
using GraphLayout;
using QuickGraph;

namespace AssemblyBrowser
{
    public class TypeInfoParser
    {
        private const BindingFlags PrivateAndPublic = BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance;

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

        public DependencyGraph GetTypeInfoGraph(Type type, List<LegendItem> legend,  bool makeFullDump = false)
        {
            var graph = new DependencyGraph();
            FillTypeInfoGraph(type, graph);
            legend.AddRange(_assemblyColors.Select(x => new LegendItem {Color = x.Value, AssemblyName = x.Key}));
            return graph;
        }

        private void FillTypeInfoGraph(Type type, DependencyGraph rootTypeInfo, TypeInfo parent = null)
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
                var matchingEdge = rootTypeInfo.Edges.FirstOrDefault(x => x.Source.Equals(parent) && x.Target.Equals(vert));
                if (matchingEdge == null && !parent.Equals(vert))
                {
                    rootTypeInfo.AddEdge(new Edge<TypeInfo>(parent, vert));
                }
            }
            else
            {
                rootTypeInfo.AddVertex(typeInfo);
                if (parent != null)
                {
                    var matchingEdge = rootTypeInfo.Edges.FirstOrDefault(x => x.Source.Equals(parent) && x.Target.Equals(typeInfo));
                    if (matchingEdge == null)
                    {
                        rootTypeInfo.AddEdge(new Edge<TypeInfo>(parent, typeInfo));
                    }
                }

                if (typeInfo.DeepDigType)
                {
                    ProcessBaseTypes(type, rootTypeInfo, typeInfo);

                    ProcessInterfaces(type, rootTypeInfo, typeInfo);

                    ProcessKnownTypeAttributes(type, rootTypeInfo, typeInfo);

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

        private void ProcessProperties(Type type, DependencyGraph rootTypeInfo, TypeInfo typeInfo)
        {
            var properties = type.GetProperties(PrivateAndPublic);
            foreach (var property in properties)
            {
                FillTypeInfoGraph(property.PropertyType, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessNetstedTypes(Type type, DependencyGraph rootTypeInfo, TypeInfo typeInfo)
        {
            var nestedTypes = type.GetNestedTypes(PrivateAndPublic);
            foreach (var nestedType in nestedTypes)
            {
                FillTypeInfoGraph(nestedType, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessGenericArguments(Type type, DependencyGraph rootTypeInfo, TypeInfo typeInfo)
        {
            var genericArguments = type.GetGenericArguments();
            foreach (var argument in genericArguments)
            {
                FillTypeInfoGraph(argument, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessEvents(Type type, DependencyGraph rootTypeInfo, TypeInfo typeInfo)
        {
            var events = type.GetEvents(PrivateAndPublic);
            foreach (var eventInfo in events)
            {
                FillTypeInfoGraph(eventInfo.EventHandlerType, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessMethods(Type type, DependencyGraph rootTypeInfo, TypeInfo typeInfo)
        {
            var methods = type.GetMethods(PrivateAndPublic);
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

        private void ProcessFields(Type type, DependencyGraph rootTypeInfo, TypeInfo typeInfo)
        {
            var fields = type.GetFields(PrivateAndPublic);
            foreach (var field in fields)
            {
                FillTypeInfoGraph(field.FieldType, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessInterfaces(Type type, DependencyGraph rootTypeInfo, TypeInfo typeInfo)
        {
            foreach (var inter in type.GetInterfaces())
            {
                FillTypeInfoGraph(inter, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessBaseTypes(Type type, DependencyGraph rootTypeInfo, TypeInfo typeInfo)
        {
            if (type.BaseType?.Namespace != null && !type.BaseType.Namespace.StartsWith("System"))
            {
                FillTypeInfoGraph(type.BaseType, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessConstructors(Type type, DependencyGraph rootTypeInfo, TypeInfo typeInfo)
        {
            var constructors = type.GetConstructors(PrivateAndPublic);
            foreach (var parameter in constructors.Select(constructor => constructor.GetParameters()).SelectMany(parameters => parameters))
            {
                FillTypeInfoGraph(parameter.ParameterType, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessKnownTypeAttributes(MemberInfo type, DependencyGraph rootTypeInfo, TypeInfo typeInfo)
        {
            foreach (var attribute in type.GetCustomAttributes<KnownTypeAttribute>().ToArray().Where(attribute => attribute.Type != null))
            {
                FillTypeInfoGraph(attribute.Type, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessCustomAttributes(MemberInfo type, DependencyGraph rootTypeInfo, TypeInfo typeInfo)
        {
            foreach (var attribute in type.GetCustomAttributesData().ToArray())
            {
                FillTypeInfoGraph(attribute.AttributeType, rootTypeInfo, typeInfo);
            }
        }

        private void ProcessMembersAttributes(IEnumerable<MemberInfo> members, DependencyGraph rootTypeInfo, TypeInfo typeInfo)
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