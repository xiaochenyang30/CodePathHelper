﻿namespace CodePathHelper
{
    using CodePathHelper.Providers;
    using Community.VisualStudio.Toolkit;
    using Microsoft.VisualStudio.Shell;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Design;
    using System.Linq;
    using System.Windows;
    using Task = System.Threading.Tasks.Task;

    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class ShareCodePath
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 4129;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("90247df7-8439-4fd8-a1ca-024a94c78a8d");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="ShareCodePath"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private ShareCodePath(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static ShareCodePath Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in ShareCodePath's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new ShareCodePath(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
#pragma warning disable VSTHRD100 // Avoid async void methods. By design to use as EventHandler.
        private async void Execute(object sender, EventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync().ConfigureAwait(false);

            var fileFullPath = docView?.FilePath?.Trim('/');
            if (fileFullPath == null)
            {
                await MessageProvider.ShowErrorInMessageBoxAsync("Failed to get file path.");
                return;
            }

            var selection = docView?.TextView?.Selection.SelectedSpans.FirstOrDefault();
            if (selection == null)
            {
                await MessageProvider.ShowErrorInMessageBoxAsync("Failed to get your selected text.");
                return;
            }

            if (!GitProvider.GetGitInfo(fileFullPath, out string repoUrl, out string branchName, out string filePath))
            {
                await MessageProvider.ShowErrorInMessageBoxAsync("Failed to extract git information.");
                return;
            }

            int line, lineEnd, lineStartColumn, lineEndColumn;
            string code = string.Empty;

            if (selection.Value.Start.Position != selection.Value.End.Position)
            {
                SnapshotSpanProvider.GetStartAndEndLineNumberAndColumn(selection.Value, out line, out lineEnd, out lineStartColumn, out lineEndColumn);
                code = selection.Value.GetText();
            }
            else
            {
                SnapshotSpanProvider.GetThisLineEndToCursor(selection.Value, out line, out lineEnd, out lineStartColumn, out lineEndColumn);
            }

            string url = AzureDevOpsCodePathProvider.GenerateUrlFromInfo(repoUrl, filePath, branchName, line, lineEnd, lineStartColumn, lineEndColumn);
            // Clip
            Dictionary<string, string> messageParameters = new Dictionary<string, string>
            {
                { @"{url}", url },
                { @"{code}", code },
                { @"{newline}", "\r\n" }
            };
            Clipboard.SetText(messageParameters.Aggregate(Options.Instance.CustomizedCopyContent, (s, kv) => s.Replace(kv.Key, kv.Value)));

            await ShowMessageAsync($"Code path copied and ready to be shared! Detailed url: {url}");

            // Background git job
            if (Options.Instance.BackgroundGitJob == BackgroundGitJob.Push)
            {
                await Task.Run(() => GitProvider.GitPush());
            }
            else if (Options.Instance.BackgroundGitJob == BackgroundGitJob.CommitAndPush)
            {
                await Task.Run(() => GitProvider.GitCommitAndPush());   
            }
        }
        
        private async Task ShowMessageAsync(string message)
        {
            if (Options.Instance.NotificationStyle == NotificationStyle.StatusBar)
            {
               await MessageProvider.ShowInStatusBarAsync(message);
            }
            else if (Options.Instance.NotificationStyle == NotificationStyle.MessageBox)
            {
                await MessageProvider.ShowInfoInMessageBoxAsync(message);
            }
        }
    }
}
