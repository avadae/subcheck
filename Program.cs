﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;

namespace SubCheck
{
	class Program
	{
		private static Config config = new Config();

		private static void Main(string[] args)
		{
			Console.WriteLine($"Submission Checker v{Assembly.GetExecutingAssembly().GetName().Version}");

			#region validate input
			if (args.Length == 0)
			{
				Console.WriteLine("Usage: SubCheck filename");
				Console.ReadKey();
				return;
			}

			// concatenate all the args, because it's probably a filepath containing spaces that was given.
			string totalArgs = args[0];
			for (int i = 1; i < args.Length; i++)
				totalArgs += " " + args[i];

			var filename = Path.GetFullPath(totalArgs);
			if (!File.Exists(filename))
			{
				Console.WriteLine($"Failed to find {filename}");
				Console.ReadKey();
				return;
			}

			string configPath = Path.GetFullPath("config.xml");
			if (!File.Exists(configPath))
			{
				config = new Config();
				XmlSerializer.Serialize(configPath, config);
			}
			else
			{
				try
				{
					config = XmlSerializer.Deserialize<Config>(configPath);
				}
				catch (InvalidOperationException ioe)
				{
					Console.WriteLine("Failed to parse config.xml: " + ioe.Message);
					Console.ReadKey();
					return;
				}
			}

			var instances = MSBuildLocator.QueryVisualStudioInstances();
			foreach (var instance in instances)
			{
				if (instance.Version.Major == 17)
				{
					MSBuildLocator.RegisterMSBuildPath(instance.MSBuildPath);
					config.SetVisualStudioPath(instance.VisualStudioRootPath);
					Console.WriteLine($"Using {instance.Name} to build and analyze - version {instance.Version}");
					break;
				}
			}
			if (!MSBuildLocator.IsRegistered)
			{
				Console.WriteLine($"Visual Studio with major version number {config.VSMajorVersionNumber} was not found!");
				Console.ReadKey();
				return;
			}

			if (!string.IsNullOrEmpty(config.DevenvPath) &&
				!File.Exists(config.DevenvPath))
			{
				Console.WriteLine($"File in DevenvPath is not found: {config.DevenvPath}");
			}

			#endregion

			PerformAnalysis(filename, config);
		}

		public static void PerformAnalysis(string filename, Config config)
		{
			Console.WriteLine($"Analyzing {filename}");
			int nbIssues = CheckName(filename);

			SolutionFile solution = null;
			string solutionFileName = null;
			string zipDirectoryName = Path.GetFileNameWithoutExtension(filename);
			if (config.UseTempFolderForAnalysis && !config.OpenVSAfterReport)
				zipDirectoryName = Path.Combine(Path.GetTempPath(), "subcheck", zipDirectoryName);

			try
			{
				if (Directory.Exists(zipDirectoryName))
					Directory.Delete(zipDirectoryName, true);
				ZipFile.ExtractToDirectory(filename, zipDirectoryName);

				var files = Directory.GetFiles(zipDirectoryName, "*.sln", SearchOption.AllDirectories);
				nbIssues += Assert(files.Length == 1, "Found exactly one solution", $" - found {files.Length} solutions.");
				if (files.Length > 0) // let's assume the first one is the correct one.
				{
					solutionFileName = files[0];
					var solutionDirectoryName = Path.GetDirectoryName(solutionFileName);
					nbIssues += CheckSlnVersion(solutionFileName);
					nbIssues += CheckCleanFolder(solutionDirectoryName);

					ProjectCollection projects = new ProjectCollection();
					solution = SolutionFile.Parse(Path.GetFullPath(solutionFileName));
					foreach (var projectInSolution in solution.ProjectsInOrder)
					{
						var projectPath = Path.Combine(solutionDirectoryName, projectInSolution.RelativePath);
						nbIssues += CheckCleanFolder(Path.GetDirectoryName(projectPath));

						if (!File.Exists(projectPath))
						{
							nbIssues += Fail($"Project {Path.GetFileName(projectPath)} could not be found");
							continue;
						}

						ProjectOptions options = new ProjectOptions
						{
							LoadSettings = ProjectLoadSettings.IgnoreMissingImports,
							ProjectCollection = projects
						};
						var project = Project.FromFile(projectPath, options);

						Console.WriteLine($"Analyzing {Path.GetFileName(projectPath)}");
						nbIssues += AnalyzeProject(project);
					}
					projects.UnloadAllProjects();

				}
			}
			catch (InvalidDataException)
			{
				nbIssues += Fail("File format (zip)");
			}
			catch (UnauthorizedAccessException)
			{
				nbIssues += Fail($"Unzip into {zipDirectoryName}");
			}

			Console.WriteLine($"Found {nbIssues} issue(s).");

			if (solution != null &&
				!string.IsNullOrEmpty(solutionFileName) &&
				!string.IsNullOrEmpty(config.DevenvPath) &&
				File.Exists(config.DevenvPath))
			{

				if (config.BuildAfterReport)
				{
					foreach (var configuration in solution.SolutionConfigurations)
					{
						try
						{
							var logFilename =
								$"build_{configuration.ConfigurationName}_{configuration.PlatformName}.log";
							if (File.Exists(logFilename))
								File.Delete(logFilename);
							Console.WriteLine($"Building {configuration.FullName}");
							ProcessStartInfo startInfo = new ProcessStartInfo(config.DevenvPath)
							{
								UseShellExecute = true,
								Arguments =
								$"\"{Path.GetFullPath(solutionFileName)}\" " +
								$"/Rebuild \"{configuration.FullName}\" " +
								$"/Out \"{logFilename}\""
							};
							var process = Process.Start(startInfo);
							process.WaitForExit();

							Console.WriteLine(GetBuildResult(logFilename));
						}
						catch (Win32Exception ex)
						{
							Console.WriteLine($"Failed to start Visual Studio: {ex.Message}");
						}
					}
				}

				if (config.OpenVSAfterReport)
				{
					try
					{
						ProcessStartInfo startInfo = new ProcessStartInfo(config.DevenvPath)
						{
							UseShellExecute = true,
							Arguments = $"\"{solutionFileName}\""
						};
						Process.Start(startInfo);
					}
					catch (Win32Exception ex)
					{
						Console.WriteLine($"Failed to start Visual Studio: {ex.Message}");
					}
				}
			}

			if (config.UseTempFolderForAnalysis && !config.OpenVSAfterReport && Directory.Exists(zipDirectoryName))
				Directory.Delete(zipDirectoryName, true);

			Console.WriteLine("Done, press a key to close.");
			Console.ReadKey();
		}

		private static string GetBuildResult(string logFileName)
		{
			try
			{
				return File.ReadLines(logFileName).First(s =>
					Regex.Match(s, "^=+ Rebuild All: \\d succeeded, \\d failed, \\d skipped =+").Success);
			}
			catch (InvalidOperationException ioe)
			{
				return $"Rebuild All failed: {ioe.Message}";
			}
		}

		private static int AnalyzeProject(Project project)
		{
			int nbIssues = 0;

			var configurations = project.GetItems("ProjectConfiguration");
			foreach (var configuration in configurations)
			{
				Console.WriteLine($"\tConfiguration {configuration.EvaluatedInclude}");

				project.SetGlobalProperty("Configuration", configuration.GetMetadataValue("Configuration"));
				project.SetGlobalProperty("Platform", configuration.GetMetadataValue("Platform"));
				project.ReevaluateIfNecessary();

				var platformToolset = project.GetProperty("PlatformToolset").EvaluatedValue;
				nbIssues += Assert(platformToolset == config.PlatformToolsetVersion,
					$"\t\tPlatform toolset version is {config.PlatformToolsetVersion}",
					$"It is {platformToolset}");

				var compilerSettings = project.ItemDefinitions["ClCompile"];

				var languageStandard = compilerSettings.GetMetadata("LanguageStandard");
				bool languageStandardIsOK = languageStandard != null &&
					(languageStandard.EvaluatedValue == "stdcpp20" || languageStandard.EvaluatedValue == "stdcpplatest");
				nbIssues += Assert(languageStandardIsOK, "\t\tC++ Language Standard is c++20 or higher");

				var warningLevel = compilerSettings.GetMetadata("WarningLevel");
				bool warningLevelIsOK = warningLevel != null &&
					(warningLevel.EvaluatedValue == "Level4" || warningLevel.EvaluatedValue == "EnableAllWarnings");
				nbIssues += Assert(warningLevelIsOK, "\t\tC++ Coding Standard #1 is respected: Warning Level is set to 4 or higher");
				if (!warningLevelIsOK && (warningLevel == null || !warningLevel.IsImported))
					compilerSettings.SetMetadataValue("WarningLevel", "Level4");

				var warningIsError = compilerSettings.GetMetadata("TreatWarningAsError");
				bool warningIsErrorIsOK = warningIsError != null && warningIsError.EvaluatedValue == "true";
				nbIssues += Assert(warningIsErrorIsOK, "\t\tC++ Coding Standard #1 is enforced: Treat Warning As Error is enabled");
				if (!warningIsErrorIsOK && (warningIsError == null || !warningIsError.IsImported))
					compilerSettings.SetMetadataValue("TreatWarningAsError", "true");

				nbIssues += FindInHeaders(project, @"^\s*?using\s+?namespace\s+?\w+\s*?;", "\t\tC++ Coding Standard #59 / C++ Core Guideline SF.7 is respected.", false);
				//nbIssues += FindInHeaders(project, @"^\s*?#pragma\s+?once", "\t\tC++ Coding Standard #24 / C++ Core Guideline SF.8 is respected (with #pragma once).");
			}

			project.Save();



			return nbIssues;
		}

		private static int FindInHeaders(Project project, string regex, string message, bool expected = true)
		{
			var includeFiles = project.GetItems("ClInclude");
			int nbInvalidHeaders = 0;
			foreach (var file in includeFiles)
			{
				var path = Path.Combine(project.DirectoryPath, file.EvaluatedInclude);
				if (File.Exists(path))
				{
					bool regexFoundInHeader = FileContainsText(path, regex);
					if(expected)
					{
						if(!regexFoundInHeader)
							nbInvalidHeaders += Fail(message, $"Violation found in {file.EvaluatedInclude}");
					}
					else
					{
						if(regexFoundInHeader)
							nbInvalidHeaders += Fail(message, $"Violation found in {file.EvaluatedInclude}");
					}
				}
				else
				{
					// what to do if the file does not exist?
				}
			}
			if (nbInvalidHeaders == 0)
				Success(message);
			return nbInvalidHeaders;
		}

		private static int CheckCleanFolder(string folder)
		{
			int nbIssues = Directory.Exists(Path.Combine(folder, "Debug")) ? 1 : 0;
			nbIssues += Directory.Exists(Path.Combine(folder, "Release")) ? 1 : 0;
			nbIssues += Directory.Exists(Path.Combine(folder, "Win32")) ? 1 : 0;
			nbIssues += Directory.Exists(Path.Combine(folder, "x64")) ? 1 : 0;
			nbIssues += Directory.Exists(Path.Combine(folder, ".vs")) ? 1 : 0;
			Assert(nbIssues == 0, $"Folder {folder} is clean");
			return nbIssues;
		}

		private static int CheckSlnVersion(string filename)
		{
			using (StreamReader reader = new StreamReader(filename))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					if (Regex.IsMatch(line, "^VisualStudioVersion = \\d+\\.\\d+\\.\\d+\\.\\d+"))
					{
						var parts = Regex.Split(line, "VisualStudioVersion = ");
						var version = new Version(parts[1]);
						return Assert(version.Major == config.VSMajorVersionNumber, $"Correct Visual Studio version ({config.VSMajorVersionNumber}): {version}");
					}
				}
			}
			return Fail($"Correct Visual Studio version ({config.VSMajorVersionNumber})");
		}

		private static int CheckName(string filename)
		{
			return Assert(!string.IsNullOrEmpty(filename) &&
						  Regex.IsMatch(filename, config.SubmissionRegex), "File name");
		}

		private static int Assert(bool value, string desc, string reason = null)
		{
			if (value)
				return Success(desc);
			return Fail(desc, reason);
		}

		private static int Success(string desc)
		{
			Console.Write($"{desc}....");
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("OK");
			Console.ResetColor();
			return 0;
		}

		private static int Fail(string desc, string reason = null)
		{
			Console.Write($"{desc}....");
			Console.ForegroundColor = ConsoleColor.Red;
			if (!string.IsNullOrEmpty(reason))
				Console.WriteLine($"NOK: {reason}");
			else
				Console.WriteLine("NOK");
			Console.ResetColor();
			return 1;
		}

		private static bool FileContainsText(string path, string regex)
		{
			using (var sr = new StreamReader(path))
			{
				string line = sr.ReadLine();
				while (line != null)
				{
					if (Regex.IsMatch(line, regex))
						return true;
					line = sr.ReadLine();
				}
			}
			return false;
		}
	}
}
