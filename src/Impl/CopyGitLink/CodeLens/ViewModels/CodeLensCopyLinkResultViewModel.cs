﻿#nullable enable

using CopyGitLink.Def;
using CopyGitLink.Def.Models;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using Task = System.Threading.Tasks.Task;

namespace CopyGitLink.CodeLens.ViewModels
{
    public sealed class CodeLensCopyLinkResultViewModel : INotifyPropertyChanged
    {
        private readonly IRepositoryService _repositoryService;

        private string? _url;
        private bool _linkGenerated;

        /// <summary>
        /// Gets the generated Url.
        /// </summary>
        public string? Url
        {
            get => _url;
            private set
            {
                _url = value;
                RaisePropertyChangedAsync().Forget();
            }
        }

        /// <summary>
        /// Gets whether the link has been generated.
        /// </summary>
        public bool LinkGenerated
        {
            get => _linkGenerated;
            private set
            {
                _linkGenerated = value;
                RaisePropertyChangedAsync().Forget();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public CodeLensCopyLinkResultViewModel(IRepositoryService repositoryService, ITextView textView, Span applicableSpan)
        {
            _repositoryService = repositoryService;

            CopyAgainCommand = new ActionCommand(ExecuteCopyAgainCommand);

            GenerateLinkAsync(textView, applicableSpan).Forget();
        }

        #region CopyAgainCommand

        public ActionCommand CopyAgainCommand { get; }

        private void ExecuteCopyAgainCommand()
        {
            Clipboard.SetText(Url);
        }

        #endregion

        private async Task GenerateLinkAsync(ITextView textView, Span applicableSpan)
        {
            // Switch to background thread on purpose to avoid blocking the main thread.
            await TaskScheduler.Default;

            if (textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument  textDocument))
            {
                string filePath = textDocument.FilePath;
                ITextSnapshotLine startLine = textView.TextBuffer.CurrentSnapshot.GetLineFromPosition(applicableSpan.Start);
                ITextSnapshotLine endLine = textView.TextBuffer.CurrentSnapshot.GetLineFromPosition(applicableSpan.Start + applicableSpan.Length);

                if (startLine != null
                    && endLine != null
                    && !string.IsNullOrEmpty(filePath)
                    && _repositoryService.TryGetKnownRepository(filePath, out string repositoryFolder, out RepositoryInfo? repositoryInfo)
                    && repositoryInfo != null)
                {
                    string url
                        = await repositoryInfo.Service.GenerateLinkAsync(
                            repositoryFolder,
                            repositoryInfo,
                            filePath,
                            startLine.LineNumber,
                            startColumnNumber: 0,
                            endLine.LineNumber,
                            endColumnNumber: endLine.Length)
                        .ConfigureAwait(false);

                    Url = url;
                    await CopyAgainCommand.ExecuteAsync().ConfigureAwait(false);
                }
            }

            LinkGenerated = true;
        }

        private async Task RaisePropertyChangedAsync([CallerMemberName] string? propertyName = null)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
