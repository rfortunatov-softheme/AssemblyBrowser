// <copyright company="Dell Inc.">
//     Confidential and Proprietary
//     Copyright © 2015 Dell Inc. 
//     ALL RIGHTS RESERVED.
// </copyright>

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace AssemblyBrowser
{
    public static class ReflectorExtensions
    {
        public static T GetAttribute<T>(this MemberInfo reflect)
        {
            return reflect.GetCustomAttributes(typeof(T), false).Cast<T>().FirstOrDefault();
        }

        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters", Justification = "I like generic way.")]
        public static T GetAttribute<T>(this Type reflect)
        {
            return reflect.GetCustomAttributes(typeof(T), false).Cast<T>().FirstOrDefault();
        }

        public static T GetAttribute<T>(this Assembly reflect)
        {
            return reflect.GetCustomAttributes(typeof(T), false).Cast<T>().FirstOrDefault();
        }
    }
}