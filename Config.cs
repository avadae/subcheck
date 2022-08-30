using System;

namespace SubCheck
{
	[Serializable]
	public class Config
	{
		[NonSerialized]
		private string vsPath;
		public void SetVisualStudioPath(string path) {  vsPath = path; }
		public string SubmissionRegex { get; set; } = @"[1,2,3]DAE[0,1,2]\d_Programming4_[0,1]\d_[a-zA-Z]+_[a-zA-Z]+(-\d)?\.zip$";
		public string DevenvPath => System.IO.Path.Combine(vsPath,"Common7\\IDE\\devenv.exe");
		public string MsBuildPath => System.IO.Path.Combine(vsPath, "MSBuild\\Current\\Bin\\MsBuild.exe");
		public bool BuildAfterReport { get; set; }
		public bool OpenVSAfterReport { get; set; }
		public int VSMajorVersionNumber { get; set; } = 17;
		public string PlatformToolsetVersion { get; set; } = "v143";
		public bool UseTempFolderForAnalysis { get; set; } = true;
	}
}
