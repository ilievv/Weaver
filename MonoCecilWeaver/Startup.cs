﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using Mono.Cecil;
using MonoCecilWeaver.Core;
using MonoCecilWeaver.Handlers;

namespace MonoCecilWeaver
{
    internal static class Startup
    {
        private const string ReadonlyAssemblySuffix = "readonly";

        private static void Main(string[] args)
        {
            var options = new Options();
            if (!Parser.Default.ParseArguments(args, options))
            {
                throw new ArgumentException("Invalid parameters.");
            }

            /* In order to leave the main assembly unlocked,
            we create a copy of the file and use it for readonly operations */
            var readonlyAssemblyPath = CreateReadonlyAssembly(options.AssemblyPath);

            var dependencyDirectories = new List<string>
            {
                Path.GetDirectoryName(options.AssemblyPath)
            };

            if (options.DependencyDirectories != null)
            {
                dependencyDirectories.AddRange(options.DependencyDirectories);
            }

            var assemblyWeaver = new AssemblyWeaver(options.AssemblyPath, dependencyDirectories);
            var assemblyResolver = new AssemblyResolver(readonlyAssemblyPath, dependencyDirectories);
            var definitionProvider = new DefinitionProvider(assemblyWeaver.AssemblyDefinition);

            if (options.ShouldEnableLogging)
            {
                SetupExceptionLogger(assemblyWeaver, assemblyResolver, definitionProvider.MethodDefinitions);
            }

            if (options.ShouldEnableProfiler)
            {
                SetupPerformanceProfiler(assemblyWeaver, assemblyResolver, definitionProvider.MethodDefinitions);
            }

            assemblyWeaver.Reweave();
        }

        private static void SetupExceptionLogger(
            AssemblyWeaver assemblyWeaver,
            AssemblyResolver assemblyResolver,
            IEnumerable<MethodDefinition> methodDefinitions) =>
                methodDefinitions
                    .Where(m => m.ShouldEnableLogging(assemblyResolver))
                    .Setup(assemblyWeaver)
                    .Rethrow<Exception, TestContextExceptionLogger>();

        private static void SetupPerformanceProfiler(
            AssemblyWeaver assemblyWeaver,
            AssemblyResolver assemblyResolver,
            IEnumerable<MethodDefinition> methodDefinitions) =>
                methodDefinitions
                    .Where(m => m.ShouldEnableProfiler(assemblyResolver))
                    .Setup(assemblyWeaver)
                    .Measure<PerformanceLogger>();

        private static string CreateReadonlyAssembly(string filePath, bool overwrite = true)
        {
            var binPath = Path.GetDirectoryName(filePath);
            var backupFilePath = $"{binPath}{Path.DirectorySeparatorChar}{Path.GetFileName(filePath)}.{ReadonlyAssemblySuffix}";

            File.Copy(filePath, backupFilePath, overwrite);

            return backupFilePath;
        }
    }
}
