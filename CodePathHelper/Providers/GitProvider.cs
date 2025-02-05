﻿namespace CodePathHelper.Providers
{
    using System;
    using System.Diagnostics;
    using System.IO;

    internal static class GitProvider 
    {
        static string _repoUrl = string.Empty;

        static string _branchName = string.Empty;

        private static Process _process = new Process
        {
            StartInfo = new ProcessStartInfo("git.exe")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        public static bool GetGitInfo(string fileFullPath, out string repoUrl, out string branchName, out string filePath)
        {
            _process.StartInfo.WorkingDirectory = Path.GetDirectoryName(fileFullPath);

            if (string.IsNullOrWhiteSpace(fileFullPath))
            {
                filePath = string.Empty;
                repoUrl = string.Empty;
                branchName = string.Empty;
                return false;
            }

            fileFullPath = fileFullPath.Replace("\\", "/");

            if (Options.Instance.ExtractGitInfoEveryTime || string.IsNullOrWhiteSpace(_repoUrl) || string.IsNullOrWhiteSpace(_repoUrl))
            {
                _branchName = Options.Instance.UseCurrentBranch ? GitGetBranch() : Options.Instance.DefaultBranchName;
                _repoUrl = GitGetRepoUrl();
            }

            string rootPath = GetGitRootPath();
            filePath = fileFullPath.Substring(rootPath.Length + 1);
            repoUrl = _repoUrl;
            branchName = _branchName;

            return true;
        }

        public static string GetGitRootPath(string solutionDir = null)
        {
            if (!string.IsNullOrWhiteSpace(solutionDir))
                _process.StartInfo.WorkingDirectory = solutionDir;

            return RunGitCommand("rev-parse --show-toplevel");
        }

        public static void GitPush(string solutionDir = null)
        {
            if (!string.IsNullOrWhiteSpace(solutionDir))
                _process.StartInfo.WorkingDirectory = solutionDir;
            RunGitCommand("push");
        }

        public static string GitGetBranch(string solutionDir = null)
        {
            if (!string.IsNullOrWhiteSpace(solutionDir))
                _process.StartInfo.WorkingDirectory = solutionDir;
            return RunGitCommand("rev-parse --abbrev-ref HEAD");
        }

        public static string GitGetRepoUrl(string solutionDir = null)
        {
            if (!string.IsNullOrWhiteSpace(solutionDir))
                _process.StartInfo.WorkingDirectory = solutionDir;
            
            return RunGitCommand("config --get remote.origin.url");
        }

        public static void GitCommitAndPush()
        {
            string savedWorkingDirectory = _process.StartInfo.WorkingDirectory;
            
            _process.StartInfo.WorkingDirectory = GetGitRootPath();

            RunGitCommand("add .");
            RunGitCommand($"commit -m \"{DateTime.Now}\"");
            RunGitCommand($"push --set-upstream origin {GitGetBranch()}"); 

            _process.StartInfo.WorkingDirectory = savedWorkingDirectory;
        }

        public static void GitCheckout(string branch, string solutionDir = null)
        {
            if (!string.IsNullOrWhiteSpace(solutionDir))
                _process.StartInfo.WorkingDirectory = solutionDir;
            
            RunGitCommand($"checkout {branch}");
        }

        private static string RunGitCommand(string arguments)
        {
            _process.StartInfo.Arguments = arguments;
            _process.Start();

            return _process.StandardOutput.ReadLine();
        }

    }
}
