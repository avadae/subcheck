using System;

namespace SubCheck
{
	[Serializable]
	public class Config
	{
		[NonSerialized]
		private string vsPath;
		[NonSerialized]
		private Version cmakeVersion;
		public void SetVisualStudioPath(string path) {  vsPath = path; }
		public void SetCMakeVersion(Version version) { cmakeVersion = version; }
		public string SubmissionRegex { get; set; } = @"[1,2,3]DAE[0,1,2]\d_Programming4_[0,1]\d_[a-zA-Z]+_[a-zA-Z]+(-\d)?\.zip$";
		public string DevenvPath => System.IO.Path.Combine(vsPath,"Common7\\IDE\\devenv.exe");
		public string MsBuildPath => System.IO.Path.Combine(vsPath, "MSBuild\\Current\\Bin\\MsBuild.exe");
		public bool BuildAfterReport { get; set; }
		public bool OpenVSAfterReport { get; set; }
		public int VSMajorVersionNumber { get; set; } = 17;
		public string PlatformToolsetVersion { get; set; } = "v143";
		public bool UseTempFolderForAnalysis { get; set; } = true;
		public Version MinCMakeVersion { get; set; } = new Version("3.26.0");
		public Version MinCodeVersion { get; set; } = new Version("1.82.0");
		public string CMakeBuildSystemGenerator { get; set; } = "Visual Studio 17 2022";
		public Version CMakeversion { get { return cmakeVersion; } }
	}
}
