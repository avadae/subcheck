using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SubCheck
{
	internal class Report
	{
		public string filename;
		public string projectType;
		public int nbSuccessfulBuilds;
		public int nbFailedBuilds;
		public int nbIssues;
	}
}
