using System;
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
			for(int i = 1; i < args.Length; i++)
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

			if (!string.IsNullOrEmpty(config.DevenvPath) &&
				!File.Exists(config.DevenvPath)) {
				Console.WriteLine($"File in DevenvPath is not found: {config.DevenvPath}");
			}
			#endregion

			Console.WriteLine($"Analyzing {filename}");
			int nbIssues = CheckName(filename);

			SolutionFile solution = null;
			string solutionFileName = null;
			string zipDirectoryName = Path.GetFileNameWithoutExtension(filename);
			if (config.UseTempFolderForAnalysis && !config.OpenVSAfterReport)
				zipDirectoryName = Path.Combine(Path.GetTempPath(),"subcheck",zipDirectoryName);

			try
			{
				ZipFile.ExtractToDirectory(filename, zipDirectoryName, true);

				var files = Directory.GetFiles(zipDirectoryName, "*.sln", SearchOption.AllDirectories);
				nbIssues += Assert(files.Length == 1, "Found exactly one solution", $" - found {files.Length} solutions.");
				if (files.Length > 0) // let's assume the first one is the correct one.
				{
					solutionFileName = files[0];
					var solutionDirectoryName = Path.GetDirectoryName(solutionFileName);
					nbIssues += CheckSlnVersion(solutionFileName);
					CheckCleanFolder(solutionDirectoryName);

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
							LoadSettings = ProjectLoadSettings.IgnoreMissingImports
						};
						var project = Project.FromFile(projectPath, options);

						Console.WriteLine($"Analyzing {Path.GetFileName(projectPath)}");
						nbIssues += AnalyzeProject(project);
					}
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
							ProcessStartInfo startInfo = new ProcessStartInfo(config.DevenvPath);
							startInfo.Arguments =
								$"\"{Path.GetFullPath(solutionFileName)}\" " +
								$"/Rebuild {configuration.FullName} " +
								$"/Out {logFilename}";
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
						Process.Start(config.DevenvPath, $"\"{solutionFileName}\"");
					}
					catch (Win32Exception ex)
					{
						Console.WriteLine($"Failed to start Visual Studio: {ex.Message}");
					}
				}
			}

			if (config.UseTempFolderForAnalysis && !config.OpenVSAfterReport && Directory.Exists(zipDirectoryName))
				Directory.Delete(zipDirectoryName,true);

			Console.WriteLine("Done, press a key to close.");
			Console.ReadKey();
		}

		private static string GetBuildResult(string logFileName)
		{
			return File.ReadLines(logFileName).First(s =>
				Regex.Match(s, "^=+ Rebuild All: \\d succeeded, \\d failed, \\d skipped =+").Success);
		}

		private static int AnalyzeProject(Project project)
		{
			int nbIssues = 0;

			var configurations = project.GetItems("ProjectConfiguration");
			foreach (var configuration in configurations)
			{
				Console.WriteLine($"\tConfiguration {configuration.EvaluatedInclude}");

				foreach (var propertyGroup in project.Xml.PropertyGroups.Where(pg =>
					pg.Label == "Configuration" && pg.Condition.Contains(configuration.EvaluatedInclude)))
				{
					var platformToolset = propertyGroup.Properties.First(p => p.Name == "PlatformToolset");
					if (platformToolset != null && Assert(platformToolset.Value == config.PlatformToolsetVersion, $"\t\tPlatform toolset version is {config.PlatformToolsetVersion}") > 0)
						nbIssues++;
				}

				foreach (var projectItemDefinitionGroupElement in project.Xml.ItemDefinitionGroups.Where(idg =>
					idg.Condition.Contains(configuration.EvaluatedInclude)))
				{
					var clCompileElement = projectItemDefinitionGroupElement.ItemDefinitions.First(item => item.ItemType == "ClCompile");
					var warningLevel = clCompileElement.Metadata.First(item => item.ElementName == "WarningLevel");
					if (warningLevel != null && Assert(warningLevel.Value == "Level4", "\t\tWarning Level 4") > 0)
					{
						nbIssues++;
						if (config.BuildAfterReport)
							warningLevel.Value = "Level4";
					}

					var warningIsError = clCompileElement.Metadata.FirstOrDefault(item => item.ElementName == "TreatWarningAsError");
					if (warningIsError != null)
					{
						if (Assert(warningIsError.Value == "true", "\t\tTreat Warning As Error") > 0)
						{
							nbIssues++;
							if (config.BuildAfterReport)
								warningIsError.Value = "true";
						}
					}
					else
					{
						nbIssues += Fail("\t\tTreat Warning As Error");
						if (config.BuildAfterReport)
							clCompileElement.AddMetadata("TreatWarningAsError", "true");
					}
				}
			}

			project.Save();
			return nbIssues;
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
						return Assert(version.Major == config.SolutionMajorVersionNumber, $"Correct Visual Studio version ({config.SolutionMajorVersionNumber}): {version}");
					}
				}
			}
			return Fail($"Correct Visual Studio version ({config.SolutionMajorVersionNumber})");
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
			Console.Write("NOK");
			Console.WriteLine(reason);
			Console.ResetColor();
			return 1;
		}
	}

}
