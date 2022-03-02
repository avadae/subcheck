using System;

namespace SubCheck
{
	[Serializable]
	public class Config
	{
		public string SubmissionRegex { get; set; } = @"[1,2,3]DAE[0,1,2]\d_Programming4_[0,1]\d_[a-zA-Z]+_[a-zA-Z]+(-\d)?\.zip$";
		public string DevenvPath { get; set; } = @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\devenv.exe";
		public bool BuildAfterReport { get; set; }
		public bool OpenVSAfterReport { get; set; }
		public int SolutionMajorVersionNumber { get; set; } = 17;
		public string PlatformToolsetVersion { get; set; } = "v143";
	}
}
