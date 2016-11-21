﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KuriimuContract;

namespace Kuriimu
{
	public static class PluginLoader<T>
	{
		public static ICollection<T> LoadPlugins(string path)
		{
			string[] dllFileNames = null;

			if(Directory.Exists(path))
			{
				Console.WriteLine("Loading plugins...");
				dllFileNames = Directory.GetFiles(path, "*.dll");

				ICollection<Assembly> assemblies = new List<Assembly>(dllFileNames.Length);
				foreach(string dllFile in dllFileNames)
				{
					AssemblyName an = AssemblyName.GetAssemblyName(dllFile);
					Assembly assembly = Assembly.Load(an);
					assemblies.Add(assembly);
				}

				Type pluginType = typeof(T);
				ICollection<Type> pluginTypes = new List<Type>();
				foreach(Assembly assembly in assemblies)
				{
					if(assembly != null)
					{
						Type[] types = assembly.GetTypes();

						foreach(Type type in types)
						{
							if(type.IsInterface || type.IsAbstract)
							{
								continue;
							}
							else
							{
								if(type.GetInterface(pluginType.FullName) != null)
								{
									pluginTypes.Add(type);
									Console.WriteLine("Loaded " + assembly.FullName);
								}
							}
						}
					}
				}

				ICollection<T> plugins = new List<T>(pluginTypes.Count);
				foreach(Type type in pluginTypes)
				{
					T plugin = (T)Activator.CreateInstance(type);
					plugins.Add(plugin);
				}

				Console.WriteLine("Plugins loaded.");
				return plugins;
			}

			return null;
		}
	}
}