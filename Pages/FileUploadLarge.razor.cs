using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Radzen;

namespace CRMBlazorServerRBS.Pages
{
    public partial class FileUploadLarge : IAsyncDisposable
    {
        [Inject] protected IJSRuntime JSRuntime { get; set; }
        [Inject] protected NavigationManager NavigationManager { get; set; }
        [Inject] protected NotificationService NotificationService { get; set; }

        // ── state ──────────────────────────────────────────────────────────
        private string selectedFileName;
        private long   selectedFileSize;
        private bool   isUploading;
        private int    uploadProgress;

        private string uploadedUrl;
        private string uploadedOriginalName;
        private long   uploadedSize;
        private string errorMessage;

        // DotNetObjectReference lets JS call back into this component
        private DotNetObjectReference<FileUploadLarge> _dotNetRef;

        // ── lifecycle ──────────────────────────────────────────────────────

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _dotNetRef = DotNetObjectReference.Create(this);
                // Attach the onchange listener to the hidden <input type="file">
                await JSRuntime.InvokeVoidAsync("fileUpload_init", "largeFileInput", _dotNetRef);
            }
        }

        // ── JS → .NET callbacks (must be [JSInvokable]) ────────────────────

        /// <summary>Called by JS when the user picks a file from the native dialog.</summary>
        [JSInvokable]
        public void OnFileSelected(string name, long size)
        {
            selectedFileName  = name;
            selectedFileSize  = size;
            uploadedUrl       = null;
            errorMessage      = null;
            uploadProgress    = 0;
            StateHasChanged();
        }

        /// <summary>Called by JS for each XHR upload-progress event.</summary>
        [JSInvokable]
        public void OnUploadProgress(int percent)
        {
            uploadProgress = percent;
            StateHasChanged();
        }

        // ── UI event handlers ──────────────────────────────────────────────

        private async Task ChooseFile()
        {
            await JSRuntime.InvokeVoidAsync("fileUpload_choose", "largeFileInput");
        }

        private async Task StartUpload()
        {
            if (selectedFileName == null) return;

            isUploading   = true;
            uploadProgress = 0;
            uploadedUrl   = null;
            errorMessage  = null;
            StateHasChanged();

            try
            {
                // Build an absolute URL to the upload endpoint.
                // XHR runs in the browser, so we need the full origin.
                var uploadUrl = NavigationManager.BaseUri.TrimEnd('/') + "/upload/large";

                var jsonResponse = await JSRuntime.InvokeAsync<string>(
                    "fileUpload_upload",
                    "largeFileInput",
                    uploadUrl,
                    _dotNetRef);

                // Parse the JSON returned by UploadController.Large()
                var result = JsonSerializer.Deserialize<UploadResult>(
                    jsonResponse,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                uploadedUrl          = result?.Url;
                uploadedOriginalName = result?.FileName ?? selectedFileName;
                uploadedSize         = result?.Size ?? selectedFileSize;
                uploadProgress       = 100;
                selectedFileName     = null;
                selectedFileSize     = 0;

                NotificationService.Notify(NotificationSeverity.Success,
                    "Готово", $"Файл «{uploadedOriginalName}» загружен.");
            }
            catch (JSException jsEx)
            {
                errorMessage = $"Ошибка загрузки: {jsEx.Message}";
            }
            catch (Exception ex)
            {
                errorMessage = $"Ошибка: {ex.Message}";
            }
            finally
            {
                isUploading = false;
                StateHasChanged();
            }
        }

        // ── helpers ────────────────────────────────────────────────────────

        private static string FormatSize(long bytes) => bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} ГБ",
            >= 1_048_576     => $"{bytes / 1_048_576.0:F1} МБ",
            >= 1_024         => $"{bytes / 1_024.0:F1} КБ",
            _                => $"{bytes} Б"
        };

        private record UploadResult(string Url, string FileName, string SavedAs, long Size);

        // ── disposal ───────────────────────────────────────────────────────

        public async ValueTask DisposeAsync()
        {
            _dotNetRef?.Dispose();
            await ValueTask.CompletedTask;
        }
    }
}
