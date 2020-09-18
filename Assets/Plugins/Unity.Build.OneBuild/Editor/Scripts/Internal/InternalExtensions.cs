﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace UnityEditor.Build.OneBuild
{
    internal static class InternalExtensions
    {
        private static readonly DateTime UtcInitializationTime = new DateTime(1970, 1, 1, 0, 0, 0);

        /// <summary>
        /// 对应java的 System.currentTimeMillis()
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static long ToUtcMilliseconds(this DateTime dt)
        {
            dt = dt.ToUniversalTime();
            return (long)dt.Subtract(UtcInitializationTime).TotalMilliseconds;
        }

        public static DateTime FromUtcMilliseconds(this long milliseconds)
        {
            return UtcInitializationTime.AddMilliseconds(milliseconds);
        }

        public static int ToUtcSeconds(this DateTime dt)
        {
            dt = dt.ToUniversalTime();
            return (int)dt.Subtract(UtcInitializationTime).TotalSeconds;
        }

        public static DateTime FromUtcSeconds(this int seconds)
        {
            return UtcInitializationTime.AddSeconds(seconds);
        }

        public static string ToStringOrEmpty(this object obj)
        {
            if (obj == null)
                return string.Empty;
            return obj.ToString();
        }

        public static object GetDefaultValue(this Type type)
        {
            object defaultValue;
            if (type == typeof(string))
                defaultValue = null;
            else
            {
                if (type.IsValueType)
                    defaultValue = Activator.CreateInstance(type);
                else
                    defaultValue = null;
            }

            return defaultValue;
        }

        public static string ReplacePathSeparator(this string path)
        {
            if (path == null)
                return null;
            if (Path.DirectorySeparatorChar == '/')
                path = path.Replace('\\', Path.DirectorySeparatorChar);
            else
                path = path.Replace('/', Path.DirectorySeparatorChar);
            return path;
        }

        #region Xml

        public static bool HasAttribute(this XmlNode node, string name)
        {
            if (node.Attributes == null)
                return false;
            var attr = node.Attributes[name];
            if (attr == null)
                return false;
            return true;
        }
        public static XmlAttribute GetOrAddAttribute(this XmlNode node, string name, string defaultValue)
        {
            var attr = node.Attributes[name];
            if (attr == null)
            {
                attr = node.OwnerDocument.CreateAttribute(name);
                attr.Value = defaultValue;
                node.Attributes.Append(attr);
            }
            return attr;
        }

        public static void SetOrAddAttributeValue(this XmlNode node, string name, string value)
        {
            if (node.Attributes == null)
                return;
            var attr = node.Attributes[name];
            if (attr == null)
            {
                attr = node.OwnerDocument.CreateAttribute(name);
                node.Attributes.Append(attr);
            }

            attr.Value = value;
        }

        public static bool TryGetAttributeValue(this XmlNode node, string name, out string value)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (node.Attributes == null)
            {
                value = null;
                return false;
            }
            var attr = node.Attributes[name];
            if (attr == null)
            {
                value = null;
                return false;
            }
            value = attr.Value;
            return true;
        }
        public static bool TryGetAttributeValue<T>(this XmlNode node, string name, out T value)
        {
            string str;
            if (node.TryGetAttributeValue(name, out str))
            {
                Type type = typeof(T);
                if (type.IsEnum)
                {
                    value = (T)Enum.Parse(type, str);
                }
                else
                {
                    value = (T)Convert.ChangeType(str, type);
                }
                return true;
            }
            value = default(T);
            return false;
        }

        public static T GetAttributeValue<T>(this XmlNode node, string name, T defaultValue)
        {
            T value;
            if (!TryGetAttributeValue(node, name, out value))
            {
                value = defaultValue;
            }
            return value;
        }


        public static string GetOrAddAttributeValue(this XmlNode node, string name, string defaultValue)
        {
            var attr = GetOrAddAttribute(node, name, defaultValue);
            return attr.Value;
        }

        public static T GetOrAddAttributeValue<T>(this XmlNode node, string name, T defaultValue)
        {
            T value;
            if (!TryGetAttributeValue<T>(node, name, out value))
            {
                SetOrAddAttributeValue(node, name, defaultValue != null ? defaultValue.ToString() : string.Empty);
                value = defaultValue;
            }
            return value;
        }



        public static void RemoveAttribute(this XmlNode node, string name)
        {
            var attr = node.Attributes[name];
            if (attr != null)
            {
                node.Attributes.Remove(attr);
            }
        }

        #endregion
        public static IEnumerable<Assembly> Referenced(this IEnumerable<Assembly> assemblies, Assembly referenced)
        {
            string fullName = referenced.FullName;

            foreach (var ass in assemblies)
            {
                if (referenced == ass)
                {
                    yield return ass;
                }
                else
                {
                    foreach (var refAss in ass.GetReferencedAssemblies())
                    {
                        if (fullName == refAss.FullName)
                        {
                            yield return ass;
                            break;
                        }
                    }
                }
            }
        }
    }
}
