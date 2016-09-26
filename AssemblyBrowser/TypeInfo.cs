// <copyright company="Dell Inc.">
//     Confidential and Proprietary
//     Copyright © 2015 Dell Inc. 
//     ALL RIGHTS RESERVED.
// </copyright>

using System;
using System.Windows.Media;

namespace AssemblyBrowser
{
    public class TypeInfo
    {
        public TypeInfo()
        {
            DeepDigType = true;
        }

        public Type Type { get; set; }

        public Type ParentType { get; set; }

        public string Name { get; set; }

        public string Assembly { get; set; }

        public TypeInfo Parent { get; set; }

        public bool DeepDigType { get; set; }

        public Brush Backgroud { get; set; }

        public Brush Foreground => InvertBrush();

        public bool Equals(TypeInfo other)
        {
            if (other == null)
            {
                return false;
            }

            return string.Equals(Name, other.Name) && string.Equals(Assembly, other.Assembly);
        }

        private Brush InvertBrush()
        {
            var color = ((SolidColorBrush)Backgroud).Color;
            var invertedColor = new Color
            {
                ScR = 1.0F - color.ScR,
                ScG = 1.0F - color.ScG,
                ScB = 1.0F - color.ScB,
                ScA = color.ScA
            };
            return new SolidColorBrush(invertedColor);
        }
    }
}